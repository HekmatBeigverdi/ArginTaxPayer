using ArginTaxPayer.Application.Signing;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Fixed-schema operation/consent evidence staged in the same context transaction.
/// There is deliberately no free-form payload or caller-supplied log message.</summary>
internal static class SigningOperationAudit
{
    public static void Stage(ArginTaxPayerDbContext db, SigningOperation operation, Guid actor,
        string action, DateTime now, SigningConsent? consent = null)
    {
        db.SigningAuditEvents.Add(Event(operation, actor, 1, operation.Id,
            operation.Revision, action, now));
        if (consent is not null)
            db.SigningAuditEvents.Add(Event(operation, actor, 5, consent.Id,
                1, "consent_recorded", now));
    }

    private static SigningAuditEvent Event(SigningOperation op, Guid actor, short kind,
        Guid subject, long revision, string action, DateTime now) => new()
    {
        Id = Guid.NewGuid(), CustomerUserId = op.CustomerUserId, TaxpayerId = op.TaxpayerId,
        FiscalMemoryId = op.FiscalMemoryId, OperationRecordId = op.Id, ActorUserId = actor,
        SubjectKind = kind, SubjectId = subject, Revision = revision, ActionCode = action,
        OutcomeCode = ((SigningOperationState)op.State).ToString(), OccurredAtUtc = now
    };
}
