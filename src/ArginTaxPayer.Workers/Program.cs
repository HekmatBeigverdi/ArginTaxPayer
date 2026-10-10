using Microsoft.Extensions.Hosting;

// Process host only. Jobs, outbox consumption and retry policies follow in later steps.
var builder = Host.CreateApplicationBuilder(args);
using var host = builder.Build();
await host.RunAsync();
