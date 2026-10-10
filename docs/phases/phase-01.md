# Phase 01 — Discovery, Domain Analysis and Architecture

**Status: IN PROGRESS.** This phase establishes decisions and implementation-ready contracts; it does not claim code, migrations or end-to-end integrations are complete.

## Step plan
| Step | Deliverable | Acceptance |
|---|---|---|
| 01 | Scope and functional/non-functional requirements | Explicit annual/quantity rules, payment methods, root/delegate policies documented |
| 02 | Inspect sanitized legacy database schema | Tables, relationships, sensitive fields, migration feasibility matrix |
| 03 | Fiscal requirements matrix | Field constraints by invoice type/pattern/subject, doc version and acceptance test examples |
| 04 | Domain bounded contexts | Identity, Delegation, Taxpayer, Catalog, Invoicing, FiscalGateway, Subscription, Finance, Migration |
| 05 | ERD v1 and constraints | Workspace + taxpayer grant + invoice + payment + settlement structures; foreign keys and uniqueness |
| 06 | API & security contracts | Versioned endpoints, authorization scopes, secret storage, idempotency |
| 07 | Architecture decision review | Signed-off ADRs, risk log, test matrix and foundation handoff |

## Step 01 — Requirement baseline (initial draft)
### Functional
- Registration, login, user profile and admin accounts.
- Two admin tiers with Level 1 granting taxpayer/company access to Level 2.
- Complete delegated business operations limited to assigned entities/people.
- Level 1-only system-wide logs, privileged metadata and platform configuration.
- Per-delegate financial segregation: banking accounts, gateway profiles, cash intake, detailed ledgers and platform settlement.
- Level 2 delegates set private selling prices for their assigned companies/persons, including per-customer overrides. Level 1 and each Level 2 are profit-sharing partners, typically 50/50 of net distributable profit after tax and approved eligible expenses, **not gross-sales commission and not wholesale/baseline-price settlement**. Level 2 submits cost evidence and tax breakdown; Level 1 reviews expenses. Snapshot agreement version, sale price and approved deductions for each distribution.
- Annual plan: single taxpayer and fiscal memory, twelve months.
- Quantity plan: arbitrary number of taxpayers, purchased unit count, max twelve-month validity.
- Support all officially documented invoice patterns and allowed subject/type combinations with versioned rules.
- Submit, poll and reconcile tax authority responses; never equate transport acceptance with fiscal acceptance.
- Import legacy taxpayer/customer/product/invoice history without resubmission.
- Import from ArginAccounting via documented schemas/API and send submission outcomes back.

### Non-functional and compliance
- Multi-tenant isolation, OWASP security controls, MFA for admin users, audit trails and encrypted credentials.
- Idempotency for payments, quota use, invoice submissions, queue processing and migrations.
- Persian RTL user experience; calendar conversion in UI but UTC timestamps and explicit timezone in persistence.
- Decimal arithmetic for IRR calculations, transaction consistency and durable recovery.
- Support generation and correspondence validation of public/private key pairs and PKCS#10 CSRs for natural/legal persons; official fiscal-memory onboarding and certificate import are separate steps.
- Prefer client-side isolated generation and owner-controlled initial export; signing key custody must be explicit, encrypted and inaccessible as plaintext to delegates.
- SDK protocol compatibility to be established against official 1405 manuals and test environment.
- Source of truth and legal interpretation of billing/settlement are still subject to contract review.

## Reference documents supplied by product owner
- Legacy `taxpayermaindb` MySQL 8 SQL export (contains live-like customer data and password hashes; **never commit**).
- Invoice issuance specification RC_IITP.IS v7.9 (Tir 1405).
- Unit-of-measure specification RC_UMGS.ST v1.18.
- Tax connection specification RC_TICS.IS v1.6.
- .NET SDK guides with and without signing certificates.
- Screenshots of existing product registration, dashboard, list, wizard and detail forms.

## Pending evidence and risks
- Complete sanitized legacy schema inventory, actual rows/counts and FK mapping require offline examination; never quote or export users' secrets to GitHub.
- Verify exact rules and date semantics for `insr`, `indati2m`, invoice references and settlement patterns.
- Confirm the allowed deduction categories, evidence and review/approval workflow, per-delegate negotiated profit-share percentages (default 50/50), settlement cadence, refunds/chargebacks, losses and tax treatment with accounting/legal review.
- Validate version and compatibility of supplied .NET SDK binaries before choosing production adapter.

## Exit criteria
Phase 01 can be marked DONE only after Steps 01-07 are evidenced, source rules traceable to official docs, contract issues resolved or tracked, and product-owner acceptance.
