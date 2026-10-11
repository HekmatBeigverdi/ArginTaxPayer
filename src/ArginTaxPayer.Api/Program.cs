using ArginTaxPayer.Api.Health;
using ArginTaxPayer.Application;
using ArginTaxPayer.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
builder.Services.AddArginTaxPayerConfiguration(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddArginTaxPayerPersistence(builder.Configuration, builder.Environment.IsProduction());

// Separate cheap process liveness from real PostgreSQL readiness.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();

// Preserve the existing bootstrap endpoint.
app.MapGet("/", () => Results.Ok(new { application = "ArginTaxPayer", stage = "bootstrap" }));

// Minimal responses by design: never return raw connection strings or exceptions.
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(context.Response.Body,
        new { status = report.Status.ToString() },
        cancellationToken: context.RequestAborted);
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

app.Run();

public partial class Program { }
