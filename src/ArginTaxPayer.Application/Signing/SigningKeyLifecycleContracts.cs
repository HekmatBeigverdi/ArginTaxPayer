namespace ArginTaxPayer.Application.Signing;

public enum SigningLifecycleAction { Select, Unselect, Suspend, ReportCompromised, RequestRotation, Resume }
public enum SigningLifecycleStatus { Applied, Invalid, Forbidden, Conflict, NotFound, IntegrityFailure, Unavailable }

public sealed record SigningKeyLifecycleCommand(
    Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId, Guid KeyVersionId,
    long ExpectedKeyRevision, SigningLifecycleAction Action, string Reason,
    Guid? ReplacementKeyVersionId = null);

public sealed record SigningKeyLifecycleResult(SigningLifecycleStatus Status, Guid KeyVersionId, long? Revision = null);

public interface ISigningKeyLifecycleService
{
    Task<SigningKeyLifecycleResult> ApplyAsync(string? bearer, SigningKeyLifecycleCommand command,
        CancellationToken ct = default);
}
