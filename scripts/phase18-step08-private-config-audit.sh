#!/usr/bin/env bash
# Phase18 Step08: read-only PRIVATE config audit; never exposes values.
set -Eeuo pipefail
umask 077
fail(){ printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[a-f0-9]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Independent matching macOS SHA required'
sudo -v
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  if systemctl is-active --quiet "$unit"; then fail "Unexpected active unit: $unit"; fi
done
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] || fail 'Activation marker present'
sudo -n python3 - <<'PY'
import os, stat, re, base64, pathlib
root=pathlib.Path('/etc/argintaxpayer')
assert root.is_dir() and stat.S_IMODE(root.stat().st_mode)==0o700, 'private config directory unsafe'
for name in ('api.env','web.env'):
    f=root/name
    s=f.stat()
    assert s.st_uid==0 and s.st_gid==0 and stat.S_IMODE(s.st_mode)==0o600, 'secret file permissions unsafe'
    assert not f.is_symlink(), 'symlink refused'
def parse(file):
    lines=(root/file).read_text().splitlines()
    result={}
    for l in lines:
        if not l.strip() or l.lstrip().startswith('#'):continue
        if '=' not in l:raise RuntimeError('malformed env')
        k,v=l.split('=',1)
        if k in result:raise RuntimeError('duplicate env key')
        result[k]=v
    return result
a=parse('api.env');w=parse('web.env')
for key in ('Identity__MfaEncryptionKey','SigningKeyProtection__DatabaseMasterKey'):
    assert len(base64.b64decode(a[key],validate=True))==32, 'invalid AES-256 key'
assert a['Identity__MfaEncryptionKey']!=a['SigningKeyProtection__DatabaseMasterKey'], 'key reuse'
assert a['SigningKeyProtection__Enabled']=='true' and a['SigningKeyProtection__Provider']=='Database'
assert a['ArginTaxPayer__EnableOperationalEndpoints']=='false'
assert a['Ghasedak__EnableLiveSending']=='false'
assert a['Identity__AllowDevelopmentRegistration']=='false'
assert a['SandboxInvoiceLegacy__Enabled']=='false'
cs=a['ConnectionStrings__ArginTaxPayerDb']
assert all(s in cs for s in ('Host=127.0.0.1','Port=15434','Database=argintaxpayer_prod','Username=argin_prod_app','SSL Mode=VerifyFull','Root Certificate=/etc/argintaxpayer/prod-db-ca.pem'))
assert re.search(r'Password=[0-9a-f]{64}(?:;|$)',cs), 'invalid app credential'
assert w['ARGIN_PRODUCTION_UI_ENABLED']=='false' and w['ARGIN_IDENTITY_UI_ENABLED']=='false'
ca=root/'prod-db-ca.pem'
assert ca.exists() and not ca.is_symlink() and ca.stat().st_uid==0 and stat.S_IMODE(ca.stat().st_mode)==0o644, 'unsafe CA'
print('[PASS] root-only 0600 files, independent 256-bit keys and dedicated TLS runtime DB config')
print('[PASS] production authority/SMS/UI execution remains disabled')
PY
echo '[BLOCKED] No service activation, runtime restart/recovery test or real-provider requests performed.'
