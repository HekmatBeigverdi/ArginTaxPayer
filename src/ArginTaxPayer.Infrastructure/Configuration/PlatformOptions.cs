namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>Public, non-secret deployment configuration. Never add credentials to this options type.</summary>
public sealed class PlatformOptions
{
    public const string SectionName = "ArginTaxPayer";

    public string DeploymentLabel { get; set; } = "local";
    public string? PublicBaseUrl { get; set; }
}
