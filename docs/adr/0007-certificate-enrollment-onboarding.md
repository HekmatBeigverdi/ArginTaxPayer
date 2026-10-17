# ADR 0007 — Guided taxpayer certificate enrollment and signing onboarding

**Date:** 2026-10-17  
**Status:** Accepted as product requirement; implementation deferred to signing/connectivity phases.  
**Context:** ArginTaxPayer uses two separately retained SDK generations: 0.0.x legacy direct-key path and 2.0.x certificate-based path. Users want as much certificate enrollment and self-service connectivity as possible inside the app.

## Decision

Implement a **guided, maximally in-app** taxpayer onboarding experience without falsely claiming to be a certification authority or to have undocumented CA/tax-portal APIs. The new certificate-based self-issuance path is the preferred candidate for new enrollments, subject to source-version and production verification. Keep the legacy SDK and key-based route available as a distinct compatibility concern. The product must not confuse a USB PKCS#11 token (optional signing device) with a short-lived signed authentication token generated in software.

## Required experience and functional boundaries

1. Customer-scoped wizard, RTL Persian, progress persisted: `select direct issuance / delegated TSP mode` → `generate or import matching key pair` → `create CSR and public-key export` → `submit CSR / identity verification at approved certification authority` → `import issued X.509 certificate` → `verify trust, validity and public/private-key match` → `link fiscal memory in tax workspace` → `configure SDK and perform read-only connection check`.
2. Implement client-controlled local key generation or a designated high-assurance signer where practical; a server-custodied signer is an **explicit informed opt-in** with HSM/KMS or equivalent protected key storage and per-tenant access control. Never silently upload a user's private key to the web server. No private key in logs, telemetry, source, localStorage, or plaintext database. Secure export requires authentication and strong encryption.
3. Create standards-compliant PKCS#8 PEM private key and CSR/public key, with identity fields and validation drawn from the chosen CA's **verified** enrollment profile. RSA-2048 support is explicitly documented in TAX-CONNECT-16 p5; update policy only on a newer authoritative instruction. CSR is a *request*, not a signed certificate.
4. Third-party CA registration, issuance, fees, and identity-proofing must remain at the authorized CA until an official integration interface, permission, terms and identity-proofing workflow are verified. Provide official destination and human-completable instructions; **do not scrape websites, automate CAPTCHAs, impersonate applicants or claim automatic issuance**.
5. The fiscal-memory identity comes from the taxpayer workspace after selecting the documented method. Do not assume its issuance can be automated via an undocumented API. Allow guided handoff and validated entry/import of issued fiscal-memory ID and public key or certificate.
6. Validate uploaded X.509: private-public match, correct subject/organization identity as relevant, certificate chain, expiration, and if required by applicable trust policy, revocation status. Surface errors in Persian without leaking secret material.
7. After validated onboarding the SDK adapter manages signing, nonce/JWS/JWT authentication and asynchronous invoice send/inquiry. A nonce/JWT is generated for requests by software; **neither is the long-term X.509 certificate**. Distinguish local connection check from tax-authority acceptance of an invoice.
8. Preserve support for source and package artifacts of **both** legacy 0.0.x and new 2.0.x in private, versioned artifact custody. Pin exact tested adapter version and record any differences between guide 2.0.31 and supplied 2.0.32.
9. All regulatory and SDK implementation must cite primary document ID, edition, relevant page, and package source evidence in commit/step completion. No third-party blog is a normative source. Any CA-facing API integration needs separate verified CA API documentation and legal authorization.
10. Phase ownership: Phase07 defines only contract implications; Phase09 implements secure key handling, certificate validation and signing interfaces; Phase10 implements live connectivity and guided handoff; user-facing full wizard scope must be reconciled with Phase08 UI scope without changing frozen phase steps.

## Primary evidence

- `RC_TICS.IS_v1.6` (Shahrivar 1404), **p5**: taxpayer portal memory registration and selection among self/TSP; taxpayer public key / certificate upload; 2048-bit key. **pp9–12**: short-lived nonce, JWT/JWS per request. 
- `راهنمای کاربری SDK اتصال به زیرسامانه‌ی جمع‌آوری .NET` (Farvardin 1405), **p4 and p6**: supported PKCS#8 private key with X.509 `crt/cer` signed certificate; file-based signer supported, hardware token optional. **pp6–7**: `Pkcs8SignatoryFactory`, `TaxApiFactory`, API URL; **p16**: signer/encryptor contracts.
- `راهنمای اتصال به زیرسامانه جمع‌آوری SDK بدون گواهی-دات نت` (Farvardin 1404), **pp4–9**: older `TaxApiService` path for compatibility comparisons.
- Full source/file names, SHA-256 and version mapping: [tax-authority-sdk-catalog.md](../references/tax-authority-sdk-catalog.md).

## Acceptance gates for future implementation

- Unit tests for CSR/key export, certificate chain, mismatch, expiry and invalid tenant context.
- Security review and secret-redaction tests for API/logging and key custody.
- Successful official CA process verification (and official API permission if automated issuance ever becomes feasible).
- Separate sandbox contract tests for legacy and new SDKs; documented fallback for a customer with a legacy fiscal memory.
- Read-only live connection check and explicit operator approval before first send; no client-owned key disclosure.
