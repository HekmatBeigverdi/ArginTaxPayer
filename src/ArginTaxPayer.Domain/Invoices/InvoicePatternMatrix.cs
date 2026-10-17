namespace ArginTaxPayer.Domain.Invoices;

/// <summary>Official numeric type identifiers, independent from internal lifecycle states.</summary>
public enum ElectronicInvoiceType { First = 1, Second = 2, Third = 3 }

/// <summary>
/// Type/pattern selection is a classification contract, NOT authority approval.
/// Type 3 has no first/second-type pattern and is represented by null.
/// </summary>
public sealed record InvoiceTypePattern(ElectronicInvoiceType Type, int? Pattern);

public enum InvoicePatternDecision
{
    AllowedByReferencedEdition,
    UnsupportedCombination,
    MissingEdition,
    OutsideEffectiveWindow,
    UnverifiedForSubmission
}

/// <summary>Immutable provenance of an external specification or published notice.</summary>
public sealed record InvoicePatternSource(
    string Identifier, string Edition, string Location,
    DateTime EffectiveFromUtc, DateTime? EffectiveToUtc,
    bool VerifiedForSubmission)
{
    public bool IsWellFormed =>
        !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrWhiteSpace(Edition) &&
        !string.IsNullOrWhiteSpace(Location) &&
        EffectiveFromUtc.Kind == DateTimeKind.Utc &&
        (EffectiveToUtc is null || EffectiveToUtc.Value.Kind == DateTimeKind.Utc &&
         EffectiveToUtc.Value > EffectiveFromUtc);

    public bool IsEffectiveAt(DateTime atUtc) =>
        IsWellFormed && atUtc.Kind == DateTimeKind.Utc &&
        atUtc >= EffectiveFromUtc && (EffectiveToUtc is null || atUtc < EffectiveToUtc.Value);
}

/// <summary>
/// Immutable versioned matrix. It never silently falls back to another edition.
/// Future regulatory editions must be registered and source-verified separately.
/// </summary>
public sealed class InvoicePatternMatrix
{
    private readonly HashSet<InvoiceTypePattern> _entries;
    public InvoicePatternSource Source { get; }
    public IReadOnlyCollection<InvoiceTypePattern> Entries =>
        Array.AsReadOnly(_entries.OrderBy(x => (int)x.Type).ThenBy(x => x.Pattern ?? 0).ToArray());

    public InvoicePatternMatrix(InvoicePatternSource source, IEnumerable<InvoiceTypePattern> entries)
    {
        if (source is null || !source.IsWellFormed || entries is null)
            throw new ArgumentException("A valid edition and entries are required.");
        _entries = new HashSet<InvoiceTypePattern>(entries);
        if (_entries.Count == 0 || _entries.Any(x =>
            !Enum.IsDefined(x.Type) || x.Type == ElectronicInvoiceType.Third && x.Pattern is not null ||
            x.Type != ElectronicInvoiceType.Third && x.Pattern is null or <= 0))
            throw new ArgumentException("Invalid invoice type/pattern entries.");
        Source = source;
    }

    public InvoicePatternDecision Evaluate(InvoiceTypePattern selection,
        DateTime issueDateUtc, bool requireSubmissionVerified = false)
    {
        if (!Source.IsEffectiveAt(issueDateUtc))
            return InvoicePatternDecision.OutsideEffectiveWindow;
        if (!_entries.Contains(selection))
            return InvoicePatternDecision.UnsupportedCombination;
        return requireSubmissionVerified && !Source.VerifiedForSubmission
            ? InvoicePatternDecision.UnverifiedForSubmission
            : InvoicePatternDecision.AllowedByReferencedEdition;
    }
}

public sealed class InvoicePatternRegistry
{
    private readonly Dictionary<string, InvoicePatternMatrix> _editions;
    public InvoicePatternRegistry(IEnumerable<InvoicePatternMatrix> editions)
    {
        if (editions is null) throw new ArgumentNullException(nameof(editions));
        _editions = new Dictionary<string, InvoicePatternMatrix>(StringComparer.Ordinal);
        foreach (var matrix in editions)
        {
            if (!_editions.TryAdd(matrix.Source.Edition, matrix))
                throw new ArgumentException("Duplicate invoice pattern edition.");
        }
    }

    public InvoicePatternDecision Evaluate(string? edition, InvoiceTypePattern selection,
        DateTime issueDateUtc, bool requireSubmissionVerified = false) =>
        edition is null || !_editions.TryGetValue(edition, out var matrix)
            ? InvoicePatternDecision.MissingEdition
            : matrix.Evaluate(selection, issueDateUtc, requireSubmissionVerified);
}

/// <summary>
/// Dated notice No. 6 describes historical type/pattern combinations.
/// It is deliberately NOT flagged as a current validated submission rule-set.
/// </summary>
public static class HistoricalInvoicePatternMatrix
{
    public const string Edition = "notice-06-1401-11-18";
    public static InvoicePatternMatrix Create() => new(
        new InvoicePatternSource(
            "Tax Authority Notice 6 (1401/11/18)", Edition,
            "https://taksatsp.ir/اطلاعیه-شماره-6/",
            new DateTime(2023, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            null,
            VerifiedForSubmission: false),
        Enumerable.Range(1, 7)
            .Select(x => new InvoiceTypePattern(ElectronicInvoiceType.First, x))
            .Concat(Enumerable.Range(1, 2)
                .Select(x => new InvoiceTypePattern(ElectronicInvoiceType.Second, x)))
            .Append(new InvoiceTypePattern(ElectronicInvoiceType.Third, null)));
}
