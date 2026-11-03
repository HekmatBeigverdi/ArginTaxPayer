# Partner batch settlements and receipt vouchers

## Requirements and agreed accounting

Replace the allocation-payment UI with a partner-selected batch receipt workflow. The owner clarified that cash collections are held by Level2 even when Level1 records them, gateway ownership/configuration is future work, and both new receipts on old sales and later operating expenses must participate in later settlements. No pending order debt is payable. All table pages contain ten database-selected rows.

- Require a selected Level2. Level1 alone posts/configures terms/approves expenses with MFA; Level2 may read its own batches/preview. Resolve currently assigned partner for unsettled receipts (current scoped customer ownership, then historical order); once a receipt is in a batch, freeze its root/partner/rate and user/plan identity.
- Each verified cash/bank receipt can enter once as revenue. New verified receipts on old orders qualify regardless of sale date or activation. Costs added later produce only a delta, never a second revenue recognition. Gateways remain unclaimed with an explicit pending-owner count; no assumption about a future merchant is made.
- Approved costs are deducted before shares. Record each new expense against a verified receipt, identify whether Level1 or Level2 actually paid it. Receipt total costs cannot exceed its collected amount. Existing commercial approved costs (if present) count as Level1-paid; existing legacy settlement activity is excluded and counted for explicit reconciliation.
- Per-receipt net due to Level1 = root share + Level1-paid costs - amounts already held by Level1. Current cash/bank receipts are held by Level2; gateway receipts require future ownership configuration. Root/partner shares divide net revenue after total costs, with integer IRR rounding toward zero for partner share. A negative batch net means Level1 pays Level2; a zero-net batch can still record new revenue/cost offsets.
- Subsequent batch lines store cumulative receipt calculation plus previous cumulative amounts and only the delta to settle. Previous root/partner/rate stay frozen through later cost corrections or ownership/profile changes.
- Use explicit per-partner terms in existing `application_settings` key `settlement.partner.<partner UUID>`; no implicit default percentage. On first configuration, Level1 approves partner basis points (0..10000); changes affect only receipts not previously covered. Existing effective partner_share_terms may supply a rate when no current setting exists. Unconfigured receipts block registration, not silently use zero.
- Immutable additive batches, batch lines and expense evidence. Unique receipt+cost cumulative inputs prevent duplicate coverage. Root/partner advisory transaction locks, deterministic receipt locks and serializable execution strategy protect commit; partner identity lock serializes same-partner commits. Root-scoped operation key and exact request payload hash provide idempotency. Preview fingerprint is recomputed at commit; changed receipts/costs/rates reject stale previews. Old per-allocation settlement journal remains untouched and its mutation UI is removed.
- Print vouchers from immutable saved header and line snapshots, never live profile or terms joins. Voucher shows receipt number/date, payer/receiver, cash/bank totals, cost totals, both shares/rates, net direction, and all customer/plan/source receipt lines. A separate authenticated print response may contain every saved line, independent of the ten-row interactive page.

## API/DTO contract

All routes are `/api/dev/subscriptions/settlements/...`, behind existing local-only bearer guards. The Next proxy uses its corresponding `/api/phase12/subscriptions/...` path, cookie identity, allowlist and root-only writes. Backend validates all UUIDs/scope, page and financial inputs independently.

- `GET partners` => `PartnerOption[]`: `{partnerUserId,displayName,partnerBasisPoints:number|null}`. Include eligible current/historical partners and existing batch counterparties only within caller scope; no customer accounts masquerading as partners.
- `PUT terms` body `{partnerUserId,partnerBasisPoints}` => same option/terms response.
- `GET batches?partnerUserId=UUID&page=1` => `{items:BatchSummary[],totalCount,page,pageSize:10}`.
- `GET preview?partnerUserId=UUID&page=1` => `Preview`.
- `POST batches` body `{partnerUserId,previewFingerprint,operationKey:UUID,reference,note}` => `BatchSummary`. It records independently confirmed actual transfer evidence. Negative direction is supported; no automatic fund transfer.
- `POST expenses` body `{partnerUserId,collectionId,amountIrr,paidByLevel1:boolean,reference,note,operationKey:UUID}` => `{expenseId}`. Approval is Level1-only and source receipt must belong to that partner/current frozen owner.
- `GET batch?batchId=UUID&page=1` => `BatchDetail` with ten lines.
- `GET print?batchId=UUID` => `BatchDetail` with all saved lines; caller scopes checked identically. Browser creates a dedicated printable document from this JSON, so no untrusted HTML interpolation.

`BatchSummary`: `{batchId,receiptNumber,partnerUserId,partnerDisplayName,rootDisplayName,createdAtUtc,reference,note,totalCollectedIrr,cashIrr,bankIrr,totalCostIrr,rootPaidCostIrr,totalPartnerShareIrr,totalRootShareIrr,netToRootIrr,lineCount}`. Header monetary values are incremental deltas; net may be negative, every field is immutable.

`Line`: `{collectionId,orderId,customerUserId,customerDisplayName,customerContact,planCode,planKind,invoiceUnitLimit,receivedAtUtc,receiptReference,channel,grossIrr,approvedCostIrr,rootPaidCostIrr,partnerBasisPoints:number|null,partnerShareIrr,rootShareIrr,previousNetToRootIrr,netToRootIrr,deltaNetToRootIrr,deltaCollectedIrr,deltaCostIrr,deltaPartnerShareIrr,deltaRootShareIrr}`. Monetary fields except delta* and previous* are cumulative source-receipt values. Rate is nullable only on preview of unconfigured receipts; saved lines always have it.

`Preview`: `{items:Line[],totalCount,page,pageSize:10,previewFingerprint,totalCollectedIrr,cashIrr,bankIrr,totalCostIrr,rootPaidCostIrr,totalPartnerShareIrr,totalRootShareIrr,netToRootIrr,missingRateCount,pendingGatewayCount,legacyConflictCount,canRegister}`.

`BatchDetail`: `{batch:BatchSummary,items:Line[],totalCount,page,pageSize}`.

## UI

Dedicated `partner-settlement-workspace.tsx` mounted by the parent tab. Mandatory partner selector; no register without selection. Standard compact toolbar/table/spinner/pager. History columns voucher/date/partner/new collections/cost/share/net/direction/actions. New opens modal preview with totals, terms configuration when missing, paged source lines and transfer reference/note; registration disabled for missing terms/no candidate lines/stale preview/write in flight. Each history row opens detail/print, including percentages and user/plan breakdown. Allow Level1 expense entry against a receipt in the detail/preview with explicit payer choice; refresh preview afterward.

## Scope and evidence

This is the requested source/local development implementation. No PSP activation or production action. Add migration; apply only that additive migration SQL to verified dedicated test DB after schema/pending migration review, with rollback-only fixtures for tests. Existing legacy financial rows are never rewritten or automatically migrated.
