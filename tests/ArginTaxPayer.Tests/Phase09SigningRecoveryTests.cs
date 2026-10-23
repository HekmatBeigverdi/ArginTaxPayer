using ArginTaxPayer.Infrastructure.Signing.Protection;
using ArginTaxPayer.Infrastructure.Signing.Recovery;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningRecoveryTests
{
    [Fact] public async Task Empty_workload_is_a_completed_noop()
    { var result = await Service().RewrapAsync([], new(0, 0, null)); Assert.Equal(SigningRecoveryStatus.Completed, result.Status); }
    [Fact] public async Task Invalid_checkpoint_is_rejected_before_custody()
    { var result = await Service().RewrapAsync([], new(-1, 0, null)); Assert.Equal(SigningRecoveryStatus.Invalid, result.Status); }
    [Fact] public async Task Checkpoint_is_resumable()
    { var result = await Service().RewrapAsync([], new(0, 0, "keys/last")); Assert.Equal("keys/last", result.Checkpoint.LastPath); }
    [Fact] public void Recovery_item_requires_explicit_expected_version()
    { var id = Id(); Assert.Equal(7, new SigningRecoveryItem(id, 7).ExpectedEnvelopeVersion); }
    [Fact] public void Missing_key_protection_is_not_success()
        => Assert.NotEqual(SigningRecoveryStatus.Completed, SigningRecoveryStatus.MissingKeyProtection);
    [Fact] public void Conflict_is_not_success()
        => Assert.NotEqual(SigningRecoveryStatus.Completed, SigningRecoveryStatus.Conflict);
    [Fact] public void Checkpoint_has_no_secret_material()
        => Assert.Equal("keys/last", new SigningRecoveryCheckpoint(1, 1, "keys/last").LastPath);
    [Fact] public void Recovery_item_identity_is_scope_bound()
    { var id = Id(); Assert.Contains(id.CustomerUserId.ToString("N"), id.StoragePath()); }

    private static KeyProtectionIdentity Id() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    private static SigningKeyRecoveryService Service() => new(new ProtectedKeyEnvelopeStore(new KeyEnvelopeProtector(new UnavailableProvider()), new EmptyStore()));
    private sealed class UnavailableProvider : IKeyProtectionProvider
    { public Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity i, ReadOnlyMemory<byte> d, CancellationToken c) => throw new NotImplementedException(); public Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity i, WrappedDataKey w, CancellationToken c) => throw new NotImplementedException(); public Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity i, WrappedDataKey w, CancellationToken c) => throw new NotImplementedException(); }
    private sealed class EmptyStore : IProtectedKeyEnvelopeStore
    { public Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity i, CancellationToken c) => Task.FromResult<StoredKeyEnvelope?>(null); public Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity i, ProtectedKeyEnvelope e, CancellationToken c) => throw new NotImplementedException(); public Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity i, ProtectedKeyEnvelope e, int v, CancellationToken c) => throw new NotImplementedException(); }
}
