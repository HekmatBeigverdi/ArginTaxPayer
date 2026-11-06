# Production Invoice Legal Operations: Owner Test Checklist

This checklist is for the owner to run against an approved, isolated Production test memory. The implementation session does not submit real invoices or activate the Production service.

## Before testing

1. Back up the current database and apply the additive migration `20261106010000_AddProductionLegalSuccessorRelations` using the approved Production migrator identity.
2. Confirm the owner-approved invoice memory is classified as Production and that the existing Original send remains the passing baseline with an acknowledged dispatch.
3. For a staged owner test instance only, set `Phase18Production__LegalSuccessorsEnabled=true` and `Phase18Production__LegalSuccessorsOperatorApproved=true` in its protected API environment file. Leave these unset/false elsewhere. Do not remove the existing service activation gate as part of this feature test.
4. Inquire the predecessor and confirm the saved authority report says `SUCCESS`. An enqueue acknowledgement alone does not qualify.
5. Verify the UI uses the existing authenticated BFF and the API selects the environment from the saved invoice memory. Do not provide an environment or endpoint in request data.

## Run one operation at a time

### Correction

1. Open correction on an accepted Production invoice.
2. Save one legally allowed correction and confirm the draft revision changes.
3. Reopen the draft and confirm the saved values persist.
4. Submit once. Record the new invoice ID, dispatch status, authority reference, and inquiry status separately.
5. Inquire the successor. Confirm the final authority report before treating it as accepted.
6. Reopen the predecessor and confirm its snapshot and fiscal ID did not change.

### Cancellation

1. Open cancellation for a different accepted Production invoice.
2. Confirm the eligibility view shows the predecessor fiscal ID.
3. Submit once and record the successor ID and dispatch status.
4. Inquire the successor and confirm the final authority result.
5. Repeat the cancellation request and confirm it returns the `cancellation_already_requested` conflict without creating or sending another successor.

### Sales return

1. Open sales return for a different accepted Production invoice.
2. Reduce a line quantity without changing its protected item, price, discount, VAT, or unit values; save and reopen the draft.
3. Confirm the saved return quantity does not exceed the remaining accepted quantity and that at least one quantity is returned.
4. Submit once, then inquire and record the final authority result.
5. Confirm another active correction/return claim on the same predecessor is rejected and the predecessor snapshot remains unchanged.

## Result handling

- `Acknowledged` means the authority accepted the packet for processing; it does not mean the invoice was fiscally accepted.
- If a send result is indeterminate or the request times out, do not submit again. Use the inquiry/reconciliation flow and keep the claim active until its outcome is known.
- Record the operation ID, invoice ID, reference, returned status and sanitized error code for each test. Do not put private keys, tokens, or invoice payloads in logs or reports.

## SDK upgrade contract

The active adapter remains pinned to legacy SELF_TSP `0.0.34` (archived source `0.0.30`; package `0.0.34`). The archived modern `2.0.32` adapter is not enabled. An SDK change requires preserving the new archive and checksum, comparing its official guide and public API, adding a separately versioned adapter and wire fixtures, running Sandbox contract tests and environment-isolation checks, recording owner approval, pinning the version explicitly, and retaining rollback to the previous adapter. Unknown versions must remain blocked.
