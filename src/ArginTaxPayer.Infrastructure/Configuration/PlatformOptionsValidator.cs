using Microsoft.Extensions.Options;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>Fail closed on unsafe production public URLs; credentials are never embedded in settings.</summary>
public sealed class PlatformOptionsValidator(bool isProduction) : IValidateOptions<PlatformOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DeploymentLabel) ||
            options.DeploymentLabel.Length > 64)
        {
            return ValidateOptionsResult.Fail("ArginTaxPayer:DeploymentLabel must contain 1 to 64 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            return isProduction
                ? ValidateOptionsResult.Fail("ArginTaxPayer:PublicBaseUrl is required in Production.")
                : ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail("ArginTaxPayer:PublicBaseUrl must be an absolute HTTP(S) URL without credentials, query, or fragment.");
        }

        if (isProduction && uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail("ArginTaxPayer:PublicBaseUrl must use HTTPS in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
