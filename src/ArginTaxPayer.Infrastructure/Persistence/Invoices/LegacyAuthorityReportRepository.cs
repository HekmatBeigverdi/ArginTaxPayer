using System.Data;
using System.Data.Common;
using System.Text.Json;
using ArginTaxPayer.Application.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record StoredLegacyAuthorityReport(Guid Id, Guid OperationId,
    string ReferenceNumber, LegacyAuthorityInquiryReport Report);

public sealed class LegacyAuthorityReportRepository(ArginTaxPayerDbContext db)
{
    public async Task<bool> AppendAsync(Guid owner, Guid taxpayer, Guid invoice,
        Guid operation, string reference, LegacyAuthorityInquiryReport report, CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            using var command=connection.CreateCommand();
            command.CommandText="""
INSERT INTO sandbox_authority_inquiry_reports
(id,operation_id,customer_user_id,taxpayer_id,invoice_id,authority_reference,report_json,observed_at_utc)
SELECT @id,d.id,d.customer_user_id,d.taxpayer_id,d.invoice_id,@reference,CAST(@report AS jsonb),@observed
FROM invoice_dispatch_operations d
WHERE d.id=@operation AND d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
 AND d.invoice_id=@invoice AND d.state=5
 AND EXISTS(SELECT 1 FROM invoice_dispatch_receipts r WHERE r.operation_id=d.id
  AND r.receipt_kind=1 AND r.authority_reference=@reference)
""";
            Add(command,"id",Guid.NewGuid());Add(command,"operation",operation);
            Add(command,"owner",owner);Add(command,"taxpayer",taxpayer);Add(command,"invoice",invoice);
            Add(command,"reference",reference);Add(command,"report",JsonSerializer.Serialize(report));
            Add(command,"observed",report.ObservedAtUtc);
            return await command.ExecuteNonQueryAsync(ct)==1;
        }
        finally {if(opened)await connection.CloseAsync();}
    }

    public async Task<StoredLegacyAuthorityReport?> ReadAsync(Guid owner,Guid taxpayer,
        Guid invoice,CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            using var command=connection.CreateCommand();
            command.CommandText="""
SELECT r.id,r.operation_id,r.authority_reference,r.report_json::text
FROM sandbox_authority_inquiry_reports r
JOIN invoice_dispatch_operations d ON d.id=r.operation_id AND d.customer_user_id=r.customer_user_id
 AND d.taxpayer_id=r.taxpayer_id AND d.invoice_id=r.invoice_id
WHERE r.customer_user_id=@owner AND r.taxpayer_id=@taxpayer AND r.invoice_id=@invoice
 AND d.state=5
ORDER BY r.observed_at_utc DESC,r.id DESC LIMIT 1
""";
            Add(command,"owner",owner);Add(command,"taxpayer",taxpayer);Add(command,"invoice",invoice);
            using var reader=await command.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))return null;
            var report=JsonSerializer.Deserialize<LegacyAuthorityInquiryReport>(reader.GetString(3));
            return report is null?null:new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),report);
        }
        finally {if(opened)await connection.CloseAsync();}
    }

    internal static void Add(DbCommand command,string name,object value)
    {
        var parameter=command.CreateParameter();parameter.ParameterName=name;
        parameter.Value=value;command.Parameters.Add(parameter);
    }
}
