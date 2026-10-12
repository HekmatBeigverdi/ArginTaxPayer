using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

public interface IIdentityOtpSender
{
    Task<bool> SendAsync(string normalizedMobile, string code, Guid deliveryId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ghasedak OTP adapter for .NET 9; live sending is explicitly opt-in.
/// This adapter never logs passwords, OTP values, API keys or raw provider messages.
/// </summary>
public sealed class GhasedakOtpSender(HttpClient http, IConfiguration config, ILogger<GhasedakOtpSender> logger) : IIdentityOtpSender
{
    private const string Template = "TaxPayerActivationCode";

    public async Task<bool> SendAsync(string normalizedMobile, string code, Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!config.GetValue<bool>("Ghasedak:EnableLiveSending"))
        {
            logger.LogInformation("OTP_SMS_SKIPPED reason=live_sending_disabled");
            return false;
        }
        var key = config["Ghasedak:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("OTP_SMS_CONFIG_INVALID reason=api_key_missing");
            return false;
        }
        if (config["Ghasedak:TemplateName"] != Template)
        {
            logger.LogWarning("OTP_SMS_CONFIG_INVALID reason=template_mismatch");
            return false;
        }
        if (!IranianMobileNumber.TryNormalize(normalizedMobile, out var mobile) ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9]{6}$"))
        {
            logger.LogWarning("OTP_SMS_REJECTED reason=invalid_input");
            return false;
        }

        var body = new
        {
            TemplateName = Template,
            Param1 = code,
            Receptors = new[] { new {
                Mobile = "0" + mobile[3..],
                ClientReferenceId = deliveryId.ToString("N")
            } }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://gateway.ghasedak.me/rest/api/v1/WebService/SendOtpWithParams");
        request.Headers.TryAddWithoutValidation("ApiKey", key);
        request.Headers.TryAddWithoutValidation("Agent", "C#");
        // The referenced Ghasedak Framework DTO serializes PascalCase field names.
        request.Content = JsonContent.Create(body, options: new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.General));
        try
        {
            using var result = await http.SendAsync(request, cancellationToken);
            if (!result.IsSuccessStatusCode)
            {
                logger.LogWarning("OTP_SMS_PROVIDER_REJECTED httpStatus={HttpStatus}", (int)result.StatusCode);
                return false;
            }
            using var document = await System.Text.Json.JsonDocument.ParseAsync(
                await result.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var success = document.RootElement.TryGetProperty("isSuccess", out var property)
                ? property.ValueKind == System.Text.Json.JsonValueKind.True
                : document.RootElement.TryGetProperty("IsSuccess", out var alt) &&
                  alt.ValueKind == System.Text.Json.JsonValueKind.True;
            if (success) logger.LogInformation("OTP_SMS_PROVIDER_ACCEPTED");
            else logger.LogWarning("OTP_SMS_PROVIDER_REJECTED reason=response_not_success");
            return success;
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("OTP_SMS_TRANSPORT_FAILED reason=network");
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OTP_SMS_TRANSPORT_FAILED reason=timeout");
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            logger.LogWarning("OTP_SMS_PROVIDER_REJECTED reason=invalid_json");
            return false;
        }
    }
}
