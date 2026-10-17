namespace ArginTaxPayer.Domain.Invoices;

public enum RuleSetLookupStatus
{
    SelectedProvisional, SelectedWithEvidence, EditionNotFound,
    OutsideWindow, UnverifiedDate, IncompleteCoverage
}

/// <summary>Official source fingerprint; provenance alone is not legal applicability.</summary>
public sealed record RuleSetPrimarySource
{
    public string Document { get; }
    public string Edition { get; }
    public string Location { get; }
    public string Sha256 { get; }

    public RuleSetPrimarySource(string document, string edition, string location, string sha256)
    {
        if (string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(edition) ||
            string.IsNullOrWhiteSpace(location) || sha256 is not { Length: 64 } ||
            !sha256.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            throw new ArgumentException("Official document identity and checksum required.");
        Document = document;
        Edition = edition;
        Location = location;
        Sha256 = sha256.ToLowerInvariant();
    }
}

/// <summary>
/// A half-open UTC window. Its limits are not inferred from document month.
/// Verified dates require separately cited official evidence.
/// </summary>
public sealed record RuleSetValidity
{
    public DateTime StartUtc { get; }
    public DateTime? EndUtc { get; }
    public bool DateVerified { get; }
    public RuleSetPrimarySource? DateEvidence { get; }

    public RuleSetValidity(DateTime startUtc, DateTime? endUtc,
        bool dateVerified = false, RuleSetPrimarySource? dateEvidence = null)
    {
        if (startUtc.Kind != DateTimeKind.Utc ||
            endUtc is not null && (endUtc.Value.Kind != DateTimeKind.Utc ||
                                    endUtc.Value <= startUtc) ||
            dateVerified != (dateEvidence is not null))
            throw new ArgumentException("UTC validity and separate date evidence required.");
        StartUtc = startUtc;
        EndUtc = endUtc;
        DateVerified = dateVerified;
        DateEvidence = dateEvidence;
    }

    public bool Contains(DateTime atUtc) =>
        atUtc.Kind == DateTimeKind.Utc && atUtc >= StartUtc &&
        (EndUtc is null || atUtc < EndUtc.Value);

    public bool Overlaps(RuleSetValidity other) =>
        StartUtc < (other.EndUtc ?? DateTime.MaxValue) &&
        other.StartUtc < (EndUtc ?? DateTime.MaxValue);
}
