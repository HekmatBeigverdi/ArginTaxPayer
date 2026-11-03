using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Preflight for user feedback; the authoritative reservation runs inside BeginSend's transaction.</summary>
public sealed class InvoiceSubscriptionGate(ArginTaxPayerDbContext db)
{
    public async Task<string?> CheckAsync(Guid customerId, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try { return await EvaluateAsync(connection, null, customerId, null, DateTime.UtcNow, ct); }
        finally { if (opened) await connection.CloseAsync(); }
    }

    internal static async Task<string?> ReserveAsync(DbConnection connection, DbTransaction tx,
        Guid customerId, Guid operationId, CancellationToken ct)
    {
        // Same customer lock as commercial activation; concurrent sends cannot spend the last unit twice.
        await using var customer = Command(connection, tx,
            "SELECT id FROM identity_users WHERE id=@customer FOR UPDATE", ("customer", customerId));
        if (await customer.ExecuteScalarAsync(ct) is null) return "subscription_required";
        return await EvaluateAsync(connection, tx, customerId, operationId, DateTime.UtcNow, ct);
    }

    private static async Task<string?> EvaluateAsync(DbConnection connection, DbTransaction? tx,
        Guid customerId, Guid? operationId, DateTime now, CancellationToken ct)
    {
        var subscriptions = new List<(Guid Id, SubscriptionPlanKind Kind, SubscriptionLifecycle State,
            DateTime? Activated, DateTime? Expires)>();
        await using (var command = Command(connection, tx, """
            SELECT id,kind,state,activated_at_utc,expires_at_utc FROM customer_subscriptions
            WHERE customer_user_id=@customer AND activated_at_utc IS NOT NULL
            ORDER BY activated_at_utc DESC,id DESC
            """ + (tx is null ? "" : " FOR UPDATE"), ("customer", customerId)))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                subscriptions.Add((reader.GetGuid(0), (SubscriptionPlanKind)reader.GetInt32(1),
                    (SubscriptionLifecycle)reader.GetInt32(2), reader.GetDateTime(3),
                    reader.IsDBNull(4) ? null : reader.GetDateTime(4)));

        string? blocked = null;
        foreach (var sub in subscriptions)
        {
            long? available = null;
            var alreadyReserved = false;
            if (sub.Kind == SubscriptionPlanKind.Quantity)
            {
                await using var balance = Command(connection, tx, """
                    SELECT available_units FROM subscription_credit_accounts
                    WHERE subscription_id=@sub AND customer_user_id=@customer
                    """ + (tx is null ? "" : " FOR UPDATE"), ("sub", sub.Id), ("customer", customerId));
                available = await balance.ExecuteScalarAsync(ct) as long?;
                if (operationId is { } operation)
                {
                    await using var prior = Command(connection, tx, """
                        SELECT 1 FROM subscription_credit_entries
                        WHERE subscription_id=@sub AND customer_user_id=@customer
                          AND operation_key=@key AND operation_type='reserve'
                        """, ("sub", sub.Id), ("customer", customerId), ("key", ReserveKey(operation)));
                    alreadyReserved = await prior.ExecuteScalarAsync(ct) is not null;
                }
            }
            now = DateTime.UtcNow; // Recheck after any balance-row lock wait.
            var code = InvoiceSubscriptionPolicy.BlockingCode(sub.Kind, sub.State, sub.Activated,
                sub.Expires, alreadyReserved ? 1 : available, now);
            if (code is not null) { blocked ??= code; continue; }
            if (operationId is { } id && sub.Kind == SubscriptionPlanKind.Quantity && !alreadyReserved)
            {
                await using var update = Command(connection, tx, """
                    UPDATE subscription_credit_accounts SET available_units=available_units-1,
                        reserved_units=reserved_units+1,revision=revision+1
                    WHERE subscription_id=@sub AND customer_user_id=@customer AND available_units>0
                    """, ("sub", sub.Id), ("customer", customerId));
                if (await update.ExecuteNonQueryAsync(ct) != 1) return "subscription_exhausted";
                await AppendAsync(connection, tx!, customerId, sub.Id, id, "reserve", now, ct);
            }
            return null;
        }
        // A previously purchased/activated plan never falls back to the free trial on expiry.
        if (subscriptions.Count > 0) return blocked ?? "subscription_required";
        await using var trial = Command(connection, tx, """
            SELECT COUNT(DISTINCT d.id) FROM invoice_dispatch_operations d
            JOIN invoice_dispatch_attempts a ON a.operation_id=d.id
            WHERE d.customer_user_id=@customer
            """, ("customer", customerId));
        var used = Convert.ToInt64(await trial.ExecuteScalarAsync(ct));
        return used < TrialSubscriptionPolicy.FreeConfirmedInvoiceLimit ? null : "subscription_required";
    }

    // This runs with the receipt transaction, even if the subscription expired while the request was in flight.
    internal static async Task ConsumeAsync(DbConnection connection, DbTransaction tx,
        Guid customerId, Guid operationId, DateTime now, CancellationToken ct)
    {
        Guid? subscriptionId;
        await using (var reservation = Command(connection, tx, """
            SELECT subscription_id FROM subscription_credit_entries
            WHERE customer_user_id=@customer AND operation_key=@key AND operation_type='reserve'
            """, ("customer", customerId), ("key", ReserveKey(operationId))))
            subscriptionId = await reservation.ExecuteScalarAsync(ct) as Guid?;
        if (subscriptionId is not { } sub) return; // Timed, trial or historical send.
        await using var prior = Command(connection, tx, """
            SELECT 1 FROM subscription_credit_entries WHERE subscription_id=@sub AND operation_key=@key
            """, ("sub", sub), ("key", ConsumeKey(operationId)));
        if (await prior.ExecuteScalarAsync(ct) is not null) return;
        await using var update = Command(connection, tx, """
            UPDATE subscription_credit_accounts SET reserved_units=reserved_units-1,
                consumed_units=consumed_units+1,revision=revision+1
            WHERE subscription_id=@sub AND customer_user_id=@customer AND reserved_units>0
            """, ("sub", sub), ("customer", customerId));
        if (await update.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("Invoice credit reservation is missing.");
        await AppendAsync(connection, tx, customerId, sub, operationId, "consume", now, ct);
    }

    private static string ReserveKey(Guid operation) => "invoice-reserve-" + operation.ToString("N");
    private static string ConsumeKey(Guid operation) => "invoice-consume-" + operation.ToString("N");

    private static async Task AppendAsync(DbConnection connection, DbTransaction tx, Guid customer,
        Guid subscription, Guid operation, string action, DateTime now, CancellationToken ct)
    {
        await using var entry = Command(connection, tx, """
            INSERT INTO subscription_credit_entries
                (id,subscription_id,customer_user_id,operation_key,operation_type,reservation_key,units,recorded_at_utc)
            VALUES(@id,@sub,@customer,@key,@action,@reservation,1,@now)
            """, ("id", Guid.NewGuid()), ("sub", subscription), ("customer", customer),
            ("key", action == "reserve" ? ReserveKey(operation) : ConsumeKey(operation)),
            ("action", action), ("reservation", "invoice-" + operation.ToString("N")), ("now", now));
        await entry.ExecuteNonQueryAsync(ct);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? tx, string sql,
        params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
