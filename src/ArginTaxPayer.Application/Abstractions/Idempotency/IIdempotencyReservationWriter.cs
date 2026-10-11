namespace ArginTaxPayer.Application.Abstractions.Idempotency;

/// <summary>
/// Adds a validated reservation to the current Unit of Work. Persistence and
/// database uniqueness, not this interface, decide conflicting requests.
/// This is not an external-operation execution or replay service.
/// </summary>
public interface IIdempotencyReservationWriter
{
    Task ReserveAsync(
        Guid reservationId,
        string operation,
        string key,
        string requestFingerprint,
        CancellationToken cancellationToken = default);
}
