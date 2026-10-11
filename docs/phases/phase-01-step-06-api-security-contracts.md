# Phase 01 — Step 06: API and security contracts v1

Status: DESIGN CONTRACT, not shipped endpoints. Applies to backend ASP.NET Core / EF Core, frontend Next.js.

## Endpoint conventions

- Root: `/api/v1/root/*`, require `PlatformRoot` policy; verified MFA and auditable actor, no regular delegate escalation.
- Delegate: `/api/v1/workspaces/{workspaceId}/*`, require authenticated principal with active workspace membership; each taxpayer-scoped operation ALSO checks active assignment.
- Taxpayer: `/api/v1/taxpayers/{taxpayerId}/*`, resolve assigned workspace and fiscal memory server-side, never trust claims in request body alone.
- Respond with RFC7807 ProblemDetails, correlationId, machine-readable violation codes (e.g., `tax.invoice.invalid_pattern`, `finance.expense.awaiting_approval`), field paths and localized Persian-friendly UI error mapping.
- List pagination, RFC3339 timestamps with UTC storage, IRR minor units as integer/decimal serialized without floating-point rounding. External fiscal decimal quantities remain `decimal` with explicit precision.

## Sample resource contracts

| Method | Route | Authorized | Invariant |
|---|---|---|---|
| POST | /api/v1/root/delegates/{id}/assignments | Root | grants a taxpayer company/person to one delegate; log history |
| GET | /api/v1/workspaces/{id}/taxpayers | Root or assigned delegate | filter to active grants |
| PUT | /api/v1/workspaces/{id}/offers/{offerId} | own delegate or Root | version-priced future offers; never mutate historical orders |
| POST | /api/v1/workspaces/{id}/orders | authorized delegate/customer | snapshot offer, customer, plan, agreement and price |
| POST | /api/v1/workspaces/{id}/collections/cash | authorized delegate | recorded receipt requires evidence; verified-vs-pending distinction |
| POST | /api/v1/payments/gateways/{provider}/callbacks | signed/verifiable provider callback | idempotent verified event then allocate; provider-specific validation |
| POST | /api/v1/root/expenses/{expenseId}/decisions | Root only | immutable approve/reject + evidence/reason/version |
| POST | /api/v1/workspaces/{id}/settlements | delegate | evidence submission cannot self-confirm |
| POST | /api/v1/root/settlements/{settlementId}/confirm | Root only | payout actually received, budget and balance check |
| POST | /api/v1/taxpayers/{id}/certificate-requests | taxpayer owner | generate CSR/key pair or accept owner-generated material; secure custodian policy |
| POST | /api/v1/taxpayers/{id}/fiscal-memories | taxpayer owner/delegate with permission | verify public-key correspondence and memory connection |
| POST | /api/v1/fiscal-invoices/{id}/submit | owner/delegate with assignment | validated immutable invoice snapshot + outbox |
| GET | /api/v1/fiscal-invoices/{id}/status | authorized owner/delegate | transport status separate from final fiscal status |
| GET | /api/v1/root/audit-logs | Root only | secrets redacted, immutable audit |

## Security requirements

1. All resource access authorized in the backend; enforce workspace+taxpayer scoping via policy handler and repository/EF query guard; negative cross-tenant integration tests mandatory.
2. Root MFA, short-lived auth sessions/secure cookies (CSRF protections for cookie endpoints), refresh token rotation and revoke, login throttling, password hashing with migration/reset policy.
3. Key generation: strong OS CSPRNG, RSA 2048+ or currently permitted profile, PKCS#8 private key, SPKI public PEM, PKCS#10 CSR. Subject data differs for natural/legal persons and must be verified against official CA profile. The provided docs establish requirements for RSA 2048 and PKCS#8; CSR subject mapping needs separate verification before production.
4. Never log plaintext private keys, PEM, signed JWT tokens, payment provider secrets, gateway credentials or PII. Secret storage: encryption envelope, KMS/Vault, narrowly scoped signer service, rotation and revocation, audit of every use; no raw private-key response to Root/Level 2 after custody.
5. Owner-controlled browser generation is an optional mode; never claim private key is recoverable if not securely stored. Validate user-supplied public/private pair and certificate chain.
6. Rate-limit CSR generation, sending and inquiry. Signed callbacks with replay defense, nonce/time validation, allowlist IP if provider specifies, and unique payment reference per workspace/provider.
7. Use `Idempotency-Key` header for order, collection, allocation, fiscal submit and settlement mutation. Persist request fingerprint, unique actor+workspace+operation scope+key, response and TTL policy; reject reuse with different payload.
8. Outbox worker signs with per-memory scoped signer. Concurrent taxpayers must not share a mutable SDK singleton or cryptographic context.
9. Audit events are append-only, include actor/action/scope/id/time/correlation and redact personal data. Delegate sees only allowed business events; Root sees security/system logs.
10. Use parameterized queries, input schema validation, file scanning and file-type limits on proofs; downloadable CSR/cert/public key permitted, private-key export only under explicitly approved one-time secure flow.
11. Financial ledger protected by immutable posting, unique external/payment identities, double-entry balancing, per-settlement availability checks and concurrency-safe allocation.
12. Payment collection vs entitlements vs profit-sharing vs partner settlement are separate state machines; never grant service solely on unverified redirect parameters or manual status flag.

## Idempotency failure examples

- Duplicate gateway callback => one verified receipt, one payment allocation, one entitlement event, one profit realization.
- Tax submission timed out => inquiry/reconciliation before resend to prevent duplicate tax invoice.
- Root settlement confirmation retried => same confirmed settlement; no second ledger posting.
- Expense approval concurrent with profit finalization => expected revision check and recomputation from immutable approved snapshot.

## Minimum evidence before implementation signoff

OpenAPI spec and negative security fixtures, threat model (STRIDE), cross-tenant integration tests, crypto library/SDK compatibility tests, key retention policy and privacy assessment, accounting/legal approval for expense and tax treatment.
