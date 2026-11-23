import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
import { canRegisterParticipant } from "../app/parties/participant-access.mjs";
import { loadTsx } from "./helpers/load-tsx.mjs";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("customer buyer registration waits for the profile and uses the authenticated owner's ID", async () => {
  const actorUserId = "12345678-1234-1234-1234-123456789abc";
  const modal = () => null;
  let resolveProfile;
  const profile = new Promise(resolve => { resolveProfile = resolve; });
  const state = [];
  let index = 0;
  const Directory = loadTsx(new URL("../app/parties/buyer-taxpayer-directory.tsx", import.meta.url), {
    globals: {
      fetch: async url => {
        assert.equal(url, "/api/software-profile/me");
        return profile;
      },
    },
    modules: {
      "./participant-registration-modal": { default: modal, __esModule: true },
      "./fiscal-record-modal": { default: () => null, __esModule: true },
      react: {
        useState(initial) {
          const slot = index++;
          return [state[slot] ?? initial, value => { state[slot] = value; }];
        },
        useRef: value => ({ current: value }),
        useEffect: () => {},
        useCallback: callback => callback,
      },
    },
  }).default;
  function find(node, predicate) {
    if (!node || typeof node !== "object") return undefined;
    if (predicate(node)) return node;
    for (const child of Array.isArray(node) ? node : [node.props?.children]) {
      const found = find(child, predicate);
      if (found) return found;
    }
  }
  const props = { admin: false, level1: false, actorUserId };
  function render() {
    index = 0;
    return Directory(props);
  }
  const buyerButton = node => node.type === "button" && node.props.children === "+ افزودن خریدار";
  const button = find(render(), buyerButton);
  assert.ok(button);
  button.props.onClick();
  assert.equal(find(render(), node => node.type === modal), undefined);
  assert.equal(find(render(), buyerButton).props.disabled, true);

  resolveProfile(Response.json({ firstName: "Test", lastName: "Customer", complete: true }));
  await new Promise(setImmediate);
  const registration = find(render(), node => node.type === modal);
  assert.ok(registration);
  assert.equal(registration.props.buyers.length, 1);
  assert.equal(registration.props.buyers[0].customerUserId, actorUserId);
  assert.equal(canRegisterParticipant(registration.props.buyers[0]), true);
  assert.equal(registration.props.initialCustomerId, actorUserId);
});

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
  assert.match(modal, /buyers\.filter\(b=>canRegisterParticipant\(b\)\)/);
  assert.match(modal, /label:b\.companyName\|\|b\.personName/);
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
