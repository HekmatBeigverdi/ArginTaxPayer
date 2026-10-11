# ADR-0002 — Delegate-scoped pricing and taxpayer key/CSR onboarding

- Status: Proposed, Phase 01
- Date: 2026-10-10
- Related: ADR-0001

## Business decisions
1. Every Level 2 delegate has an independent catalog of annual and quantity-pack selling prices, visible only within their explicitly assigned customer/taxpayer scope. Company/person-specific prices can override the delegate's standard price.
2. Level 1 and Level 2 are profit-sharing partners; **there is no wholesale/base-price settlement**. Level 2 independently sets selling prices and submits verifiable tax/eligible expense breakdowns. A per-delegate, versioned profit-sharing agreement specifies each party's percentage (normally 50%/50%). Level 1 approves eligible cost policy, audits deductions and reports, and controls assignments. Delegate A cannot see Delegate B prices, costs, payment accounts or ledgers.
3. Store an immutable pricing snapshot per order line: product/version, currency, gross price, discount, net charged, tax treatment, delegate workspace, customer, and effective settlement agreement. Subsequent catalog edits cannot change historical orders.
4. Resolve an order price by the authorized customer's active override, then the delegate catalog. Never infer an authorized workspace from client-submitted IDs; validate current grants server-side.
5. Keep gross customer receipts, tax collected/remitted, eligible operating expenses, net distributable profit, Level 1 share, Level 2 share, receivables/payables and actual cash settlement separate. Compute profit share on the agreed tax-exclusive revenue less verified deductible costs, avoiding double tax deductions. Post immutable, idempotent ledgers and reconciliation; disputed or unapproved costs remain pending rather than silently reducing shares.

## Recommended domain model
- DelegatePriceList(workspaceId, version, effectiveFrom, effectiveTo, status)
- DelegatePrice(priceListId, productId, price, currency)
- CustomerPriceOverride(workspaceId, taxpayerId, productId, price, effective dates)
- OrderPriceSnapshot(orderId, workspaceId, taxpayerId, catalogVersion, settlementAgreementVersion, unitPrice, discounts, totals)
- ProfitSharingAgreement(workspaceId, effective dates, level1Percentage, level2Percentage, allowedExpensePolicy, approvalRules); SaleCostLine(orderId, costType, amount, receiptReference, approvalStatus); ProfitDistributionSnapshot(orderId, agreementVersion, netRevenue, approvedCosts, distributableProfit, level1Share, level2Share); SettlementLine and immutable journal postings. Preserve revisions and refunds as adjustment entries.
- Constraints and policies reject cross-workspace reads and mutations including exports, search, jobs and price lookup APIs.

## Taxpayer key and CSR onboarding
Provide a guided setup for natural persons and legal entities:
1. Capture identity attributes applicable to the requested certificate/CSR and validate them according to the issuing authority's current requirements.
2. Generate a cryptographically secure RSA key pair of the strength specified by the current tax authority documentation (currently 2048-bit public-key onboarding in the supplied connection guide); export public PEM, private PKCS#8 PEM, and PKCS#10 CSR where required. Do not use predictable random seeds.
3. Check correspondence between CSR, public key and private key, show fingerprints/metadata, and support separate certificate import/verification when certificate-backed authentication is used.
4. Guide users to register the required public key or issued certificate in their own official taxpayer portal and enter the resulting fiscal memory ID in ArginTaxPayer. CSR creation does not itself issue a government certificate or automatically allocate a fiscal memory.
5. Check that the fiscal memory, sender identity, selected authentication mode, certificate and key correspond before activating submission.
6. Support credential versioning, rotation, expiry notifications, revocation, connection tests, error diagnosis and safe replacement without rewriting previous tax IDs.

## Custody and security
- Offer client-side/private local generation where feasible. Private keys must not be sent to analytics, logs, browser storage or unrelated administrators.
- If automated server-side signing is enabled, require explicit owner opt-in and encrypt keys with envelope encryption backed by a dedicated managed key service. Decryption is performed only inside the authorized signing service with access auditing and scoped policies.
- A one-time owner-controlled private-key export may be offered only at initial client-side creation, with strong warnings and encryption; no later plaintext retrieval by Level 1 or Level 2 operators.
- The CSR contains public subject data and a public key; it is not a certificate. A recognized CA-signed certificate is required for workflows that use certificate-backed authentication.
- Document supported algorithms/formats against current authority specifications, test sample vectors and avoid reimplementing cryptographic primitives.
- Do not commit production private keys, CSRs containing personal identity data, certificates, SQL backups or secrets to this public repository.

## Acceptance tests
- Delegate A can edit its own catalog; B cannot discover A's prices by listing, direct URL, identifier probing, API, exports or caches.
- Specific customer override applies only to the authorized customer; profit sharing uses the versioned agreement and verified expense/tax basis, never an invented platform baseline.
- A historical purchase and settlement remain unchanged after repricing.
- Correct public key can verify the CSR signature and matches the generated private key; a mismatched key is rejected.
- Both individual and company onboarding paths generate correctly encoded artifacts; certificate path validates issuer, expiry and key matching.
- The user can configure a fiscal memory ID only with verified permissions and a matching key/certificate; failed handshake never marks connection active.
- Signing service never leaks plaintext key material to Level 2, logs, diagnostics or frontend APIs.
