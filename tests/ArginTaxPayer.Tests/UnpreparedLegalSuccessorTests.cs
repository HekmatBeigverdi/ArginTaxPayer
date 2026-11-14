using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class UnpreparedLegalSuccessorTests
{
    [Theory]
    [InlineData(InvoiceSubject.Correction)]
    [InlineData(InvoiceSubject.Cancellation)]
    [InlineData(InvoiceSubject.SalesReturn)]
    public void Resume_requires_an_active_finalized_Production_legal_document_without_any_dispatch_or_serial(InvoiceSubject subject)
    {
        Assert.True(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Production,true,false,false));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Production,true,true,false));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Production,true,false,true));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Production,false,false,false));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Draft,TaxAuthorityEnvironment.Production,true,false,false));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(subject,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Sandbox,true,false,false));
        Assert.False(Phase18UnpreparedLegalSuccessorService.CanResume(InvoiceSubject.Original,InvoiceLifecycleState.Finalized,TaxAuthorityEnvironment.Production,true,false,false));
    }
    [Fact]
    public void Invoice_timestamp_normalizes_UTC_only_and_preserves_database_precision()
    {
        var raw=new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc).AddTicks(1234567);
        var normalized=InvoiceTimestamp.TruncateToMicroseconds(raw);
        Assert.Equal(0,normalized.Ticks%10);
        Assert.Equal(raw.AddTicks(-7),normalized);
        Assert.Throws<ArgumentException>(()=>InvoiceTimestamp.TruncateToMicroseconds(DateTime.SpecifyKind(raw,DateTimeKind.Local)));
    }
}
