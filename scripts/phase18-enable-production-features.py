#!/usr/bin/env python3
"""Operator-approved activation; preserve credentials and never switch databases."""
import os
from pathlib import Path
import stat
import tempfile

API_FLAGS = {
    'ArginTaxPayer__EnableOperationalEndpoints': 'true',
    'ArginTaxPayer__HostedSandboxEnabled': 'true',
    'Ghasedak__EnableLiveSending': 'true',
    'SandboxInvoiceLegacy__Enabled': 'true',
    'SandboxInvoiceLegacy__OperatorApprovedEndpoint': 'true',
    'ProductionInvoiceLegacy__Enabled': 'true',
    'ProductionInvoiceLegacy__OperatorApprovedEndpoint': 'true',
}
WEB_FLAGS = {'ARGIN_IDENTITY_UI_ENABLED': 'true', 'ARGIN_PRODUCTION_UI_ENABLED': 'true'}


def updated(content, flags):
    lines = [line for line in content.splitlines()
             if line.partition('=')[0] not in flags]
    return '\n'.join(lines + [f'{key}={value}' for key, value in flags.items()]) + '\n'


def main():
    if os.geteuid() != 0:
        raise SystemExit('Run with sudo as the VPS operator')
    root = Path('/etc/argintaxpayer')
    # Validate both files before any mutation. No credentials are printed.
    entries = []
    for name, flags in [('api.env', API_FLAGS), ('web.env', WEB_FLAGS)]:
        path = root / name
        info = path.lstat()
        if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
            raise SystemExit(f'Unsafe configuration file: {name}')
        content = path.read_text()
        if name == 'api.env' and 'Database=argintaxpayer_prod;' not in content:
            raise SystemExit('Expected base database argintaxpayer_prod; configuration was not changed')
        entries.append((path, info, updated(content, flags)))
    for path, info, content in entries:
        backup = path.with_suffix('.env.before-feature-activation')
        if not backup.exists():
            fd = os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, 'w') as stream:
                stream.write(path.read_text())
        fd, temporary = tempfile.mkstemp(dir=root, prefix='.feature-activation-')
        try:
            with os.fdopen(fd, 'w') as stream:
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
            os.chown(temporary, info.st_uid, info.st_gid)
            os.chmod(temporary, stat.S_IMODE(info.st_mode))
            os.replace(temporary, path)
        finally:
            if os.path.exists(temporary):
                os.unlink(temporary)
    print('[PASS] Production API/UI, SMS and both invoice environments enabled; credentials and database selection preserved.')
    print('[NEXT] Restart API/Web or approve the next production deployment to load these settings.')


if __name__ == '__main__':
    main()
