using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

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
public sealed class GhasedakOtpSender(HttpClient http, IConfiguration config) : IIdentityOtpSender
{
    private const string Template = "TaxPayerActivationCode";

    public async Task<bool> SendAsync(string normalizedMobile, string code, Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!config.GetValue<bool>("Ghasedak:EnableLiveSending"))
            return false;
        var key = config["Ghasedak:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) ||
            config["Ghasedak:TemplateName"] != Template ||
            !IranianMobileNumber.TryNormalize(normalizedMobile, out var mobile) ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9]{6}$"))
            return false;

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
        request.Content = JsonContent.Create(body);
        try
        {
            using var result = await http.SendAsync(request, cancellationToken);
            if (!result.IsSuccessStatusCode) return false;
            using var document = await System.Text.Json.JsonDocument.ParseAsync(
                await result.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("isSuccess", out var property)
                ? property.ValueKind == System.Text.Json.JsonValueKind.True
                : document.RootElement.TryGetProperty("IsSuccess", out var alt) &&
                  alt.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
