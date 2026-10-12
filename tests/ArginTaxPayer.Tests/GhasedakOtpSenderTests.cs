using System.Net;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class GhasedakOtpSenderTests
{
    [Fact]
    public async Task SendingIsDisabledByDefault()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler);
        var sender = new GhasedakOtpSender(client, new ConfigurationBuilder().Build());
        Assert.False(await sender.SendAsync("+989123456789", "123456", Guid.NewGuid()));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task LiveOptInUsesExpectedTemplateParamAndApiKey()
    {
        var handler = new RecordingHandler();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Ghasedak:EnableLiveSending"] = "true",
                ["Ghasedak:ApiKey"] = "TEST_NOT_A_REAL_KEY",
                ["Ghasedak:TemplateName"] = "TaxPayerActivationCode"
            }).Build();
        var sender = new GhasedakOtpSender(new HttpClient(handler), settings);
        var id = Guid.NewGuid();
        Assert.True(await sender.SendAsync("+989123456789", "654321", id));
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("https://gateway.ghasedak.me/rest/api/v1/WebService/SendOtpWithParams",
            handler.Url);
        Assert.Equal("TEST_NOT_A_REAL_KEY", handler.ApiKey);
        Assert.Contains("TaxPayerActivationCode", handler.Body);
        Assert.Contains("654321", handler.Body);
        Assert.Contains("09123456789", handler.Body);
        Assert.Contains(id.ToString("N"), handler.Body);
        Assert.DoesNotContain("Param2", handler.Body);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? Url { get; private set; }
        public string? ApiKey { get; private set; }
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Url = request.RequestUri?.ToString();
            ApiKey = request.Headers.GetValues("ApiKey").SingleOrDefault();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"isSuccess\":true}", Encoding.UTF8, "application/json")
            };
        }
    }
}
