import test from 'node:test';
import assert from 'node:assert/strict';
import {loadTsx} from './helpers/load-tsx.mjs';

const model=()=>loadTsx(new URL('../lib/fiscal-connection-status.ts',import.meta.url));
const at='2026-11-11T12:00:00Z';

test('configured standalone Sandbox wins over its unconfigured duplicate; Production never uses that key',()=>{
 const {sandboxSourceChoice}=model();
 const legacy={memoryIdentifier:'ABC123',keyCustodyReady:true};
 assert.equal(sandboxSourceChoice(legacy,{memoryIdentifier:'ABC123',hasKey:false},2),'legacy');
 assert.equal(sandboxSourceChoice(legacy,{memoryIdentifier:'ABC123',hasKey:false},1),'registered');
 assert.equal(sandboxSourceChoice(legacy,{memoryIdentifier:'ABC123',hasKey:true},2),'choice');
 assert.equal(sandboxSourceChoice(legacy,{memoryIdentifier:'ABC123'},2),'loading');
 assert.equal(sandboxSourceChoice(legacy,{memoryIdentifier:'DIFF01',hasKey:true},2),'separate');
});

test('stored keys without a recorded probe are not presented as connected',()=>{
 const view=model().getConnectionPresentation(true,null);
 assert.equal(view.state,'ready');assert.equal(view.needsKeyAction,false);
 assert.notEqual(view.label,'اتصال موفق');
});

test('legacy last-test fields remain visible when the new durable history table is unavailable',()=>{
 const {historyWithLegacyAttempt}=model();
 const history=historyWithLegacyAttempt({available:false,lastAttempt:null,lastSuccess:null},true,at);
 assert.deepEqual(JSON.parse(JSON.stringify(history)),{available:false,
  lastAttempt:{status:'Authenticated',testedAtUtc:at},lastSuccess:{status:'Authenticated',testedAtUtc:at}});
 const failed=historyWithLegacyAttempt({available:false,lastAttempt:null,lastSuccess:null},false,at);
 assert.equal(failed.lastAttempt.status,'Indeterminate');assert.equal(failed.lastSuccess,null);
});

test('a timeout retains previous success evidence without offering key replacement',()=>{
 const history={available:true,lastAttempt:{status:'TimedOut',testedAtUtc:at},lastSuccess:{status:'Authenticated',testedAtUtc:at}};
 const view=model().getConnectionPresentation(true,history);
 assert.equal(view.state,'service-error');assert.equal(view.needsKeyAction,false);
 assert.equal(view.lastSuccess.testedAtUtc,at);
});

test('key-related failure offers entry/recovery; missing configuration is not an authority rejection',()=>{
 assert.equal(model().getConnectionPresentation(true,{available:true,lastAttempt:{status:'KeyNotReady',testedAtUtc:at},lastSuccess:null}).needsKeyAction,true);
 assert.equal(model().getConnectionPresentation(false,null).state,'setup');
});

test('the API projection drops secrets, rejects invalid statuses/dates and does not fabricate history availability',()=>{
 const {projectConnectionHistory}=model();
 const view=projectConnectionHistory({available:true,lastAttempt:{status:'Authenticated',testedAtUtc:at,token:'secret'},lastSuccess:{status:'evil',testedAtUtc:'invalid'},privateKey:'secret'});
 assert.deepEqual(JSON.parse(JSON.stringify(view)),{available:true,lastAttempt:{status:'Authenticated',testedAtUtc:at},lastSuccess:null});
 assert.equal(projectConnectionHistory(undefined).available,false);
 assert.equal(projectConnectionHistory({available:true,lastAttempt:{status:'Authenticated',testedAtUtc:'invalid'}}).lastAttempt,null);
});

test('invoice selection hides only unusable duplicate Sandbox registrations and preserves an existing draft binding',()=>{
 const rows=[{id:'pending',memoryIdentifier:'ABC123',status:1,authorityEnvironment:2},
  {id:'active',memoryIdentifier:'ABC123',status:2,authorityEnvironment:2},
  {id:'production',memoryIdentifier:'ABC123',status:1,authorityEnvironment:1},
  {id:'other',memoryIdentifier:'OTHER1',status:1,authorityEnvironment:2}];
 const legacy=[{memoryIdentifier:'ABC123'}];
 const {visibleInvoiceMemories}=model();
 assert.deepEqual(Array.from(visibleInvoiceMemories(rows,legacy,''),m=>m.id),['active','production','other']);
 assert.deepEqual(Array.from(visibleInvoiceMemories(rows,legacy,'pending'),m=>m.id),['pending','active','production','other']);
});
