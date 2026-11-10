# Release Version and Administrator Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Assign a unique display version to every web release, make the active version visible/update-aware in the console, and preserve the existing customer-scoped administrator behavior with customer-only personal plans.

**Architecture:** Product version remains `apps/web/package.json` version. Each release injects a unique `NEXT_PUBLIC_APP_VERSION`, also recorded in `build-info.txt`; a no-cache API route reports the active identifier. The console compares its build identity to the active endpoint and provides a refresh action. Admin scope remains enforced by existing API/database authorization; customer plan route stays Customer-only.

**Spec:** `docs/superpowers/specs/2026-11-10-release-version-and-admin-scope-design.md`

## File Map

- Modify `apps/web/app/components/console-shell.tsx`: show release identifier and stale-release notice/refresh action.
- Create `apps/web/app/api/app-version/route.ts`: serve the current release identifier from the build environment with no-cache headers.
- Modify `.github/workflows/phase18-production-release.yml`: inject a unique release identifier before web build and include it in build-info.
- Modify `scripts/phase18-step05-build-release.sh`: generate unique metadata for the local release and package it with web output.
- Modify `scripts/phase18-manual-build-deploy.sh`: use the same release metadata contract when building manual bundles.
- Modify `apps/web/app/api/fiscal-participants/route.ts` and `apps/web/app/parties/buyer-taxpayer-directory.tsx`: pass the selected customer filter through the BFF to the already-scoped server query.
- Modify `apps/web/app/catalog/workspace.tsx`, `apps/web/app/(console)/catalog/page.tsx`, `apps/web/app/invoices/workspace.tsx`, and `apps/web/app/parties/buyer-taxpayer-directory.tsx`: keep administrator writes targeted to a selected subordinate customer and omit their own account.
- Modify `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/CustomerCheckoutService.cs`: require an active Customer identity and deny Level 1/2 role holders from personal checkout.
- Modify the customer target scope predicates in `CustomerCommercialOwnershipService`, `CustomerProductService`, `CustomerServiceCatalogService`, `TaxIdentifierMappingService`, `InvoiceCatalogHandoffService`, `CatalogSearchService`, `ParticipantScopeService`, `FiscalParticipantDirectoryService`, and `TaxpayerScopeService`: customer targets must be pure Customer identities, never administrator identities; admin self-scope is denied.
- Modify `apps/web/app/(console)/users/page.tsx` and `apps/web/app/(console)/customer-plans/page.tsx` only if review finds a missing existing role gate or help text; do not broaden data scope.
- Modify this plan with actual outcomes after implementation.

## Task 1: Define and surface release identity

1. CI/manual package scripts derive a release identifier from product version, run/build number or timestamp, and short SHA.
2. Implement the no-cache `/api/app-version` route from the build-time environment value.
3. Update `ConsoleShell` footer to show the release ID and poll the endpoint every five minutes while mounted. On a differing ID, show a Persian update banner with a refresh button. Ignore stale in-flight responses after unmount; endpoint errors remain non-blocking.
4. Do not include secrets, account information, or environment configuration in the response.

## Task 2: Stamp CI and local release bundles

1. CI derives the identifier from `GITHUB_RUN_NUMBER`, `GITHUB_RUN_ATTEMPT`, `GITHUB_SHA`, and the web package version.
2. Before `next build`, provide build identity to compile-time UI environment so the built console shows its own immutable version.
3. Include the same ID and full SHA in `build-info.txt` for artifact/operator traceability.
5. Local scripts derive build number from a UTC timestamp and the checked out full SHA, then use the same package metadata shape.
6. Keep archives/checksum generation and deployment gates unchanged.

## Task 3: Confirm administrator boundaries

1. Preserve Level 2 current-assignment scope and Level 1 root scope; preserve customer selection as a target only, never as an authorization grant.
2. Ensure catalog and invoice administrator forms start with no target selected and do not offer the administrator's own account.
3. Ensure party registration excludes the administrator's own account; pass customer filter to the fiscal directory's existing server-side scoped query.
4. Keep the customer plan route Customer-only and enforce that role in the backend checkout service, including mixed-role accounts. Business-data scope predicates must reject admin-role targets even when Customer is also present; do not implement global Level 2 data access.

## Verification

- Inspect all changed files and `git diff --check`.
- Do not run tests or builds in this task, per the active instruction not to run tests unless requested.
- No database migration, deploy, or live service operation.

## Implementation outcome

- Added immutable, unique CI/local/manual release identifiers, exposed the running identifier through a no-cache route, displayed it in the console, and added a five-minute update check with refresh action.
- Added customer filtering to the buyer/taxpayer directory by passing its selected customer ID through the BFF to the existing scoped backend query.
- Removed administrator self-targeting from catalog, invoice, and party-registration target selectors; catalog starts without a selected customer.
- Added a server-side customer-role requirement for checkout that denies Level 1/2 identities, including mixed-role accounts.
- Hardened customer target queries for account selectors, catalog, party and taxpayer scopes so mixed-role administrators are not treated as customer data owners. Invoice routes inherit this through `TaxpayerScopeService`.
- Preserved Level 2 assigned-customer boundaries and Level 1 root scope; no schema or deployment changes.
- No tests or builds were run. `git diff --check` was run during review.
