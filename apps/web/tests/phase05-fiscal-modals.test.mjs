import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
const read = file => readFileSync(new URL("../" + file, import.meta.url), "utf8");

test("buyer button opens fiscal person modal, never software account creation", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  assert.match(directory, /startRegistration\("buyer"\)/);
  assert.match(directory, /startRegistration\("seller"\)/);
  assert.match(directory, /ParticipantRegistrationModal/);
  assert.doesNotMatch(directory, /<AdminCreateCustomer/);
  assert.match(directory, /onCompleted=\{reload\}/);
});

test("buyer and seller share staged entry with strict roles and correct path mapping", () => {
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(modal, /\/api\/party-workspace\/customers\//);
  assert.match(modal, /\/api\/taxpayers\/new/);
  assert.match(modal, /<InvoiceParticipantForm/);
  assert.match(modal, /<TaxpayerWorkspace/);
  assert.match(modal, /role="dialog"/);
  assert.match(modal, /aria-modal="true"/);
  assert.match(modal, /level1/);
  assert.match(modal, /onCompleted\(\)/);
});
