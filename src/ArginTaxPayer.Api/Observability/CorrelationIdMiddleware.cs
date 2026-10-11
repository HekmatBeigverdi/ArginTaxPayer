using System.Diagnostics;
using System.Security.Cryptography;

namespace ArginTaxPayer.Api.Observability;

/// <summary>
/// Per-request correlation and minimal structured access logging.
/// Never records query strings, bodies, cookies, auth headers or exception content.
/// Untrusted request-supplied IDs are accepted only when strictly validated.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var received = context.Request.Headers[HeaderName];
        var correlationId = received.Count == 1 && IsValid(received[0])
            ? received[0]!
            : Generate();

        context.Response.Headers[HeaderName] = correlationId;
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var timer = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            timer.Stop();
            logger.LogInformation(
                "HTTP request completed: {Method} {Path} {StatusCode} in {ElapsedMilliseconds} ms (CorrelationId: {CorrelationId})",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                timer.ElapsedMilliseconds,
                correlationId);
        }
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength)
            return false;

        foreach (var character in value)
        {
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                or >= '0' and <= '9' or '-' or '_'))
                return false;
        }
        return true;
    }

    private static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
