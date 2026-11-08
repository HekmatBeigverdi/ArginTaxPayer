import test from 'node:test';
import assert from 'node:assert/strict';
import {loadTsx} from './helpers/load-tsx.mjs';

for (const [actor, destination] of [
  [null, '/identity'],
  [{userId:'signed-in-customer',roles:['Customer']}, '/dashboard'],
  [{userId:'signed-in-admin',roles:['Level1']}, '/dashboard'],
]) {
  test(`home sends ${actor?.roles[0] ?? 'signed-out visitor'} to ${destination}`, async()=>{
    const redirected = new Error('redirect');
    const home=loadTsx(new URL('../app/page.tsx',import.meta.url),{modules:{
      '../lib/identity':{identityUiEnabled:()=>true,currentIdentity:async()=>actor},
      '../lib/api-health':{getApiHealth:()=>assert.fail('Active application must open login or dashboard')},
      'next/navigation':{redirect(path){assert.equal(path,destination);throw redirected;}},
    }});
    await assert.rejects(home.default(),error=>error===redirected);
  });
}
