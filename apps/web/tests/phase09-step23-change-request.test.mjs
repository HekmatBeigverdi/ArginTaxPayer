import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {createRequire} from "node:module";
import {loadTsx} from "./helpers/load-tsx.mjs";
const React=createRequire(import.meta.url)("react");

const ui = readFileSync(new URL("../app/taxpayers/signing-change-request-panel.tsx", import.meta.url), "utf8");
const bff = readFileSync(new URL("../app/api/signing/[taxpayerId]/[memoryId]/change-requests/route.ts", import.meta.url), "utf8");
const api = readFileSync(new URL("../../../src/ArginTaxPayer.Api/Taxpayers/SigningChangeRequestEndpoints.cs", import.meta.url), "utf8");
const service = readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningChangeRequestSubmission.cs", import.meta.url), "utf8");
const program = readFileSync(new URL("../../../src/ArginTaxPayer.Api/Program.cs", import.meta.url), "utf8");

test("same selected fiscal memory drives change requests", () => {
  for (const environment of ["production", "sandbox"]) {
    let index=0;
    const panel=loadTsx(new URL("../app/taxpayers/taxpayer-connection-panel.tsx",import.meta.url),{
      modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useRef:()=>({current:null}),
        useState:initial=>{const i=index++;return [i===1?environment:i===4?true:i===7?{hasKey:true,connectionHistory:{available:true,lastAttempt:null,lastSuccess:null}}:i===12?false:initial,()=>{}];}}}
    });
    const tree=panel.default({taxpayerId:"taxpayer",loadingMemories:false,memories:[
      {id:"real-memory",authorityEnvironment:1},{id:"test-memory",authorityEnvironment:2},
      {id:"unclassified",authorityEnvironment:null}]});
    const walk=node=>!node||typeof node!=="object"?[]:[node,...React.Children.toArray(node.props?.children).flatMap(walk)];
    const child=walk(tree).find(node=>node.type?.name==="SigningChangeRequestPanel");
    assert.ok(child);
    assert.equal(child.props.taxpayerId,"taxpayer");
    assert.equal(child.props.memoryId,environment==="production"?"real-memory":"test-memory");
  }
  assert.match(ui, /expectedKeyRevision: revision/);
  assert.match(ui, /crypto\.randomUUID\(\)/);
  assert.match(ui, /state === 1 \|\| x\.state === 2/);
});
test("change requests do not approve, activate a second memory or alter keys", () => {
  assert.match(service, /AuthorizeOwnerAsync/);
  assert.doesNotMatch(service, /ReadImportOnboardingAsync/);
  assert.match(service, /BeginTransactionAsync\(IsolationLevel\.Serializable/);
  assert.match(service, /memories\.Count != 1/);
  assert.match(service, /key\.Revision != input\.ExpectedKeyRevision/);
  assert.match(service, /SubjectKind = 4/);
  assert.match(service, /change_request_submitted/);
  assert.match(service, /State = 1/);
  assert.doesNotMatch(service, /SigningKeyVersions\.Add|\.LifecycleState\s*=(?!=)/);
  assert.doesNotMatch(ui, /privateKey|privatePem|tokenValue/i);
});
test("BFF validates origin, session, body and allowlists response", () => {
  assert.match(bff, /isSameLocalOrigin/);
  assert.match(bff, /ACCESS_COOKIE/);
  assert.match(bff, /request_too_large/);
  assert.match(bff, /currentKeyRevision/);
  assert.match(api, /IsLoopbackAddress/);
  assert.match(api, /RequireRateLimiting/);
});
test("signing rate limits are isolated per operation path and expose throttling", () => {
  assert.match(program, /http\.Request\.Path/);
  assert.match(bff, /upstream\.status === 429/);
  assert.match(bff, /rate_limited/);
  assert.match(ui, /rate_limited/);
});
