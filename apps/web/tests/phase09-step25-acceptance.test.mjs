import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const src = p => readFileSync(new URL("../../" + p, import.meta.url), "utf8");

test("Existing and generated keys use the same custody and real test route, not a mock token", () => {
  const panel = src("apps/web/app/taxpayers/taxpayer-connection-panel.tsx");
  const connection = src("src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyTaxAuthorityConnection.cs");
  assert.match(panel, /ExistingKeyImport/);
  assert.match(panel, /SigningKeyGeneration/);
  assert.match(panel, /SigningConnectionTest/);
  assert.match(connection, /GET_TOKEN/);
  assert.match(connection, /GET_FISCAL_INFORMATION/);
  assert.match(connection, /ReadImportOnboardingAsync/);
  assert.doesNotMatch(connection, /FakeAccessToken|test-only-token|dummy-token/i);
});

test("No admin decision endpoint accepts private key or tokens", () => {
  const review = src("src/ArginTaxPayer.Api/Taxpayers/SigningAdminReviewEndpoints.cs");
  const bff = src("apps/web/app/api/admin/signing-change-requests/[requestId]/decision/route.ts");
  assert.match(review, /IsLoopbackAddress/);
  assert.match(review, /SigningAdminReviewDecision|DecisionPayload/);
  assert.match(bff, /isSameLocalOrigin/);
  assert.doesNotMatch(review + bff, /PrivateKeyPem|privateMaterial|TokenValue|plaintextPkcs8/);
});

test("One-time approval binds selected version and an expiring review", () => {
  const journal = src("src/ArginTaxPayer.Infrastructure/Persistence/Signing/EfSigningGenerationJournal.cs");
  const review = src("src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningAdminReviewWorkflow.cs");
  assert.match(journal, /r\.ExpectedKeyRevision == prior\.Revision/);
  assert.match(journal, /r\.ExpiresAtUtc > now/);
  assert.match(journal, /r\.ConsumedByOperationRecordId == null/);
  assert.match(journal, /approval\.ConsumedByOperationRecordId = op\.Id/);
  assert.match(journal, /previous\.LifecycleState = 3/);
  assert.match(review, /IdentityRoleNames\.Level1/);
  assert.match(review, /EnabledAtUtc != null/);
});

test("Second memory entitlement is denied absent authoritative plan capacity", () => {
  const submission = src("src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningChangeRequestSubmission.cs");
  assert.match(submission, /memories\.Count != 1/);
  assert.match(submission, /SecondMemoryDenied/);
});

test("Database private key storage is wrapped, integrity-bound and no plaintext is inserted", () => {
  const provider = src("src/ArginTaxPayer.Infrastructure/Signing/Protection/DatabaseKeyProtectionProvider.cs");
  const store = src("src/ArginTaxPayer.Infrastructure/Signing/Protection/DatabaseProtectedKeyEnvelopeStore.cs");
  assert.match(provider, /AesGcm/);
  assert.match(provider, /id\.TransitBinding\(\)/);
  assert.match(store, /EnvelopeJson = JsonSerializer\.Serialize\(envelope\)/);
  assert.doesNotMatch(store, /PrivateKeyPem|RawPrivateKey|PlaintextKey/);
});
