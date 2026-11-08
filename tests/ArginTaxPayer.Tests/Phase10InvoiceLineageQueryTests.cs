using System.IO;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10InvoiceLineageQueryTests
{
    [Fact]
    public void Related_node_query_projects_the_relation_columns_read_by_the_mapper()
    {
        var source = File.ReadAllText(FindService());
        var queryStart = source.IndexOf("related.CommandText=\"\"\"", StringComparison.Ordinal);
        Assert.True(queryStart >= 0);
        var queryEnd = source.IndexOf("\"\"\";", queryStart, StringComparison.Ordinal);
        Assert.True(queryEnd > queryStart);
        var query = source[queryStart..queryEnd];

        Assert.Contains("lineage.is_production", query);
        Assert.Contains("lineage.state", query);
        Assert.Contains("next.GetBoolean(7)", source);
        Assert.Contains("next.GetInt32(8)", source);
    }

    [Fact]
    public void Related_node_query_walks_the_full_chain_with_cycle_protection()
    {
        var source = File.ReadAllText(FindService());
        var queryStart = source.IndexOf("related.CommandText=\"\"\"", StringComparison.Ordinal);
        var queryEnd = source.IndexOf("\"\"\";", queryStart, StringComparison.Ordinal);
        var query = source[queryStart..queryEnd];

        Assert.Contains("WITH RECURSIVE lineage", query);
        Assert.Contains("lineage.depth<100", query);
        Assert.Contains("NOT r.successor_invoice_id=ANY(lineage.visited)", query);
        Assert.Contains("ORDER BY lineage.depth,lineage.created_at_utc,lineage.id", query);
    }

    private static string FindService()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "ArginTaxPayer.Infrastructure",
                "Persistence", "Invoices", "Phase10InvoiceLineageReadService.cs");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("Phase10InvoiceLineageReadService.cs");
    }
}
