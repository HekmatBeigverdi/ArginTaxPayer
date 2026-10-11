using ArginTaxPayer.Application;
using ArginTaxPayer.Infrastructure;
using Microsoft.Extensions.Hosting;

// Process host only. Jobs, outbox consumption and retry policies follow in later steps.
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
using var host = builder.Build();
await host.RunAsync();
