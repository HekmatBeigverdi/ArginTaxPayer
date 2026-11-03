# SMS Credit Enforcement Toggle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let Level 1 disable customer SMS wallet enforcement while continuing to record Ghasedak provider expense.

**Architecture:** Extend the existing Level 1 SMS service setting with a default-on customer billing switch. Snapshot it on every dispatch; the coordinator uses that snapshot to decide whether it reserves/debits customer wallet credit, and always records actual provider expense. Keep registration OTP linking consistent with the dispatch snapshot.

**Tech Stack:** .NET 9, PostgreSQL additive migration, Next.js settings UI.

**Spec:** `docs/superpowers/specs/2026-11-02-sms-credit-enforcement-toggle-design.md`.

## Global Constraints

- Default the setting to enabled, including existing setting rows and dispatch history.
- Disabled means no wallet balance check/reservation, customer debit, or spend ledger event; provider expense still records.
- Do not change past dispatch accounting or create negative wallet balances.
- Do not send live SMS.
- Do not apply a database migration without separate explicit approval for `argintaxpayer_test`.
- Automated tests/build are outside the current authorization unless separately requested.

## Review Focus

- Missing JSON key → default enabled for compatibility.
- Toggle changed after a dispatch → existing dispatch/registration attribution uses its saved setting.
- Disabled customer with zero/low balance → SMS proceeds without wallet mutation.
- Disabled billing with known Ghasedak cost → provider expense is recorded and customer charge is absent.
- Re-enabled setting → new dispatches reserve and debit normally.

---

### Task 1: Persist and expose the Level 1 setting

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SmsServiceSettingsService.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12SmsEndpoints.cs`
- Modify: `apps/web/app/subscriptions/sms-service-settings.tsx`
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261102050000_AddSmsCustomerBillingEnforcement.cs`

- [x] Add `CustomerBillingEnabled` to the service settings record and save request; default missing persisted value to `true`.
- [x] Persist the value in `application_settings` JSON and the immutable setting-history table.
- [x] Add `customer_billing_enabled_snapshot BOOLEAN NOT NULL DEFAULT TRUE` to `sms_provider_dispatches` so existing rows are interpreted as enabled.
- [x] Add an accessible Level 1 switch labeled to describe customer credit control and deduction; initialize it from the settings API and include it on save.
- [x] Review migration SQL for additive-only changes and confirm predecessor `20261102040000_AddSepSmsTopups` is already applied to `argintaxpayer_test`.

### Task 2: Apply the setting consistently during dispatch

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SmsDispatchCoordinator.cs`

- [x] Snapshot the setting in each new provider dispatch.
- [x] When enabled, preserve current pre-send reservation, insufficient-credit outcome, settlement and release behavior.
- [x] When disabled, skip customer account reservation and balance rejection; after provider response, always record provider expense but do not create customer-charge or spend-ledger records.
- [x] For successful provider submission with known cost and disabled billing, mark the dispatch settled without requiring a customer reservation; continue reconciliation for ambiguous or missing provider-cost evidence.
- [x] In registration linking, use the dispatch snapshot: always link provider evidence to the new customer, but skip customer debit if billing was disabled at send time.

### Task 3: Document status and migration gate

**Files:**
- Modify: `docs/phases/phase-12.md`
- Modify: `docs/roadmap/18-phases.md`

- [x] Document default-on behavior, disabled-mode cost reporting, dispatch snapshot semantics and migration ID.
- [x] Obtain separate explicit approval and apply the migration only to `argintaxpayer_test`; verify history and default-true columns.

## Verification boundary

No automated tests or live SMS send are included without separate owner request. The migration was applied to `argintaxpayer_test` after separate owner approval; migration history and default-true columns were verified.
