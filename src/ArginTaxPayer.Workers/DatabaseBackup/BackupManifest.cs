namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record BackupManifest(
    Guid ArtifactId,
    DateTime CreatedAtUtc,
    string DatabaseName,
    string SchemaIdentity,
    string EncryptionKeyVersion,
    bool ArchiveValidated,
    string Sha256 = "",
    long EncryptedBytes = 0);

public sealed record BackupArtifactMetadata(
    Guid ArtifactId,
    DateTime CreatedAtUtc,
    string DatabaseName,
    string SchemaIdentity,
    string EncryptionKeyVersion,
    long EncryptedBytes,
    string Sha256,
    bool ArchiveValidated,
    bool ChecksumMatches,
    string FilePath,
    string ManifestPath);
