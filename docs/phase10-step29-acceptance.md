# Phase 10 · Step 29 — Integrated macOS / Ubuntu acceptance

## Scope and acceptance rule

Step 28 sales return is owner-approved. Step 29 freezes one commit SHA and verifies the **same source** on macOS and Ubuntu, plus a separate authenticated **real Sandbox** walkthrough. A build passing on one platform is not cross-platform acceptance.

Step 29 is accepted only when:
1. Both hosts report the exact same commit SHA and a clean tracked tree.
2. Both hosts pass .NET solution tests, React/Next web tests, typecheck and build.
3. Both hosts are scoped to the test database `argintaxpayer_test`; macOS uses AutoSSH to `127.0.0.1:15432` and Ubuntu directly uses `127.0.0.1:5432`.
4. The owner confirms role isolation and immutable snapshots for original, cancellation, correction, sales return.
5. Fresh invoices are **actually** sent to Sandbox and verified using reference inquiry; preserve authority results, code, reference number, fiscal tax ID and chain timestamps. Never resend any Indeterminate or Pending outcome.
6. One list row per original invoice, latest subject/status on that row, and complete lineage history with each unique fiscal tax ID.
7. Database audit shows no illegal cross-tenant relations, duplicate active legal successors or over-returned quantities, including pending/indeterminate operations.
8. No Production send, OTP, signing-key export or external credentials in the evidence directory.

## Run the offline evidence gate

On both hosts, pull the **same** Step29 branch commit. The gate refuses an unexpected branch and tracked local modifications.

```bash
git fetch origin
git switch work/phase10-step29-cross-platform-acceptance
git pull --ff-only
git rev-parse HEAD
# Set this to the exact SHA captured from the first host:
export PHASE10_EXPECT_SHA='<same-commit-sha>'
bash scripts/phase10-step29-acceptance.sh
```

Before running, install frontend dependencies with `cd apps/web && npm ci`, if needed. Results are placed in ignored local `artifacts/phase10-step29/<sha>/<host>-<UTC>/` folders.

## Live operations are manual and distinct

On macOS, use the existing AutoSSH PostgreSQL tunnel and authenticated local API/web browser. Ubuntu is kept as the test server; its test DB is not replaced or moved to macOS. Do not re-run already accepted live invoice operations; use fresh invoices and tax identifiers for each Sandbox scenario.

- Original normal sale: submit once, get official `SUCCESS`, verify correct buyer/seller, goods ID, VAT and receipt/reference inquiry.
- Cancellation: start from accepted original, confirm once; verify no second visible row, immutable original and subject=3 in chain.
- Correction: edit only lawful fields, save/reopen, send once; verify reference to original and subject=2; test invalid field rejection.
- Sales return: return one item partially, verify calculations/subject=4, and a second partial return within the remaining original quantity; reject over-return before outbound send.
- Security: Level1/Level2/customer and foreign-tenant access checks; two-tab stale revision; Idempotency under retries; external timeout must remain Indeterminate without resend.
- Chain and list: latest status/subject on one visible row and all legal successors, fiscal IDs and timestamps in lineage.

### Evidence / sign-off

Attach one manifest from each platform with matching SHA, migration list and read-only audit results; record Sandbox test invoice identifiers **without tokens or secret keys**. Do not claim automated gates perform authority send, operator logins or browser acceptance.

Status: Step29 remains pending until both hosts and real Sandbox outcomes are supplied. Step30 release/tag/merge is deliberately excluded.
