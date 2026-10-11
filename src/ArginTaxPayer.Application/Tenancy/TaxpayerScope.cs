using ArginTaxPayer.Application.Abstractions.Tenancy;

namespace ArginTaxPayer.Application.Tenancy;

/// <summary>
/// Request/job-scoped identity container, not an access-control decision.
/// Initialization is single-shot, including if the same taxpayer ID is passed twice.
/// </summary>
public sealed class TaxpayerScope : ITaxpayerScope, ITaxpayerScopeInitializer
{
    private Guid? _taxpayerId;

    public bool IsInitialized => _taxpayerId.HasValue;

    public Guid RequireTaxpayerId() =>
        _taxpayerId ?? throw new InvalidOperationException(
            "An authorized taxpayer scope must be initialized before taxpayer-scoped operations.");

    public void Initialize(Guid taxpayerId)
    {
        if (taxpayerId == Guid.Empty)
            throw new ArgumentException("Taxpayer identifier cannot be empty.", nameof(taxpayerId));

        if (_taxpayerId.HasValue)
            throw new InvalidOperationException("A taxpayer scope cannot be initialized more than once.");

        _taxpayerId = taxpayerId;
    }
}
