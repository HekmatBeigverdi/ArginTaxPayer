# Chained Sales Return Design

## Intent

Allow an authorized user to prepare a sales return against the latest eligible invoice in a legal invoice chain, including a correction or a prior sales return. Keep each invoice immutable and preserve every predecessor/successor edge so the chain remains Original → Correction → SalesReturn (and later lawful successors).

## Source rule

RC_IITP.IS V7.9 §5.2 p16 item 3 permits a correction/return to be referenced only when it has an approved, system-approved, or no-response-required result and has no other non-canceled correction/return. §5.4 pp17–18 defines returns as remaining quantities; Table8 p30 requires the registered predecessor tax ID and same issuer scope. Existing source-derived domain rules already model these conditions.

## Goals / non-goals

- Show the sales-return action for the current chain node when it is an Original, Correction, or SalesReturn and has a dispatch attempt.
- Open and edit a distinct Production sales-return draft using that node's snapshot, fiscal tax ID, and ID as predecessor.
- Persist the new relation from the selected predecessor to the return and make all descendants visible in the lineage modal.
- Keep the original document and every prior successor immutable.
- Keep legal/authority checks server-side. A draft is preparation only; this change does not send or claim acceptance.
- Do not allow Cancellation to be referenced or continue the chain.

## Approaches

1. Add only a button for corrections. Rejected because the backend and chain read model would still anchor to the root and could lose future descendants.
2. Make legal relations recursively authoritative in the UI, draft service, and lineage read model. Chosen because the legal rule is predecessor-relative and each successor must remain a distinct node.
3. Flatten or overwrite the root invoice with latest values. Rejected because it destroys immutable fiscal evidence and the recorded reference chain.

## Design decisions

- Use `effectiveInvoiceId` as the sales-return predecessor while retaining the original root ID for the overview row and lineage entry point.
- Accept Original, Correction, and SalesReturn snapshots as candidate predecessors. Reject Cancellation.
- Draft lines and original quantities come from the exact predecessor snapshot; `referenceTaxId` comes from its persisted Production fiscal reservation.
- Keep predecessor acceptance, active-child conflict, same-memory, quantity, and final-send guards on the API side. UI availability never grants legal eligibility.
- Read lineage with a recursive, same-owner/same-taxpayer traversal. Never infer edges from IDs or invoice labels.

## Verification

Focused tests cover supported and terminal subjects, exact predecessor anchoring, corrected-line quantities and fiscal reference, recursive node ordering, and UI use of the effective predecessor ID. No external authority send is part of this change.
