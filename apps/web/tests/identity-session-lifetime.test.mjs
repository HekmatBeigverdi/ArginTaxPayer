import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');

for(const environment of ['development','production']){
test(`login and refresh use browser-session cookies with server-controlled expiry in ${environment}`,async()=>{
 for(const operation of ['login','refresh','mfa/complete','phone/login/complete']){
  const api=loadTsx(new URL('../app/api/identity/[...path]/route.ts',import.meta.url),{
   globals:{process:{env:{NODE_ENV:environment}}},
   modules:{
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
  assert.equal(response.cookies.get('access').maxAge,undefined);
  assert.equal(response.cookies.get('access').httpOnly,true);
  assert.equal(response.cookies.get('access').secure,environment==='production');
  assert.equal(response.cookies.get('refresh').maxAge,undefined);
  assert.equal(response.cookies.get('refresh').secure,environment==='production');
  assert.deepEqual(await response.json(),{status:'authenticated'});
 }
});
}

test('session activity is authenticated, keeps credentials off the response, and propagates expiry', async () => {
 const calls = [];
 let status = 200;
 const api = loadTsx(new URL('../app/api/identity/[...path]/route.ts', import.meta.url), {
  globals: { process },
  modules: {
   '../../../../lib/identity': {
    ACCESS_COOKIE: 'access', REFRESH_COOKIE: 'refresh', identityUiEnabled: () => true,
    identityBackend: async (path, options) => {
     calls.push({ path, options });
     return Response.json({ expiresInMs: 3500000, accessToken: 'must-not-leak' }, { status });
    },
   },
  },
 });
 const context = { params: Promise.resolve({ path: ['activity'] }) };
 function request(method, origin = 'http://localhost:3000') {
  return new NextRequest('http://localhost:3000/api/identity/activity', {
   method, headers: { host: 'localhost:3000', origin, cookie: 'access=old-access' },
  });
 }
 for (const method of ['GET', 'POST']) {
  const response = await api[method](request(method), context);
  assert.equal(response.status, 200);
  assert.equal(calls.at(-1).path, 'activity');
  assert.equal(calls.at(-1).options.method, method);
  assert.equal(calls.at(-1).options.headers.Authorization, 'Bearer old-access');
  assert.deepEqual(await response.json(), { expiresInMs: 3500000 });
  assert.equal(response.headers.get('set-cookie'), null);
 }
 assert.equal((await api.POST(request('POST', 'https://other.example'), context)).status, 403);
 assert.equal(calls.length, 2);
 status = 401;
 assert.equal((await api.GET(request('GET'), context)).status, 401);
});

test('trusted activity resets the idle timer, and an expired session navigates away without a reload', async () => {
 const listeners = new Map();
 const timers = new Map();
 const methods = [];
 const blocked = [];
 let nextTimer = 0;
 let now = 0;
 let expirations = 0;
 let serverStatus = 200;
 const document = {
  visibilityState: 'visible',
  addEventListener: (event, callback) => listeners.set(event, callback),
  removeEventListener: event => listeners.delete(event),
 };
 const window = {
  addEventListener: document.addEventListener,
  removeEventListener: document.removeEventListener,
  setTimeout(callback, delay) {
   const id = ++nextTimer;
   timers.set(id, { callback() { timers.delete(id); callback(); }, delay });
   return id;
  },
  clearTimeout: id => timers.delete(id),
 };
 const { trackSessionActivity } = loadTsx(new URL('../lib/session-activity.ts', import.meta.url), {
  globals: {
   document, window, AbortSignal, performance: { now: () => now },
   fetch: async (path, { method }) => {
    assert.equal(path, '/api/identity/activity');
    methods.push(method);
    return serverStatus !== 200 ? new Response(null, { status: serverStatus })
     : Response.json({ expiresInMs: method === 'POST' ? 3600000 : 2000 });
   },
  },
 });
 const settle = () => new Promise(setImmediate);
 const stop = trackSessionActivity(() => expirations++, value => blocked.push(value));
 await settle();
 assert.deepEqual(methods, ['GET']);
 assert.equal([...timers.values()][0].delay, 2000);
 listeners.get('keydown')({ isTrusted: false });
 assert.deepEqual(methods, ['GET']);
 listeners.get('pointerdown')({ isTrusted: true });
 await settle();
 assert.deepEqual(methods, ['GET', 'POST']);
 assert.equal(timers.size, 1);
 assert.equal([...timers.values()][0].delay, 3600000);

 now += 3600000;
 serverStatus = 503;
 [...timers.values()][0].callback();
 await settle();
 assert.deepEqual(methods, ['GET', 'POST', 'GET']);
 assert.equal(blocked.at(-1), true);
 assert.equal(expirations, 0);

 serverStatus = 401;
 [...timers.values()][0].callback();
 await settle();
 assert.deepEqual(methods, ['GET', 'POST', 'GET', 'GET']);
 assert.equal(expirations, 1);
 assert.equal(listeners.size, 0);
 assert.equal(timers.size, 0);
 stop();

 serverStatus = 503;
 blocked.length = 0;
 const stopAfterInitialFailure = trackSessionActivity(() => expirations++, value => blocked.push(value));
 await settle();
 assert.deepEqual(blocked, [true]);
 assert.equal(expirations, 1);
 stopAfterInitialFailure();
});
