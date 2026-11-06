using System.IO;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ProductionLegalSuccessorAdmissionTests
{
    [Fact]
    public void Production_reservation_guard_admits_successors_only_through_persisted_relation()
    {
        var migration = File.ReadAllText(Find("20261106010000_AddProductionLegalSuccessorRelations.cs"));
        Assert.Contains("authority_invoice_legal_relations", migration);
        Assert.Contains("authority_invoice_correction_workspaces", migration);
        Assert.Contains("subject integer NOT NULL CHECK(subject IN (2,3,4))", migration);
        Assert.Contains("lr.successor_invoice_id=i.id", migration);
        Assert.Contains("lr.state IN (1,2,4)", migration);
    }

    [Fact]
    public void Production_preparation_keeps_original_branch_and_requires_relation_for_successor()
    {
        var source = File.ReadAllText(Find("Phase18ProductionInvoicePreparationService.cs"));
        Assert.DoesNotContain("production_legal_successor_not_supported", source);
        Assert.Contains("authority_invoice_legal_relations", source);
        Assert.Contains("production_legal_relation_not_verified", source);
    }

    private static string Find(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            foreach (var relative in new[]
            {
                Path.Combine("src", "ArginTaxPayer.Infrastructure", "Persistence", "Migrations", name),
                Path.Combine("src", "ArginTaxPayer.Infrastructure", "Persistence", "Invoices", name)
            })
            {
                var candidate = Path.Combine(current.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
            current = current.Parent;
        }
        throw new FileNotFoundException(name);
    }
}
