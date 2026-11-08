import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const mid='22222222-2222-2222-2222-222222222222';
function harness(overrides={}){
 const states=[];let index=0;const calls=[];let refreshed=0;
 const component=loadTsx(new URL('../app/taxpayers/fiscal-memory-authority-actions.tsx',import.meta.url),{
  modules:{react:{...React,useState(initial){const i=index++;if(!(i in states))states[i]=initial;return [states[i],v=>{states[i]=typeof v==='function'?v(states[i]):v;}];}}},
  globals:{fetch:async(url,options)=>{calls.push({url,options});return {ok:true,json:async()=>({})};}}
 });
 const props={taxpayerId:id,isLevel1:true,memories:[{id:mid,memoryIdentifier:'REAL01',status:1,revision:7,authorityEnvironment:null}],
  selectedMemory:null,testedMemoryId:'',onChanged:async()=>{refreshed++;},...overrides};
 return {render(){index=0;return component.default(props);},calls,refreshed:()=>refreshed};
}
function nodes(element,type){if(!element||typeof element!=='object')return [];return [element.type===type?element:null,...React.Children.toArray(element.props?.children).flatMap(child=>nodes(child,type))].filter(Boolean);}
test('classification is explicit, scoped, revision-bound, and refreshes server memory metadata',async()=>{
 const ui=harness();let tree=ui.render();assert.equal(ui.calls.length,0);
 assert.equal(nodes(tree,'button')[0].props.disabled,true);
 nodes(tree,'select')[0].props.onChange({target:{value:'2'}});tree=ui.render();
 await nodes(tree,'button')[0].props.onClick();
 assert.equal(ui.calls.length,1);
 assert.equal(ui.calls[0].url,`/api/taxpayers/${id}/memories/${mid}/classify-authority`);
 assert.deepEqual(JSON.parse(ui.calls[0].options.body),{authorityEnvironment:2,expectedRevision:7});
 assert.equal(ui.calls[0].options.credentials,'same-origin');assert.equal(ui.refreshed(),1);
});
test('activation needs a separate action after a successful read-only test for the same selected memory',async()=>{
 const selected={id:mid,memoryIdentifier:'REAL01',status:1,revision:8,authorityEnvironment:1};
 const pending=harness({memories:[],selectedMemory:selected});
 assert.equal(nodes(pending.render(),'button')[0].props.disabled,true);
 const ready=harness({memories:[],selectedMemory:selected,testedMemoryId:mid});const tree=ready.render();
 assert.equal(ready.calls.length,0);
 assert.equal(nodes(tree,'button')[0].props.disabled,false);
 await nodes(tree,'button')[0].props.onClick();
 assert.equal(ready.calls[0].url,`/api/taxpayers/${id}/memories/${mid}/verify-and-activate`);
 assert.deepEqual(JSON.parse(ready.calls[0].options.body),{expectedRevision:8});assert.equal(ready.refreshed(),1);
 const wrong=harness({memories:[],selectedMemory:selected,testedMemoryId:id});
 assert.equal(nodes(wrong.render(),'button')[0].props.disabled,true);
});
test('administrative memory mutations are absent for customer-only views',()=>{
 assert.equal(harness({isLevel1:false}).render(),null);
});
test('unclassified memory uses the selected Production tab for its one-time test and never unlocks activation',async()=>{
 const states=[];let index=0;const calls=[];const auth=[];
 const component=loadTsx(new URL('../app/taxpayers/signing-connection-test.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useState(initial){const i=index++;if(!(i in states))states[i]=initial;return [states[i],value=>{states[i]=typeof value==='function'?value(states[i]):value;}];}}},
  globals:{AbortSignal,fetch:async(url,options)=>{calls.push({url,options});return {ok:true,json:async()=>({status:'Authenticated',testedAtUtc:'2026-11-05T00:00:00Z',fiscalInformation:{nameTrade:'x',fiscalStatus:'ACTIVE',economicCode:'x',nationalId:'x'},oneTimeTest:true,testEnvironment:'Production'})};}}
 });
 const props={taxpayerId:id,memories:[{id:mid,memoryIdentifier:'OLD001'}],loadingMemories:false,hideMemorySelector:true,
  oneTimeEnvironment:'Production',onAuthenticated:value=>auth.push(value)};
 const render=()=>{index=0;return component.default(props);};
 let tree=render();assert.equal(nodes(tree,'select').length,0);
 assert.equal(nodes(tree,'button')[0].props.disabled,false);
 await nodes(tree,'button')[0].props.onClick();
 assert.deepEqual(JSON.parse(calls[0].options.body),{testEnvironment:'Production'});
 assert.equal(calls[0].options.headers['Content-Type'],'application/json');
 assert.deepEqual(auth,[false]);
});
test('a failed connection test offers to reopen the same memory private-key entry',async()=>{
 const states=[];let index=0;let reopened=0;
 const component=loadTsx(new URL('../app/taxpayers/signing-connection-test.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useState(initial){const i=index++;if(!(i in states))states[i]=initial;return [states[i],value=>{states[i]=typeof value==='function'?value(states[i]):value;}];}}},
  globals:{AbortSignal,fetch:async()=>({ok:false,status:503,json:async()=>({})})}
 });
 const props={taxpayerId:id,memories:[{id:mid,memoryIdentifier:'OLD001'}],loadingMemories:false,hideMemorySelector:true,
  onReenterPrivateKey:()=>{reopened++;}};
 const render=()=>{index=0;return component.default(props);};
 let tree=render();nodes(tree,'button')[0].props.onClick();await new Promise(resolve=>setImmediate(resolve));tree=render();
 const reentry=nodes(tree,'button').find(button=>String(button.props.children).includes('ورود دوباره'));
 assert.ok(reentry,nodes(tree,'button').map(button=>String(button.props.children)).join('|'));
 reentry.props.onClick();assert.equal(reopened,1);
});
test('taxpayer proxy allows the two bounded memory operations and preserves safe rejection details',async()=>{
 const calls=[];
 const route=loadTsx(new URL('../app/api/taxpayers/[...path]/route.ts',import.meta.url),{
  modules:{'../../../../lib/identity':{ACCESS_COOKIE:'access',identityApiOrigin:()=>'http://backend',identityUiEnabled:()=>true}},
  globals:{AbortSignal,fetch:async(url,options)=>{calls.push({url,options});return new Response(JSON.stringify({error:'authority_memory_not_active'}),{status:422});}}
 });
 for(const action of ['classify-authority','verify-and-activate']){
  const request=new NextRequest('http://localhost:3000/api/test',{method:'POST',headers:{host:'localhost:3000',origin:'http://localhost:3000',cookie:'access=token','content-type':'application/json'},body:JSON.stringify({expectedRevision:7,...(action==='classify-authority'?{authorityEnvironment:1}:{})})});
  const response=await route.POST(request,{params:Promise.resolve({path:[id,'memories',mid,action]})});
  assert.equal(response.status,422);assert.deepEqual(await response.json(),{error:'authority_memory_not_active'});
  assert.equal(calls.at(-1).url,`http://backend/api/dev/taxpayers/${id}/memories/${mid}/${action}`);
  assert.equal(calls.at(-1).options.headers.Authorization,'Bearer token');
 }
});
test('each environment presents only its persisted memories to import and read-only testing',()=>{
 for(const environment of ['production','sandbox']){
  let state=0;
  const component=loadTsx(new URL('../app/taxpayers/taxpayer-connection-panel.tsx',import.meta.url),{
   modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useState(initial){const i=state++;return [i===1?environment:i===2&&environment==='production'?'old':initial,()=>{}];}}}
  });
  const tree=component.default({taxpayerId:id,loadingMemories:false,memories:[
   {id:'prod',taxpayerId:id,memoryIdentifier:'REAL01',status:1,revision:1,authorityEnvironment:1},
   {id:'test',taxpayerId:id,memoryIdentifier:'TEST01',status:1,revision:1,authorityEnvironment:2},
   {id:'old',taxpayerId:id,memoryIdentifier:'OLD001',status:1,revision:1,authorityEnvironment:null}]});
  const walk=element=>!element||typeof element!=='object'?[]:[element,...React.Children.toArray(element.props?.children).flatMap(walk)];
  const elements=walk(tree);
  for(const name of ['ExistingKeyImport','SigningConnectionTest']){
   const child=elements.find(element=>element.type?.name===name);assert.ok(child,name);
   assert.deepEqual(Array.from(child.props.memories,memory=>memory.id),environment==='production'?['old']:['test']);
   if(environment==='production'&&name==='SigningConnectionTest')assert.equal(child.props.oneTimeEnvironment,'Production');
  }
  assert.equal(elements.filter(element=>element.type?.name==='SandboxFiscalMemoryPanel').length,environment==='sandbox'?1:0);
 }
});
