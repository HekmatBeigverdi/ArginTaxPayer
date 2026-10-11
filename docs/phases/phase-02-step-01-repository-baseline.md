# Phase 02 — Step 01: Repository & Development Baseline

Date: 2026-10-11
Status: DOCUMENTATION BASELINE RECORDED; LOCAL TEST EXECUTION PENDING
Branch: `phase/02-foundation`
Source of design truth: `phase/01-domain-architecture`

## Verified remote repository state

- Repository: `HekmatBeigverdi/ArginTaxPayer` (public), default branch `main`.
- Existing branches: `main`, `phase/01-domain-architecture`, `phase/02-foundation`.
- At inspection, `main` was 33 commits behind the Phase 01 design branch.
- At inspection, the Phase 02 branch was one commit ahead of Phase 01 and had **no file differences** against it.
- Phase 01 design handoff: `docs/phases/phase-01-architecture-freeze-handoff.md`. Design scope is handoff-ready, not production verified.
- The Phase 01 PR remained subject to owner review; do not auto-merge or claim production release.
- No executable .NET/Next.js solution, database migrations or automated tests are represented in the compared file inventory.

## Baseline design contracts retained from Phase 01

1. Modular monolith: ASP.NET Core API, workers, Next.js App Router TypeScript RTL client, PostgreSQL/EF Core.
2. Server-enforced workspace and taxpayer grants: Root/Level 1 global administration; Level 2 limited to specifically assigned taxpayers.
3. Private payment gateway/bank/accounting per delegate; cash-basis profit sharing applies to verified payment allocated to each order, with Root-approved expenses and order-specific settlement allocations.
4. Commercial order, collection, share calculation, and partner bank settlement are distinct workflows; immutable snapshots and auditable entries.
5. Signing keys require consent, secure custody and redaction; migration cannot imply that fiscal key possession proves authority connectivity.
6. Legacy import is repeatable, read-only, and does not resend history or turn prior payments into new purchases.
7. Fiscal rules and tax adapter behavior are versioned; later-phase validation and security gates remain mandatory.

## Phase 02 delivery scope (acceptance blueprint)

- `ArginTaxPayer.sln` with Domain, Application, Infrastructure, API, Workers and isolated tests.
- Next.js TypeScript App Router Persian RTL shell with shared UI primitives.
- PostgreSQL dev setup, EF Core migration baseline and tenant-aware schema.
- Environment/config template with no secrets, secret custody abstraction and authorization placeholders.
- Health endpoint/OpenAPI, structured logging, correlation/redaction, outbox/idempotency.
- CI pipelines for .NET and frontend; developer setup for macOS and Windows.
- Negative tenant isolation and financial ledger invariant tests.
- English technical documentation and explicit review and acceptance records.

## Implementation guardrails

- Do not add application scaffolding or migrations in Step 01; those belong to subsequent steps.
- Do not modify Phase 01 documents or the default branch.
- Keep all production/customer SQL dumps, live identities, payment secrets and private keys out of the public repository.
- Preserve all deferred Phase 01 verification gates; no signoff implies that production permissions or fiscal rules have been tested.
- Make one reviewed step-level commit. Do not merge/tag/release without approval.

## Environment and local verification

GitHub connector inspection verifies **remote state only**. It cannot establish whether a developer's local working tree is clean or execute local .NET/npm tests. A local clone request from this execution environment failed due to unavailable GitHub DNS; this is **not** a project test failure.

Execute on the developer's macOS machine before implementation:

```bash
git status --short
git remote -v
git fetch origin --prune
git branch -a
git log -1 --oneline origin/main
git log -1 --oneline origin/phase/01-domain-architecture
git log -1 --oneline origin/phase/02-foundation
git diff --stat origin/phase/01-domain-architecture...origin/phase/02-foundation
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
find . -maxdepth 3 \( -name '*.sln' -o -name '*.csproj' -o -name 'package.json' -o -name '*Tests*' \) -print
```

If uncommitted changes exist, review/stash/commit intentionally before switching branches; never overwrite local work.

## Step 01 acceptance evidence

| Criterion | Status | Evidence |
|---|---|---|
| Correct repository and branch identified | PASS (remote) | GitHub repository and branch inspection |
| Phase 01 handoff inspected | PASS | Architecture freeze/handoff and phase documents |
| Baseline risks and design contracts documented | PASS | This document |
| No duplicate Phase 02 branch created | PASS | Reuse `phase/02-foundation` |
| Local working tree clean | NOT VERIFIED | Developer terminal required |
| Local tests/build | NOT RUN | No executable application/test project yet; local environment unavailable |
| Phase 01 merge to main | NOT DONE | Separate owner-controlled decision |
| Phase 02 Step 02 | NOT STARTED | Explicit approval required |

## Completion record

Step 01 records the remote repository baseline and project readiness without claiming implementation or successful tests. Complete the local checks above before marking the step fully accepted. Future phase steps will add runnable code and automated tests.
