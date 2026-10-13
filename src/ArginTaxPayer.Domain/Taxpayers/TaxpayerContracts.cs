namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>Phase 04 taxonomy. The owning administrator controls assignment, not taxpayer identity.</summary>
public enum TaxpayerKind { NaturalPerson = 1, LegalEntity = 2 }
public enum TaxpayerStatus { Active = 1, Suspended = 2, Archived = 3 }
public enum FiscalMemoryStatus { PendingVerification = 1, Active = 2, Suspended = 3, Retired = 4 }
public enum CertificateStatus { MetadataOnly = 1, Valid = 2, Expired = 3, Revoked = 4 }
public enum DelegateGrantStatus { Active = 1, Revoked = 2 }

/// <summary>Immutable contract used by the persistence/API implementation in later steps.</summary>
public sealed record TaxpayerProfileContract(
    Guid Id,
    TaxpayerKind Kind,
    string EconomicOrNationalIdentifier,
    string DisplayName,
    TaxpayerStatus Status,
    long Revision);

public sealed record FiscalMemoryContract(
    Guid Id,
    Guid TaxpayerId,
    string MemoryIdentifier,
    FiscalMemoryStatus Status);

/// <summary>Only opaque custody references are permitted; never embed private key or secret material.</summary>
public sealed record CertificateMetadataContract(
    Guid Id,
    Guid FiscalMemoryId,
    string FingerprintSha256,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? NotAfterUtc,
    CertificateStatus Status,
    string? CustodyReference);

public sealed record DelegateGrantContract(
    Guid Id,
    Guid TaxpayerId,
    Guid DelegateUserId,
    Guid GrantedByLevel1UserId,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset? RevokedAtUtc);

/// <summary>Pure transition predicates: commands must enforce permissions and concurrency separately.</summary>
public static class TaxpayerLifecyclePolicy
{
    public static bool CanTransition(TaxpayerStatus from, TaxpayerStatus to) =>
        (from, to) switch
        {
            (TaxpayerStatus.Active, TaxpayerStatus.Suspended) => true,
            (TaxpayerStatus.Suspended, TaxpayerStatus.Active) => true,
            (TaxpayerStatus.Active, TaxpayerStatus.Archived) => true,
            (TaxpayerStatus.Suspended, TaxpayerStatus.Archived) => true,
            _ => false
        };

    public static bool CanTransition(FiscalMemoryStatus from, FiscalMemoryStatus to) =>
        (from, to) switch
        {
            (FiscalMemoryStatus.PendingVerification, FiscalMemoryStatus.Active) => true,
            (FiscalMemoryStatus.PendingVerification, FiscalMemoryStatus.Retired) => true,
            (FiscalMemoryStatus.Active, FiscalMemoryStatus.Suspended) => true,
            (FiscalMemoryStatus.Suspended, FiscalMemoryStatus.Active) => true,
            (FiscalMemoryStatus.Active, FiscalMemoryStatus.Retired) => true,
            (FiscalMemoryStatus.Suspended, FiscalMemoryStatus.Retired) => true,
            _ => false
        };
}
