import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const service = readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/FiscalMemoryManagementService.cs",import.meta.url),"utf8");
const proxy = readFileSync(new URL("../app/api/taxpayers/[...path]/route.ts",import.meta.url),"utf8");

test("initial fiscal memory list and register use the same live taxpayer authorization",()=>{
  assert.match(service,/private async Task<FiscalMemoryOperationStatus\?> AuthenticateParticipantAsync/);
  assert.match(service,/taxpayerScope\.ResolveAsync\(bearer, ct\)/);
  assert.match(service,/taxpayerScope\.ScopedQuery\(principal\.ActorId, principal\.Level1\)/);
  const list = service.slice(service.indexOf("public async Task<(FiscalMemoryOperationStatus Status, FiscalMemoryView[]? Memories)> ListAsync"),service.indexOf("public async Task<FiscalMemoryOperationResult> TransitionAsync"));
  assert.match(list,/AuthenticateParticipantAsync\(bearer, taxpayerId, ct\)/);
  assert.doesNotMatch(list,/AuthenticateLevel1Async/);
});

test("initial registration remains one memory per taxpayer until approved exception lifecycle",()=>{
  assert.match(service,/AnyAsync\(x => x\.TaxpayerId == taxpayerId/);
  assert.match(service,/RequiresApprovedBindingReview/);
  assert.match(proxy,/parts\[1\] === "memories"/);
});
