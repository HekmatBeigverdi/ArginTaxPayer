# SMS Wallet, Usage Billing, and SEP Top-Ups Design

## Intent

Give ordinary software users a 200,000 toman SMS credit at account creation, meter SMS usage through Ghasedak, show remaining credit beside the active plan, and let finance administrators report usage and actual provider cost. Level 1 and Level 2 accounts may use SMS without a wallet charge. The user-facing software must continue to work when a customer has insufficient SMS credit; only SMS dispatch is rejected with a clear insufficient-credit result.

Payment through SEP is a separate second stage. The first stage establishes trustworthy SMS accounting and the purchase contract. Stage 2 source implementation is now present; payment initiation remains disabled by default until merchant setup and sandbox behavior are verified.

## Owner-confirmed decisions

- Stage the work: SMS wallet, Ghasedak accounting, header, and report first; SEP configuration and top-ups second.
- Ordinary customers receive 200,000 toman (2,000,000 IRR) once at creation.
- Level 1 and Level 2 administrators have unlimited SMS usage and no SMS wallet balance.
- For an ordinary customer, wallet charge is the actual Ghasedak cost plus the configured SMS service-cost percentage.
- Preserve actual provider expense separately from the customer charge. Snapshot the percentage used for each send.
- Add a Level 2 partner filter and allow a Level 1 administrator to initiate an SEP top-up for a selected customer in Stage 2.
- Stage 2 administration includes SEP terminal configuration and assignment to an administrator. The supplied OnlinePG v3.6 token flow uses a configured terminal; do not send tokens/secrets through chat or expose them in browser state.

## Approaches considered

1. Implement wallet, usage reporting, and live SEP top-ups in one change. This combines unknown merchant integration with the accounting foundation and makes it harder to establish correct SMS charging before money is accepted.
2. Implement SMS accounting and the usage report first, then add SEP as a separate verified payment stage. **Chosen by the owner.** This gives the ledger a stable contract before connecting a payment provider and keeps top-ups disabled until SEP verification is trustworthy.

## Scope and stages

### Stage 1 — Wallet and per-send accounting

- Store monetary values as IRR. The initial promotional grant is 2,000,000 IRR and is posted exactly once, in the same database transaction as each new ordinary Customer account, whether self-registered or provisioned by an administrator. Do not grant this to Level 1/2 administrators. Do not silently convert historic unit-based SMS rows into money.
- Replace the current unit-only internal spend path with an append-only monetary ledger: promotional/admin/payment grants, any reservation/release entries needed for safe dispatch, provider actual cost, customer charge, send correlation, timestamp, charge percentage snapshot, and immutable provider/send identifiers. Use idempotency keys to prevent duplicate debits and grants.
- Extend the Ghasedak adapter to return structured per-recipient results, including `MessageId`, provider `Cost`, and provider `SendDate` when supplied. Verify the currency/unit of `Cost` against the merchant account/provider billing evidence before converting it to IRR or enabling charged sends; persist the raw provider amount/unit with the normalized IRR value. Reject malformed, negative, overflowing, or unverified cost values and keep the send pending reconciliation. Do not persist OTP body, password-reset code, API key, or full recipient phone in the financial report. A response timeout/ambiguous submission must be reconciled by stable `clientReferenceId`/provider status before retrying or releasing a reservation.
- Stage 1 sends one recipient per dispatch, matching the current identity OTP flow and giving each user a separate, attributable ledger line. A multi-user notification fanout must create one independently reserved dispatch per user; do not bundle recipients in one Ghasedak request.
- At dispatch, snapshot the current Level 1 configured service percentage and calculate `customerChargeIrr = providerCostIrr + ceil(providerCostIrr * percentage / 100)`. Store calculation inputs and rounded result. Changes to the percentage apply prospectively only.
- The official Ghasedak contract reports `Cost` after submission; the current docs do not describe a pre-send exact-cost quote. Therefore Stage 1 must define and test an atomic preauthorization ceiling before enabling charged live sends. A Level 1-configured per-dispatch reservation ceiling is the safeguard: reject before contacting Ghasedak when remaining customer credit is below the ceiling, reserve it, then settle the actual provider cost plus fee and release the remainder. If provider cost is absent or exceeds its reservation, retain a pending/reconciliation state, do not retry automatically, and prevent further customer sends until resolved. The ceiling is a Stage 1 setting and must be visible to Level 1. Admins bypass customer wallet reservations but their provider costs remain in usage reporting.
- On exhausted/insufficient customer credit, return a typed `insufficient_sms_credit` outcome through identity UI/API for password reset and other customer SMS. This must not block login by password, software access, invoices, plans, or any non-SMS workflow. Keep anti-abuse throttling and account-enumeration protections intact.
- Provide a server-authorized SMS usage page in Subscriptions & Finance with server-side pages of 10 rows, total provider cost, total customer charges, and administrator-originated provider costs, optional Level 2 partner filter, and time filters. Level 1 can see permitted global totals; Level 2 sees only its own customer scope. Customer charge is zero for Level 1/2 sends because they have no wallet; their provider cost is included in administrator-originated operating costs. Include user, purpose/category, provider ID, Persian-local display of timestamp, provider cost, service percentage, customer charge, and reconciliation status; omit message body and full destination number.
- Show the current ordinary customer's remaining SMS credit in the top bar next to active plan status. Display admins as unlimited and never request/create an admin wallet.
- Add a Level 1-only administrator setting for the SMS service-cost percentage and reservation ceiling. Validate percentage bounds server-side and append an audit/version record so existing sends retain their original setting snapshot.
- Keep existing unit-based `sms_wallet_*` records immutable and unconverted. A reviewed additive migration may introduce money-ledger structures and mark the unit wallet path retired; historical records remain available for audit. New accounts use only the monetary ledger.

### Stage 2 — SEP configuration and purchases

- Use the owner-supplied SEP OnlinePG Technical Guide v3.6 (1404-10) for protocol shape. The uploaded sample archive was reviewed as corroboration only. No sample cookies, test identifiers, SQL or unsafe TLS workarounds were copied.
- Level 1 can configure an SEP merchant/terminal (including terminal number and protected merchant credentials) and assign it to an administrator. The public/UI contract returns only safe display metadata, never secrets. Define whether each selected administrator can initiate purchases for customers within their authorization scope.
- Customer self-service top-up and administrator customer-specific top-up both create an immutable purchase attempt, redirect to SEP, verify server-to-server, and post a wallet grant exactly once only after provider verification matches amount, terminal, attempt/reference, and successful status. Level 1 may act for any customer in its root scope; Level 2 may act only for its existing assigned customers. Terminal assignment grants no customer access or ownership. Browser return/callback parameters alone never grant credit. Unknown outcomes require reconciliation and cannot be retried into a second payment.
- Reuse the SMS money ledger with a payment grant entry tied to the verified SEP attempt; no separate mutable balance adjustment path.
- `SmsSep:EnablePayments` defaults false. Keep payment initiation disabled until the merchant terminal, absolute HTTPS callback/return URLs and server-IP registration are configured, and sandbox success/cancellation/mismatch/duplicate/timeout/recheck behavior is recorded.

## Non-goals

- No live Ghasedak send or SEP payment as part of development verification without separate test authorization.
- No message-body storage, bulk marketing campaign system, or change to non-SMS application access when credit is insufficient.
- No conversion of historical unit counts into a money balance.
- No Level 2 control of pricing/service percentage or provider merchant settings.

## Constraints and repository fit

- The existing `SmsWalletService` is unit-based, has append-only `sms_wallet_events`, but `SpendConfirmedAsync` is internal-only and has no call site. The current Ghasedak adapter serves only OTP and returns `bool`, dropping provider cost and message identifiers.
- Both email/password signup and phone registration create Customer accounts; administrator customer provisioning is a third ordinary-customer creation path. All must apply the initial grant atomically.
- Current top-bar plan status is in `apps/web/app/components/console-shell.tsx` and `subscription-status.tsx`. The existing finance workspace includes an SMS tab with manual unit grant/read controls. The Level 1 admin settings page currently hosts finance tax settings.
- Existing Phase 12 roadmap already requires separate customer tariff/provider expense ledgers and immutable rate snapshots. This design implements the provider-expense and percentage portions now while keeping the existing SMS wallet schema/history auditable.
- Current code uses development-only/local identity routes in several paths. A production authorization/deployment boundary must not be implied by development API completion.

## Acceptance outline

- Registration paths atomically create exactly one 2,000,000 IRR promotional credit for Customer accounts; concurrent/replayed creation cannot double grant it; admins never get a wallet.
- Percentage calculation rounds up to a rial, is bounds-checked, and uses the immutable percentage captured at send reservation.
- Provider success with per-item cost/ID records one provider expense and one customer debit; duplicate delivery callbacks/replays do not charge twice. Missing cost, provider timeout, and ambiguous outcome remain reconcilable and do not cause unsafe automatic resend.
- Below-reservation balance produces the localized insufficient SMS-credit result and no provider request; non-SMS application actions remain available.
- Level 1/2 dispatches record provider expense without a customer wallet debit.
- Usage page totals are computed over the full server scope before paging/filter presentation; Level 2 can neither query nor infer other partner data.
- Top-bar shows current Customer balance, refreshes after a grant/debit, and shows administrator unlimited status without a wallet read.
- Stage 2 tests verify callback authenticity/verification and payment idempotency before any wallet grant; until then UI/API do not initiate SEP.

## Remaining builder choices

- Choose precise message-purpose categories and redaction policy for the report, preserving the no-message-body/no-full-phone invariant.
- Decide how the Level 1 SMS percentage setting version is stored and audited, following current `application_settings` patterns if safe; use a dedicated immutable table if mutable settings cannot preserve snapshots.
- Select Stage 1 migration shape after inspecting any existing test/installation `sms_wallet_accounts` data; never destroy or reinterpret old units.
- The proposed reservation ceiling is necessary because the provider's actual `Cost` is returned after dispatch and the user requested a hard insufficient-credit stop. Confirm the configuration UI/input range and safe default during implementation review; if no ceiling is configured, charged customer sends fail closed until Level 1 configures one.

## External provider reference

Ghasedak's official REST documentation lists `Cost`, `MessageId`, and `SendDate` in send responses, including per-recipient cost in OTP send responses. It documents `clientReferenceId` for correlation and status lookup. It does not describe a pre-send exact-cost quote or clearly state a currency unit in the reviewed send API documentation, so implementation must verify the unit from the merchant billing evidence before enabling charge: [Ghasedak REST API documentation](https://ghasedak.me/docs).


## Implementation status — 2026-11-02

Wallet accounting and Stage 2 SEP top-up source are authored. SEP uses the documented token endpoint, browser form POST and server VerifyTransaction endpoint; verified payment posts exactly one grant into the SMS credit ledger. The additive migrations `20261102030000_AddPhase12SmsMoneyLedger` and `20261102040000_AddSepSmsTopups` were applied to `argintaxpayer_test` after explicit owner approval on 2026-11-02; required relations and migration history entries were confirmed. API build succeeded; no automated tests, typecheck or merchant sandbox transaction was run. Callback/public route deployment is currently within the existing Development + database enabled API boundary; this does not claim production deployment readiness. See `docs/phases/phase-12.md` for the activation gate and endpoint/config details.
