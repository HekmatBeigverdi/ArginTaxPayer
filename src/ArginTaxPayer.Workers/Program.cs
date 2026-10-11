using ArginTaxPayer.Application;
using ArginTaxPayer.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Process host only. Jobs, outbox consumption and retry policies follow in later steps.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
builder.Services.AddArginTaxPayerConfiguration(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddArginTaxPayerPersistence(builder.Configuration, builder.Environment.IsProduction());
using var host = builder.Build();
await host.StartAsync();
await host.WaitForShutdownAsync();
