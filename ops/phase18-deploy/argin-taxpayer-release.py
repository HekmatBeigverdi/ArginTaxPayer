#!/usr/bin/python3 -I
"""Root-owned deployment entry point. Migrate with a restricted DB role; never run bundle scripts."""
import fcntl
from contextlib import contextmanager
import gzip
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import pwd
import grp
import re
import shutil
import signal
import secrets
import stat
import subprocess
import sys
import tarfile
import tempfile
import time

BASE = Path('/opt/argintaxpayer')
RELEASES = BASE / 'releases'
RUNTIME = BASE / 'runtime/node/bin'
INCOMING = Path('/home/argin-deploy/argin-deploy-incoming')
PROBE = Path('/usr/local/libexec/argintaxpayer/local-health.sh')
UNITS = ('argintaxpayer-api.service', 'argintaxpayer-web.service')
MAX_ARCHIVE_BYTES = 1024 ** 3
MAX_EXPANDED_BYTES = 4 * 1024 ** 3
MAX_ENTRIES = 200000
MAX_TAR_BYTES = MAX_EXPANDED_BYTES + MAX_ENTRIES * 1024
MAX_METADATA_BYTES = 64 * 1024
MAX_TOTAL_METADATA_BYTES = 16 * 1024 * 1024
ENV = {'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LANG': 'C.UTF-8'}


class Refused(RuntimeError):
    pass


def validate_sha(value):
    if not re.fullmatch(r'[a-f0-9]{40}', value):
        raise Refused('Expected exactly 40 lowercase hexadecimal SHA characters')
    return value


def snapshot_file(directory_fd, name, destination, expected_uid, limit):
    """Pin a regular upload file and copy bytes into a private root-only snapshot."""
    try:
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK,
                     dir_fd=directory_fd)
    except OSError:
        raise Refused('Upload is missing or contains a symlink') from None
    with os.fdopen(fd, 'rb') as source:
        info = os.fstat(source.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_uid != expected_uid:
            raise Refused('Uploads must be regular files owned by argin-deploy')
        if info.st_size > limit:
            raise Refused('Upload exceeds size limit')
        with open(destination, 'xb') as output:
            os.chmod(destination, 0o600)
            total = 0
            while chunk := source.read(1024 * 1024):
                total += len(chunk)
                if total > limit:
                    raise Refused('Upload grew past its size limit')
                output.write(chunk)


def verify_bundle(bundle, sha):
    expected = {}
    for line in (bundle / 'SHA256SUMS').read_text().splitlines():
        match = re.fullmatch(r'([a-f0-9]{64})  (api\.tar\.gz|web\.tar\.gz)', line)
        if not match or match[2] in expected:
            raise Refused('Manifest must contain exactly the two application archives')
        expected[match[2]] = match[1]
    if set(expected) != {'api.tar.gz', 'web.tar.gz'}:
        raise Refused('Incomplete checksum manifest')
    for name, digest in expected.items():
        with (bundle / name).open('rb') as stream:
            actual = hashlib.file_digest(stream, 'sha256').hexdigest()
        if actual != digest:
            raise Refused('Archive checksum mismatch')
    metadata = (bundle / 'build-info.txt').read_text().splitlines()
    if (metadata.count(f'sha={sha}') != 1 or len(metadata) not in (1, 2)
            or any(not line.startswith(('sha=', 'version=')) for line in metadata)
            or sum(line.startswith('sha=') for line in metadata) != 1):
        raise Refused('Embedded source SHA does not match requested release')


def verify_existing_release(release, bundle):
    for name in ('build-info.txt', 'SHA256SUMS'):
        if not (release / name).is_file() or (release / name).is_symlink():
            raise Refused('Existing release has no trusted manifest; use a new source SHA')
        if (release / name).read_bytes() != (bundle / name).read_bytes():
            raise Refused('Existing immutable release does not match uploaded artifact')


@contextmanager
def bounded_tar(archive):
    # tarfile parses PAX/GNU extensions before yielding members. Bound their raw
    # bodies first so a tiny gzip stream cannot allocate gigabytes in the parser.
    with tempfile.TemporaryFile(dir=archive.parent) as raw:
        expanded = 0
        with gzip.open(archive, 'rb') as compressed:
            while chunk := compressed.read(1024 * 1024):
                expanded += len(chunk)
                if expanded > MAX_TAR_BYTES:
                    raise Refused('Decompressed tar exceeds its size limit')
                raw.write(chunk)
        raw.seek(0)
        metadata = entries = 0
        while header := raw.read(512):
            if len(header) != 512:
                raise Refused('Truncated tar header')
            if not header.strip(b'\x00'):
                break
            entries += 1
            try:
                size = tarfile.nti(header[124:136])
            except ValueError:
                raise Refused('Invalid tar size field') from None
            if size < 0 or size > MAX_TAR_BYTES or entries > MAX_ENTRIES:
                raise Refused('Tar header exceeds limits')
            if header[156:157] in (b'x', b'g', b'L', b'K'):
                metadata += size
                if size > MAX_METADATA_BYTES or metadata > MAX_TOTAL_METADATA_BYTES:
                    raise Refused('Tar extension metadata exceeds limits')
            if header[156:157] == b'S':
                raise Refused('Sparse archives are forbidden')
            raw.seek(((size + 511) // 512) * 512, os.SEEK_CUR)
            if raw.tell() > expanded:
                raise Refused('Truncated tar payload')
        raw.seek(0)
        with tarfile.open(fileobj=raw, mode='r:') as stream:
            yield stream


def extract_archive(archive, destination, root_name):
    """Never delegate archive paths, ownership, links or permissions to tar."""
    total = entries = 0
    with bounded_tar(archive) as stream:
        for member in stream:
            entries += 1
            raw = member.name
            path = PurePosixPath(raw)
            if (not raw or path.is_absolute() or '..' in raw.split('/')
                    or '\\' in raw or '\x00' in raw
                    or not path.parts or path.parts[0] != root_name):
                raise Refused('Archive path is outside its application root')
            if not (member.isdir() or member.isfile()) or member.issparse():
                raise Refused('Archive links, devices, FIFOs and sparse files are forbidden')
            if (path.name == '.npmrc' or path.name == '.env'
                    or path.name.startswith('.env.')
                    or path.suffix.lower() in ('.key', '.pem', '.pfx', '.p12')):
                raise Refused('Private configuration is forbidden in application archives')
            total += member.size
            if entries > MAX_ENTRIES or member.size < 0 or total > MAX_EXPANDED_BYTES:
                raise Refused('Archive exceeds extraction limits')
            target = destination.joinpath(*path.parts)
            target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
            if member.isdir():
                target.mkdir(exist_ok=True, mode=0o700)
            else:
                try:
                    with stream.extractfile(member) as source, target.open('xb') as output:
                        shutil.copyfileobj(source, output, 1024 * 1024)
                    target.chmod(0o700 if member.mode & 0o111 else 0o600)
                except FileExistsError:
                    raise Refused('Duplicate or conflicting archive member') from None


def copy_runtime_tree(source, destination, owner_uid, group_gid, source_uid=None):
    """Copy through pinned no-follow descriptors; npm output never controls root paths."""
    source_uid = os.getuid() if source_uid is None else source_uid
    total = entries = 0
    for directory, directories, files, source_fd in os.fwalk(source, follow_symlinks=False):
        entries += 1
        if entries > MAX_ENTRIES:
            raise Refused('Too many runtime directories')
        relative = Path(directory).relative_to(source)
        output_dir = destination / relative
        output_dir.mkdir(parents=True, exist_ok=True, mode=0o750)
        os.chown(output_dir, owner_uid, group_gid)
        os.chmod(output_dir, 0o750)
        for name in directories:
            info = os.stat(name, dir_fd=source_fd, follow_symlinks=False)
            if not stat.S_ISDIR(info.st_mode):
                raise Refused('Runtime directory was replaced with a link')
        for name in files:
            info = os.stat(name, dir_fd=source_fd, follow_symlinks=False)
            if stat.S_ISLNK(info.st_mode) and relative.parts[-2:] == ('node_modules', '.bin'):
                continue  # next is launched directly; these npm CLI shims are unnecessary.
            try:
                fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK,
                             dir_fd=source_fd)
            except OSError:
                raise Refused('Runtime file was replaced with a link') from None
            with os.fdopen(fd, 'rb') as stream:
                info = os.fstat(stream.fileno())
                if not stat.S_ISREG(info.st_mode) or info.st_uid != source_uid:
                    raise Refused('Unexpected runtime file type or owner')
                entries += 1
                if entries > MAX_ENTRIES:
                    raise Refused('Too many runtime files')
                target = output_dir / name
                with target.open('xb') as output:
                    while chunk := stream.read(1024 * 1024):
                        total += len(chunk)
                        if total > MAX_EXPANDED_BYTES:
                            raise Refused('Runtime exceeds size limit')
                        output.write(chunk)
                    os.fchown(output.fileno(), owner_uid, group_gid)
                    os.fchmod(output.fileno(), 0o750 if info.st_mode & 0o111 else 0o640)


def atomic_current(current, target):
    temporary = current.parent / f'.current-deploy-{os.getpid()}'
    try:
        temporary.symlink_to(target)
        os.replace(temporary, current)
    finally:
        temporary.unlink(missing_ok=True)


def promote(release, current, restart, health, stop=lambda: None):
    previous = current.resolve(strict=True) if current.is_symlink() else None
    if current.exists() and previous is None:
        raise Refused('Current release path is not a symlink')
    try:
        atomic_current(current, release)
        restart()
        if not health():
            raise Refused('Local health checks failed')
    except (Exception, KeyboardInterrupt):
        print('[ROLLBACK] Restoring previous application release', flush=True)
        if previous is not None:
            atomic_current(current, previous)
            restart()
            if not health():
                raise Refused('Previous application also failed health; inspect VPS logs')
        else:
            stop()
            current.unlink(missing_ok=True)
        raise Refused('New application failed; promotion was rolled back') from None


def trusted(path, directory=False, _seen=frozenset()):
    """All privileged destinations and executable ancestors must be root-controlled."""
    # Validate lexical ancestors too: resolving first can hide a replaceable link
    # in a writable directory. Root-owned runtime/npm symlinks remain supported.
    absolute = path.absolute()
    for item in reversed((absolute, *absolute.parents)):
        info = item.lstat()
        if info.st_uid != 0:
            raise Refused('Privileged path is not exclusively root-controlled')
        if stat.S_ISLNK(info.st_mode):
            if item in _seen or len(_seen) >= 40:
                raise Refused('Privileged path contains a symlink loop')
            target = Path(os.readlink(item))
            if not target.is_absolute():
                target = item.parent / target
            trusted(target, _seen=_seen | {item})
        elif info.st_mode & 0o022:
            raise Refused('Privileged path is writable by a non-root identity')
    if directory and not absolute.is_dir():
        raise Refused('Required directory is absent')


def run(command, **kwargs):
    subprocess.run(command, check=True, env=ENV, **kwargs)


def restart_services():
    for unit in UNITS:
        run(['/usr/bin/systemctl', 'restart', unit], timeout=90)


def stop_services():
    run(['/usr/bin/systemctl', 'stop', *reversed(UNITS)], timeout=90)


def healthy(probe_mode="--probe-local"):
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        try:
            result = subprocess.run(['/bin/bash', str(PROBE), probe_mode],
                                    env=ENV, stdout=subprocess.DEVNULL,
                                    stderr=subprocess.DEVNULL,
                                    timeout=min(45, deadline-time.monotonic()))
            if result.returncode == 0:
                return True
        except subprocess.TimeoutExpired:
            pass
        time.sleep(2)
    return False


def snapshot_upload(sha, destination, deployment_uid):
    # Each directory component is pinned independently; no writable ancestor symlinks.
    fd = os.open('/home', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for component in ('argin-deploy', 'argin-deploy-incoming', sha):
            child = os.open(component, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                            dir_fd=fd)
            os.close(fd)
            fd = child
            if os.fstat(fd).st_uid != deployment_uid:
                raise Refused('Upload directories must belong to argin-deploy')
        for name in ('SHA256SUMS', 'build-info.txt', 'api.tar.gz', 'web.tar.gz'):
            limit = MAX_ARCHIVE_BYTES if name.endswith('.tar.gz') else 4096
            snapshot_file(fd, name, destination / name, deployment_uid, limit)
    finally:
        os.close(fd)


def prepare_release(sha, deployment, service_gid):
    release = RELEASES / sha
    if release.is_symlink():
        raise Refused('Release directory is a symlink')
    with tempfile.TemporaryDirectory(prefix='.deploy-', dir=BASE) as temporary:
        temporary = Path(temporary)
        bundle = temporary / 'bundle'
        bundle.mkdir(mode=0o700)
        snapshot_upload(sha, bundle, deployment.pw_uid)
        verify_bundle(bundle, sha)
        if release.exists():
            trusted(release, directory=True)
            for name in ('build-info.txt', 'SHA256SUMS'):
                trusted(release / name)
            verify_existing_release(release, bundle)
            print('[PASS] Reusing matching immutable release', flush=True)
            return release
        stage = temporary / 'stage'
        stage.mkdir(mode=0o700)
        for root in ('api', 'web'):
            extract_archive(bundle / f'{root}.tar.gz', stage, root)
        for name in ('api/ArginTaxPayer.Api.dll', 'web/.next/BUILD_ID',
                     'web/package.json', 'web/package-lock.json'):
            if not (stage / name).is_file() or not (stage / name).stat().st_size:
                raise Refused('Incomplete application archive')
        # Give npm only this staging tree. The enclosing snapshot remains root-only.
        for directory, directories, files in os.walk(stage):
            os.chown(directory, deployment.pw_uid, deployment.pw_gid)
            for name in files:
                os.chown(Path(directory) / name, deployment.pw_uid, deployment.pw_gid)
        # Traverse-only parent; uploads/snapshots remain mode 0700/0600 root-owned.
        temporary.chmod(0o711)
        npm_environment = {
            **ENV, 'PATH': str(RUNTIME) + ':' + ENV['PATH'],
            'HOME': deployment.pw_dir, 'NODE_ENV': 'production',
            'NPM_CONFIG_USERCONFIG': '/dev/null',
            'NPM_CONFIG_CACHE': deployment.pw_dir + '/.npm',
            'NPM_CONFIG_IGNORE_SCRIPTS': 'true',
        }
        command = ['/usr/sbin/runuser', '-u', 'argin-deploy', '--',
                   '/usr/bin/env', '-i',
                   *(f'{key}={value}' for key, value in npm_environment.items()),
                   str(RUNTIME / 'npm'), 'ci', '--omit=dev', '--ignore-scripts',
                   '--no-audit', '--no-fund']
        print('[START] Installing locked web dependencies without root privileges', flush=True)
        logs = BASE / 'deploy-logs'
        logs.mkdir(mode=0o700, exist_ok=True)
        trusted(logs, directory=True)
        log_fd = os.open(logs / f'{sha}.npm.log',
                         os.O_WRONLY | os.O_CREAT | os.O_TRUNC | os.O_NOFOLLOW, 0o600)
        with os.fdopen(log_fd, 'wb') as output:
            try:
                run(command, cwd=stage / 'web', stdout=output,
                    stderr=subprocess.STDOUT, timeout=600)
            except subprocess.CalledProcessError:
                raise Refused('Locked npm installation failed; operator can inspect the private deploy-logs directory') from None
        # Copy to another private root-owned tree, never chown a user-writable tree in place.
        immutable = temporary / 'immutable'
        immutable.mkdir(mode=0o700)
        copy_runtime_tree(stage, immutable, 0, service_gid, deployment.pw_uid)
        if not (immutable / 'web/node_modules/next/dist/bin/next').is_file():
            raise Refused('Next.js executable is missing')
        for name in ('build-info.txt', 'SHA256SUMS'):
            target = immutable / name
            shutil.copyfile(bundle / name, target)
            os.chown(target, 0, service_gid)
            target.chmod(0o640)
        cache = immutable / 'web/.next/cache'
        if cache.exists():
            shutil.rmtree(cache)
        cache.mkdir(mode=0o700)
        service = pwd.getpwnam('argintaxsvc')
        os.chown(cache, service.pw_uid, service_gid)
        os.chown(immutable, 0, service_gid)
        immutable.chmod(0o750)
        os.rename(immutable, release)
    print(f'[PASS] Immutable release staged: {sha}', flush=True)
    return release


MIGRATION_CONFIG = Path('/etc/argintaxpayer/migrations.env')
BACKUPS = Path('/var/backups/argintaxpayer')
BACKUP_KEY = Path('/etc/argintaxpayer/migration-backup.key')


def migration_manifest(path):
    if path.stat().st_size > 256 * 1024:
        raise Refused('Migration manifest exceeds limit')
    values = json.loads(path.read_text())
    if (not isinstance(values, list) or not values or len(values) > 10000
            or any(not isinstance(value, str) or not re.fullmatch(r'[0-9]{14}_[A-Za-z0-9_]+', value) for value in values)
            or values != sorted(set(values))):
        raise Refused('Invalid ordered migration manifest')
    return values


def pending_migrations(expected, applied):
    # A hole, unknown migration or newer DB is never silently downgraded/repaired.
    if applied != expected[:len(applied)]:
        raise Refused('Database migration history is not a prefix of this release')
    return expected[len(applied):]


def migration_config():
    trusted(MIGRATION_CONFIG)
    if MIGRATION_CONFIG.stat().st_mode & 0o077:
        raise Refused('Migration credentials must be readable only by root')
    values = {}
    for line in MIGRATION_CONFIG.read_text().splitlines():
        if not line or line.startswith('#'):
            continue
        key, separator, value = line.partition('=')
        if not separator or key in values:
            raise Refused('Invalid migration configuration')
        values[key] = value
    required = {'CONTAINER', 'PGHOST', 'PGPORT', 'PGDATABASE', 'PGUSER',
                'PGPASSWORD', 'PGSSLMODE', 'PGSSLROOTCERT'}
    if (set(values) != required or not re.fullmatch(r'[a-zA-Z0-9_.-]+', values['CONTAINER'])
            or values['PGDATABASE'] != 'argintaxpayer_prod'
            or values['PGUSER'] != 'argin_prod_migrator'
            or values['PGHOST'] != '127.0.0.1' or values['PGPORT'] != '5432'
            or values['PGSSLMODE'] != 'verify-full' or not values['PGPASSWORD']
            or not values['PGSSLROOTCERT'].startswith('/') or '\x00' in ''.join(values.values())):
        raise Refused('Migration target must be the dedicated TLS production database/migrator')
    binding = subprocess.run(['/usr/bin/docker', 'port', values['CONTAINER'], '5432/tcp'],
                             check=True, capture_output=True, text=True, timeout=15, env=ENV)
    if binding.stdout.strip() != '127.0.0.1:15434':
        raise Refused('Migration container is not the API dedicated database listener')
    # Never let a migration configuration drift away from the API database.
    trusted(Path('/etc/argintaxpayer/api.env'))
    runtime = Path('/etc/argintaxpayer/api.env').read_text().splitlines()
    connection = [line.partition('=')[2].strip('"').strip("'") for line in runtime
                  if line.startswith('ConnectionStrings__ArginTaxPayerDb=')]
    if len(connection) != 1:
        raise Refused('Cannot establish runtime database identity')
    fields = dict(part.split('=', 1) for part in connection[0].split(';') if '=' in part)
    if any(fields.get(key) != value for key, value in
           {'Host': '127.0.0.1', 'Port': '15434', 'Database': 'argintaxpayer_prod',
            'Username': 'argin_prod_app', 'SSL Mode': 'VerifyFull'}.items()):
        raise Refused('API is not using the dedicated production database')
    return values


class MigrationDatabase:
    def __init__(self, configuration):
        self.configuration = configuration

    def command(self, executable, *arguments, **kwargs):
        values = {key: value for key, value in self.configuration.items() if key.startswith('PG')}
        values.update(PGCONNECT_TIMEOUT='10', PGOPTIONS='-c lock_timeout=5000 -c statement_timeout=120000')
        command = ['/usr/bin/docker', 'exec', '-i', '--user', '65534:65534']
        # Docker obtains credential values from the child environment, not argv.
        for key in values:
            command.extend(['--env', key])
        command.extend([self.configuration['CONTAINER'], executable, *arguments])
        return subprocess.run(command, env={**ENV, **values}, check=True, timeout=600, **kwargs)

    def query(self, sql):
        result = self.command('psql', '-X', '-Atq', '-v', 'ON_ERROR_STOP=1',
                              input='BEGIN READ ONLY;\n'+sql+'\nCOMMIT;\n',
                              capture_output=True, text=True)
        return result.stdout.strip()

    def identity(self):
        return self.query("SELECT current_database()||'|'||CASE WHEN "
                          "(SELECT rolsuper OR rolcreaterole FROM pg_roles WHERE rolname=current_user) OR "
                          "pg_has_role(current_user,'pg_execute_server_program','MEMBER') OR "
                          "pg_has_role(current_user,'pg_write_server_files','MEMBER') OR "
                          "pg_has_role(current_user,'pg_read_server_files','MEMBER') "
                          "THEN 'unsafe_role' ELSE current_user END||'|'||"
                          "(SELECT ssl::text FROM pg_stat_ssl WHERE pid=pg_backend_pid());")

    def applied(self):
        exists = self.query("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
        if exists == 'f':
            return []
        return self.query('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";').splitlines()

    def backup(self, destination):
        trusted(BACKUP_KEY)
        if BACKUP_KEY.stat().st_mode & 0o077 or BACKUP_KEY.stat().st_size < 32:
            raise Refused('Backup encryption key must be private to root')
        # Unnamed temporary file, verified before encryption; never a plaintext archive path.
        with tempfile.TemporaryFile(dir=BACKUPS) as dump:
            self.command('pg_dump', '--format=custom', '--no-owner', '--no-acl', '--schema=public',
                         stdout=dump, stderr=subprocess.PIPE)
            if dump.tell() < 16:
                raise Refused('Database backup is empty')
            dump.seek(0)
            self.command('pg_restore', '--list', stdin=dump, stdout=subprocess.DEVNULL,
                         stderr=subprocess.PIPE)
            dump.seek(0)
            with destination.open('xb') as encrypted:
                subprocess.run(['/usr/bin/openssl', 'enc', '-aes-256-cbc', '-salt',
                                '-pbkdf2', '-iter', '200000', '-pass', 'file:'+str(BACKUP_KEY)],
                               stdin=dump, stdout=encrypted, stderr=subprocess.PIPE,
                               check=True, env=ENV, timeout=600)
                encrypted.flush()
                os.fsync(encrypted.fileno())


    def apply(self, sql):
        # Random capability unavailable to uploaded SQL. Old clients fail at the first
        # command under ON_ERROR_STOP; modern clients cannot run \!, \copy or \i.
        restricted = ("\\restrict " + secrets.token_hex(32) + "\n").encode() + sql.read_bytes()
        self.command('psql', '-X', '-v', 'ON_ERROR_STOP=1', input=restricted,
                     stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def migrate_release(release, database=None, stop=stop_services, restart=restart_services):
    manifest = release / 'api/schema/migrations.json'
    sql = release / 'api/schema/migrations.sql'
    for path in (manifest, sql):
        trusted(path)
    expected = migration_manifest(manifest)
    if sql.stat().st_size > 32 * 1024 * 1024 or sql.stat().st_size == 0:
        raise Refused('Invalid migration SQL size')
    database = database or MigrationDatabase(migration_config())
    if database.identity() != 'argintaxpayer_prod|argin_prod_migrator|true':
        raise Refused('Unexpected actual migration database, role or TLS state')
    pending = pending_migrations(expected, database.applied())
    if not pending:
        print('[PASS] Database already matches release migration manifest', flush=True)
        return False
    print(f'[START] Applying {len(pending)} pending migrations before release activation', flush=True)
    BACKUPS.mkdir(mode=0o700, parents=True, exist_ok=True)
    trusted(BACKUPS, directory=True)
    if BACKUPS.stat().st_mode & 0o077:
        raise Refused('Backup directory must be private to root')
    backup = BACKUPS / f'{release.name}-{time.time_ns()}.dump.enc'
    try:
        stop()
        # Recheck after stopping writers; the release-wide flock serializes deploys.
        pending_migrations(expected, database.applied())
        database.backup(backup)
        database.apply(sql)
        if database.applied() != expected:
            raise Refused('Post-migration history differs from release manifest')
    except BaseException:
        restart()  # current still points to the old release; never roll back the DB.
        raise
    print('[PASS] Backup verified and all release migrations applied', flush=True)
    return True


def main(arguments):
    if arguments == ['--capabilities']:
        print('schema-before-promote-v1')
        return
    if (len(arguments) not in (2, 3) or arguments[0] not in ('--deploy', '--promote-staged')
            or len(arguments) == 3 and (arguments[0] != '--promote-staged' or arguments[2] != '--initial-disabled-ui')):
        raise Refused('Usage: argin-taxpayer-release --deploy <full-SHA>')
    sha = validate_sha(arguments[1])
    if os.geteuid() != 0:
        raise Refused('Use the dedicated sudo deployment entry point')
    os.umask(0o077)
    os.chdir('/')
    deployment = pwd.getpwnam('argin-deploy')
    service_gid = grp.getgrnam('argintaxsvc').gr_gid
    for path in (BASE, RELEASES):
        trusted(path, directory=True)
    for path in (PROBE, RUNTIME / 'node', RUNTIME / 'npm'):
        trusted(path)
    for name in ('api.env', 'web.env', 'phase18-activation-approved'):
        trusted(Path('/etc/argintaxpayer') / name)
    for unit in UNITS:
        result = subprocess.run(['/usr/bin/systemctl', 'show', unit, '-p', 'User', '--value'],
                                check=True, capture_output=True, text=True, env=ENV)
        if result.stdout.strip() != 'argintaxsvc':
            raise Refused('Application services must run as argintaxsvc')
    # One lock for all workflow events and manual invocations, including retries.
    lock_fd = os.open('/run/lock/argintaxpayer-release.lock',
                      os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    with os.fdopen(lock_fd, 'w') as lock:
        info = os.fstat(lock.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
            raise Refused('Unsafe deployment lock file')
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Refused('Another deployment is in progress') from None
        current = BASE / 'current'
        if current.is_symlink():
            previous = current.resolve(strict=True)
            if previous.parent != RELEASES or not re.fullmatch(r'[a-f0-9]{40}', previous.name):
                raise Refused('Unexpected current release target')
            trusted(previous, directory=True)
        elif current.exists():
            raise Refused('Current path must be a release symlink')
        if arguments[0] == '--promote-staged':
            release = RELEASES / sha
            trusted(release, directory=True)
        else:
            release = prepare_release(sha, deployment, service_gid)
        for name in ('api/ArginTaxPayer.Api.dll', 'web/.next/BUILD_ID',
                     'web/node_modules/next/dist/bin/next'):
            trusted(release / name)
            run(['/usr/sbin/runuser', '-u', 'argintaxsvc', '--',
                 '/usr/bin/test', '-r', str(release / name)], timeout=10)
        trusted(release / 'build-info.txt')
        if (release / 'build-info.txt').read_text().splitlines().count(f'sha={sha}') != 1:
            raise Refused('Staged release source SHA mismatch')
        probe_mode = '--probe-local'
        if len(arguments) == 3:
            for filename, required in (
                ('api.env', ('ArginTaxPayer__EnableOperationalEndpoints=false', 'Ghasedak__EnableLiveSending=false',
                             'SandboxInvoiceLegacy__Enabled=false')),
                ('web.env', ('ARGIN_PRODUCTION_UI_ENABLED=false',))):
                lines = (Path('/etc/argintaxpayer') / filename).read_text().splitlines()
                if not all(line in lines for line in required):
                    raise Refused('Disabled startup requires provider and business gates false')
            probe_mode = '--probe-local-disabled-ui'
        migrate_release(release)
        promote(release, current, restart_services, lambda: healthy(probe_mode), stop_services)
    print(f'[PASS] Release {sha} promoted and loopback health PASS.', flush=True)


if __name__ == '__main__':
    try:
        def interrupted(signum, frame):
            raise Refused('Deployment interrupted')
        signal.signal(signal.SIGTERM, interrupted)
        signal.signal(signal.SIGHUP, interrupted)
        main(sys.argv[1:])
    except (Refused, OSError, subprocess.SubprocessError, tarfile.TarError,
            UnicodeError, EOFError, ValueError, KeyError, MemoryError, RecursionError):
        # Never expose private configuration, payloads, subprocess output or credentials.
        error = sys.exc_info()[1]
        label = str(error) if isinstance(error, Refused) else type(error).__name__
        print(f'[REFUSED] {label}', file=sys.stderr)
        sys.exit(2)
