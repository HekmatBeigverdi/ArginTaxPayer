import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
const read = p => readFileSync(new URL("../" + p, import.meta.url), "utf8");
test("live MFA and scoped commercial owner control all fiscal reads", () => {
 const scope = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/ParticipantScopeService.cs");
 const fiscal = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/FiscalParticipantDirectoryService.cs");
 assert.match(scope, /IdentityMfaStates/);
 assert.match(scope, /RootLevel1UserId == actor/);
 assert.match(scope, /PartnerLevel2UserId == actor/);
 assert.match(fiscal, /allowedCustomers.Contains/);
});
test("natural and legal party reads use centralized scoped authorization", () => {
 for (const p of ["NaturalPersonService.cs", "LegalEntityService.cs"]) {
 const s = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/" + p);
 assert.match(s, /scope.CanManageAsync\(actor, customer, ct\)/);
 }
 const directory = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyDirectoryService.cs");
 assert.match(directory, /participantScope.CanManageAsync\(actor.Value, customerId, ct\)/);
});
test("taxpayer reads, changes, archives and scoped commands retain current root and partner ownership", () => {
 const s = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerScopeService.cs");
 assert.match(s, /ScopedProfiles\(Guid actorId\)/);
 assert.match(s, /o.RootLevel1UserId == actorId/);
 assert.match(s, /o.PartnerLevel2UserId == actorId/);
 assert.match(s, /ScopedQuery\(Guid actorId, bool level1\) =>[\s\S]*ScopedProfiles\(actorId\)/);
 assert.match(s, /ScopedCommandQuery\(Guid actorId, bool level1\) =>[\s\S]*ScopedProfiles\(actorId\)/);
 assert.match(s, /ArchiveAsync[\s\S]*IsolationLevel.Serializable/);
 assert.match(s, /ArchiveAsync[\s\S]*ScopedCommandQuery\(actorId.Value, false\)/);
});
test("direct root taxpayer lookup is scoped rather than role-wide", () => {
 const s = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/CustomerCommercialOwnershipService.cs");
 assert.match(s, /root && o.RootLevel1UserId == actor.Value/);
 assert.match(s, /partner && o.PartnerLevel2UserId == actor.Value/);
 assert.match(s, /TransferAsync[\s\S]*IsolationLevel.Serializable/);
});
