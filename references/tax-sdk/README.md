# Tax Authority SDK Reference Archive

**Project:** ArginTaxPayer  
**Location:** `references/tax-sdk/`  
**Status:** Reference material only — not an approved production dependency  
**Reviewed:** 2026-10-23  
**Primary integration phase:** Phase 10 (invoice submission); complete asynchronous inquiry is reserved for Phase 11.

## Purpose and scope

This directory preserves two locally supplied generations of the Iranian Tax Authority tax collection .NET SDK for protocol comparison, deterministic fixtures and security review. It is **not** the application's implementation, a verified official live endpoint specification, or proof that a package is currently accepted by the Tax Authority.

The application remains .NET 9 Web API + Next.js + PostgreSQL. Reuse the existing Phase 07–08 invoice domain and Phase 09 protected key custody; do not create a second invoice engine or expose keys to API/UI code.

## Repository inventory

| Path | Contents | Intended use |
| --- | --- | --- |
| `legacy-0.0.34/TaxCollectData.Library.0.0.34.nupkg` | Supplied binary NuGet package | Compare package metadata, binary compatibility and legacy behavior in an isolated environment |
| `legacy-0.0.34/TaxCollectData.Library.0.0.34.zip` | Supplied legacy archive | Preserve source evidence; inspect its internal file inventory before claiming source availability or parity |
| `modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32/` | Extracted C# project, sample and tests | Inspect authentication, JWS signing, serialization, invoice APIs, results and inquiry contracts |
| `modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32_1.zip` | Supplied archive | Retain original input for provenance comparison |

The modern source project declares `TargetFramework=netstandard2.0` and `Version=2.0.32` in `TaxCollectData.Library/TaxCollectData.Library.csproj`. Its own nested README contains an installation example using **2.0.22**. That README is third-party example text, not proof of the installed version, present package availability, production-ready code or correct VAT rules.

The legacy source for version **0.0.30** and NuGet binary **0.0.34** were already recorded separately in Phase 09; **do not assume those versions are behaviorally identical**.

## Source priority and authoritative validation

1. **Primary SDK evidence:** repository-local files under `references/tax-sdk/` (extracted source, `.zip`, and `.nupkg`). Inspect and hash these before considering any external copy.
2. **Normative authority:** applicable official Tax Authority documents determine whether an implementation and transmission format are valid; discrepancies must be resolved before live submission.
3. Do not silently replace repository-local artifacts with an externally downloaded NuGet package. Any new external dependency needs explicit review and approval.

## Relevant modern-source entry points

Paths below are relative to `modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32/TaxCollectData.Library/`.

| File or folder | Review focus |
| --- | --- |
| `Clients/TaxApi.cs` | `SendInvoicesAsync`, receipt/UID/reference mapping, inquiry surface |
| `Clients/LowLevelTaxApi.cs` | Request routing and responses |
| `Clients/RestSharpHttpClient.cs` | Nonce request, signed authorization, HTTP/error handling, TLS review |
| `Cryptography/JwsSignatory.cs` | JWS signing, certificate header `x5c`, timestamp header `sigT` |
| `Cryptography/JweEncryptor.cs` | Encryption and packet handling |
| `Providers/PacketProvider.cs`, `Providers/RequestProvider.cs` | Packet construction and serialization |
| `Dto/`, `Models/` and `src/Application/DTOs/` | Invoice payload, inquiry, and response field definitions |
| `Factories/`, `Repositories/` | Per-taxpayer configuration and dependency lifetimes |
| `TaxCollectData.Library.csproj` | Package versions and target framework |

The SDK source exposes invoice submission and several inquiry methods. **Presence of a method does not establish a valid current tax-authority contract.** In the phase plan, Phase 10 covers submission and only tightly scoped reconciliation needed to avoid duplicate dispatch; comprehensive asynchronous status synchronization remains Phase 11.

## Phase 09 evidence and compatibility boundary

Phase 09 documented supplied SDK artifacts and SHA-256 evidence, including:

- Legacy SDK package **0.0.34**: `91f7b10914778b0ce13406bf09adce037c669ffd954b6db243b3f96ad7a50252ca1e72e230b868690`
- Modern SDK source archive **2.0.32**: `66f2936c67c61e1c9c40660e7f9e0bd7666576b8b741fc2d5232a7a1f0d4d2f0`
- Modern SDK package **2.0.32**: `abc9a5ea63b7d0ac7d4fe4548d3d92f981f8ab586b28fe1f4ba500bfc8d1997d`

These are **previously recorded evidence hashes**, not newly verified hashes of the files currently present in this checkout; exact path-to-hash correspondence must be reconfirmed before claiming a match.

Phase 09's selected **legacy-key-source-0.0.30-rsa-sha256-pkcs1-base64** profile is an internally named, offline evidence profile and must not be misrepresented as an official current API mode. The modern source contains certificate-backed JWS functionality. Never silently substitute certificate/JWS signing for the user's legacy registered-public-key path. If current official rules preclude a path, block that capability and request an architectural decision.

## Security and quality review gates

Before reusing any component, verify all of the following:

1. **Official authority evidence:** record document identifier, edition/date, relevant page, exact endpoint and algorithm/encoding. Resolve conflicts before implementation.
2. **License/provenance:** confirm redistribution and modification rights from the original publisher. A badge or README claim of MIT does not substitute for an actual license grant; no root license file was evident in the inspected extracted tree.
3. **Third-party execution:** do not execute sample binaries or load hardware vendor libraries by default. The extracted sample contains `TaxCollectData.Sample/ShuttleCsp11_3003.dll`; treat it as untrusted until provenance and necessity are established.
4. **Secrets:** never commit or log real PEM private keys, certificate private-key bundles, PINs, passwords, DEKs/KEKs, access tokens, nonce signatures or taxpayer data. Never copy SDK demo credentials into real configuration.
5. **TLS and URLs:** never disable server-certificate validation or accept user-controlled outbound endpoints; pin authority-approved targets and enforce trusted TLS.
6. **Isolation:** no process-global mutable signer/authentication state shared among taxpayers; scope all credentials, tokens, memory IDs, signing context and dispatch operations by the authenticated owner.
7. **Data integrity:** immutable invoice snapshot, deterministic payload bytes, source-pinned signing, durable operation identity, append-only audit and explicit deduplication/reconciliation.
8. **Failures:** handle non-2xx, malformed/error bodies, timeouts and unknown responses without leaking raw sensitive content. A timeout must not trigger blind duplicate submission.
9. **Package audit:** check transitive dependencies, versions, security advisories and cross-platform behavior on macOS and Ubuntu at the **same commit SHA**.
10. **No implicit changes:** no automatic migrations, outbound production requests, operational deployment, merge, tag or release without owner authorization.

### Observations requiring investigation

- `Clients/RestSharpHttpClient.cs` constructs a signed nonce-based Authorization header and currently builds `UnknownResponseException` from the raw response body; inspect error propagation and redact sensitive material.
- `Clients/TaxApi.cs` maps invoice submission responses using trace/UID correspondence; validate partial, reordered, missing and duplicate result handling rather than trusting the sample mapping.
- `Cryptography/JwsSignatory.cs` includes certificate-based `x5c` in a JWS header. It is not equivalent to the certificate-free legacy RSA signature output.
- Sample documentation uses a concrete VAT rate and file-based private-key paths; do **not** copy either as a universal current tax rule or an ArginTaxPayer custody design.

## Safe local inspection

From the repository root:

```bash
find references/tax-sdk -type f | sort
shasum -a 256 references/tax-sdk/legacy-0.0.34/*.nupkg
shasum -a 256 references/tax-sdk/legacy-0.0.34/*.zip
shasum -a 256 references/tax-sdk/modern-2.0.32/*.zip
unzip -l references/tax-sdk/legacy-0.0.34/TaxCollectData.Library.0.0.34.zip
unzip -l references/tax-sdk/legacy-0.0.34/TaxCollectData.Library.0.0.34.nupkg
```

The commands above only list/read files or compute hashes. Extract and build third-party code in a controlled environment after source/license review. Do not run sample code against real Tax Authority endpoints.

## Phase 10 usage policy

- **Step 01:** inspect repository structure, official documents, SDK references and Phase 09 deferred operational evidence.
- **Step 02:** build a versioned protocol matrix from official documentation and these source references; pin golden test vectors and supported auth/signature profiles.
- **Later approved steps only:** implement isolated ArginTaxPayer-owned adapters; never import the SDK project wholesale or introduce a runtime PackageReference solely from this archive.
- Track source paths, artifact SHA-256, official rule citations, exact tests and deviations in `docs/phases/phase-10.md`.
- Preserve the fixed approved step count and proceed one step at a time after owner approval.

**This directory is a reference archive, not an authorization to send live invoices.**
