using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceSubjectsTests
{
    private static readonly DateTime Utc = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private const string TaxId = "A123456789012345678901"; // 22 characters

    private static InvoiceAggregate Draft(Guid? id = null,
        InvoiceSubjectSelection? selection = null) =>
        InvoiceAggregate.CreateDraft(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, Utc, Utc, new InvoicePartySnapshot(null, "فروشنده"),
            subjectSelection: selection);

    [Theory]
    [InlineData(InvoiceSubject.Original, 1)]
    [InlineData(InvoiceSubject.Correction, 2)]
    [InlineData(InvoiceSubject.Cancellation, 3)]
    [InlineData(InvoiceSubject.SalesReturn, 4)]
    public void Subject_values_match_header_ins(InvoiceSubject subject, int value) =>
        Assert.Equal(value, (int)subject);

    [Fact]
    public void Original_has_no_predecessor()
    {
        var draft = Draft();
        Assert.Equal(InvoiceSubject.Original, draft.SubjectSelection.Subject);
        Assert.Null(draft.SubjectSelection.Reference);
        Assert.Throws<ArgumentException>(() => InvoiceSubjectSelection.Create(
            InvoiceSubject.Original, new InvoiceReference(TaxId)));
    }

    [Theory]
    [InlineData(InvoiceSubject.Correction)]
    [InlineData(InvoiceSubject.Cancellation)]
    [InlineData(InvoiceSubject.SalesReturn)]
    public void Referring_subject_requires_fiscal_reference(InvoiceSubject subject)
    {
        Assert.Throws<ArgumentException>(() => InvoiceSubjectSelection.Create(subject));
        var selection = InvoiceSubjectSelection.Create(subject, new InvoiceReference(TaxId));
        Assert.Equal(TaxId, selection.Reference!.ReferenceTaxId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("A12345678901234567890!")]
    [InlineData("A12345678901234567890 ")]
    public void Invalid_reference_identifier_is_rejected(string taxId) =>
        Assert.Throws<ArgumentException>(() => new InvoiceReference(taxId));

    [Fact]
    public void Reference_rejects_empty_local_id_and_self_reference()
    {
        Assert.Throws<ArgumentException>(() => new InvoiceReference(TaxId, Guid.Empty));
        var id = Guid.NewGuid();
        var subject = InvoiceSubjectSelection.Create(InvoiceSubject.Correction,
            new InvoiceReference(TaxId, id));
        Assert.Throws<ArgumentException>(() => Draft(id, subject));
        var draft = Draft(id);
        Assert.Throws<ArgumentException>(() => draft.UpdateDraftSubject(subject));
        Assert.Equal(1, draft.Revision);
    }

    [Fact]
    public void Draft_subject_changes_are_revisable_and_idempotent()
    {
        var draft = Draft();
        var selection = InvoiceSubjectSelection.Create(InvoiceSubject.SalesReturn,
            new InvoiceReference(TaxId));
        draft.UpdateDraftSubject(selection);
        Assert.Equal(2, draft.Revision);
        Assert.Equal(InvoiceSubject.SalesReturn, draft.SubjectSelection.Subject);
        draft.UpdateDraftSubject(selection);
        Assert.Equal(2, draft.Revision);
        draft.UpdateDraftSubject(InvoiceSubjectSelection.Original());
        Assert.Equal(3, draft.Revision);
        Assert.Null(draft.SubjectSelection.Reference);
    }

    [Fact]
    public void Null_and_unknown_subjects_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => InvoiceSubjectSelection.Create((InvoiceSubject)9));
        var draft = Draft();
        Assert.Throws<ArgumentNullException>(() => draft.UpdateDraftSubject(null!));
        Assert.Equal(1, draft.Revision);
    }

    [Fact]
    public void Changing_subject_does_not_change_identity_scope_or_issue()
    {
        var draft = Draft();
        var customer = draft.CustomerUserId;
        var taxpayer = draft.TaxpayerId;
        draft.UpdateDraftSubject(InvoiceSubjectSelection.Create(InvoiceSubject.Cancellation,
            new InvoiceReference(TaxId)));
        Assert.Equal(customer, draft.CustomerUserId);
        Assert.Equal(taxpayer, draft.TaxpayerId);
        Assert.Equal(InvoiceLifecycleState.Draft, draft.State);
    }
}
