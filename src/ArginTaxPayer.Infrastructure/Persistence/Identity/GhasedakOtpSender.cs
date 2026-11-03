using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

public interface IIdentityOtpSender
{
    Task<bool> SendAsync(string normalizedMobile, string code, string purpose, Guid deliveryId,
        CancellationToken cancellationToken = default);
    async Task<SmsProviderSendResult> SendWithCostAsync(string normalizedMobile,string code,string purpose,
        Guid deliveryId,CancellationToken cancellationToken=default)
    { var accepted=await SendAsync(normalizedMobile,code,purpose,deliveryId,cancellationToken);return new(accepted,false,false,null,null,null,null,null); }
}

public sealed record SmsProviderSendResult(bool Accepted, bool Ambiguous, bool LocalOnly,
    decimal? RawCost, string? CostUnit, long? CostIrr, string? MessageId, DateTime? SentAtUtc);

/// <summary>
/// Ghasedak OTP adapter for .NET 9; live sending is explicitly opt-in.
/// This adapter never logs passwords, OTP values, API keys or raw provider messages.
/// </summary>
public sealed class GhasedakOtpSender(HttpClient http, IConfiguration config, ILogger<GhasedakOtpSender> logger) : IIdentityOtpSender
{
    private const decimal MaxPersistableRawCost = 9999999999999999.9999m;
    private const string RegistrationTemplate = "TaxPayerActivationCode";
    private const string LoginTemplate = "TaxPayerLoginCode";

    public async Task<bool> SendAsync(string normalizedMobile,string code,string purpose,Guid deliveryId,CancellationToken cancellationToken=default)
        => (await SendWithCostAsync(normalizedMobile,code,purpose,deliveryId,cancellationToken)).Accepted;

    public async Task<SmsProviderSendResult> SendWithCostAsync(string normalizedMobile, string code, string purpose, Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!config.GetValue<bool>("Ghasedak:EnableLiveSending"))
        {
            logger.LogInformation("OTP_SMS_SKIPPED reason=live_sending_disabled");
            return new(false,false,false,null,null,null,null,null);
        }
        var key = config["Ghasedak:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("OTP_SMS_CONFIG_INVALID reason=api_key_missing");
            return new(false,false,false,null,null,null,null,null);
        }
        var template = purpose == "login" ? LoginTemplate : RegistrationTemplate;
        if (purpose is not ("register" or "login" or "reset") ||
            config["Ghasedak:TemplateName"] != RegistrationTemplate)
        {
            logger.LogWarning("OTP_SMS_CONFIG_INVALID reason=template_mismatch");
            return new(false,false,false,null,null,null,null,null);
        }
        if (!IranianMobileNumber.TryNormalize(normalizedMobile, out var mobile) ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9]{6}$"))
        {
            logger.LogWarning("OTP_SMS_REJECTED reason=invalid_input");
            return new(false,false,false,null,null,null,null,null);
        }

        // Keep live sending constrained by default. Development operators may
        // explicitly disable this gate when testing an authorized number.
        if (config.GetValue("Ghasedak:RestrictToTestRecipient", true) &&
            (!IranianMobileNumber.TryNormalize(config["Ghasedak:TestRecipientMobile"],
                out var permittedMobile) || mobile != permittedMobile))
        {
            logger.LogWarning("OTP_SMS_REJECTED reason=test_recipient_not_allowed");
            return new(false,false,false,null,null,null,null,null);
        }

        var body = new
        {
            TemplateName = template,
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
                var status=(int)result.StatusCode;
                return new(false,status>=500||status is >=300 and <400,false,null,null,null,null,null);
            }
            using var document = await System.Text.Json.JsonDocument.ParseAsync(
                await result.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var success = document.RootElement.TryGetProperty("isSuccess", out var property)
                ? property.ValueKind == System.Text.Json.JsonValueKind.True
                : document.RootElement.TryGetProperty("IsSuccess", out var alt) &&
                  alt.ValueKind == System.Text.Json.JsonValueKind.True;
            if (!success) { logger.LogWarning("OTP_SMS_PROVIDER_REJECTED reason=response_not_success"); return new(false,false,false,null,null,null,null,null); }
            var item = FindFirstItem(document.RootElement);
            var rawCost = ReadDecimal(item,"Cost","cost");
            var costUnit = config["Ghasedak:CostUnit"]?.Trim().ToUpperInvariant();
            long? costIrr = null;
            if(rawCost is not null && (costUnit is "IRR" or "TOMAN"))
            {
                var multiplier=costUnit=="TOMAN"?10m:1m;
                if(rawCost.Value<=MaxPersistableRawCost && rawCost.Value<=long.MaxValue/multiplier)
                    costIrr=(long)Math.Ceiling(rawCost.Value*multiplier);
            }
            var messageId = ReadString(item,"MessageId","messageId","messageID");
            var sentAt = ReadDate(item,"SendDate","sendDate");
            logger.LogInformation("OTP_SMS_PROVIDER_ACCEPTED cost_reported={HasCost}", rawCost is not null);
            return new(true,false,false,rawCost,costUnit,costIrr,messageId,sentAt);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("OTP_SMS_TRANSPORT_FAILED reason=network");
            return new(false,true,false,null,null,null,null,null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OTP_SMS_TRANSPORT_FAILED reason=timeout");
            return new(false,true,false,null,null,null,null,null);
        }
        catch (System.Text.Json.JsonException)
        {
            logger.LogWarning("OTP_SMS_PROVIDER_REJECTED reason=invalid_json");
            return new(false,true,false,null,null,null,null,null);
        }
    }

    private static System.Text.Json.JsonElement FindFirstItem(System.Text.Json.JsonElement root)
    {
        foreach (var key in new[] { "Data", "data" })
            if (root.TryGetProperty(key, out var data))
                foreach (var itemKey in new[] { "Items", "items" })
                    if (data.TryGetProperty(itemKey, out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array && items.GetArrayLength() > 0)
                        return items[0];
        return default;
    }
    private static decimal? ReadDecimal(System.Text.Json.JsonElement item, params string[] keys)
    {
        foreach(var key in keys) if(item.ValueKind==System.Text.Json.JsonValueKind.Object&&item.TryGetProperty(key,out var value))
            if(value.ValueKind==System.Text.Json.JsonValueKind.Number&&value.TryGetDecimal(out var number)&&number>=0)return number;
        return null;
    }
    private static string? ReadString(System.Text.Json.JsonElement item, params string[] keys)
    { foreach(var key in keys)if(item.ValueKind==System.Text.Json.JsonValueKind.Object&&item.TryGetProperty(key,out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String)return value.GetString();return null; }
    private static DateTime? ReadDate(System.Text.Json.JsonElement item, params string[] keys)
    { var value=ReadString(item,keys);return DateTimeOffset.TryParse(value,out var date)?date.UtcDateTime:null; }
}
