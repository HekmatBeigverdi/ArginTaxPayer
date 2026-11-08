import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync, writeFileSync, rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const script=fileURLToPath(new URL('../../../scripts/phase18-step06-local-health.sh',import.meta.url));
function probe(overrides={}, mode='--probe-local') {
 const bin=mkdtempSync(join(tmpdir(),'argin-health-'));
 try {
  const programs={
   uname:'#!/bin/sh\necho Linux\n',
   ss:'#!/bin/sh\nprintf "LISTEN 0 128 127.0.0.1:15090 0.0.0.0:*\\nLISTEN 0 128 127.0.0.1:15091 0.0.0.0:*\\n"\n',
   curl:`#!/bin/sh
for argument do url="$argument"; done
case "$url" in
 */health/live|*/health/ready) printf 200;;
 */health/dev-identity) printf 404;;
 */api/dev/identity/authorization/me) printf '%s' "$TEST_API_AUTH";;
 */api/identity/me) printf '%s' "$TEST_BFF_AUTH";;
 */identity) printf '%s' "$TEST_LOGIN";;
 *) exit 9;;
esac
`
  };
  for(const [name,content] of Object.entries(programs))writeFileSync(join(bin,name),content,{mode:0o700});
  return spawnSync('bash',[script,mode],{encoding:'utf8',env:{...process.env,
   PATH:bin+':'+process.env.PATH,TEST_LOGIN:'200',TEST_API_AUTH:'401',TEST_BFF_AUTH:'401',...overrides}});
 } finally {rmSync(bin,{recursive:true,force:true});}
}

test('active deployment requires an accessible login and session-protected API/BFF',()=>{
 const r=probe();assert.equal(r.status,0,r.stdout+r.stderr);
});
for(const status of ['307','308','404'])test(`active deployment rejects disabled login HTTP ${status}`,()=>{
 const r=probe({TEST_LOGIN:status});assert.notEqual(r.status,0,r.stdout+r.stderr);
});
test('active deployment rejects disabled BFF even when login HTML is available',()=>{
 const r=probe({TEST_BFF_AUTH:'404'});assert.notEqual(r.status,0,r.stdout+r.stderr);
});
test('active deployment rejects API operational routes that are not registered',()=>{
 const r=probe({TEST_API_AUTH:'404'});assert.notEqual(r.status,0,r.stdout+r.stderr);
});
test('disabled staging remains distinguishable from an active application',()=>{
 const r=probe({TEST_LOGIN:'404',TEST_BFF_AUTH:'404',TEST_API_AUTH:'404'},'--probe-local-disabled-ui');
 assert.equal(r.status,0,r.stdout+r.stderr);
 assert.doesNotMatch(r.stdout,/authenticated application ready/i);
});
