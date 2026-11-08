# Production Invoice and Legal Operations Design

**Date:** 2026-11-06
**Project:** ArginTaxPayer
**Status:** Owner-approved; updated with tested workflow results

## Intent

Complete Production correction (the user's “edit” operation), cancellation, and sales return so the owner can test each one. The owner confirms the existing Production original-invoice send works and has been tested, and all Sandbox invoice operations are tested and working. Preserve those working paths. Reuse the active legacy protocol for both environments where the only runtime difference is the server-owned endpoint, keep environment-specific storage and credentials isolated, and make future SDK generations explicit versioned adapters with contract tests.

## Current state

- The owner reports that the existing Production original-invoice send has been live-tested and works. Do not replace or refactor this path as part of this task; preserve it with regression tests.
- All current Sandbox invoice operations (original, correction, cancellation and sales return) have been tested by the owner. Their legal rules and interactive draft workflows are the reference behavior, but their Sandbox-specific tables/keys must not be used for Production.
- The active Phase18 Production send and working Sandbox send both use the legacy SELF_TSP contract with environment-selected pinned endpoints. The owner's live verification takes precedence over older project status notes that marked this path untested.
- The archived modern SDK 2.0.x is a different protocol generation. It is not active in the tested workflows and must not silently replace them.
- The supplied artifacts include legacy source SDK 0.0.30 plus package 0.0.34, and modern source/package 2.0.32; the modern guide cites 2.0.31. Preserve this distinction for future upgrades and contract verification.
- An enqueue acknowledgement means the authority accepted the packet for processing; it does not prove fiscal acceptance. A trusted Production inquiry/status result is required before a successor operation can rely on its predecessor.
- Project status documents still require current protocol/credential verification and separately authorized live acceptance. No official current API specification was located in the initial public-source search.

## Goals

- Preserve explicit, immutable authority environment binding from invoice and fiscal memory through operation, outbox, attempt, receipt, inquiry, and legal lineage.
- Let the owner test each Production operation independently with clear readiness blockers and outcome states.
- Support correction drafts with the existing save/revision semantics, separate cancellation successor invoices, and sales returns that preserve the original and enforce remaining quantities.
- Require a verified Production-accepted predecessor and reference Tax ID before creating correction, cancellation, or return documents.
- Keep dispatch single-attempt and fail closed: indeterminate outcomes require reconciliation and are never automatically resent.
- Run offline tests and validate endpoint/configuration behavior without issuing real invoices.

## Non-goals and anti-goals

- Do not send a real invoice or authority inquiry from this implementation session.
- Do not claim current official protocol compatibility based only on an archived SDK guide or by changing a host string.
- Do not route Production operations through Sandbox tables, Sandbox keys, `argintaxpayer_test`, or Sandbox endpoints.
- Do not mutate a finalized predecessor to represent a correction, cancellation, or return.
- Do not equate enqueue acknowledgement with fiscal acceptance.
- Do not enable Production deployment or change systemd activation gates as part of code readiness.

## Approaches

1. Rebuild Production submission on modern SDK 2.0.x. Rejected for this task: the owner already live-tested the existing Production original send. Replacing it would introduce avoidable risk and is not necessary to support legal successors.
2. Preserve the tested legacy sender for both environments, share existing V7.9 legal rules, and add Production-specific successor/lineage persistence and operations behind a versioned authority-provider boundary. Chosen: it matches verified behavior, prevents Sandbox DB/key leakage, and keeps future SDK upgrades isolated.
3. Clone all Sandbox services for Production wholesale. Rejected: it duplicates business rules and risks copying Sandbox SQL/table assumptions into Production.

## Proposed behavior

### Protocol and SDK boundary

- Keep SDK DTOs and protocol details outside Domain/Application fiscal rules. Retain the existing legacy SELF_TSP implementation as the currently tested adapter for both environments; endpoint selection remains pinned to persisted environment.
- Place the selected authority adapter behind an explicit versioned internal boundary without changing the tested Production sender. `legacy-self-tsp/0.0.34` is the active profile (the source archive is 0.0.30 and package archive 0.0.34); `modern-rest/2.0.32` remains an inactive future adapter until deliberately implemented and contract-verified.
- Record SDK family/version, wire-contract version, route profile and evidence source in sanitized adapter diagnostics. Unknown versions fail closed; never auto-upgrade or load arbitrary SDK versions at runtime.
- To adopt a future SDK release, add a version-specific adapter, compare public API and wire mapping with the archived official guide, update contract fixtures/tests and compatibility notes, obtain owner approval, then switch the explicitly pinned version. Keep the prior adapter available for rollback.
- Use archived sources: `TAX-SDK-GUIDE-OLD` and `SDK-SOURCE-OLD` for the legacy protocol; `TAX-SDK-GUIDE-NEW`, `TAX-CONNECT-16` (RC_TICS.IS v1.6), `SDK-SOURCE-NEW` and `SDK-NUGET-NEW` for modern Production. `TAX-ISSUE-79` (RC_IITP.IS V7.9) governs invoice/legal rules independently of SDK code. Preserve the source/package discrepancy (old source 0.0.30 vs binary 0.0.34; new guide 2.0.31 vs source/package 2.0.32) as an explicit compatibility check.

1. Original: resolve the active Production memory from persisted records, validate and freeze the invoice, prepare a Production-classified operation/outbox, then allow one explicit dispatch attempt. Display acknowledged, indeterminate, blocked, and later authoritative fiscal status distinctly.
2. Correction (“edit”): preserve the original invoice; create an independently revisioned successor draft with only legally allowed changes; require authoritative accepted predecessor and same-environment reference; finalize and submit via its own Production operation.
3. Cancellation: create a distinct successor document referencing the accepted predecessor, preserve its immutable snapshot, enforce one active cancellation claim, and submit through the same Production dispatch model.
4. Sales return: create a distinct successor with remaining quantities and unchanged protected commercial fields; enforce no simultaneous active correction/return claim and require accepted predecessor; submit through the same Production dispatch model.
5. All operations: record authority outcome per operation, preserve predecessor/successor links, reject cross-environment references, and require explicit operator action for every dispatch. An unknown send result remains indeterminate and is reconciled by inquiry, not resent.

## Constraints and acceptance

- Use authoritative, versioned tax-authority rules already present in the repository for legal invoice rules. The owner has verified the active original-send route; preserve it. Treat a future SDK generation as unverified until its protocol contract and integration tests are complete.
- Production sender credentials, signing/encryption key custody, authority server encryption key, endpoint allowlist, and response parser must stay scoped to the Production memory.
- Existing migrations and historical records are immutable evidence. New schema changes must be additive and safe for populated databases.
- Acceptance is staged: preserve original Production send via regression tests; implement and test (1) correction, (2) cancellation, (3) sales return. Each successor stage gets offline negative/positive tests and a manual checklist; owner performs any live authority test with their own authorized memory and credentials.
- Deployment remains disabled until source tests, migration review, credential setup, protocol confirmation, and the owner's staged live checks are complete.

## Builder decisions

- Interpret “edit” as a formal Correction successor, not editing a finalized invoice in place.
- Reuse the tested legacy wire adapter for both environments while the server-selected endpoint is the only active protocol difference. Keep per-environment credentials, memory binding and successor persistence isolated.
- Build Production operations atop Phase18 classified persistence/dispatch. Retain Sandbox persistence and live flow unchanged.
- Keep modern SDK 2.0.x as a separate, inactive adapter family. Future upgrades require an explicit versioned adapter and contract tests; never runtime auto-upgrade.
- Make SDK upgrades explicit, pinned, versioned adapter changes with contract tests and a recorded evidence/version compatibility decision; never runtime auto-upgrade.
- Treat current official protocol confirmation as a prerequisite to enabling any live Production request. Offline code paths may be prepared while this prerequisite remains explicit.
