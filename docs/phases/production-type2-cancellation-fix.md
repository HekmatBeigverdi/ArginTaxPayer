# Production Type 2 general-sale cancellation

The `unsupported_predecessor_profile` response came from a Type 1-only check before cancellation document creation. The Production cancellation snapshot builder now accepts Type 1 or Type 2, Pattern 1, while retaining Production binding, accepted-predecessor inquiry, subscription, immutable original and duplicate-cancellation controls.

The frozen projector and wire mapper now also support Type 2 cancellation. The source classification remains internal; cancellation output omits `inty`, `inp`, buyer and settlement fields, has an empty body, a new fiscal ID and the accepted predecessor's `irtaxid`. Type 2 correction and sales return were subsequently implemented; see [Type 2 sales legal operations](type2-sales-legal-operations.md).

Source: owner-supplied `RC_IITP.IS V7.9`, Table 10 p32, items 1, 3 and 4; Table 1 p14 for Type 2 settlement field applicability. PDF SHA-256: `fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be`. Existing pinned legacy transport and cryptographic profile are unchanged; this is a content projection change, not an SDK upgrade.

Regression evidence: 7 new cases cover Type 1/2 cancellation projection, invalid type/pattern/environment rejection, and the separate correction/return profile behavior covered by the follow-up tests. The Type 2 cancellation case failed on the prior projector and passed after the fix. Full backend suite: 1635 passed, 55 skipped. Web suite: 748 passed. These runs do not establish real authority acceptance and send no cancellation.

The rejected `unsupported_predecessor_profile` branch occurs before cancellation document, relation, serial or outbox creation. Deploy the updated code through the normal pipeline before reopening eligibility and explicitly submitting again. Existing uncertain or previously created cancellation operations still require reconciliation and are not automatically retried.
