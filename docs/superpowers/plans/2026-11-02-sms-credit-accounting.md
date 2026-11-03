# SMS Credit and Usage Accounting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each ordinary Customer a one-time 2,000,000 IRR SMS grant, meter Ghasedak sends as actual provider cost plus the configured service percentage, and expose safe balance and usage reporting without disrupting non-SMS software access.

**Architecture:** Add a money-denominated append-only ledger and immutable setting snapshots while retaining legacy unit records unchanged. A send coordinator reserves a Level 1-configured ceiling before calling Ghasedak, settles each provider result exactly once, and returns an SMS-specific insufficient-credit error. Existing signup, phone OTP, console top bar, finance workspace and Level 1 settings surfaces consume the new server-owned contracts.

**Tech Stack:** .NET 9 minimal APIs, EF Core/Npgsql additive migration and SQL ledger, Next.js 15/React 19, existing authenticated same-origin Phase 12 proxy.

**Spec:** `docs/superpowers/specs/2026-11-02-sms-wallet-and-sep-topups-design.md` — Stage 1.

## Global Constraints

- Initial customer credit is exactly 2,000,000 IRR and can be granted only once, atomically with Customer account creation.
- Level 1/2 accounts have no SMS wallet charge; provider expense is still recorded.
- Customer charge is `providerCostIrr + ceil(providerCostIrr * percentage / 100)` using the percentage snapshot for that dispatch.
- Verify Ghasedak `Cost` currency/unit from merchant billing evidence before enabling charged SMS; preserve raw value and unit, normalize to IRR only on verified contract.
- Send one recipient per dispatch; fanout notifications use one independently reserved provider request per customer.
- Keep SMS message text, OTP/password reset values, provider secret and full recipient number out of financial records and logs.
- If SMS credit is insufficient, fail only the SMS action. Keep login by password and all other application operations available.
- Use an additive migration. Preserve and do not reinterpret existing unit-wallet data.
- Keep any live Ghasedak call disabled in development unless separately authorized; SEP is outside this plan.
- Do not modify unrelated in-progress finance behavior or apply migrations to any database without the established explicit test-database procedure.

## Review Focus

- Ambiguous Ghasedak submission or missing/invalid/unverified cost → hold reservation for reconciliation, never resend automatically or silently release funds.
- A notification sent to N customers → create N single-recipient dispatches, each with its own reservation and ledger line; do not make one bundled Ghasedak request.
- Customer registration OTP arrives before account creation → correlate by challenge; after successful registration atomically post initial grant and applicable send charge; expired/uncompleted registration remains unattributed acquisition expense.
- Level 1/2 send → record provider cost with zero customer charge and no wallet row.
- Partner filter and page totals → enforce current server-side partner scope before count, aggregation and paging.

---

## File map

- Create `src/ArginTaxPayer.Domain/Subscriptions/SmsCreditPolicy.cs` for IRR charge, percentage validation, reservation, and settlement arithmetic.
- Create an additive `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/<timestamp>_AddPhase12SmsMoneyLedger.cs` for account, event, delivery/reconciliation and setting audit tables, indexes, checks and append-only guards.
- Create focused subscription services under `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/` for SMS settings, credit, send reservation/settlement and paged reporting.
- Modify `src/ArginTaxPayer.Infrastructure/Persistence/Identity/GhasedakOtpSender.cs` to return structured provider data and keep its current credential/logging protections.
- Modify `src/ArginTaxPayer.Infrastructure/Persistence/Identity/LocalFileOtpSender.cs` to satisfy the structured transport contract and mark that no provider cost was incurred.
- Modify `src/ArginTaxPayer.Infrastructure/Persistence/Identity/IdentityPhoneService.cs`, `IdentityCredentialService.cs`, and `IdentityAdministratorUserProvisioning.cs` for the dispatch/account lifecycle and atomic Customer grant.
- Modify `src/ArginTaxPayer.Api/Identity/IdentityDevelopmentEndpoints.cs`, create SMS finance API endpoint(s) under `src/ArginTaxPayer.Api/Subscriptions/`, and register services in `src/ArginTaxPayer.Api/Program.cs`.
- Modify `apps/web/app/api/phase12/[...parts]/route.ts` for allowlisted authenticated SMS credit/settings/report routes.
- Modify `apps/web/app/components/console-shell.tsx` and `apps/web/app/components/subscription-status.tsx` to show the Customer's live balance beside plan status and administrator unlimited use.
- Create a focused SMS usage workspace under `apps/web/app/subscriptions/`; integrate it into `subscription-workspace.tsx` and `subscription-workspace.css`.
- Create a Level 1 SMS settings panel under `apps/web/app/subscriptions/` and mount it in `apps/web/app/(console)/admin-settings/page.tsx`.
- Update `docs/phases/phase-12.md` and the SMS section of `docs/roadmap/18-phases.md` after implementation evidence exists.

## Interfaces

- `IIdentityOtpSender.SendAsync(normalizedMobile, code, purpose, deliveryId, smsOwnerUserId?, cancellationToken)` returns a structured single-recipient outcome with provider acceptance, provider message ID, raw cost/unit, normalized IRR cost, and send time; no boolean-only cost-dropping path remains for charged sends. `LocalFileOtpSender` marks `NoProviderCost` and must never create a customer charge. Notification fanout uses one call/dispatch per recipient.
- `SmsCreditPolicy.CalculateCharge(long providerCostIrr, decimal percent)` returns the service percentage amount and final customer charge using ceiling-to-rial rounding; invalid amounts/rates fail closed.
- `SmsCreditService.ReadMineAsync(bearer)` returns `{isUnlimited, availableIrr}`; ordinary customers receive their balance while Level 1/2 receive `{isUnlimited:true, availableIrr:null}`.
- `SmsUsagePageService.GetAsync(bearer, page, level2PartnerUserId?, fromUtc?, toUtc?)` returns a 10-row server page, full-scope count, provider-cost/customer-charge/admin-cost totals and filtered rows.
- `SmsServiceSettingsService.GetAsync/SaveAsync(bearer, percent, reservationCeilingIrr)` is Level 1+MFA only and writes an audited immutable version.
- The send coordinator exposes `ReserveAsync(dispatchId, userId?, category)` for exactly one recipient and `SettleAsync(dispatchId, structuredProviderResult)`; a retry with the same dispatch ID is idempotent and conflicting replay fails.

## Tasks

### Task 1: Define money accounting and database structure

**Files:**
- Create: `src/ArginTaxPayer.Domain/Subscriptions/SmsCreditPolicy.cs`
- Create: additive Phase 12 SMS money migration in `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/`
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SmsServiceSettingsService.cs`

- [x] Define exact charge arithmetic, validation boundaries, IRR rounding, and reservation state transitions in the domain policy.
- [x] Add tables for per-user credit accounts, immutable credit events, provider dispatch/reconciliation snapshots, and versioned settings. Use unique idempotency constraints, money bounds, per-recipient IDs, normalized and raw provider cost/unit, percentage snapshots, and indexes for user/date/partner report access.
- [x] Ensure customer charge can be represented separately from provider expense; Level 1/2 sends have zero customer charge.
- [x] Preserve `sms_wallet_accounts` and `sms_wallet_events` unchanged; do not migrate unit values into IRR.
- [x] Document the exact migration application/audit command without applying the migration.

### Task 2: Add atomic Customer signup grant and current balance contract

**Files:**
- Modify: `IdentityCredentialService.cs`
- Modify: `IdentityPhoneService.cs`
- Modify: `IdentityAdministratorUserProvisioning.cs`
- Modify: `LocalFileOtpSender.cs` for structured-result compatibility only; local-file delivery has no provider expense or wallet charge.
- Create/modify: `SmsCreditService.cs`

- [x] Add the one-time 2,000,000 IRR promotional grant in the same transaction as each new Customer in all three creation paths; administrators never receive a wallet.
- [ ] Bind pre-registration OTP dispatch to the new Customer only after successful registration; post initial grant and that registration dispatch's charge atomically. Keep incomplete/expired registration expense unattributed.
- [x] Add a current-user balance read that determines role from live authorization and never trusts a client-supplied user ID.
- [ ] Leave legacy unit wallet rows readable only through the explicitly retained compatibility path; do not silently treat those units as money.

### Task 3: Return Ghasedak costs and make sends reservation-safe

**Files:**
- Modify: `GhasedakOtpSender.cs`
- Modify: `IdentityPhoneService.cs`
- Create: `SmsDispatchCoordinator.cs` and any focused result contracts under `Persistence/Subscriptions/`
- Modify: `IdentityDevelopmentEndpoints.cs`

- [ ] Change the adapter contract to capture per-recipient `MessageId`, `Cost`, `SendDate`, and stable client reference while continuing to redact keys, OTP values, message bodies and phone numbers.
- [ ] Confirm the merchant/provider cost unit and normalize to IRR only after confirmation; preserve raw amount/unit alongside normalized value.
- [ ] Before each one-recipient external send, read and lock the customer account, capture service percentage and reservation ceiling, and reserve atomically. Reject with typed insufficient-credit outcome and do not call Ghasedak if the account cannot cover it.
- [ ] On provider success, settle actual customer charge per recipient and release unused reservation. On ambiguous outcome, invalid cost or ceiling overrun, hold reconciliation state and block subsequent customer sends; do not issue an automatic retry.
- [ ] For administrator-originated SMS, bypass reservation/customer charge while persisting provider expense and actor identity.
- [ ] Map insufficient credit to a stable SMS-specific HTTP error for existing account actions; preserve generic anti-enumeration behavior for anonymous registration.

### Task 4: Add secure settings and scoped usage report API

**Files:**
- Create: settings/report API endpoint under `src/ArginTaxPayer.Api/Subscriptions/`
- Create: `SmsUsagePageService.cs`
- Modify: `Program.cs`

- [ ] Implement Level 1+MFA-only percentage and reservation-ceiling reads/writes with append-only setting versions and current-settings snapshots.
- [ ] Implement 10-row server paging and date filtering; calculate full filtered provider cost, customer charge and admin operating cost before paging.
- [ ] Apply Level 1 root and Level 2 partner scope in SQL before aggregation/count/page, including historical ownership snapshots only where permitted by existing commercial ownership policy.
- [ ] Return safe identities, provider references, cost, surcharge rate, charged amount and reconciliation status; omit message body and full destination phone.
- [ ] Keep endpoints authenticated, no-store, local-development-scoped where existing Phase 12 API requires it, and free of merchant/API secrets.

### Task 5: Update top bar, finance SMS page, settings UI and proxy

**Files:**
- Modify: `console-shell.tsx`, `subscription-status.tsx`
- Create: `apps/web/app/subscriptions/sms-usage-workspace.tsx`
- Modify: `subscription-workspace.tsx`, `subscription-workspace.css`
- Create: `apps/web/app/subscriptions/sms-service-settings.tsx`
- Modify: `(console)/admin-settings/page.tsx`
- Modify: `apps/web/app/api/phase12/[...parts]/route.ts`

- [ ] Display Customer SMS balance beside plan status with correct loading/error states; show Level 1/2 as unlimited without fetching a wallet.
- [ ] Replace the current unit-based manual controls with usage table, aggregate cards, date filters, Level 2 filter, 10-row server pagination, and localized insufficient-credit error presentation.
- [ ] Add Level 1 settings for service percentage and reservation ceiling; do not expose settings mutation to Level 2.
- [ ] Whitelist route/method/query contracts in the BFF; enforce roles and current customer/partner scopes in the backend as well.
- [ ] Keep unrelated finance tables and existing plan/settlement behaviors intact.

### Task 6: Update operational documentation

**Files:**
- Modify: `docs/phases/phase-12.md`
- Modify: `docs/roadmap/18-phases.md`
- Modify/create: `scripts/phase12-sms-accounting-audit.sql` only if the migration/ledger needs a read-only DB audit

- [ ] Replace outdated SMS unit-only/unfinished statements with the delivered IRR ledger, grant, adapter, report and remaining live-activation gates.
- [ ] Record the provider unit verification evidence, migration target/safety procedure, authorization matrix, unresolved-send reconciliation steps and test/build evidence actually collected.
- [ ] Keep SEP initiation explicitly deferred to `2026-11-02-sep-sms-topups.md`; do not imply a working SEP checkout.

## Verification boundary

No automated tests or builds are included in this plan. The owner did not request test/verification execution; do not add or run tests unless the owner separately requests verification. Any production/live-provider operation remains separately gated.
