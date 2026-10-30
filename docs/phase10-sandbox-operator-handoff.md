# Phase 10 Sandbox operator handoff

Scope: taxpayer `425a8f26-7633-41ca-a3cc-431c9e2f1719`, invoice business identifier
`00010-05-AAA`, Sandbox only.

## Current local result

The local implementation currently provides owner-scoped, read-only readiness,
dispatch history and the legacy transport contract. The readiness report is
intentionally fail-closed: the current database audit contains 3 invoice
documents, 3 immutable revisions, 1 Sandbox memory, and **0 dispatch
operations, 0 attempts and 0 receipts**. Therefore no invoice has been sent.
No Production endpoint, real invoice POST, credential, private key or authority
receipt was used by Codex.

The ordered implementation work is tracked in
`docs/superpowers/plans/2026-10-25-phase10-legacy-sandbox-readiness.md`.
Tasks are completed only when their tests and evidence are recorded; a local
connection test is not a fiscal submission.

### Current implementation ledger

- Task 1: readiness/BFF and evidence inventory complete.
- Task 2: transactional serial repository is registered and exposed through a
  guarded local endpoint; no reservation exists because the selected invoices
  remain Draft.
- Task 3: the certificate-free legacy `INVOICE` packet builder now signs the
  exact packet bytes with the protected RSA key and emits only bounded packet
  metadata/request JSON. Offline packet tests pass. Persistence and Outbox
  admission are intentionally deferred to Task 4. The Phase09 snapshot
  signature is not reused as a submission packet.
- Task 4: legacy packet admission is now represented as a separate durable
  Outbox event with idempotent operation identity and no stored access token.
  The protected token-refresh boundary now has an opt-in, Sandbox-host-pinned
  legacy `GET_TOKEN` client; the token remains memory-only. Receipt persistence,
  Outbox consumption, and HTTP invoice send are still pending, so no HTTP send
  is enabled by this preparation step.
- The loopback `/dispatch/send` route now invokes the explicit legacy Outbox
  worker for the selected invoice, but remains fail-closed until
  `SandboxInvoiceLegacy__OperatorApprovedSend=true` is set locally.
- The Outbox endpoint code is now compatible with the append-only attempt table,
  and the `Reserved -> Sending` state CAS plus attempt-number allocation are
  atomic. Token custody, external invoice POST, receipt persistence and marking
  the Outbox message processed remain the next steps.
- Local custody is the database provider: the public SPKI is metadata and the
  private PKCS#8 is recovered from the encrypted `DbSigningKeyEnvelopes`
  envelope using `SigningKeyProtection__DatabaseMasterKey`. The master key is
  not stored in PostgreSQL, Outbox or logs.

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
4. Confirm readiness shows the exact blockers for the selected revision. The
   owner consent checkbox, send and inquiry actions remain disabled until the
   corresponding plan tasks are complete.
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
