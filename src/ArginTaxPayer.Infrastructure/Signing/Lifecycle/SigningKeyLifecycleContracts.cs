using ArginTaxPayer.Application.Signing;

namespace ArginTaxPayer.Infrastructure.Signing.Lifecycle;

internal sealed record LifecycleKeyState(Guid KeyVersionId, Guid CustomerUserId, Guid TaxpayerId,
    Guid FiscalMemoryId, short LifecycleState, long Revision, Guid? SelectedKeyVersionId);

internal interface ISigningKeyLifecycleJournal
{
    Task<(SigningLifecycleStatus Status, LifecycleKeyState? State)> AuthorizeAsync(
        string? bearer, SigningKeyLifecycleCommand command, CancellationToken ct);
    Task<SigningKeyLifecycleResult> ApplyAtomicAsync(string? bearer, SigningKeyLifecycleCommand command,
        LifecycleKeyState state, CancellationToken ct);
}
