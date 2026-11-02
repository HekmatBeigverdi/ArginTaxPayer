namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record PartnerSettlementOption(Guid PartnerUserId,string DisplayName,int? PartnerBasisPoints);
public sealed record PartnerBatchRegisterRequest(Guid PartnerUserId,string PreviewFingerprint,Guid OperationKey,string Reference,string Note);
public sealed record PartnerBatchExpenseRequest(Guid PartnerUserId,Guid CollectionId,long AmountIrr,bool PaidByLevel1,string Reference,string Note,Guid OperationKey);
public sealed record PartnerBatchSummary(Guid BatchId,string ReceiptNumber,Guid PartnerUserId,string PartnerDisplayName,string RootDisplayName,DateTime CreatedAtUtc,string Reference,string Note,long TotalCollectedIrr,long CashIrr,long BankIrr,long TotalCostIrr,long RootPaidCostIrr,long TotalPartnerShareIrr,long TotalRootShareIrr,long NetToRootIrr,int LineCount);
public sealed record PartnerBatchLine(Guid CollectionId,Guid OrderId,Guid CustomerUserId,string CustomerDisplayName,string CustomerContact,string PlanCode,int PlanKind,long? InvoiceUnitLimit,DateTime ReceivedAtUtc,string ReceiptReference,int Channel,long GrossIrr,long ApprovedCostIrr,long RootPaidCostIrr,int? PartnerBasisPoints,long PartnerShareIrr,long RootShareIrr,long PreviousNetToRootIrr,long NetToRootIrr,long DeltaNetToRootIrr,long DeltaCollectedIrr,long DeltaCostIrr,long DeltaPartnerShareIrr,long DeltaRootShareIrr);
public sealed record PartnerBatchPreview(PartnerBatchLine[] Items,int TotalCount,int Page,int PageSize,string PreviewFingerprint,long TotalCollectedIrr,long CashIrr,long BankIrr,long TotalCostIrr,long RootPaidCostIrr,long TotalPartnerShareIrr,long TotalRootShareIrr,long NetToRootIrr,int MissingRateCount,int PendingGatewayCount,int LegacyConflictCount,bool CanRegister);
public sealed record PartnerBatchPage(PartnerBatchSummary[] Items,int TotalCount,int Page,int PageSize);
public sealed record PartnerBatchDetail(PartnerBatchSummary Batch,PartnerBatchLine[] Items,int TotalCount,int Page,int PageSize);
