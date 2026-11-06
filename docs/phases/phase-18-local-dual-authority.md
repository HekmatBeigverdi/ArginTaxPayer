# Local memory-selected tax authority test

This change prepares the local Development UI/API for an owner-driven authority test. It does not deploy Ubuntu, change the public site, automatically submit a tax invoice or close Phase18.

## Archived evidence

The original files are in `../Docs` beside this checkout. The certificate-free .NET SDK guide, **Farvardin1404**, has22 pages: pages4 and6 use `https://tp.tax.gov.ir/req/api/`; pages17–20 cover server information, invoice sending and reference-number inquiry; page21 covers `GetFiscalInformation(CLIENT_ID)`. The provided0.0.30 source uses `ClientType.SELF_TSP`, signed/encrypted asynchronous `normal-enqueue`, and `GET_TOKEN`, `GET_FISCAL_INFORMATION`, `INQUIRY_BY_REFERENCE_NUMBER`. Its `Enums/FiscalStatus.cs` defines `ACTIVE=0` and `INACTIVE=1`.

These references establish the archived legacy contract used by the application. The separate2.0.x certificate SDK is not substituted. Actual current acceptance by either remote service must be observed in the owner's test.

## Destination and credential selection

| Stored memory environment | Pinned legacy base |
|---|---|
| Production (`FiscalConnectionEnvironment=1`) | `https://tp.tax.gov.ir/req/api/` |
| Sandbox (`FiscalConnectionEnvironment=2`) | `https://sandboxrc.tax.gov.ir/req/api/` |

The saved owned memory chooses the environment. Its identifier's text does not encode or override that choice. Tokens, server encryption keys, enqueue and inquiry use that same authority. No cross-host fallback occurs. Existing separate `sandbox_fiscal_memories` records retain the original Sandbox-only workflow.

Archive reinspection on 2026-11-05 confirmed the original certificate-free guide pages4 and6 (base URL), pages17–20 (send and receipt/inquiry), and page21 (fiscal information). The original0.0.30 ZIP's `Dto/Properties/NormalProperties.cs` maps SELF_TSP invoice submission to `self-tsp/async/normal-enqueue`. Thus a Production memory sends the signed, encrypted invoice to **`https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue`**. The certificate guide page6 instead configures `TaxApiFactory` with a certificate and `/requestsmanager`; that distinct2.0.x protocol is not substituted into this adapter.

## Repeatable Mac regression check

```sh
zsh scripts/phase18-local-dual-authority-check.zsh
```

This loads private local configuration without printing secrets, requires the loopback test database, runs the full .NET suite with the classified producer/serial/onboarding database tests enabled, and runs web tests, typecheck and build. Database fixtures use disposable schemas and fake authority responses; no real invoice is submitted. The onboarding fixture includes public-key material required by the actual activation query and covers rejection before authority I/O when it is missing.

## Local preparation

Keep the API in `Development` at `127.0.0.1:15089` and the web UI at `localhost:3000`. The private `.env.dev.local` must retain its current database, encryption master key, MFA key and OTP pepper sources. Add:

```sh
ArginTaxPayer__EnableOperationalEndpoints=true
ProductionInvoiceLegacy__Enabled=true
ProductionInvoiceLegacy__OperatorApprovedEndpoint=true
ProductionInvoiceLegacy__ApiBaseUrl=https://tp.tax.gov.ir/req/api/
```

Keep existing Sandbox opt-ins for Sandbox testing. Restart the local API after private configuration changes; .NET does not load shell `.env` files itself. These settings authorize local owner-initiated calls, not an automatic background sender.

The local test database needs the existing fiscal-memory and dispatch environment migrations, followed by `20261105010000_AddAuthorityInvoiceFiscalReservations` and `20261105020000_AddFiscalMemoryAuthorityEvents`. The new migrations add authority-specific serial evidence, preserve dispatch memory foreign-key integrity for both memory sources, and add append-only onboarding events. Existing memory environments and old operation evidence are not automatically relabeled. No automatic application rollback removes fiscal evidence.

If the same memory has issued invoices through another application, configure its last-used serial before the first local send using `ProductionInvoiceLegacy__LastUsedSerial__<memory UUID as32 hexadecimal characters>`. A new memory starts at1; subsequent reservations advance beyond local reservations and the configured lower bound. Do not assume the application can discover all invoices issued elsewhere.

## Owner-driven test

1. Sign in as a Level1 administrator with MFA. Open the taxpayer's connection panel.
2. For a legacy unclassified memory, explicitly select its real authority once. Classification is refused when dispatch history already exists; reconcile that history instead of relabeling it.
3. Import/select the matching key or complete the existing key-generation workflow and register its public key in the corresponding tax portal. Use **Test connection** to inspect the returned fiscal information.
4. Use the separate **verify and activate** action for a pending memory. It checks the authenticated authority reports ACTIVE, taxpayer national identity matches, key and memory revision remain unchanged, membership is current and subscription is active. A read-only test alone never changes status.
5. Select this classified active memory in a new invoice, review its actual seller/buyer/items/date/amounts and initiate sending explicitly. The new classified producer currently supports Original Type1/Pattern1; legal successors and other patterns remain guarded.
6. Record the returned reference number and invoke inquiry explicitly. An enqueue acknowledgement is not final fiscal acceptance. Indeterminate or previously-started operations are not resent automatically.

Phase18 closure still requires the owner's successful real/Sandbox acceptance, review of remaining legal-successor and production deployment evidence, and a separate deployment decision.
