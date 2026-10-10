# ArginTaxPayer

Web-based, multi-taxpayer Iranian electronic invoicing platform. ASP.NET Core / C# backend and Next.js / TypeScript frontend. This repository is being designed phase-by-phase.

## Current status
**Phase 01 — Domain Discovery & Architecture: IN PROGRESS.** Documentation baseline only; application code and tests are not implemented yet.

## Core product rules
- Super Admin (Level 1) provisions Level 2 admins, grants/revokes taxpayer access, and exclusively accesses platform-wide security/audit logs and sensitive global information.
- Level 2 Admins operate exclusively on assigned legal entities/individual taxpayers. Full business operations within scope do not imply access to platform-wide logs, raw private keys, other admins' payment accounts, or privileged infrastructure operations.
- Each Level 2 admin has independent payment-gateway configuration, receiving bank accounts, cash receipts, accounting books, and settlement ledger with Level 1.
- Annual subscription: one taxpayer / one active fiscal memory, valid for 12 months (invoice cap determined by contract).
- Quantity subscription: unlimited taxpayers, purchased invoice units, validity no longer than 12 months; safe, auditable consumption and reversals.
- Invoice drafts, signing/submission, asynchronous inquiry, final fiscal outcomes, and migrations are distinct processes.
- Compatibility with ArginAccounting imports/APIs and migration from the legacy MySQL application.

## Roadmap
See [18-phase roadmap](docs/roadmap/18-phases.md), [Phase 01](docs/phases/phase-01.md), and [ADR 0001](docs/adr/0001-tenancy-access-finance.md).

## Confidentiality
Never commit actual production SQL dumps, phone numbers, certificates, private keys, secrets, payment credentials or customer data. Source documents must be sanitized before being added to a public repository.
