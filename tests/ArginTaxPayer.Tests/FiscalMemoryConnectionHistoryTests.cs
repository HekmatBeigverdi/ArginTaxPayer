using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalMemoryConnectionHistoryTests
{
    private static readonly ConnectionBinding Binding = new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "LegacySandbox", "ABC123", 2, 7, Guid.NewGuid(), 7, new string('a', 64));

    [Fact]
    public void A_matching_binding_can_record_but_changed_owner_source_memory_environment_or_key_cannot()
    {
        Assert.True(ConnectionHistoryPolicy.CanRecord(Binding, Binding));
        ConnectionBinding[] changed = [Binding with { OwnerId = Guid.NewGuid() },
            Binding with { TaxpayerId = Guid.NewGuid() }, Binding with { MemoryId = Guid.NewGuid() },
            Binding with { Source = "RegisteredMemory" }, Binding with { Environment = 1 },
            Binding with { MemoryIdentifier = "OTHER1" }, Binding with { MemoryRevision = 8 },
            Binding with { KeyId = Guid.NewGuid() }, Binding with { KeyRevision = 8 },
            Binding with { Fingerprint = new string('b', 64) }];
        foreach (var current in changed) Assert.False(ConnectionHistoryPolicy.CanRecord(Binding, current));
        Assert.False(ConnectionHistoryPolicy.CanRecord(Binding, null));
    }

    [Fact]
    public void Latest_attempt_is_ordered_by_start_not_completion_and_keeps_prior_success_separate()
    {
        var now = DateTime.UtcNow;
        var success = new ConnectionObservation(Binding, "Authenticated", now, now.AddSeconds(30), Guid.NewGuid());
        var failure = new ConnectionObservation(Binding, "TimedOut", now.AddSeconds(5), now.AddSeconds(10), Guid.NewGuid());
        var view = ConnectionHistoryPolicy.Project(Binding, [failure, success]);
        Assert.True(view.Available);
        Assert.Equal("TimedOut", view.LastAttempt!.Status);
        Assert.Equal(success.TestedAtUtc, view.LastSuccess!.TestedAtUtc);
    }

    [Fact]
    public void Reload_never_attributes_another_sources_success_or_an_old_keys_evidence()
    {
        var now = DateTime.UtcNow;
        ConnectionObservation[] attempts = [new(Binding with { Source = "RegisteredMemory" }, "Authenticated", now, now, Guid.NewGuid()),
            new(Binding with { KeyId = Guid.NewGuid() }, "Authenticated", now, now, Guid.NewGuid())];
        var view = ConnectionHistoryPolicy.Project(Binding, attempts);
        Assert.Null(view.LastAttempt);
        Assert.Null(view.LastSuccess);
    }

    [Fact]
    public void Local_activation_revision_does_not_invalidate_same_registered_key_but_key_restore_does()
    {
        var regular = Binding with { Source = "RegisteredMemory", Environment = 1 };
        var now = DateTime.UtcNow;
        var old = new ConnectionObservation(regular, "Authenticated", now, now, Guid.NewGuid());
        Assert.NotNull(ConnectionHistoryPolicy.Project(regular with { MemoryRevision = 8 }, [old]).LastSuccess);
        Assert.Null(ConnectionHistoryPolicy.Project(regular with { KeyRevision = 8 }, [old]).LastSuccess);
    }

    [Fact]
    public void First_classification_and_activation_follow_exact_two_revision_transition_without_changing_the_probed_key()
    {
        var before=Binding with { Source="RegisteredMemory",Environment=null,MemoryRevision=1 };
        var after=before with {Environment=1,MemoryRevision=3};
        Assert.True(ConnectionHistoryPolicy.CanFollowActivation(before,after,1));
        Assert.False(ConnectionHistoryPolicy.CanFollowActivation(before,after,2));
        Assert.False(ConnectionHistoryPolicy.CanFollowActivation(before,after with {KeyId=Guid.NewGuid()},1));
        Assert.False(ConnectionHistoryPolicy.CanFollowActivation(before,after with {MemoryRevision=4},1));
    }
}
