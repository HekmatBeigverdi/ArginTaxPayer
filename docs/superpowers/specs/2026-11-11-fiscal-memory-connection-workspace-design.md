# Fiscal memory and connection workspace redesign

Date: 2026-11-11 (Asia/Tehran)
Status: Owner selected UI/status redesign preserving data; implementation prepared locally. No commit or deployment.
Builder review: completed; no material unanswered planning questions reported.

## User intent

Make fiscal-memory setup understandable for an ordinary user. Separate Sandbox
and Production, remove competing setup/test panels, and show a compact summary
after setup. Show key-entry controls only during setup, applicable recovery after
a failed test, or an explicit management/change request. Keep existing fiscal
records, signing keys and submitted-invoice identities intact.

## Observed problem and evidence

An authenticated browser reproduction on tax.argin.ir found that the standalone
Sandbox memory has encrypted custody and passes both an offline RSA challenge
and the explicit Sandbox authority test (`200`, `Authenticated`, authority status
`ACTIVE`). The regular fiscal-memory row with the same displayed identifier has
no regular signing-key version (`hasKey=false`). Its generic connection test
returns `KeyNotReady` and records a failed test on `fiscal_memories`.

`SandboxFiscalMemoryMetadataService.TestAuthorityAsync` returns the successful
result without persisting it. `FiscalMemoryManagementService.RecordConnectionTestAsync`
persists the generic test result. The list renders only the latter, creating the
contradiction in the owner's screenshot. The generic re-entry button opens an
existing-key form above the test but does not focus or scroll to it. A fresh-login
session had editable inputs; the user's earlier disabled-input state was not
reproduced and must not be attributed conclusively to login freshness.

## Approaches

1. **Recommended: one workspace and source-aware status, preserving current key
   stores.** Reuse existing Sandbox and regular-memory services through a common
   safe view model. Persist each test against its actual memory and key. This
   removes duplicate UI and incorrect status attribution without moving keys.
2. Consolidate key stores and migrate existing Sandbox keys and associations.
   This affects encrypted identities, dispatch foreign keys and historical
   evidence. It exceeds the requested display redesign and needs a separate
   migration project.

The owner selected approach 1 in the conversation. Keys and existing data remain in their current stores.

## Proposed interface

Replace the two separate accordions, “حافظه‌های مالیاتی” and
“اتصال به سامانه مؤدیان — کلید/توکن”, with one “حافظه مالیاتی و اتصال” workspace.
Use distinct, labelled Production and Sandbox tabs with independent selection,
setup state, errors and actions. Never use a display-tab selection as authority
routing input.

Each memory has one compact card:

- Memory identifier and explicit environment.
- Key state: missing, stored, recovery required, or unavailable.
- Last recorded connection outcome, its timestamp, and whether it applies to the
  currently selected key. No green verified state merely from encrypted-key
  presence or an old result for another key.
- Activation/dispatch readiness separate from successful authentication. An
  authority `ACTIVE` response is an observation, not permission to overwrite a
  separate local activation/subscription decision.
- Primary action “تست اتصال”; secondary “مدیریت تنظیمات”. When appropriate,
  “بررسی و فعال‌سازی” is shown with an explanation of the remaining requirement.

After successful setup, hide import/generation forms, RSA/provider details,
consent controls, change-request forms, and multi-step instructions. Advanced
metadata and fingerprints live in a collapsed “جزئیات”. Do not hide a genuine
current failure or make it disappear by resetting local React state.

First setup is a short, environment-specific flow: identify the memory, supply
its matching key using an existing-key/import or generation choice, then test
the connection. Reuse supported backend contracts and required portal steps;
do not generate a replacement key as a side effect of choosing setup.

“مدیریت تنظیمات” opens the selected memory's controls and focuses the relevant
step. “انصراف” clears transient private inputs and returns to the summary.
Successful storage and testing return to the summary automatically.

## Failure and recovery behavior

Classify failures into missing/invalid key, interrupted custody, session or
permission, provider/configuration, authentication rejection, and network/timeout.
Offer a suitable next action. A network failure offers retry and retains previous
successful evidence; it does not suggest key replacement. A key-related failure
offers same-key entry/recovery. Fresh-login expiry provides an actionable sign-in
message, rather than silently disabled fields. Re-entry opens and scrolls to the
form or its blocker.

Entering the same key and replacing a different key remain distinct operations.
Any same-key recovery must verify the saved public-key binding and preserve the
memory identity and history. Different-key replacement retains applicable
approval/ownership constraints. Existing Sandbox custody currently rejects a
second import; the new UI must not advertise unsupported replacement. Implement
an explicit, owner-authorized same-key restore operation where necessary, with
fresh authentication, revision checking and consent; do not bypass the current
guard by changing identifiers or deleting rows. Different-key management must
use a source-aware approved operation, or clearly explain its current restriction.

## Memory sources and duplicate identifiers

The backend view model carries taxpayer/owner scope, persisted environment,
internal memory identifier, and actual source (`RegisteredMemory` or
`LegacySandbox`). These bindings determine every status and action; a fiscal
identifier string alone never authorizes key use or dispatch.

For the observed duplicate with no regular signing key and an existing standalone
Sandbox key, show the configured Sandbox connection as the primary card. Keep
the unused regular row's old `KeyNotReady` in its own history/advanced detail,
not as the configured connection's current failure. Preserve both physical rows.
If both sources contain keys or incompatible bindings, show an explicit
configuration conflict and require a source choice/approved reconciliation; do
not silently merge tests, keys, activation or dispatch identities. Existing
invoice bindings remain immutable. New invoice selectors must agree with the
same source/readiness model and must not present an unconfigured duplicate as
the successfully configured memory.

## Persisted status and API contract

Add an additive, source-aware connection-observation store, or equivalent
source-bound schema, containing owner/taxpayer scope, environment, actual memory
ID/source, key version or fingerprint binding, attempt ID, safe outcome, attempt
time, and non-secret authority/identity-verification flags. Store both success
and failure observations without secrets, raw payloads, tokens or private keys.
Keep latest attempt and previous successful observation distinguishable.

Capture the memory/key binding before the probe. A late response must not verify
a newer key, a changed memory revision, a different source, or another owner.
Return explicit stale/unapplied results on concurrent change. Guard last-status
ordering so an older completed attempt cannot overwrite newer evidence.

Expose an allowlisted shared read model to the UI and refresh it after each
explicit test or successful setup. Persist successful Sandbox probes; retain
the generic source's observations separately. Legacy boolean/timestamp records
remain historical evidence and are not fabricated into key-bound verification.
The already observed Sandbox success was not persisted by the old code: after
upgrade, present “کلید ذخیره شده؛ آزمون ثبت‌شده موجود نیست” until an explicit new
test records it. Do not invent a successful timestamp during migration.

GET/read operations and opening a tab never invoke a tax-authority request,
activate a memory, or write a key. Only explicit user actions initiate tests.

## Boundaries and delivery

Keep existing .NET/Next.js architecture, RTL styling, session/CSRF/tenant checks,
protected key custody, environment-specific origins and manual submission rules.
No automatic invoice submission, key rotation, data migration, server flag
change, production migration application, merge, or deployment is included.
Prepare the additive SQL/migration and operator notes for review separately from
the existing application-only release workflow.

## Validation required for implementation

- Focused backend tests for source/owner/environment isolation, durable Sandbox
  observations, stale key/concurrent result rejection, and unchanged activation.
- UI behavior checks for first setup, compact configured state, reload, tab and
  memory changes, failed tests, fresh-login blockers, re-entry focus, and explicit
  management. Avoid source-text-only assertions for these interactions.
- Regression coverage that invoice bindings and their selected authority/key
  source are not changed by the display grouping.
- Browser checks on a local instance with synthetic API responses for both
  environments; no real invoice submission as a validation step.
- Relevant web typecheck and production build, plus focused existing suites.

## Approval checkpoint

Owner review is requested for the recommended scope and compact, per-environment
interaction. Once approved, write the implementation plan, implement in an
isolated branch/workspace, and present the resulting changes and validation.
