# Type 2 general-sale correction and sales return

Type 2 / Pattern 1 invoices now use the correction and sales-return workflow on Production-classified memories, including when the application runs locally against the TEST invoice database. The existing legacy Sandbox implementations use the same profile and settlement rules. Classified modern Sandbox routing is a separate existing limitation and is not changed here.

The server checks the saved environment and type/pattern before opening a draft. Draft responses include `invoiceType` (default 1 for backward compatibility). Type 2 drafts expose numeric empty settlement values to the editor while retaining null monetary settlement fields in their saved snapshots. Client settlement input is rejected for Type 2. Type 1 continues to validate cash, credit and mixed settlement as before.

The UI removes the settlement tab for Type 2. Both draft proxies preserve and validate invoice type and accept the empty settlement contract. The frozen projector retains `inty=2`, `inp=1`, the legal subject and the accepted predecessor's `irtaxid`; buyer and settlement fields are omitted. Sales returns continue to exclude zero-quantity lines from the outbound body.

Accepted-predecessor inquiry, memory and owner binding, active relation conflicts, quantity accounting, unchanged-correction rejection, immutable originals, subscription checks and explicit send confirmation remain enforced. Production feature gates must be enabled in the deployed API environment. No actual invoice is sent by the regression tests.

## Source and compatibility

Normative source: owner-provided `RC_IITP.IS V7.9`, Table 10 p32 (subjects and buyer fields on referring invoices), Table 8 p30 (reference ID), Table 1 p14 and Table 24 p45 (Type 2 settlement). Verified PDF SHA-256: `fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be`.

This extends content projection and draft handling on the existing pinned legacy transport. It does not replace the legacy SDK/protocol with the separate modern SDK profile, change signing/encryption, or select an authority from the runtime's local/production deployment label. No migration or serial reinitialization is needed for this change.

## Verification and rollout

Regression tests exercise both environments' Type 2 wire projections, each draft mapper's nullable settlement handling, invalid client settlement rejection, Type 1 settlement compatibility, draft-proxy validation, and both editors' save requests. Full backend suite: 1653 passed, 55 skipped. Web suite: 756 passed. TypeScript type checking passed.

Deploy the updated backend and web together through the normal pipeline. The local process must also load the updated build. Runtime readiness and automated tests do not establish authority acceptance; each actual legal document still requires explicit owner submission and inquiry.
