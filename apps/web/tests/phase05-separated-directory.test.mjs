import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("registered identity accounts never become buyer rows without a stored Party", () => {
  const page = read("app/parties/buyer-taxpayer-directory.tsx");
  assert.match(page, /const buyerRows: Row\[\] = parties\.map/);
  assert.doesNotMatch(page, /type: "buyer", kind: null, name: account/);
  assert.doesNotMatch(page, /id: buyer\.customerUserId, buyerId: buyer\.customerUserId,\s*type: "buyer"/);
  assert.match(page, /const taxpayerRows: Row\[\] = taxpayers\.map/);
  assert.match(page, /type: "taxpayer"/);
});

test("buyer and seller registrations do not create software login accounts", () => {
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(modal, /\/api\/party-workspace\/customers\//);
  assert.match(modal, /\/api\/taxpayers\/new/);
  assert.doesNotMatch(modal, /\/api\/identity\/register|AdminCreateCustomer/);
});

test("separate persistence models exist for identity buyer and fiscal seller", () => {
  const buyer = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs");
  const fiscalSeller = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerProfile.cs");
  const link = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/CustomerPartyLink.cs");
  assert.match(buyer, /class Party/);
  assert.match(fiscalSeller, /class TaxpayerProfile/);
  assert.match(link, /CustomerUserId/);
  assert.match(link, /PartyId/);
});
