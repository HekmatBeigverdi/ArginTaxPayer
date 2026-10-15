import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("buyer editor changes type, legal identifier and name atomically", () => {
  const ui = read("app/parties/fiscal-record-modal.tsx");
  assert.match(ui, /kind: buyerKind/);
  assert.match(ui, /\/parties\/".*\/identity/);
  assert.match(ui, /expectedRevision: person\.revision/);
  assert.match(ui, /نوع شخصیت خریدار/);
});

test("backend captures previous buyer version before editing across all update paths", () => {
  const service = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyIdentityEditService.cs");
  const natural = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/NaturalPersonService.cs");
  const legal = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/LegalEntityService.cs");
  assert.match(service, /db\.PartyIdentityHistories\.Add\(CapturePrevious/);
  assert.match(service, /party\.Kind = newKind/);
  assert.match(service, /IsolationLevel\.Serializable/);
  assert.match(service, /Revision != input\.ExpectedRevision/);
  for (const legacy of [natural, legal])
    assert.match(legacy, /PartyIdentityEditService\.CapturePrevious/);
});

test("archive remains separate from identity conversion and invoice snapshots reserved for future", () => {
  const ui = read("app/parties/fiscal-record-modal.tsx");
  const migration = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261015170000_AddPartyIdentityHistory.cs");
  assert.match(ui, /mode === "archive" \? buyerPath/);
  assert.match(migration, /party_identity_history/);
  assert.match(migration, /ReferentialAction\.Restrict/);
});
