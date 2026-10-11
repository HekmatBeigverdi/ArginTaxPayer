namespace ArginTaxPayer.Application.Contracts;

/// <summary>Cross-module communication contract, not a delivered or durable event bus.</summary>
/// <param name="EventId">Unique identifier for eventual at-least-once delivery deduplication.</param>
/// <param name="EventType">Versioned event contract name.</param>
/// <param name="OccurredAtUtc">UTC event timestamp.</param>
/// <param name="CorrelationId">Trace identifier shared across boundary calls.</param>
/// <param name="WorkspaceId">Optional scoped workspace identifier; never grants access by itself.</param>
public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid? WorkspaceId);
