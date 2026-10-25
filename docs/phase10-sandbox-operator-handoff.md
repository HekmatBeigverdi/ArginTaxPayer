# Phase 10 Sandbox operator handoff

Scope: taxpayer `425a8f26-7633-41ca-a3cc-431c9e2f1719`, invoice business identifier
`00010-05-AAA`, Sandbox only.

## Current local result

The local implementation has completed the admission, packet, durable outbox,
evidence and RTL readiness/history layers. The tested source SHA is recorded by
the final gate. No Production endpoint, Production database, real invoice POST,
credential, private key or authority receipt was used by Codex.

The browser send and inquiry controls remain disabled until the operator has
verified the real Sandbox endpoint, credential/certificate custody and current
authority protocol. A local test pass is not a real Sandbox transmission and an
HTTP acknowledgment is not fiscal acceptance.

## Safe local verification

Run from the repository root on macOS:

```bash
git pull --ff-only origin phase/10-invoice-submission
git status --short
bash scripts/phase10-sandbox-v2-offline-gate.sh
```

The last command must end with `SOURCE/CRYPTO GATE PASS`. It performs no invoice
POST and no migration.

For the read-only database audit, export the existing operator-managed connection
string without putting it in a command argument, file, screenshot or log:

```bash
export ConnectionStrings__ArginTaxPayerDb='(operator supplies the existing local-tunnel value)'
bash scripts/phase10-step20-db-audit.sh
unset ConnectionStrings__ArginTaxPayerDb
```

The script refuses any database other than the tunneled `argintaxpayer_test` and
uses PostgreSQL read-only mode. Do not run `dotnet ef database update` as part of
this handoff. No new migration is required by the current local changes.

## Manual UI check

1. Start the existing local stack with the project’s normal development launcher.
2. Sign in as the owner of the taxpayer and open `/invoices`.
3. Open invoice `00010-05-AAA` and confirm the panel shows taxpayer scope,
   `Sandbox`, invoice revision, Sandbox memory identifier and the current
   blockers.
4. Confirm the owner consent checkbox is explicit, the inquiry action is
   disabled, and no browser request is made to a tax-authority host.
5. Do not treat `Acknowledged`, `referenceNumber`, or a green local build as
   fiscal acceptance.

## Real Sandbox approval gate

Before any future live run, the operator must explicitly confirm all of the
following in the same session:

- taxpayer and invoice are exactly the displayed owner-scoped records;
- environment is `Sandbox`, not Production;
- Sandbox fiscal memory, certificate chain, public-key fingerprint and protected
  private-key custody match the displayed invoice revision;
- the current official endpoint/protocol and nonce authentication have been
  independently verified;
- a backup/restore rehearsal for the isolated test database is complete;
- one-shot send is approved for this exact invoice and no prior indeterminate
  attempt exists.

The current branch intentionally has no enabled real-send UI/API command. Do not
invent a curl request or bypass the disabled endpoint. Once the live adapter is
operator-approved and enabled in a separate reviewed change, its evidence must
record the exact SHA, endpoint allowlist result, operation ID, request trace ID,
sanitized receipt and subsequent inquiry result. Unknown outcome requires
reconciliation before any resend.
