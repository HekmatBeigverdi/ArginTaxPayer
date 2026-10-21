using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;

namespace ArginTaxPayer.Infrastructure.Signing.Lifecycle;

internal sealed class SigningKeyLifecycleService(ISigningKeyLifecycleJournal journal) : ISigningKeyLifecycleService
{
    public async Task<SigningKeyLifecycleResult> ApplyAsync(string? bearer,
        SigningKeyLifecycleCommand command, CancellationToken ct = default)
    {
        if (!Valid(command))
            return new(SigningLifecycleStatus.Invalid, command?.KeyVersionId ?? Guid.Empty);
        var authorization = await journal.AuthorizeAsync(bearer, command, ct);
        if (authorization.Status != SigningLifecycleStatus.Applied || authorization.State is null)
            return new(authorization.Status, command.KeyVersionId);
        var state = authorization.State;
        if (state.KeyVersionId != command.KeyVersionId || state.Revision != command.ExpectedKeyRevision ||
            state.CustomerUserId != command.CustomerUserId || state.TaxpayerId != command.TaxpayerId ||
            state.FiscalMemoryId != command.FiscalMemoryId)
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if (command.Action == SigningLifecycleAction.Select && !SigningKeyLifecyclePolicy.CanSelect(state.LifecycleState))
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if (command.Action == SigningLifecycleAction.Suspend && state.LifecycleState != SigningKeyLifecyclePolicy.LocallyReady)
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if ((command.Action is SigningLifecycleAction.ReportCompromised or SigningLifecycleAction.RequestRotation) &&
            state.LifecycleState is not (SigningKeyLifecyclePolicy.LocallyReady or SigningKeyLifecyclePolicy.Suspended))
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if (command.Action == SigningLifecycleAction.Resume && state.LifecycleState != SigningKeyLifecyclePolicy.Suspended)
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if (command.Action == SigningLifecycleAction.Unselect && state.SelectedKeyVersionId != command.KeyVersionId)
            return new(SigningLifecycleStatus.Conflict, command.KeyVersionId);
        if (command.Action == SigningLifecycleAction.RequestRotation && command.ReplacementKeyVersionId is not null)
            return new(SigningLifecycleStatus.Invalid, command.KeyVersionId);
        return await journal.ApplyAtomicAsync(bearer, command, state, ct);
    }

    private static bool Valid(SigningKeyLifecycleCommand? c) => c is not null &&
        c.CustomerUserId != Guid.Empty && c.TaxpayerId != Guid.Empty && c.FiscalMemoryId != Guid.Empty &&
        c.KeyVersionId != Guid.Empty && c.ExpectedKeyRevision > 0 && Enum.IsDefined(c.Action) &&
        c.Reason is { Length: > 0 and <= 1000 } && !c.Reason.Any(char.IsControl) &&
        (c.ReplacementKeyVersionId is null || c.ReplacementKeyVersionId != Guid.Empty);
}
