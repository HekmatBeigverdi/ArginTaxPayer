import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("actor identity reaches buyer registration modal from authenticated server page", () => {
  const page = read("app/(console)/parties/page.tsx");
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(page, /actorUserId=\{actor\.userId\}/);
  assert.match(directory, /actorUserId=\{actorUserId\}/);
  assert.match(modal, /buyer\.isActive && buyer\.profileComplete && \(level1 \|\|/);
  assert.match(modal, /partnerLevel2UserId === actorUserId/);
});

test("inaccessible customers are disabled and cannot submit even with modified client state", () => {
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(modal, /disabled=\{!permitted\}/);
  assert.match(modal, /!authorizedCustomer/);
  assert.match(modal, /مالکیت تجاری/);
  assert.match(modal, /disabled=\{busy \|\| !authorizedCustomer/);
});

test("Level1 registration keeps completed profiles visible without root ownership filters", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(directory, /x\.isActive && x\.profileComplete && \(level1 \|\|/);
  assert.match(modal, /buyer\.isActive && buyer\.profileComplete && \(level1 \|\|/);
  assert.match(modal, /const hasPermittedCustomer = buyers\.some\(isPermitted\)/);
});

test("registration modal opens immediately and waits inside while the customer list loads", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(directory, /setRegisterMode\(mode\);[\s\S]*setRegistrationLoading\(true\);/);
  assert.match(directory, /registrationLoading={registrationLoading}/);
  assert.match(modal, /registrationLoading \? <div className="console-empty" role="status"/);
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
