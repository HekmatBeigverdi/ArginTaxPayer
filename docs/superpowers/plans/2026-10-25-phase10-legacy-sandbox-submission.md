# Plan: Phase 10 legacy Sandbox submission

## Tasks

1. Add failing contract tests for legacy transport framing and safe response handling.
2. Implement a legacy `SELF_TSP` transport using the existing signer/custody boundary and pinned endpoint allowlist.
3. Replace v2-only readiness blockers with profile-aware legacy blockers and register the selected transport only behind explicit local Sandbox configuration.
4. Wire the dispatch control to the durable admission/outbox path without exposing secrets or enabling Production.
5. Run focused, full .NET, web and offline security gates; document remaining operator-only acceptance.

## Constraints

- No direct use of the old SDK's insecure TLS bypass.
- No private key in source, browser, logs or request arguments.
- No blind retry after an unknown authority outcome.
- Existing v2 code remains deferred and unregistered.
