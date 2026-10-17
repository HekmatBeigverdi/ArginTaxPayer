import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
import { canRegisterParticipant } from "../app/parties/participant-access.mjs";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("actor identity reaches buyer registration modal from authenticated server page", () => {
  const page = read("app/(console)/parties/page.tsx");
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(page, /actorUserId=\{actor\.userId\}/);
  assert.match(directory, /actorUserId=\{actorUserId\}/);
  assert.match(modal, /canRegisterParticipant\(chosenCustomer\)/);
  assert.match(directory, /canRegisterParticipant\(x\)/);
});

test("only completed profiles appear, regardless of assignment or active status", () => {
  for (const isActive of [true, false]) {
    for (const partnerLevel2UserId of [null, "partner", "other-partner"]) {
      const customer = { customerUserId: "customer", isActive, partnerLevel2UserId };
      assert.equal(canRegisterParticipant({ ...customer, profileComplete: true }), true);
      assert.equal(canRegisterParticipant({ ...customer, profileComplete: false }), false);
      assert.equal(canRegisterParticipant(customer), false);
    }
  }
  assert.equal(canRegisterParticipant(undefined), false);
});

test("modal hides incomplete profiles and uses names without email or mobile fallbacks", () => {
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(modal, /buyers\.filter\(b => canRegisterParticipant\(b\)\)/);
  assert.match(modal, /\{b\.companyName \|\| b\.personName\}/);
  assert.doesNotMatch(modal, /\|\| b\.email|\|\| b\.mobile/);
  assert.match(modal, /disabled=\{busy \|\| !authorizedCustomer/);
});

test("backend participant authorization uses scope service rather than global role alone", () => {
  const natural = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/NaturalPersonService.cs");
  const legal = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/LegalEntityService.cs");
  const scope = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/ParticipantScopeService.cs");
  for (const code of [natural, legal]) assert.match(code, /scope.CanManageAsync/);
  assert.match(scope, /u.Roles.Any\(r => r.Role == IdentityRoleNames.Level1\)/);
  assert.doesNotMatch(scope, /o.RootLevel1UserId == actor/);
  assert.match(scope, /o.PartnerLevel2UserId == actor/);
  assert.match(scope, /u.Id == customer/);
  assert.match(scope, /m.EnabledAtUtc != null/);
});
