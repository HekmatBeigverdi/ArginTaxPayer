# ArginTaxPayer — Phase 07: Invoice Domain and Rule Engine

## Summary

Completes the fixed 24-step Phase07 implementation for the Iranian electronic invoice domain and V7.9 rules. Step23 macOS/Ubuntu acceptance was confirmed by the project owner on 2026-10-18.

## Delivered

- Versioned legal invoice type, pattern and subject compatibility with reference rules for original, correction, cancellation and sales return.
- Deterministic invoice amounts, line discounts, VAT and settlement checks (cash, credit, mixed), validation and rounding.
- Customer-isolated taxpayer/buyer and Phase06 goods/services snapshot integration; revision-aware persisted invoice-entry drafts and Persian RTL wizard.
- Negative-case regression suites, isolated development-only API endpoints, read-only PostgreSQL and HTTP verification scripts.
- Reproducible macOS/Ubuntu builds: untracked Next.js development cache and generated type files, distinct development and build distributions.

## Boundaries

Phase07 does **not** send fiscal invoices to the Tax Authority or sign/submit/finalize official invoices. Fiscal authentication, certificates, submission, authority responses, entitlement/billing and financial settlement are future-phase responsibilities. Do not apply migrations to production as part of this GitHub Release.

## Verification

Step23 owner-approved, with independent raw test logs not archived in this release. Scripts:
- macOS: `zsh scripts/phase07-step23-macos.zsh`
- Ubuntu test VPS: `bash scripts/phase07-step23-ubuntu.sh`
- Dedicated nonproduction DB: `argintaxpayer_test`

Full history: `docs/phases/phase-07-invoice-domain-rule-engine.md`.
