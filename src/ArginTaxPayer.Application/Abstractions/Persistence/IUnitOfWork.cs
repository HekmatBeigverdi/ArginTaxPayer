namespace ArginTaxPayer.Application.Abstractions.Persistence;

/// <summary>
/// Scoped persistence boundary. Operations in a transaction callback may run
/// more than once after a transient database failure; callers must keep them
/// database-only and safe to retry (never send external messages/payments here).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs database-only work with EF Core's configured transient-fault strategy.
    /// Saves pending changes and commits atomically on success; exceptions roll back.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
