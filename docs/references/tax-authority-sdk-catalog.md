# ArginTaxPayer — Tax Authority and .NET SDK Evidence Catalogue
**Indexed 2026-10-17** from user-uploaded PDF/ZIP bytes. This catalogue preserves reference metadata, not the original binaries. The official taxpayer documents take precedence over SDK implementation samples and third-party websites.

## Original evidence — SHA-256 for each uploaded outer file

| ID | File | Contents | SHA-256 |
|---|---|---|---|
| TAX-ISSUE-79 | RC_IITP_IS_V7_9_2(1).pdf | RC_IITP.IS V7.9; Tir 1405; official invoice rules, pattern matrix and field validation | fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be |
| TAX-CONNECT-16 | ازدیبهشت ماه 1405(1).pdf | **RC_TICS.IS_v1.6, Shahrivar 1404 per cover** (filename is misleading); 48 pages. v2 REST, nonce, certificate authentication, JWS/JWE, dispatch and inquiry | a4995c5a1f8766338a3816d28fd87bd3488dce6f91514557d304fe17d7eb595e |
| TAX-SDK-GUIDE-OLD | راهنمای اتصال به زیرسامانه جمع آوری SDK بدون گواهی-دات نت_8(1).pdf | Farvardin 1404, 22 pages. Legacy `TaxApiService` SDK guide without certificate; transport and content layers | ef3439dcb1a8aa7b5dc1ca46dadf8ee333da01a38fe7910d483342135f1c134a |
| TAX-SDK-GUIDE-NEW | راهنمای کاربری SDK اتصال به زیرسامانه_ی جمع_آوری .NET_8(1).pdf | Farvardin 1405, 30 pages. Certificate SDK guide, `TaxApiFactory`, `ITaxApi`, `ILowLevelTaxApi`; guide explicitly names NuGet **2.0.31** | 5c4145ae701832ea5017ff68cc1a8a9ef0cffe2761a3b5ca94ceceef1823accf |
| SDK-SOURCE-OLD | tax-collect-data-sdk-dotnet-tax-collect-data-sdk-0.0.30_1.zip | 0.0.30 source ZIP, 142 entries; netstandard2.0, `TaxApiService`, `ITaxApis`, `ITransferApi` | 91f7b10914778b0ce13406bf09adce037c669ffd954b6db243b3f0f08528387a |
| SDK-SOURCE-NEW | tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32_1.zip | 2.0.32 source ZIP, 251 entries; netstandard2.0, `TaxApiFactory`, `ITaxApi`, `ITaxPublicApi`, `ILowLevelTaxApi`, `TaxIdProvider` | 66f2936c67c61e1c9c40660e7f9e0bd7666576b8b741fc2d5232a7a1f0d4d2f0 |
| SDK-NUGET-OLD | TaxCollectData.Library.0.0.34.zip | Contains `TaxCollectData.Library.0.0.34.nupkg`; netstandard2.0 binary and nuspec | 8c08bc486ac7081cc1063058860c2accb3cf96ad7a50252ca1e72e230b868690 |
| SDK-NUGET-NEW | TaxCollectData.Library.2.0.32.zip | Contains `TaxCollectData.Library.2.0.32.nupkg`; netstandard2.0 binary and nuspec | abc9a5ea63b7d0ac7d4fe4548d3d92f981f8ab586b28fe1f4ba500bfc8d1997d |

## SDK generations and incompatibility
- **0.0.x legacy family**: `TaxApiService.Instance.Init`, `ITaxApis` and `ITransferApi`; old guide uses `https://tp.tax.gov.ir/req/api/`. 0.0.30 *source* and 0.0.34 *binary* are not proven byte-for-byte matching.
- **2.0.x newer family**: `TaxApiFactory`, `TaxProperties`, `Pkcs8SignatoryFactory`, `EncryptorFactory`, `ITaxApi`, `ITaxPublicApi`, `ILowLevelTaxApi`, `ISignatory`, `IEncryptor`; official connection guide uses `https://tp.tax.gov.ir/requestsmanager` v2. 2.0.32 source/NuGet version match; compiling and auditing binary parity remain open.
- Legacy NuGet depends on Newtonsoft.Json 13.0.1, RestSharp 108.0.1 and Pkcs11Interop 5.1.2. Newer NuGet uses Newtonsoft.Json 13.0.2, RestSharp 108.0.1, JWT 10.0.2, jose-jwt 4.1.0, Pkcs11Interop.X509Store 0.4.0. These are declared dependencies, not a security assessment.
- **Compatibility gap**: certificate guide cites NuGet 2.0.31, provided package is 2.0.32. Validate API mapping using contract tests before integration. Version 0.0.34 is not a successor to 2.0.32.

## Page/topic routing
| Work | Primary references | Precise guide pointers |
|---|---|---|
| Phase 07 invoice data/rules/subjects | TAX-ISSUE-79 (official) | V7.9 table 9 p31 patterns and table 10 p32 subjects; SDK is not regulatory authority |
| Phase 07 DTO field adapter | TAX-ISSUE-79 + TAX-SDK-GUIDE-NEW | New guide pp8–13: `InvoiceDto` header/body/payment including `inty`, `inp`, `ins`, `irtaxid`, `sstid`, `am`, `fee`, `vra`, `vam`, `insr`, `nti1`, `nti2` |
| Phase 09 security/signing | TAX-CONNECT-16 + TAX-SDK-GUIDE-NEW | Connection document p5 and pp9–17: nonce, JWS, certificate; new SDK guide pp4–7 and pp16–17: PKCS8/cert, `ISignatory`, `IEncryptor` |
| Phase 10 transmit | TAX-CONNECT-16 + SDK-SOURCE-NEW | Connection document pp6–8 and p18 onward; guide pp14–16 signed/encrypted packet |
| Phase 11 inquiry | TAX-CONNECT-16 + TAX-SDK-GUIDE-NEW | Guide pp17–22; `Uid` / `ReferenceNumber`; `IN_PROGRESS`, `FAILED`, `SUCCESS`, `TIMEOUT`, `NOT_FOUND` |
| Historical comparison | TAX-SDK-GUIDE-OLD + SDK-SOURCE-OLD | Legacy `TaxApiService` API, **not** default new implementation |

## Security and custody
**Both SDK source ZIP files contain sample PEM private-key blocks**: `TaxCollectData.Sample/private.pem` in 0.0.30 and `TaxCollectData.Sample/privatekey1.pem` in 2.0.32. Assume sensitive even when labeled sample. Never commit or reveal them, add them to logs, or use them in production. The raw ZIPs and NuGet binaries were deliberately **not** committed to GitHub. This catalogue stores file metadata and checksums; it **does not guarantee binary retrieval in future sessions**. Store originals in a controlled *private* artifact repository or persistent project file storage for long-term access. Never treat the historical PDF publication date or old SDK example as proof of current operational validity.

## Architectural decisions / status
1. Keep Phase07 domain and rule engine independent of SDK DTOs; official TAX-ISSUE-79 is first-party primary rule source.
2. For a future adapter, pin a single 2.0.x implementation only after verifying published API contract and end-to-end sandbox tests; don't mix legacy 0.0.x code paths.
3. Isolate secrets and certificates behind protected signer abstractions; store no private keys in source, plaintext databases or logs.
4. Current action is **analysis/documentation only**. No SDK adoption, live endpoint call, signing, build, security audit, migration or production integration was performed.


## Mandatory source-governance policy (owner directive, 2026-10-17)

**MUST retain and consult BOTH generations.** Keep the two historical 0.0.x source/package artifacts and corresponding guide as a versioned *legacy reference*, and both 2.0.32 source/package artifacts and the 1405 certificate guide as the *newer reference*. An architecture decision to use 2.0.x for a future new adapter does not authorize deleting or rewriting legacy evidence or breaking an older deployment. Versions must never be silently cross-wired.

**MANDATORY for every future phase, design, PR and code change related to taxpayer rules, data or connection:**
1. Read the most relevant supplied **Tax Authority primary** instruction first (identifier + edition + exact chapter/table/page). For invoicing this includes `RC_IITP.IS V7.9`; for the certificate-based protocol `RC_TICS.IS_v1.6`. Do not use blog/third-party sites as the rule source.
2. For SDK or wire-facing work, consult **both SDK generations** and their corresponding manuals to identify migration or compatibility differences. Choose one explicitly pinned implementation and document the reason; maintain a separate legacy-compatibility assessment.
3. Record per implementation step: source ID, exact page/table, relevant SDK generation and package version, any contradiction, security effect, and tests. If original evidence is unavailable, BLOCK normative implementation and request original bytes instead of guessing or inventing support.
4. For conflicting rules, the newer applicable *official* document prevails only after confirming legal scope and effective date. Do not equate publication date with effective date. Log unresolved conflict; fail closed for submission-readiness.
5. A supplied NuGet package or sample does not supersede the official instruction; no automatic live endpoint assumptions. Verify sandbox handshake, contract compatibility, signatures and asynchronous status before production release.
6. Files with PEM keys, credentials or production customer details may only live in approved private artifact storage. Preserve SHA-256 and restricted access. Never push raw uploads to public Git. This catalogue is persistent, but **not** binary archival; original copies must be preserved privately for future source builds.

### Direct taxpayer enrollment and signing: official-document interpretation

- `RC_TICS.IS_v1.6`, **p5**, says the taxpayer obtains fiscal-memory ID in the tax portal and selects among `by taxpayer`, `by TSP/government with taxpayer key`, or `by TSP/government with TSP key`. In the certificate-based path, a **2048-bit public key or signing certificate** can be uploaded to the tax workspace. It explicitly distinguishes TSP-managed credentials.
- `RC_TICS.IS_v1.6`, **pp9–12**, defines v2 nonce/JWS authentication, signature certificate `x5c`, `RS256`, `sigT` and `clientId`. The server performs certificate/trust/expiry validation.
- `TAX-SDK-GUIDE-NEW`, **pp4,6–8 and 16**, requires a valid signer certificate `CRT/CER` and matching private `PKCS#8 PEM` for the file-based option. It also discusses hardware PKCS#11 with currently documented ePass3003; hardware token is **optional**, not universally required. No trusted TSP is intrinsically required when registering self-issuance.
- `TAX-SDK-GUIDE-OLD`, **pp4–9**, describes the older file/private-key `TaxApiService` path; it cannot be construed as a replacement for the certificate in new v2 authentication. The 1404 technical instruction says legacy API remained available *until further notice at its publication*, NOT guaranteed today.

**Decision:** ArginTaxPayer should expose an explicit signing mode abstraction (legacy direct key mode vs new certificate-based direct mode; potential delegated TSP modes separately). Do not require a USB token for certificate-based file signing; do require credential validity, matching key pair and suitable trust chain. Registration, signing key handling, API integration and live authorization are deferred to the correct later phase; this paragraph does not mean they are implemented.

## Binding product decision: maximal in-app enrollment (2026-10-17)

Per owner instruction, certificate/fiscal-memory onboarding should happen inside ArginTaxPayer as far as legitimately possible. The architecture and boundaries are fixed in [ADR 0007](../adr/0007-certificate-enrollment-onboarding.md): key/CSR generation, importing and validating certificates, secure custody choice, guided handoff to authorized CA and taxpayer portal, and SDK-authenticated connection checking. **A certificate must be issued by an authorized CA; do not presume an official CA enrollment API exists.** Maintain both SDK generations and official-document citation obligations unchanged.
