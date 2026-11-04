using System.Data;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Authoritative, fail-closed per-operation authority scope. This reads the
/// immutable dispatch operation and fiscal memory from PostgreSQL. It does not
/// accept a caller's environment label, choose a URL, sign, or send an invoice.
/// Historical dispatch operations with NULL environment cannot pass this gate.
/// </summary>
public sealed class Phase18PersistedAuthorityOperationGate(ArginTaxPayerDbContext db)
{
    public async Task<AuthorityOperationBinding?> ResolveAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || invoiceId == Guid.Empty)
            return null;

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT d.fiscal_memory_id, d.authority_environment, m.authority_environment
FROM invoice_dispatch_operations d
JOIN fiscal_memories m ON m.id=d.fiscal_memory_id AND m.taxpayer_id=d.taxpayer_id
JOIN taxpayer_profiles t ON t.id=d.taxpayer_id
JOIN invoice_documents i ON i.id=d.invoice_id
  AND i.taxpayer_id=d.taxpayer_id AND i.customer_user_id=d.customer_user_id
WHERE d.id=@operation AND d.customer_user_id=@customer
  AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
  AND t.customer_user_id=@customer
  AND m.status=2
  AND EXISTS (SELECT 1 FROM customer_taxpayer_memberships c
      WHERE c.customer_user_id=@customer AND c.taxpayer_id=@taxpayer
      AND c.unlinked_at_utc IS NULL)
LIMIT 1
""";
            foreach (var (name, value) in new (string, Guid)[] {
                ("operation", operationId), ("customer", customerUserId),
                ("taxpayer", taxpayerId), ("invoice", invoiceId)
            })
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.IsDBNull(1) || reader.IsDBNull(2))
                return null;
            var memoryId = reader.GetGuid(0);
            var operationEnvironment = FromPersistedValue(reader.GetInt32(1));
            var memoryEnvironment = FromPersistedValue(reader.GetInt32(2));
            if (operationEnvironment == TaxAuthorityEnvironment.Unknown ||
                memoryEnvironment != operationEnvironment)
                return null;
            var operation = new AuthorityOperationBinding(
                customerUserId, taxpayerId, invoiceId, memoryId, operationEnvironment);
            return Phase18AuthorityRoutingPolicy.CanUseMemory(operation,
                operation with { Environment = memoryEnvironment }) ? operation : null;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    public async Task<bool> CanUseProviderAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment registeredProvider, CancellationToken ct = default)
    {
        var operation = await ResolveAsync(operationId, customerUserId, taxpayerId, invoiceId, ct);
        return operation is { } binding &&
            Phase18AuthorityRoutingPolicy.CanUseProvider(
                binding, binding, registeredProvider);
    }

    internal static TaxAuthorityEnvironment FromPersistedValue(int value) =>
        Phase18PersistedAuthorityEnvironmentResolver.ToAuthorityEnvironment(
            Enum.IsDefined(typeof(FiscalConnectionEnvironment), value)
                ? (FiscalConnectionEnvironment)value : null);
}
