#!/usr/bin/python3 -I
"""Operator-only provisioning of a dedicated read-only whole-database backup role."""
import grp
from importlib.machinery import SourceFileLoader
import importlib.util
import os
from pathlib import Path
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
    spec = importlib.util.spec_from_loader('reviewed_helper', SourceFileLoader('reviewed_helper', str(helper)))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    profile = module.migration_config()
    target = Path('/etc/argintaxpayer/backup-read.env')
    if target.is_symlink():
        fail('Backup reader configuration cannot be a symlink.')
    if target.exists():
        if target.stat().st_uid != 0 or stat.S_IMODE(target.stat().st_mode) != 0o640:
            fail('Existing reader configuration requires permission review; preserved.')
        print('[PASS] Preserved existing backup reader credentials.')
        return
    def sql(statement):
        result = subprocess.run(['/usr/bin/docker','exec','-i','-u','postgres',profile['CONTAINER'],
                                 'psql','--username','argin_prod_bootstrap','-X','-qAt','-v','ON_ERROR_STOP=1','-d','postgres'],
                                input=statement,text=True,capture_output=True,timeout=30)
        if result.returncode:
            fail('Backup reader provisioning failed; private SQL/credentials withheld.')
        return result.stdout.strip()
    if sql("SELECT 1 FROM pg_roles WHERE rolname='argin_backup_reader';"):
        fail('Backup reader role exists without its configuration; refusing password reset.')
    lines = Path('/etc/argintaxpayer/api.env').read_text().splitlines()
    connection = next(x.partition('=')[2].strip().strip('"').strip("'") for x in lines if x.startswith('ConnectionStrings__ArginTaxPayerDb='))
    fields = dict(x.split('=',1) for x in connection.split(';') if '=' in x)
    password = secrets.token_hex(32)
    fields.update(Username='argin_backup_reader',Password=password)
    value = ';'.join(k+'='+v for k,v in fields.items()).replace('\\','\\\\').replace('"','\\"')
    staging = target.with_name('.backup-read.'+secrets.token_hex(8)+'.tmp')
    fd = os.open(staging,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    role_created = False
    try:
        with os.fdopen(fd,'w') as output:
            output.write('ConnectionStrings__DatabaseBackupRead="'+value+'"\n')
            output.flush()
            os.fsync(output.fileno())
        sql("BEGIN; CREATE ROLE argin_backup_reader LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '"
            +password+"'; GRANT CONNECT ON DATABASE argintaxpayer_prod TO argin_backup_reader; GRANT pg_read_all_data TO argin_backup_reader; COMMIT;")
        role_created = True
        os.chown(staging,0,grp.getgrnam('argintaxsvc').gr_gid)
        os.chmod(staging,0o640)
        os.link(staging,target)
        print('[PASS] Dedicated read-only backup reader created; application role unchanged.')
    finally:
        if role_created and not target.exists():
            os.chown(staging,0,0)
            os.chmod(staging,0o600)
            print('[RECOVERY] Private reader credentials retained at '+str(staging))
        else:
            staging.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
