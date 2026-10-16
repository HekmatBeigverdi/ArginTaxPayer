namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

/// <summary>Tax authority reference release; never modify activated reference rows in place.</summary>
public sealed class CatalogReferenceRelease
{
    public Guid Id { get; set; }
    public string SourceDocument { get; set; } = "";
    public string Edition { get; set; } = "";
    public string SourceSha256 { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public int State { get; set; } = 1; // Staged=1, Validated=2, Active=3, Superseded=4
    public DateTime ImportedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
}
/// <summary>Official measurement unit; this does not express a customer's per-item conversion.</summary>
public sealed class OfficialMeasurementUnit
{
    public Guid Id { get; set; }
    public Guid ReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int Status { get; set; } = 1;
}
public sealed class OfficialGoodsServiceEntry
{
    public Guid Id { get; set; }
    public Guid ReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int Kind { get; set; } // Goods=1, Service=2
    public int VerificationStatus { get; set; } = 1; // Unverified=1, Verified=2
}
public sealed class VatClassificationEntry
{
    public Guid Id { get; set; }
    public Guid ReleaseId { get; set; }
    public string Code { get; set; } = "";
    public string LegalBasis { get; set; } = "";
    public decimal? RatePercent { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
}
/// <summary>Compatible with ArginAccounting ItemKind and exact base unit semantics.</summary>
public sealed class CustomerCatalogItem
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public string InternalCode { get; set; } = "";
    public string Title { get; set; } = "";
    public int Kind { get; set; } // Stock=1, NonStock=2, Service=3
    public Guid BaseUnitId { get; set; }
    public int Status { get; set; } = 1; // Active=1, Inactive=2, Blocked=3, Discontinued=4
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
public sealed class CatalogItemUnitConversion
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid FromUnitId { get; set; }
    public long ToBaseNumerator { get; set; }
    public long ToBaseDenominator { get; set; }
    public int RoundingPolicy { get; set; } = 1; // Exact=1, HalfUp=2, RejectFraction=3
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
}
public sealed class CatalogOfficialMapping
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid OfficialEntryId { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public long Revision { get; set; } = 1;
}
