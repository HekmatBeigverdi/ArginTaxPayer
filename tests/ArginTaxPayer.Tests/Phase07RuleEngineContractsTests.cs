using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07RuleEngineContractsTests
{
    private static readonly DateTime Utc = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private static InvoiceRuleSource Source() => new("RC_IITP.IS", "V7.9", "Table 9 p31");

    private static InvoiceRuleContext Context(string edition = "v7.9-test", IEnumerable<Guid>? lineIds = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Utc, new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            InvoiceSubject.Original, lineIds ?? new[] { Guid.NewGuid() }, edition);

    private sealed class Rule : IInvoiceRule
    {
        public InvoiceRuleKey Key { get; }
        public InvoiceRuleSource Source { get; } = Phase07RuleEngineContractsTests.Source();
        private readonly Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> _eval;

        public Rule(string key, Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> eval)
        {
            Key = new InvoiceRuleKey(key);
            _eval = eval;
        }

        public IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context) => _eval(context);
    }

    [Fact]
    public void Stable_rule_keys_validate_and_compare_by_value()
    {
        Assert.Equal(new InvoiceRuleKey("invoice.header.issue_date"), new InvoiceRuleKey("invoice.header.issue_date"));
        foreach (var bad in new[] { "", "Header.Date", "invoice..date", ".invoice", "invoice/date", "ایراد" })
            Assert.Throws<ArgumentException>(() => new InvoiceRuleKey(bad));
        Assert.Equal("invoice.lines.amount", new InvoiceRuleKey("invoice.lines.amount").ToString());
    }

    [Fact]
    public void Source_requires_official_identifier_edition_location_and_optional_valid_hash()
    {
        var source = new InvoiceRuleSource("RC_IITP.IS", "V7.9", "Table 44 p62", new string('a', 64));
        Assert.Equal("RC_IITP.IS", source.DocumentId);
        Assert.Equal(64, source.Sha256!.Length);
        Assert.Throws<ArgumentException>(() => new InvoiceRuleSource("", "V7.9", "p62"));
        Assert.Throws<ArgumentException>(() => new InvoiceRuleSource("RC_IITP.IS", "V7.9", "p62", "bad"));
    }

    [Fact]
    public void Context_copies_line_id_sequence_and_does_not_expose_modifiable_collection()
    {
        var id = Guid.NewGuid();
        var original = new List<Guid> { id };
        var context = Context(lineIds: original);
        original.Clear();
        Assert.Equal(id, Assert.Single(context.LineIds));
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)context.LineIds).Add(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => Context(lineIds: new[] { id, id }));
        Assert.Throws<ArgumentException>(() => Context(lineIds: new[] { Guid.Empty }));
    }

    [Fact]
    public void Same_inputs_and_rules_produce_stable_result_order_even_if_registration_order_changes()
    {
        var context = Context();
        var first = new Rule("invoice.header.a", _ => new[]
        {
            new InvoiceRuleFinding(new("invoice.header.a"), InvoiceRuleSeverity.Warning, "invoice.a.warning", "header.tins"),
            new InvoiceRuleFinding(new("invoice.header.a"), InvoiceRuleSeverity.Error, "invoice.a.error", "header.tins")
        });
        var second = new Rule("invoice.lines.b", _ => new[]
        {
            new InvoiceRuleFinding(new("invoice.lines.b"), InvoiceRuleSeverity.Information, "invoice.b.info", "body[0].mu")
        });
        var results1 = new InvoiceRuleEngine("v7.9-test", new IInvoiceRule[] { second, first }).Evaluate(context);
        var results2 = new InvoiceRuleEngine("v7.9-test", new IInvoiceRule[] { first, second }).Evaluate(context);
        Assert.Equal(results1.Findings, results2.Findings);
        Assert.Equal(new[] { "invoice.header.a", "invoice.header.a", "invoice.lines.b" },
            results1.Findings.Select(x => x.RuleKey.Value));
        Assert.Equal(InvoiceRuleSeverity.Error, results1.Findings[0].Severity);
        Assert.True(results1.HasErrors);
        Assert.False(results1.IsSubmissionAuthorized);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceRuleFinding>)results1.Findings).Clear());
    }

    [Fact]
    public void Empty_rule_set_must_not_be_confused_with_official_acceptance()
    {
        var evaluation = new InvoiceRuleEngine("v7.9-test", Array.Empty<IInvoiceRule>())
            .Evaluate(Context());
        Assert.Empty(evaluation.Findings);
        Assert.False(evaluation.HasErrors);
        Assert.False(evaluation.IsSubmissionAuthorized);
    }

    [Fact]
    public void Edition_mismatch_and_duplicate_rule_keys_fail_closed()
    {
        var rule = new Rule("invoice.a", _ => Array.Empty<InvoiceRuleFinding>());
        Assert.Throws<ArgumentException>(() => new InvoiceRuleEngine(
            "v7.9-test", new IInvoiceRule[] { rule, rule }));
        Assert.Throws<InvalidOperationException>(() =>
            new InvoiceRuleEngine("another-edition", new[] { rule }).Evaluate(Context()));
    }

    [Fact]
    public void A_rule_cannot_emit_another_rules_finding_key()
    {
        var bad = new Rule("invoice.a", _ => new[]
        {
            new InvoiceRuleFinding(new("invoice.b"), InvoiceRuleSeverity.Error, "invoice.error")
        });
        Assert.Throws<InvalidOperationException>(() =>
            new InvoiceRuleEngine("v7.9-test", new[] { bad }).Evaluate(Context()));
    }

    [Fact]
    public void Exceptions_from_rules_propagate_and_never_become_success()
    {
        var throwing = new Rule("invoice.a", _ => throw new InvalidOperationException("rule failure"));
        Assert.Throws<InvalidOperationException>(() =>
            new InvoiceRuleEngine("v7.9-test", new[] { throwing }).Evaluate(Context()));
    }

    [Fact]
    public void Invalid_severity_and_unlocalized_messages_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new InvoiceRuleFinding(new("invoice.a"), (InvoiceRuleSeverity)99, "key"));
        Assert.Throws<ArgumentException>(() =>
            new InvoiceRuleFinding(new("invoice.a"), InvoiceRuleSeverity.Error, " "));
    }
}
