# Fiscal memory workspace implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Provide separated Sandbox/Production setup with compact configured cards and durable, correctly attributed connection results.

**Architecture:** Preserve both existing key stores and invoice bindings. Add source/key-bound observations to existing explicit test paths and project safe history through existing BFF endpoints. Replace duplicate dossier accordions with one connection workspace, retaining existing cryptographic import/generation contracts behind explicit management.

**Tech Stack:** ASP.NET Core/.NET 9, PostgreSQL, Next.js 15, React 19, TypeScript, node:test, xUnit, Playwright for browser verification.

**Spec:** `docs/superpowers/specs/2026-11-11-fiscal-memory-connection-workspace-design.md`

## Global constraints

- No key migration, invoice submission, server config writes, production database migration, merge or deployment.
- Persisted owner/taxpayer/environment/source/internal-ID binding governs status and actions.
- Successful authentication does not replace activation or subscription checks.
- Keep forms hidden after setup except explicit management or actionable key recovery.
- Preserve original checkout and concurrent user work. Native execution in `/tmp/argintax-fiscal-memory-workspace`, branch `codex/fiscal-memory-workspace`.

## Review focus

- Same displayed Sandbox identifier exists in both stores: use the configured source without changing invoice identities.
- Late test response after key or memory change: do not apply it to the new configuration.
- Newer failed network attempt after earlier success: display both facts without proposing key rotation.
- Browser reload/tab or taxpayer switch: no leaked state, secret inputs, or invented successful connection.
- Missing new schema or expired fresh login: provide actionable state instead of silently disabled controls.

### Task 1: Durable source-bound connection observations

Files: add `Persistence/Signing/FiscalMemoryConnectionHistory.cs`, additive migration `20261111180000_AddFiscalMemoryConnectionObservations.cs`, focused xUnit tests; extend signing import/connection and Sandbox metadata/test endpoints and their BFF allowlists.

Interfaces: `ConnectionBinding`, `ConnectionAttemptView`, `ConnectionHistoryView`; capture a source's current memory/key identity before test; append results only if binding still matches; read latest attempt and last success for the current binding. Expose `connectionHistory` in safe GET responses.

- [x] Add failing tests for binding equality, source/environment isolation, stale-key and revision rejection, current-history projection and migration preservation.
- [x] Implement observation policy, SQL-backed store and additive guarded migration; no historical backfill or key update.
- [x] Integrate explicit probes, capture before network, persist before optional regular-memory activation, and expose persistence availability.
- [x] Verify focused backend tests and compile; add disposable-schema integration verification when a local isolated PostgreSQL is available.

### Task 2: One compact workspace with separated environments

Files: `taxpayer-workspace.tsx`, `taxpayer-connection-panel.tsx`, `sandbox-fiscal-memory-panel.tsx`, `signing-connection-test.tsx`, new shared connection summary/model helpers, `globals.css`, focused behavior tests.

Interfaces: shared safe history; source-specific actions always retain their original endpoints. Configured standalone Sandbox is primary over an unconfigured duplicate with the same identifier. Conflicting configured sources require explicit source selection.

- [x] Add failing behavior tests for source selection, compact/configuring/error states and network-versus-key failure actions.
- [x] Merge the memory-list and connection accordions into one workspace. Show explicit environment tabs and selected-memory summary.
- [x] Hide setup/import/generation/change-request forms behind first setup or management. Focus the correct entry controls and explain fresh-login blockers.
- [x] Persist/read status after reload and refresh summary after each explicit operation; display activation separately.
- [x] Preserve existing initial registration and applicable review/recovery flows; update obsolete source-text tests with behavior assertions.
- [x] Verify focused web tests and typecheck.

### Task 3: Applicable Sandbox same-key recovery and verification

Files: Sandbox metadata service/API, bounded BFF restore route, Sandbox management controls, focused tests and operator notes.

Interfaces: explicit same-key restore accepts matching private key, expected revision and custody consent; requires owner and fresh login. Preserves IDs/public key/dispatch references. Different-key replacement remains approval-controlled and is not implemented by deleting/re-registering data.

- [x] Add failing tests for same-key binding, stale revision/owner/fresh-login rejection and safe response projection.
- [x] Add restore action and show it only on explicit management or key-related failure. Clear private inputs on completion/cancel/unmount.
- [x] Run focused tests, full relevant web/.NET regression, typecheck/build and synthetic local browser flows for both environments.
- [x] Prepare reviewed migration/operator notes, independent whole-change review, resolve material findings, and report exact validation limits.

## Execution decisions

Owner selected the recommended scope (UI/status redesign preserving data). Proceed with native execution and one independent final review; another execution-method approval is unnecessary. A worktree outside the repository avoids changing ignore rules on the user's active branch. Changes are prepared for review, without pushing or deploying.

Validation details and migration readiness: see `docs/fiscal-memory-workspace-local-testing.md`. No commits were made, as explicitly requested by the owner.
