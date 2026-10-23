using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Lifecycle;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningKeyLifecycleTests
{
    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 2, false)]
    [InlineData(5, 2, false)]
    public void Lifecycle_policy_has_terminal_compromised_and_superseded_states(short from, short to, bool allowed) =>
        Assert.Equal(allowed, SigningKeyLifecyclePolicy.CanTransition(from, to));

    [Fact]
    public async Task Invalid_reason_or_revision_is_rejected_before_journal()
    {
        var journal = new Journal();
        var service = new SigningKeyLifecycleService(journal);
        var command = Command(SigningLifecycleAction.Suspend, 0, "\nsecret");
        var result = await service.ApplyAsync("token", command);
        Assert.Equal(SigningLifecycleStatus.Invalid, result.Status);
        Assert.Equal(0, journal.AuthorizeCalls);
        var invalidAction = Command((SigningLifecycleAction)99, 4, "safe reason");
        Assert.Equal(SigningLifecycleStatus.Invalid, (await service.ApplyAsync("token", invalidAction)).Status);
        Assert.Equal(0, journal.AuthorizeCalls);
    }

    [Fact]
    public async Task Selected_key_can_only_be_unselected_with_matching_selection()
    {
        var journal = new Journal { State = State() with { SelectedKeyVersionId = null } };
        var result = await new SigningKeyLifecycleService(journal).ApplyAsync("token",
            Command(SigningLifecycleAction.Unselect, 4, "owner stop"));
        Assert.Equal(SigningLifecycleStatus.Conflict, result.Status);
        Assert.Equal(0, journal.ApplyCalls);
    }

    [Fact]
    public async Task Expected_revision_is_forwarded_to_atomic_lifecycle_update()
    {
        var journal = new Journal();
        var result = await new SigningKeyLifecycleService(journal).ApplyAsync("token",
            Command(SigningLifecycleAction.Suspend, 4, "safety stop"));
        Assert.Equal(SigningLifecycleStatus.Applied, result.Status);
        Assert.Equal(1, journal.ApplyCalls);
        Assert.Equal(4, journal.LastCommand!.ExpectedKeyRevision);
    }

    [Fact]
    public async Task Authorization_denial_never_reaches_atomic_update()
    {
        var journal = new Journal { Authorization = SigningLifecycleStatus.Forbidden };
        var result = await new SigningKeyLifecycleService(journal).ApplyAsync("token",
            Command(SigningLifecycleAction.ReportCompromised, 4, "compromise report"));
        Assert.Equal(SigningLifecycleStatus.Forbidden, result.Status);
        Assert.Equal(0, journal.ApplyCalls);
    }

    [Fact]
    public async Task Selection_requires_locally_ready_state()
    {
        var journal = new Journal { State = State() with { LifecycleState = SigningKeyLifecyclePolicy.Suspended } };
        var result = await new SigningKeyLifecycleService(journal).ApplyAsync("token",
            Command(SigningLifecycleAction.Select, 4, "select current key"));
        Assert.Equal(SigningLifecycleStatus.Conflict, result.Status);
        Assert.Equal(0, journal.ApplyCalls);
    }

    private static SigningKeyLifecycleCommand Command(SigningLifecycleAction action, long revision, string reason) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), revision, action, reason);

    private static LifecycleKeyState State() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        SigningKeyLifecyclePolicy.LocallyReady, 4, Guid.NewGuid());

    private sealed class Journal : ISigningKeyLifecycleJournal
    {
        public SigningLifecycleStatus Authorization = SigningLifecycleStatus.Applied;
        public LifecycleKeyState State = null!;
        public int AuthorizeCalls, ApplyCalls;
        public SigningKeyLifecycleCommand? LastCommand;
        public Task<(SigningLifecycleStatus Status, LifecycleKeyState? State)> AuthorizeAsync(string? bearer,
            SigningKeyLifecycleCommand command, CancellationToken ct)
        {
            AuthorizeCalls++;
            var state = State ?? new LifecycleKeyState(command.KeyVersionId, command.CustomerUserId,
                command.TaxpayerId, command.FiscalMemoryId, SigningKeyLifecyclePolicy.LocallyReady, 4, command.KeyVersionId);
            return Task.FromResult((Authorization, Authorization == SigningLifecycleStatus.Applied ? state : null));
        }
        public Task<SigningKeyLifecycleResult> ApplyAtomicAsync(string? bearer, SigningKeyLifecycleCommand command,
            LifecycleKeyState state, CancellationToken ct)
        {
            ApplyCalls++; LastCommand = command;
            return Task.FromResult(new SigningKeyLifecycleResult(SigningLifecycleStatus.Applied,
                command.KeyVersionId, command.ExpectedKeyRevision + 1));
        }
    }
}
