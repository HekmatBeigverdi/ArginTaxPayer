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
  assert.match(modal, /rootLevel1UserId === actorUserId/);
  assert.match(modal, /partnerLevel2UserId === actorUserId/);
});

test("inaccessible customers are disabled and cannot submit even with modified client state", () => {
  const modal = read("app/parties/participant-registration-modal.tsx");
  assert.match(modal, /disabled=\{!permitted\}/);
  assert.match(modal, /!authorizedCustomer/);
  assert.match(modal, /مالکیت تجاری/);
  assert.match(modal, /disabled=\{busy \|\| !authorizedCustomer/);
});

test("backend participant authorization uses scope service rather than global role alone", () => {
  const natural = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/NaturalPersonService.cs");
  const legal = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/LegalEntityService.cs");
  const scope = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/ParticipantScopeService.cs");
  for (const code of [natural, legal]) assert.match(code, /scope.CanManageAsync/);
  assert.match(scope, /o.RootLevel1UserId == actor/);
  assert.match(scope, /o.PartnerLevel2UserId == actor/);
  assert.match(scope, /u.Id == customer/);
  assert.match(scope, /m.EnabledAtUtc != null/);
});
