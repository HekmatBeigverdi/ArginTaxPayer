# Phase 10 — Step 28: Sandbox Sales Return

## Implementation
- Independent sales-return workspace `sandbox_invoice_sales_return_workspaces`, with optimistic revision, state transitions, access scope and one active draft per predecessor.
- Only Type 1 / Pattern 1 finalized Sandbox invoices accepted by the authority can be used as references.
- Original is immutable; legal relation subject 4 and a new fiscal identifier are created for the successor.
- Quantity is specified per original line. Zero means the line is not returned; the wire projector removes zero-quantity lines.
- Price, discount, official identifiers, units, description and VAT rate are locked to the referenced invoice.
- Server recomputes the return amounts. Other tax/fees on the original are not copied to the return.
- A serializable transaction locks the original invoice and checks cumulative returned quantities in all pending/accepted successors.
- Failed authority returns are excluded; pending and indeterminate returns remain reserved until definitively reconciled.
- One-click sender performs the existing sandbox dispatch and inquiry. Unknown submission outcome must never trigger a second send.
- The invoice table retains the original row identity; legal successor chain holds the actual return records and tax IDs.

## Endpoints
- `POST /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/sales-return-draft`
- `GET /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/sales-return-draft`
- `PUT /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/sales-return-draft`
- `DELETE /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/sales-return-draft`
- `POST /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/sales-return-send-and-inquire`

The web proxies are in `apps/web/app/api/invoice-dispatch` and require cookie/session and same-origin checks. No raw credentials are sent to the browser.

## macOS verification
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch work/phase10-step28-sales-return
git pull --ff-only
zsh scripts/phase10-step28-mac-verify.zsh
```

The script **only** targets `argintaxpayer_test` through the AutoSSH forwarded localhost port 15432.
The migration is `20261030210000_AddSandboxSalesReturnWorkspaces`.

## Real Sandbox acceptance (operator-only)
1. Send and obtain SUCCESS for a fresh original Type1/Pattern1 invoice with two item lines and official 13-digit item codes.
2. Open sales-return modal from the original row; choose a positive partial quantity on only one line; save and reopen the draft.
3. Confirm the return line price, VAT, other item fields and calculated totals; send once. Record original tax ID, return tax ID, reference, receipt and authority inquiry result.
4. On SUCCESS, verify the original row is not duplicated and chain contains both snapshots in legal order.
5. Return another allowed partial quantity and verify the cumulative cap; attempt over-return and expect conflict with no external send.
6. Simulate a second browser tab and stale revision; expect conflict and no additional legal successor.
7. Exercise timeout/indeterminate response: inspect inquiry, **never retry SEND**.
8. Test user isolation for Level 1, Level 2 and a customer account. Keep production endpoints disabled.

## Acceptance status
Source changes and test scripts are committed; actual .NET/Next builds, migration effects and tax authority Sandbox replies must be verified on the operator Mac before this step is accepted.
