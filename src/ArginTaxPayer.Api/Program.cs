var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Bootstrap endpoint only. Versioned API, authorization and observability follow in later steps.
app.MapGet("/", () => Results.Ok(new { application = "ArginTaxPayer", stage = "bootstrap" }));
app.Run();

public partial class Program;
