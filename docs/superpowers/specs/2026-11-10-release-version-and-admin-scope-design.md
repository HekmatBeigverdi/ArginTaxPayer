# Release version and administrator scope design

## Intent

Give each application release a human-readable unique version and show when a newer release is available. Preserve administrator workflows so Level 1 and Level 2 work only within their server-authorized customer scope; administrators do not buy or consume a personal customer plan.

## Current behavior

- Release packaging records the full Git SHA in `build-info.txt`, while the web package version stays at `0.1.0`; the console labels the footer as a generic development version.
- Customer plan routes are already restricted to the `Customer` role.
- Level 2 account, party, catalog, invoice and taxpayer paths are already scoped to current assigned customers. Level 1 has broader root scope and may filter/manage Level 2 ownership. The customer selector in admin workflows writes records to the selected customer.
- The database authorization checks remain authoritative; a browser-selected customer ID alone grants no access.

## Goals and non-goals

- Mint a unique release identifier from the product version, CI run number, and short source SHA; retain the full SHA for traceability.
- Include release metadata in every official build path and expose it in the console.
- Let an already-open console detect a newer deployed release and offer a refresh.
- Keep Level 2 limited to its assigned customer accounts and Level 1 limited to its existing root scope; retain customer-targeted create/list behavior.
- Keep personal plans and customer checkout unavailable to administrator roles.
- Do not grant Level 2 global access to every account or change ownership, plan entitlements, or financial history.

## Approaches considered

1. Display the Git SHA only. Minimal change, but not a clear product version and no explicit update check.
2. Combine the package version, CI run number, and short SHA; ship metadata with the release and poll the deployed version endpoint. Selected because it is unique per packaged release, traceable, and supports an update banner without a database migration.
3. Persist releases in a database and administer them from a new release-management UI. Rejected for now because deploy already owns immutable release artifacts and a database catalog adds unrelated mutable operational state.

## Design

The web package version is the product version. CI/manual packaging injects a unique, human-readable release identifier into `NEXT_PUBLIC_APP_VERSION`, using a CI run number/attempt or local UTC timestamp: `<productVersion> (Build <run>.<attempt>)`, `<productVersion> (Manual <timestamp>)`, or `<productVersion> (Local <timestamp>)`. Full commit SHA remains recorded in `build-info.txt` for technical traceability. The web build exposes the display identifier through a dynamic, no-cache `/api/app-version` route. Console clients display their own build identifier and periodically compare it with the active route; a mismatch shows an update notice and refresh action. No application data or identity is included in the endpoint.

The account plan page and backend remain customer-only; the backend requires the Customer role and denies Level 1/2 even if an identity has a mixed role assignment. Business-data scopes reject administrator identities as customer targets, including the acting administrator's account, for catalog, invoice, buyer, and taxpayer reads/writes. The account selector endpoint returns customer-only accounts; the user-management page retains its separate role-aware directory. The fiscal buyer/taxpayer list supports filtering to one selected customer, matching its existing server query capability. API ownership/MFA checks remain the source of authorization. Level 2 sees all accounts currently assigned to that Level 2; Level 1 may filter by customer within the existing root scope and the dashboard already supports partner-level financial filtering. No global Level 2 query bypass is introduced.

## Constraints and success criteria

- No EF migration or production database changes.
- Release metadata is generated in CI and local release packaging, included in the web bundle, and verifiable from its public JSON endpoint.
- Stale console sessions can notice a changed release and refresh; endpoint failures do not block normal work.
- Existing role-based route guards and server scope checks remain intact.
- Focused implementation checks are documented; tests are not added or run as part of this request.

## Follow-up recommendations

- If Level 2 truly needs cross-tenant access, define an explicit audited support grant with expiry, reason, and read/write scope rather than making the role globally privileged.
- Add administrator activity audit for customer selection and writes, and provide a read-only “acting for customer” context banner in the console.
- Consider a release notes link keyed to the displayed version once release notes become part of the package process.
