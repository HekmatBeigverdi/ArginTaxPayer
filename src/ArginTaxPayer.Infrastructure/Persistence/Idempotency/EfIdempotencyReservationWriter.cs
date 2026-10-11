using ArginTaxPayer.Application.Abstractions.Idempotency;
using ArginTaxPayer.Application.Abstractions.Tenancy;

namespace ArginTaxPayer.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Stages a reservation on the current DbContext. Must be saved in the same
/// transaction as the corresponding database business action.
/// </summary>
public sealed class EfIdempotencyReservationWriter(
    ArginTaxPayerDbContext dbContext,
    ITaxpayerScope taxpayerScope) : IIdempotencyReservationWriter
{
    public Task ReserveAsync(
        Guid reservationId,
        string operation,
        string key,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (reservationId == Guid.Empty)
            throw new ArgumentException("Reservation ID must not be empty.", nameof(reservationId));
        if (string.IsNullOrWhiteSpace(operation) || operation.Length > 100)
            throw new ArgumentException("Operation must contain 1-100 characters.", nameof(operation));
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
            throw new ArgumentException("Idempotency key must contain 1-200 characters.", nameof(key));
        if (requestFingerprint is null || requestFingerprint.Length != 64 || !requestFingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("Fingerprint must be a SHA-256 hexadecimal digest.", nameof(requestFingerprint));

        var taxpayerId = taxpayerScope.RequireTaxpayerId();
        dbContext.IdempotencyReservations.Add(new IdempotencyReservation
        {
            Id = reservationId,
            TaxpayerId = taxpayerId,
            Operation = operation,
            Key = key,
            RequestFingerprint = requestFingerprint.ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
