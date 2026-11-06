using System.Data;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase18InvoiceMemoryBindingResult(
    InvoiceApiDecision Decision, string Code, Guid Owner = default,
    Guid MemoryId = default, TaxAuthorityEnvironment Environment = TaxAuthorityEnvironment.Unknown,
    long Revision = 0, InvoiceLifecycleState State = default, bool LegacySandbox = false);

/// <summary>Resolves a selected immutable invoice memory against its live owner and persisted authority.</summary>
public sealed class Phase18InvoiceMemoryBindingService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope,
    Phase18PersistedAuthorityEnvironmentResolver environments)
{
    public async Task<Phase18InvoiceMemoryBindingResult> ResolveAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty)
            return new(InvoiceApiDecision.Invalid, "invalid_invoice_id");
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated, "unauthenticated");
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden, "forbidden");
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(t => t.Id == taxpayerId).Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty)
            return new(InvoiceApiDecision.NotFound, "invoice_not_found");
        if (!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m =>
            m.CustomerUserId == owner.Value && m.TaxpayerId == taxpayerId && m.UnlinkedAtUtc == null, ct))
            return new(InvoiceApiDecision.Forbidden, "membership_inactive");
        var invoice = await db.InvoiceDocuments.AsNoTracking().Where(i =>
            i.Id == invoiceId && i.TaxpayerId == taxpayerId && i.CustomerUserId == owner.Value)
            .Select(i => new { i.SnapshotJson, i.State, i.Revision }).SingleOrDefaultAsync(ct);
        if (invoice is null) return new(InvoiceApiDecision.NotFound, "invoice_not_found");
        if (!TryReadMemory(invoice.SnapshotJson, out var memory, out var legacySandbox, out var selectedEnvironment))
            return new(InvoiceApiDecision.Invalid, "invoice_memory_binding_invalid");
        var environment = TaxAuthorityEnvironment.Unknown;
        if (legacySandbox)
        {
            var conn = db.Database.GetDbConnection();
            var opened = conn.State != ConnectionState.Open;
            if (opened) await conn.OpenAsync(ct);
            try
            {
                await using var command = conn.CreateCommand();
                command.CommandText = """
SELECT EXISTS(SELECT 1 FROM sandbox_fiscal_memories
WHERE id=@memory AND taxpayer_id=@taxpayer AND customer_user_id=@owner)
""";
                foreach (var (name, value) in new[] { ("memory", memory), ("taxpayer", taxpayerId), ("owner", owner.Value) })
                {
                    var p = command.CreateParameter(); p.ParameterName = name; p.Value = value;
                    command.Parameters.Add(p);
                }
                if (await command.ExecuteScalarAsync(ct) is true) environment = TaxAuthorityEnvironment.Sandbox;
            }
            finally { if (opened) await conn.CloseAsync(); }
        }
        else
            environment = await environments.ResolveAsync(owner.Value, taxpayerId, invoiceId, memory, ct);
        // A modern memory is classified by its live persisted row. The frozen
        // snapshot label must agree and cannot relabel or redirect that memory.
        if (environment == TaxAuthorityEnvironment.Unknown || environment != selectedEnvironment)
            return new(InvoiceApiDecision.Invalid, "invoice_memory_environment_mismatch");
        return new(InvoiceApiDecision.Success, "invoice_memory_bound", owner.Value, memory,
            environment, invoice.Revision, invoice.State, legacySandbox);
    }

    internal static bool TryReadMemory(string raw, out Guid memoryId, out bool legacySandbox) =>
        TryReadMemory(raw, out memoryId, out legacySandbox, out _);

    internal static bool TryReadMemory(string raw, out Guid memoryId, out bool legacySandbox,
        out TaxAuthorityEnvironment selectedEnvironment)
    {
        memoryId = default; legacySandbox = false; selectedEnvironment = TaxAuthorityEnvironment.Unknown;
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("environment", out var env) || env.ValueKind != JsonValueKind.String)
                return false;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name)) return false;
            bool Read(string name, out Guid value)
            {
                value = default;
                return root.TryGetProperty(name, out var field) && field.ValueKind != JsonValueKind.Null &&
                    field.ValueKind == JsonValueKind.String && field.TryGetGuid(out value) && value != Guid.Empty;
            }
            var production = Read("fiscalMemoryId", out var realId);
            var sandbox = Read("sandboxFiscalMemoryId", out var sandboxId);
            bool HasValue(string name) => root.TryGetProperty(name, out var field) && field.ValueKind != JsonValueKind.Null;
            if (production && !HasValue("sandboxFiscalMemoryId") && env.GetString() is "Production" or "Sandbox")
            {
                memoryId = realId;
                selectedEnvironment = env.GetString() == "Production" ? TaxAuthorityEnvironment.Production : TaxAuthorityEnvironment.Sandbox;
                return true;
            }
            if (sandbox && !HasValue("fiscalMemoryId") && env.GetString() == "Sandbox")
            { memoryId = sandboxId; legacySandbox = true; selectedEnvironment = TaxAuthorityEnvironment.Sandbox; return true; }
            return false;
        }
        catch (JsonException) { return false; }
    }
}
