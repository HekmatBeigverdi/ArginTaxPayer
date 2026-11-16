import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const read=p=>readFileSync(new URL('../../../'+p,import.meta.url),'utf8');
const unit=read('ops/phase18-systemd/argintaxpayer-backup.service');
test('backup controller permits only required sudo capabilities and privileged selection writes',()=>{
 assert.match(unit,/^NoNewPrivileges=false$/m);
 for(const option of ['PrivateDevices','ProtectKernelTunables','ProtectKernelModules','RestrictSUIDSGID','LockPersonality'])assert.doesNotMatch(unit,new RegExp('^'+option+'=true$','m'));
 assert.match(unit,/^CapabilityBoundingSet=CAP_SETUID CAP_SETGID CAP_CHOWN CAP_DAC_OVERRIDE CAP_FOWNER CAP_AUDIT_WRITE$/m);
 assert.match(unit,/^ReadWritePaths=.*\/etc\/argintaxpayer$/m);
 assert.equal((unit.match(/Environment=BackupController__EncryptionKeyPath=/g)||[]).length,1);
});
test('worker storage is separate from root migration archive directory',()=>{
 assert.match(unit,/\/var\/lib\/argintaxpayer\/database-backups/);
 assert.doesNotMatch(unit,/\/var\/backups\/argintaxpayer\/database/);
});
test('activation starts API before an independent delayed controller restart',()=>{
 const helper=read('scripts/phase18-backup-host-helper.sh');
 assert.doesNotMatch(helper,/restart --no-block argintaxpayer-backup/);
 assert.ok(helper.indexOf('/usr/bin/systemctl start argintaxpayer-api.service')<helper.indexOf('/usr/bin/systemd-run'));
 assert.match(helper,/--on-active=60s/);
});
test('upgrade installs database-selection API drop-in and deployment invokes safe preflight',()=>{
 assert.match(read('scripts/phase18-install-deploy-helper.sh'),/argintaxpayer-api-backup\.conf/);
 assert.match(read('ops/phase18-systemd/argintaxpayer-api-backup.conf'),/EnvironmentFile=-\/etc\/argintaxpayer\/backup-db\.env/);
 assert.match(read('scripts/phase18-manual-build-deploy.sh'),/sudo bash scripts\/phase18-backup-preflight\.sh/);
 assert.match(read('scripts/phase18-backup-preflight.sh'),/status-business/);
});

test('production workflow builds, checksums and uploads the backup worker and rejects legacy helper',()=>{
 const workflow=read('.github/workflows/production-release.yml');
 assert.match(workflow,/dotnet publish src\/ArginTaxPayer.Workers\/ArginTaxPayer.Workers.csproj/);
 assert.match(workflow,/cp -a "\$RUNNER_TEMP\/backup\/." bundle\/stage\/backup\//);
 assert.match(workflow,/tar -czf ..\/backup.tar.gz backup/);
 assert.match(workflow,/sha256sum api.tar.gz web.tar.gz backup.tar.gz/);
 assert.match(workflow,/bundle\/api.tar.gz bundle\/web.tar.gz bundle\/backup.tar.gz/);
 assert.match(workflow,/schema-and-backup-before-promote-v2/);
 assert.doesNotMatch(workflow,/schema-before-promote-v1/);
});
