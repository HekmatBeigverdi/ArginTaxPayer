import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const cs = p => readFileSync(new URL("../../../"+p,import.meta.url),"utf8");
const ts = p => readFileSync(new URL("../"+p,import.meta.url),"utf8");

test("key claim admits replacement only with an unconsumed 24h approval tied to prior revision",()=>{
  const journal=cs("src/ArginTaxPayer.Infrastructure/Persistence/Signing/EfSigningGenerationJournal.cs");
  assert.match(journal,/\(r\.Kind == 2 \|\| r\.Kind == 3\) && r\.State == 2 && r\.ExpiresAtUtc > now/);
  assert.match(journal,/r\.CurrentKeyVersionId == prior\.Id/);
  assert.match(journal,/r\.ExpectedKeyRevision == prior\.Revision/);
  assert.match(journal,/r\.ConsumedAtUtc == null/);
  assert.match(journal,/VersionNumber = \(prior\?\.VersionNumber \?\? 0\) \+ 1/);
});

test("key finalization consumes approval and selects new key in the same journal transaction",()=>{
  const journal=cs("src/ArginTaxPayer.Infrastructure/Persistence/Signing/EfSigningGenerationJournal.cs");
  assert.match(journal,/approval\.State = 4/);
  assert.match(journal,/approval\.ConsumedByOperationRecordId = op\.Id/);
  assert.match(journal,/previous\.LifecycleState = 3/);
  assert.match(journal,/selection\.KeyVersionId = key\.Id/);
  assert.match(journal,/db\.SigningKeySelections\.Add/);
  assert.match(journal,/SubjectKind = 3/);
  assert.match(journal,/SubjectId = id\.FiscalMemoryId/);
  assert.match(journal,/change_approval_consumed/);
  assert.doesNotMatch(journal,/DbSigningKeyEnvelopes\.Remove/);
});

test("existing and generated key entry both check active approval and never bypass fresh login",()=>{
  const existing=ts("app/taxpayers/existing-key-import.tsx");
  const generation=ts("app/taxpayers/signing-key-generation.tsx");
  assert.match(existing,/state\.replacementApproved/);
  assert.match(generation,/state\.hasKey && !state\.replacementApproved/);
  assert.match(generation,/state\.freshLogin/);
  assert.match(existing,/state\.freshLogin/);
});
