import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');

test('login and refresh keep access cookies for 30 minutes',async()=>{
 for(const operation of ['login','refresh','mfa/complete','phone/login/complete']){
  const api=loadTsx(new URL('../app/api/identity/[...path]/route.ts',import.meta.url),{modules:{
   '../../../../lib/identity':{
    ACCESS_COOKIE:'access',REFRESH_COOKIE:'refresh',identityUiEnabled:()=>true,
    identityBackend:async()=>Response.json({accessToken:'new-access',refreshToken:'new-refresh'})
   },
   '../../../../lib/identity-origin.mjs':{isSameLocalOrigin}
  }});
  const request=new NextRequest('http://localhost:3000/api/identity/'+operation,{method:'POST',
   headers:{host:'localhost:3000',origin:'http://localhost:3000',cookie:'refresh=old-refresh','Content-Type':'application/json'},body:'{}'});
  const response=await api.POST(request,{params:Promise.resolve({path:operation.split('/')})});
  assert.equal(response.status,200);
  assert.equal(response.cookies.get('access').maxAge,1800);
  assert.equal(response.cookies.get('access').httpOnly,true);
  assert.equal(response.cookies.get('refresh').maxAge,7*86400);
  assert.deepEqual(await response.json(),{status:'authenticated'});
 }
});
