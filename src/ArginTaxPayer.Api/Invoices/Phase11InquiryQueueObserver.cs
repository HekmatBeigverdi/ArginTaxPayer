using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Api.Invoices;

/// <summary>
/// Phase 11 read-only queue observer. This is intentionally NOT the Phase 10
/// sending worker and must never invoke its ProcessInvoiceAsync method.
/// The later inquiry phase will add separately authorized inquiry processing.
/// </summary>
internal sealed class Phase11InquiryQueueObserver(
    IServiceScopeFactory scopes, ILogger<Phase11InquiryQueueObserver> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // No external requests, no invoice state mutation, and no sensitive
                // exception serialization. The next tick can safely retry observing.
                logger.LogWarning("Phase11 queue observation unavailable: {ErrorType}",
                    exception.GetType().Name);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ObserveAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArginTaxPayerDbContext>();
        var database = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (database != "argintaxpayer_test") return;

        // The Phase 10 schema stores 5=Acknowledged and 6=Indeterminate.
        // Read only: no receipt, outbox or operation state update.
        var pending = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::integer AS "Value"
            FROM invoice_dispatch_operations
            WHERE state IN (5,6)
            """).SingleAsync(cancellationToken);
        logger.LogInformation("Phase11 outstanding inquiry candidates: {Count}", pending);
    }
}
