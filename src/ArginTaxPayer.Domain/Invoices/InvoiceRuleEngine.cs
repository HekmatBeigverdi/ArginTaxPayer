using System.Collections.ObjectModel;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase07 Step07 contracts only. This is NOT a catalogue of legally complete
/// validation rules. Primary future rule evidence: RC_IITP.IS V7.9; consult
/// docs/references/tax-authority-sdk-catalog.md before adding actual rules.
/// </summary>
public enum InvoiceRuleSeverity { Information = 1, Warning = 2, Error = 3 }

/// <summary>Stable internal rule key, e.g. invoice.header.issue_date.required.</summary>
public sealed record InvoiceRuleKey
{
    public string Value { get; }
    public InvoiceRuleKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value[0] is < 'a' or > 'z' ||
            value.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_')) ||
            value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid stable rule identifier.", nameof(value));
        Value = value;
    }
    public override string ToString() => Value;
}

/// <summary>
/// The immutable basis of a *specific* rule edition. Publication timestamp
/// does not imply legal effective date or approval for submission.
/// </summary>
public sealed record InvoiceRuleSource
{
    public string DocumentId { get; }
    public string Edition { get; }
    public string Section { get; }
    public string? Sha256 { get; }

    public InvoiceRuleSource(string documentId, string edition, string section,
        string? sha256 = null)
    {
        if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(edition) ||
            string.IsNullOrWhiteSpace(section) ||
            sha256 is not null && (sha256.Length != 64 ||
                sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))))
            throw new ArgumentException("Rule source requires document/edition/location and valid optional hash.");
        DocumentId = documentId;
        Edition = edition;
        Section = section;
        Sha256 = sha256;
    }
}

/// <summary>
/// A frozen evaluation context; it cannot be mutated by a rule. Customer and
/// taxpayer IDs describe scope but do NOT replace API-layer authorization.
/// </summary>
public sealed class InvoiceRuleContext
{
    public Guid InvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public long Revision { get; }
    public DateTime IssueDateUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubject Subject { get; }
    public IReadOnlyList<Guid> LineIds { get; }
    public string RuleSetEdition { get; }
    public InvoiceHeaderFields? Header { get; }
    public DateTime AsOfUtc { get; }

    public InvoiceRuleContext(Guid invoiceId, Guid customerUserId, Guid taxpayerId,
        long revision, DateTime issueDateUtc, InvoiceTypePattern classification,
        InvoiceSubject subject, IEnumerable<Guid> lineIds, string ruleSetEdition,
        InvoiceHeaderFields? header = null, DateTime? asOfUtc = null)
    {
        if (invoiceId == Guid.Empty || customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            revision < 1 || issueDateUtc.Kind != DateTimeKind.Utc ||
            classification is null || !Enum.IsDefined(classification.Type) ||
            classification.Type == ElectronicInvoiceType.Third && classification.Pattern is not null ||
            classification.Type != ElectronicInvoiceType.Third && classification.Pattern is null or <= 0 ||
            !Enum.IsDefined(subject) || lineIds is null ||
            string.IsNullOrWhiteSpace(ruleSetEdition) ||
            (asOfUtc is not null && asOfUtc.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Invalid invoice rule context.");
        var frozen = lineIds.ToArray();
        if (frozen.Any(id => id == Guid.Empty) || frozen.Distinct().Count() != frozen.Length)
            throw new ArgumentException("Invalid invoice line identities.");
        InvoiceId = invoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        Revision = revision;
        IssueDateUtc = issueDateUtc;
        Classification = classification;
        Subject = subject;
        LineIds = Array.AsReadOnly(frozen);
        RuleSetEdition = ruleSetEdition;
        Header = header;
        // Caller injects evaluation clock: never read DateTime.UtcNow in a rule.
        // Missing clock does not imply current legal validity.
        AsOfUtc = asOfUtc ?? issueDateUtc;
    }
}

/// <summary>
/// MessageKey is a stable localization key, never uncontrolled tax authority
/// text or a formatted message containing tenant/customer data.
/// </summary>
public sealed record InvoiceRuleFinding
{
    public InvoiceRuleKey RuleKey { get; }
    public InvoiceRuleSeverity Severity { get; }
    public string MessageKey { get; }
    public string? FieldPath { get; }

    public InvoiceRuleFinding(InvoiceRuleKey ruleKey, InvoiceRuleSeverity severity,
        string messageKey, string? fieldPath = null)
    {
        ArgumentNullException.ThrowIfNull(ruleKey);
        if (!Enum.IsDefined(severity) || string.IsNullOrWhiteSpace(messageKey) ||
            messageKey.Length > 160 || messageKey.Any(char.IsControl) ||
            fieldPath is not null && (fieldPath.Length > 160 || fieldPath.Any(char.IsControl)))
            throw new ArgumentException("Invalid rule finding.");
        RuleKey = ruleKey;
        Severity = severity;
        MessageKey = messageKey;
        FieldPath = fieldPath;
    }
}

public interface IInvoiceRule
{
    InvoiceRuleKey Key { get; }
    InvoiceRuleSource Source { get; }
    IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context);
}

public sealed record InvoiceRuleEvaluation(
    string Edition, IReadOnlyList<InvoiceRuleFinding> Findings)
{
    public bool HasErrors => Findings.Any(x => x.Severity == InvoiceRuleSeverity.Error);
    // Step07 is purely local contract evaluation; legal submission readiness
    // additionally requires full, verified and effective rules in Step12.
    public bool IsSubmissionAuthorized => false;
}

/// <summary>
/// Pure and deterministic orchestration when supplied rules are pure.
/// Registered rules must never perform I/O or depend on wall-clock time.
/// A rule exception is deliberately propagated (fail closed, no approval).
/// </summary>
public sealed class InvoiceRuleEngine
{
    private readonly IReadOnlyList<IInvoiceRule> _rules;
    public string Edition { get; }

    public InvoiceRuleEngine(string edition, IEnumerable<IInvoiceRule> rules)
    {
        if (string.IsNullOrWhiteSpace(edition) || rules is null)
            throw new ArgumentException("A named rule-set edition and rules are required.");
        var snapshot = rules.ToArray();
        if (snapshot.Any(x => x is null || x.Key is null || x.Source is null) ||
            snapshot.GroupBy(x => x.Key.Value, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Rule identifiers must be unique and non-null.");
        Edition = edition;
        _rules = Array.AsReadOnly(snapshot.OrderBy(x => x.Key.Value, StringComparer.Ordinal).ToArray());
    }

    public InvoiceRuleEvaluation Evaluate(InvoiceRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.RuleSetEdition, Edition, StringComparison.Ordinal))
            throw new InvalidOperationException("Rule-set edition mismatch.");

        var findings = new List<InvoiceRuleFinding>();
        foreach (var rule in _rules)
        {
            var returned = rule.Evaluate(context)
                ?? throw new InvalidOperationException("A rule returned null findings.");
            foreach (var finding in returned)
            {
                if (finding is null || finding.RuleKey != rule.Key)
                    throw new InvalidOperationException("A rule returned invalid or foreign-key findings.");
                findings.Add(finding);
            }
        }
        var ordered = findings
            .OrderBy(x => x.RuleKey.Value, StringComparer.Ordinal)
            .ThenBy(x => x.FieldPath ?? "", StringComparer.Ordinal)
            .ThenByDescending(x => x.Severity)
            .ThenBy(x => x.MessageKey, StringComparer.Ordinal)
            .ToArray();
        return new InvoiceRuleEvaluation(Edition, Array.AsReadOnly(ordered));
    }
}
