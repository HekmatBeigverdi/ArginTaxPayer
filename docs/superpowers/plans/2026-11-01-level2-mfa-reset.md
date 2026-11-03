# Level2 MFA reset implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Track work with checkboxes.

**Goal:** Level1 can find Level2 users and reset MFA from software users.
**Architecture:** Extend existing paginated listing, add a narrow transactional identity service/endpoint, and expose it through the identity BFF and users table.
**Tech Stack:** ASP.NET/EF Core/PostgreSQL; Next.js/React/TypeScript; xUnit/node:test.
**Spec:** docs/superpowers/specs/2026-11-01-level2-mfa-reset-design.md

## Global Constraints
Preserve Level2 ownership scoping and existing enrollment proof. No phone hardcoding, secrets in logs, dependencies or migrations. Preserve unrelated workspace edits.

## Review Focus
Domestic/Persian mobile search; pending Level2 visibility; malicious Level2 caller/Level1 target; old sessions and recovery codes after reset; duplicate and concurrent reset/enrollment.

### Task 1: Server behavior and regression tests
Files: CustomerCommercialOwnershipService.cs, IdentityLevel2MfaResetService.cs, IdentityDevelopmentEndpoints.cs, TaxpayerDevelopmentEndpoints.cs, Program.cs; tests/ArginTaxPayer.Tests/Level2MfaResetTests.cs.
- [x] Write and run failing PostgreSQL HTTP tests for domestic/Persian search, role filtering, authorization, reset invalidation and re-enrollment.
- [x] Extend ListCustomerAccountsPageAsync with optional role after CancellationToken, plus Roles/MfaEnabled response metadata; normalize mobiles without breaking partial search.
- [x] Add ResetAsync(bearer, targetUserId, ct), validate actor and target in a serializable transaction, reset state and suspend role; expose POST mfa/level2/reset and log completed action.
- [x] Run targeted integration tests, including denied callers and targets.

### Task 2: Table and proxy
Files: apps/web/app/users/users-workspace.tsx, apps/web/app/api/customer-owners/[...path]/route.ts, apps/web/app/api/identity/[...path]/route.ts; frontend tests.
- [x] Add role parameter forwarding, authenticated reset proxy, role filter/status and Level1-only reset column with confirmation dialog.
- [x] Refresh list after reset, disable duplicate submission and pending/no-state reset, show result/errors.
- [x] Run focused node tests and TypeScript check; review diff for permissions, scope and unintended changes.


## Validation and execution notes
Implemented in the existing feature checkout; preserved the preexisting finance UI test edit. No migrations, dependency changes, commits or account resets were performed as part of implementation. The local managed API was restarted with its existing private configuration and OTP pepper; readiness returned 200 and the new reset endpoint returned 401 without authentication.

The initial regression failed at the missing reset route (404 versus 401). The complete PostgreSQL reset/re-enrollment and concurrent reset test passed, as did the existing MFA onboarding integration test. Frontend: 498 tests passed and TypeScript passed. Identity/ownership offline selection: 94 tests passed (integration methods gated off in that run). Reviewer findings about pending enrollment visibility and in-flight enrollment were fixed; the final deterministic interleaving test also passed against PostgreSQL (including pending enrollment reset, concurrent reset, and password/ownership preservation).
