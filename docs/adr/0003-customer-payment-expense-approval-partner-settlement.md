# ADR-0003 — Per-customer collections, expense approval and profit-share settlement

- Status: Proposed — Phase 01
- Date: 2026-10-10
- Related: ADR-0001 and ADR-0002

## Decisions

1. Level 2 delegates establish independent annual/quantity product prices within authorized customer scopes. Level 1 approves eligibility and documentary evidence of deductible tax/costs; Level 1 and each Level 2 share **net distributable profit** according to a versioned agreement, normally 50/50.
2. Every commercial event is a unique `Order` and `OrderType`: first plan purchase, annual renewal, additional quantity pack, supplemental units, and refund/adjustment. Each order belongs to one delegate workspace and one explicitly authorized company/person. Historic orders keep immutable commercial/contract snapshots.
3. Store and compute three **separate status dimensions** for each order: customer collection (unpaid, partially collected, fully collected, refunded), cost approval (pending, approved, rejected, revision requested), and partner settlement (not settled, partially settled, settled, reversed). An invoice's fiscal submission/acceptance status and its subscription activation status are separate again.
4. Customer payments can be cash or gateway receipts held by the appropriate Level 2 delegate's accounts. Post receipt transactions and allocate them to one or more orders using `PaymentAllocation`. A deposit alone does not prove an order is fully paid unless the allocated amounts reconcile; partial payment and refunds must be supported.
5. A delegate must submit expense records with expense category, amount, taxable treatment, invoice/order allocation, evidence reference, creation/user/time; Level 1 approves or rejects each line with a reason and immutable decision audit. Never silently remove contested costs. Profit-distribution finalization is blocked until required costs have been reviewed under the active agreement.
6. Calculate distributable profit using agreed tax-exclusive realized revenue less *approved, eligible, non-duplicated* associated expenses. Avoid deducting output VAT twice. Use decimal amounts and explicit currency, sign/range validation, rounding policies, and explicit policies for partial receipts, unpaid receivables, refunds, losses and chargebacks.
7. Snapshot versioned agreement percentage, eligible approved expenses, recognized revenue basis, and calculated Level 1/Level 2 shares per order. Record settlement transfers separately from calculated liabilities, with `PartnerSettlement` and per-order `PartnerSettlementAllocation` enabling a single bank transfer to settle many orders and partial settlement of an individual order.
8. Compute the settlement state of each customer/company and delegate by aggregating eligible constituent orders and allocation balances, **never by setting an independent manually editable settled flag**. A Level 1 administrator may record/approve an actual settlement with supporting banking evidence and date, and may inspect all delegates. Level 2 can view only their own customer/finance ledgers.
9. Every monetary event, adjustment, approval and allocation has tenant/workspace authorisation checks, append-only audit history, idempotency keys, concurrency checks and ledger reconciliation. Correct records by reversal entries rather than changing posted history.

## Suggested entities
- `Order`, `OrderLine`, `OrderPriceSnapshot`, `CustomerCollection`, `PaymentAllocation`
- `SaleExpense`, `ExpenseDocument`, `ExpenseApprovalDecision`
- `ProfitSharingAgreement`, `OrderProfitDistributionSnapshot`
- `PartnerSettlement`, `PartnerSettlementAllocation`, `FinancialJournalEntry`, `FinancialJournalLine`
- `Workspace`, `DelegateAssignment`, `Taxpayer` as defined in ADR-0001.

## Worked example
A company first buys a plan and later renews and buys a new quantity pack. These are three independent orders; each has its own customer receipt, expense review, profit distribution and partner settlement balance. Payment of one order must not automatically mark another settled. A single partner transfer can be allocated to all three as separate, traceable lines. For example, if a sale's tax-exclusive recognized revenue is 900,000 toman and approved eligible non-tax operating expense is 100,000 toman, the distributable amount is 800,000 toman and an agreement of 50/50 allocates 400,000 to each partner; cash settlement remains a separate step.

## Acceptance tests
- Initial sale, renewal and additional packs each create distinct financial records and settlement balances for the same company.
- A Level 2 admin cannot approve their own expenses or mark another delegate's settlement as completed.
- Level 1 approval/rejection updates eligible cost state with reason and audit trail; disputed expenses cannot silently reduce partner profit.
- Cash/gateway payment, partial payment, cancellation, refund, partial partner settlement, and many-orders-one-transfer scenarios reconcile to the cent/lowest currency unit.
- Multiple simultaneous callbacks or repeats never duplicate revenue, credits, expenses, partner debt or allocations.
- Editing a plan price, sharing agreement or expense rule cannot change already-finalized order snapshots.
- Company report includes customer collection, cost approval, distributable profit, each partner's share, cumulative partner transfers and outstanding balance.

## Open policy decisions
- **Resolved:** partner-profit entitlement and settlement use verified cash received and allocated to the order, never uncollected invoice face value. Recognize customer receivable separately. Deduct tax and Root-approved eligible costs without double-counting output VAT; allocate expenses to collection slices using a controlled policy.
- Define allowable tax/expense categories and proof rules, approval/delegation exceptions, loss carry-forward, chargeback responsibilities and settlement cadence.
- Confirm who can approve actual settlement transfers and whether Level 2 may submit bank evidence pending Level 1 final confirmation.

## Design follow-up
See [Phase 01 logical ERD](../phases/phase-01-step-05-logical-erd.md) for entity relationships, uniqueness requirements, payment/settlement allocation invariants and tenant-boundary tests.
