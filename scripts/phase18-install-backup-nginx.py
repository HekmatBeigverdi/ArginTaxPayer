#!/usr/bin/python3 -I
"""Operator-only upgrade of the tax host's authenticated ZIP proxy location."""
import hashlib
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile


def main():
    if os.geteuid() != 0 or sys.argv[1:] != ['--apply']:
        raise SystemExit('Run with sudo and --apply after review.')
    config = Path('/etc/nginx/sites-available/tax.argin.ir')
    snippet = Path('/etc/nginx/snippets/argintaxpayer-backup.conf')
    if Path('/etc/nginx/sites-enabled/tax.argin.ir').resolve() != config:
        raise SystemExit('Unexpected active tax host configuration.')
    for path in [config, config.parent, snippet.parent, Path('/etc/nginx')]:
        if path.is_symlink() or not path.exists() or path.stat().st_uid != 0 or path.stat().st_mode & 0o022:
            raise SystemExit('Unsafe or missing privileged Nginx path: ' + str(path))
    if snippet.is_symlink():
        raise SystemExit('Snippet must not be a symlink.')
    old = config.read_bytes()
    source = (Path(__file__).resolve().parent.parent / 'ops/phase18-nginx/tax-next-location.conf').read_text()
    location = source[source.index('location ^~ /api/backup-control/ {'):].encode()
    previous = snippet.read_bytes() if snippet.exists() else None
    if previous is not None and previous != location:
        raise SystemExit('Existing backup snippet differs; review before replacing.')
    include = b'    include /etc/nginx/snippets/argintaxpayer-backup.conf;\n'
    marker = b'    listen 443 ssl; # managed by Certbot'
    if include not in old and old.count(marker) != 1:
        raise SystemExit('Unexpected HTTPS host layout; no configuration changed.')
    new = old if include in old else old.replace(marker, include + b'\n' + marker, 1)
    archive = Path('/var/backups/argintaxpayer-helper')
    if archive.is_symlink():
        raise SystemExit('Unsafe archive directory.')
    archive.mkdir(mode=0o700, exist_ok=True)
    if archive.stat().st_uid != 0 or stat.S_IMODE(archive.stat().st_mode) != 0o700:
        raise SystemExit('Nginx archive directory must be root-owned 0700.')
    backup = archive / ('nginx-tax-' + hashlib.sha256(old).hexdigest() + '.conf')
    if not backup.exists():
        with backup.open('xb') as output:
            os.chmod(backup, 0o600)
            output.write(old)

    def publish(path, content):
        fd, name = tempfile.mkstemp(prefix='.argin-backup-', dir=path.parent)
        try:
            with os.fdopen(fd, 'wb') as output:
                os.fchmod(output.fileno(), 0o644)
                output.write(content)
                output.flush()
                os.fsync(output.fileno())
            os.replace(name, path)
        finally:
            Path(name).unlink(missing_ok=True)
    try:
        publish(snippet, location)
        publish(config, new)
        subprocess.run(['/usr/sbin/nginx', '-t'], check=True, timeout=30)
        subprocess.run(['/usr/bin/systemctl', 'reload', 'nginx'], check=True, timeout=30)
    except Exception:
        publish(config, old)
        if previous is None:
            snippet.unlink(missing_ok=True)
        else:
            publish(snippet, previous)
        raise
    print('[PASS] Tax host backup ZIP location installed, nginx syntax checked and reloaded.')


if __name__ == '__main__':
    main()
