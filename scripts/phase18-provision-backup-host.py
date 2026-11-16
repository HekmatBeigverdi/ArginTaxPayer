#!/usr/bin/python3 -I
"""Operator-only: provision missing backup key and a separate non-superuser restore role.
Never changes application data, restarts services, or replaces existing credentials.
"""
import grp
import importlib.util
import os
from pathlib import Path
import pwd
import re
import secrets
import stat
import subprocess
import sys


def fail(message):
    raise SystemExit('[FAIL] ' + message)


def main():
    if os.geteuid() != 0 or sys.argv[1:] != ['--apply']:
        fail('Run with sudo and --apply after reviewing this script.')
    helper = Path('/usr/local/sbin/argin-taxpayer-release')
    if helper.is_symlink() or helper.stat().st_uid != 0 or helper.stat().st_mode & 0o022:
        fail('Reviewed root-owned deployment helper required.')
    spec = importlib.util.spec_from_file_location('reviewed_release_helper', helper)
    # The installed helper has no .py suffix, so use an explicit source loader.
    from importlib.machinery import SourceFileLoader
    spec = importlib.util.spec_from_loader('reviewed_release_helper', SourceFileLoader('reviewed_release_helper', str(helper)))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    configuration = module.migration_config()  # Verifies exact production container/DB/TLS identity.
    uid = pwd.getpwnam('argintaxsvc').pw_uid
    gid = grp.getgrnam('argintaxsvc').gr_gid
    key = Path('/etc/argintaxpayer/database-backup.key')
    if key.is_symlink():
        fail('Encryption key cannot be a symlink.')
    if not key.exists():
        fd = os.open(key, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, 'w') as output:
            output.write('ubuntu-1\n' + secrets.token_hex(32) + '\n')
            output.flush()
            os.fsync(output.fileno())
        os.chown(key, uid, gid)
        print('[PASS] Created private backup key; retain an independent secure escrow copy.')
    else:
        lines = key.read_text().splitlines()
        if (len(lines) != 2 or not re.fullmatch(r'[A-Za-z0-9_-]{1,32}', lines[0])
                or not re.fullmatch(r'[A-Fa-f0-9]{64}', lines[1])
                or key.stat().st_uid != uid or stat.S_IMODE(key.stat().st_mode) != 0o600):
            fail('Existing key requires operator review; it was not replaced.')
        print('[PASS] Preserved existing private backup key.')

    destination = Path('/etc/argintaxpayer/backup-restore.env')
    if destination.is_symlink():
        fail('Restore environment cannot be a symlink.')
    if destination.exists():
        print('[PASS] Preserved existing restore credentials; validate with backup preflight.')
        return

    def sql(statement):
        result = subprocess.run(['/usr/bin/docker', 'exec', '-i', '-u', 'postgres', configuration['CONTAINER'],
                                 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-d', 'postgres'],
                                input=statement, text=True, capture_output=True, timeout=30)
        if result.returncode:
            fail('PostgreSQL role provisioning failed; diagnostic SQL/credentials withheld.')
        return result.stdout.strip()

    role = 'argin_backup_restore'
    if sql("SELECT 1 FROM pg_roles WHERE rolname='argin_backup_restore';"):
        fail('Restore role already exists without its credential file; refusing password reset.')
    password = secrets.token_hex(32)
    runtime = Path('/etc/argintaxpayer/api.env').read_text().splitlines()
    connection = next(line.partition('=')[2].strip().strip('"').strip("'") for line in runtime
                      if line.startswith('ConnectionStrings__ArginTaxPayerDb='))
    fields = dict(part.split('=', 1) for part in connection.split(';') if '=' in part)
    fields.update(Database='postgres', Username=role, Password=password)
    restore_connection = ';'.join(k + '=' + v for k, v in fields.items())
    encoded = restore_connection.replace('\\', '\\\\').replace('"', '\\"')
    staging = destination.with_name('.backup-restore.' + secrets.token_hex(8) + '.tmp')
    fd = os.open(staging, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, 'w') as output:
            output.write('ConnectionStrings__DatabaseBackupRestore="' + encoded + '"\n')
            output.flush()
            os.fsync(output.fileno())
        # Password is generated hexadecimal; SQL is sent on stdin and never logged here.
        sql("BEGIN; CREATE ROLE argin_backup_restore LOGIN CREATEDB NOSUPERUSER NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '"
            + password + "'; GRANT argin_prod_app TO argin_backup_restore; COMMIT;")
        os.chown(staging, 0, gid)
        os.chmod(staging, 0o640)
        os.link(staging, destination)  # Never overwrite concurrently provisioned credentials.
        print('[PASS] Created separate non-superuser restore role and private connection file.')
    finally:
        staging.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
