# SEP SMS Top-Ups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let customers purchase SMS credit through SEP and let an authorized administrator initiate a top-up for a selected customer, granting credit only after verified payment.

**Architecture:** Reuse the SMS monetary ledger from `2026-11-02-sms-credit-accounting.md`. The supplied SEP OnlinePG Technical Guide v3.6 (1404-10) is the protocol source. Persist a payment attempt and terminal/admin assignment, then grant once after server-to-server SEP verification.

**Tech Stack:** .NET 9 minimal APIs, EF Core/Npgsql additive migrations, Next.js 15 server proxy and existing IRR SMS credit ledger.

**Spec:** `docs/superpowers/specs/2026-11-02-sms-wallet-and-sep-topups-design.md` — Stage 2.

## Global Constraints

- No route may issue SMS credit from browser return/query/callback data alone.
- SEP verification must match merchant terminal, amount, reference/attempt and successful provider result.
- The supplied OnlinePG flow requires the configured terminal number; this implementation does not persist or request a merchant secret. Never place tokens or credentials in logs or persistent browser state.
- Top-up attempts and verified grants are immutable/idempotent; unknown provider outcomes require reconciliation and cannot trigger duplicate grants.
- Terminal assignment does not change Level 1/Level 2 customer ownership or data scope.
- Level 1 can top up any customer within its root scope; Level 2 can top up only its currently assigned customers, after a terminal is assigned to that Level 2.
- Payments stay disabled unless `SmsSep:EnablePayments=true`; do not enable until terminal configuration, HTTPS callback/return URLs, registered merchant server IP and merchant sandbox tests are confirmed.
- Do not run tests/builds or apply database changes unless the owner requests verification or an approved test migration.

## Review Focus

- Browser return claims success but server verification is absent/mismatched → no SMS credit grant.
- SEP timeout after initiation → durable unknown state, reconciliation path, no blind retry/second attempt.
- Duplicate callback or repeated verification → one immutable grant only.
- Level 2 requests another partner's customer ID → authorization denial before payment attempt creation.
- Terminal assignment or merchant reconfiguration after purchase creation → attempt uses its immutable merchant/terminal snapshot.

---

## File map

- Inspect likely owner sources outside this repository under `Desktop/WORKS/Hamgamafzar`, `Desktop/WORKS/GreedyDev/X-ui/Royal`, `Desktop/WORKS/GreedyDev/X-ui/Royalsoftware`, and `Desktop/WORKS/GreedyDev/X-ui/hamgamafzar`; do not copy unrelated user data or secrets.
- Create the SEP adapter and contracts under `src/ArginTaxPayer.Infrastructure/Payments/` or existing Phase 12 payment namespace selected after source inspection.
- Create `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SmsTopUpService.cs` for configuration, authorization, attempt and verified grant lifecycle.
- Inspect `ISecretProvider`, `EnvironmentSecretProvider`, and existing encryption/key-protection interfaces before selecting merchant-secret custody; the current environment provider is read-only and is not an admin-editable secret vault.
- Add an additive migration under `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/` for encrypted terminal configuration references, admin assignment, immutable top-up attempts and replay-safe provider evidence.
- Add local-development-scoped endpoint(s) under `src/ArginTaxPayer.Api/Subscriptions/` and registrations in `Program.cs`.
- Modify `apps/web/app/api/phase12/[...parts]/route.ts` for allowlisted customer/admin top-up paths.
- Create `apps/web/app/subscriptions/sms-topup.tsx` and add it to the customer credit view and finance SMS workspace; create a Level 1 terminal assignment form in `apps/web/app/(console)/admin-settings/page.tsx`.
- Update `docs/phases/phase-12.md`, `docs/roadmap/18-phases.md`, and the Phase 1 SMS accounting plan evidence only after acceptance evidence exists.

## Interfaces

- `ISepSmsPaymentGateway.InitiateAsync(SepMerchantSnapshot merchant, SmsTopUpRequest request)` returns only a provider attempt/reference and redirect URI after input validation.
- `ISepSmsPaymentGateway.VerifyAsync(SepMerchantSnapshot merchant, string providerReference, string expectedAttemptReference)` returns structured verification evidence: terminal, amount, provider transaction reference, final state and verified timestamp.
- `SmsTopUpService.CreateCustomerAttemptAsync(bearer, amountIrr, operationKey)` uses the authenticated Customer's account and selects the enabled terminal assigned to that Customer's current responsible administrator (Level 2 partner when assigned, otherwise Level 1 root).
- `SmsTopUpService.CreateAdminAttemptAsync(bearer, customerUserId, amountIrr, operationKey)` enforces current Level 1 root or Level 2 partner scope and administrator terminal assignment.
- `SmsTopUpService.ConfirmAsync(bearer, attemptId)` invokes `ISepSmsPaymentGateway.VerifyAsync` itself and posts exactly one `SmsCreditEvent(kind=SepPaymentGrant, sourceAttemptId=attemptId)` only after checks pass. A browser return/callback can request status reconciliation but cannot submit trusted provider evidence.

## Tasks

### Task 1: Locate and assess existing SEP integration

**Files:**
- Inspect only: prior Hamgam connection and Royal bot projects listed in File map
- Record findings in: `docs/phases/phase-12.md` or a new short source-compatibility note

- [ ] Search source files while excluding bot user-data SQL, build output, generated files and secrets.
- [ ] Identify SEP vs similarly named providers, exact API version, credentials required, initiation/verification methods, callback behavior, idempotency/reference design and any sandbox configuration.
- [ ] Confirm whether the implementation can be safely adapted to server-side .NET 9 with encrypted credential custody and independent verification.
- [ ] If there is no reusable SEP implementation, use only the current merchant-specific official SEP contract to define the adapter; do not substitute Zarinpal or infer a SEP contract from it.

### Task 2: Add encrypted terminal configuration and administrator assignment

**Files:**
- Create: additive SEP merchant configuration/assignment migration
- Create: `SepMerchantConfigurationService.cs`
- Create: Level 1 settings endpoint and UI panel

- [ ] Let Level 1 define a terminal number and protected server-side merchant credential reference, choose enabled status, and assign it to a Level 1 or Level 2 administrator.
- [ ] Select or implement admin-editable merchant-secret custody backed by a server-held encryption key; the current environment secret provider is read-only, so do not persist plaintext credentials. Return masked safe metadata only.
- [ ] Write immutable configuration revisions so every purchase attempt snapshots its selected merchant/terminal/assigned administrator.
- [ ] Enforce Level 1+MFA authorization server-side; ensure terminal assignment never grants unrelated customer access.

### Task 3: Persist attempts and implement SEP server verification

**Files:**
- Create/modify: `ISepSmsPaymentGateway` and SEP adapter contracts
- Create: `SmsTopUpService.cs`
- Create: additive attempt/evidence migration
- Create: SEP initiation/confirmation endpoints

- [ ] Create an immutable attempt with customer, initiating actor, terminal snapshot, amount, currency, unique operation key and expiry before redirecting.
- [ ] Implement initiation from the inspected compatible code or the merchant-specific verified SEP contract; validate every outbound amount/reference and reject disabled/missing terminal configuration.
- [ ] On browser return or provider callback, load the stored attempt and query SEP server-to-server; verify exact amount, terminal, unique attempt reference and final success state before posting a ledger payment grant.
- [ ] Persist every verification evidence state and make duplicate/reordered callbacks idempotent; keep timeout/unknown outcomes pending for reconciliation.
- [ ] Enforce root/customer scope for Level 1 and partner/customer scope plus assigned-terminal rule for Level 2.

### Task 4: Add customer and administrator top-up UI

**Files:**
- Create: `apps/web/app/subscriptions/sms-topup.tsx`
- Modify: `subscription-status.tsx`, `sms-usage-workspace.tsx`, proxy route and admin settings page

- [ ] Let a Customer choose a positive IRR top-up amount and initiate SEP payment using same-origin authenticated routes.
- [ ] Add administrator action on a selected Customer's SMS row; Level 1 may select within root scope, Level 2 within its assigned scope and only with assigned terminal.
- [ ] Display pending/success/failure/unknown states from server-owned attempt status; refresh SMS balance only after a verified grant.
- [ ] Never report success from the browser redirect alone and never place secrets or sensitive amounts in an untrusted URL.

### Task 5: Document gated activation and operational evidence

**Files:**
- Modify: `docs/phases/phase-12.md`
- Modify: `docs/roadmap/18-phases.md`

- [ ] Record exact SEP source reused or the authoritative provider contract, credential storage and terminal assignment model.
- [ ] Keep initiation disabled until dedicated merchant test credentials verify success, amount/terminal mismatch, duplicate/reordered callbacks, cancellation, timeout/unknown state, reconciliation and one-time credit grant.
- [ ] Record verified test evidence, migration audit and operator activation procedure; do not claim live payments without actual merchant test evidence.

## Verification boundary

No automated tests or builds are included in this plan. The owner did not request test/verification execution; do not add or run tests unless the owner separately requests verification. No real payment is authorized by approval of this design or plan.


## Implementation status — 2026-11-02

Source files and migration `20261102040000_AddSepSmsTopups` are authored. The supplied guide specifies the token request, browser form POST, and server-side VerifyTransaction request; the sample archive was used only to corroborate this flow. No sample credentials, cookies, SQL snippets or TLS workarounds were copied. Level 1 terminal/admin assignment is audited; customer and scoped admin initiation, same-origin callback forwarding, provider verification, unknown state and recheck, reverse request on amount mismatch, and one-time ledger credit are implemented in source. The admin finance table action and customer SMS-credit page are wired.

Still open: configure valid merchant terminal and absolute HTTPS callback/return URLs; have SEP register the callback server IP; perform dedicated merchant sandbox cases (success, cancel, duplicate callback, wrong amount/terminal, timeout/unknown, recheck and reverse); review resulting ledger evidence. `SmsSep:EnablePayments` defaults false. API build succeeded; the two SMS migrations were applied to `argintaxpayer_test` after owner approval. No automated tests, typecheck or merchant payment was run.
