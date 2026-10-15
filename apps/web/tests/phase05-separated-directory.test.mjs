import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("fiscal directory is independent of software-user account pagination", () => {
  const page = read("app/parties/buyer-taxpayer-directory.tsx");
  const backend = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/FiscalParticipantDirectoryService.cs");
  assert.match(page, /\/api\/fiscal-participants/);
  assert.doesNotMatch(page, /directory\/page/);
  assert.match(backend, /CustomerPartyLinks/);
  assert.match(backend, /TaxpayerProfiles/);
  assert.match(backend, /\.Skip\(\(page - 1\) \* pageSize\)\.Take\(pageSize\)/);
  assert.doesNotMatch(backend, /CustomerAccountSummary/);
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
