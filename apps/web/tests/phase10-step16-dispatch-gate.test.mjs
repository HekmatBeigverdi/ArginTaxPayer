import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const api = readFileSync("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs", "utf8");
const service = readFileSync("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchReadService.cs", "utf8");
const bff = readFileSync("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/route.ts", "utf8");
const startup = readFileSync("src/ArginTaxPayer.Api/Program.cs", "utf8");

test("Step16 API never registers operational authority transport", () => {
  assert.match(api, /MapGet\("\/status"/);
  assert.match(api, /MapPost\("\/send"/);
  assert.match(api, /Local\(http\)/);
  assert.match(api, /invoice_dispatch_not_enabled/);
  assert.match(api, /StatusCodes\.Status503ServiceUnavailable/);
  assert.doesNotMatch(api, /HttpClient|SendAsync\(/);
});
test("Step16 scopes every dispatch lookup to current actor and invoice owner", () => {
  assert.match(service, /scope\.ResolveAsync\(bearer/);
  assert.match(service, /scope\.ScopedQuery\(actor\.ActorId, actor\.Level1\)/);
  assert.match(service, /x\.CustomerUserId == owner\.Value/);
  assert.match(service, /customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice/);
  assert.match(service, /submissionEnabled/i);
  assert.doesNotMatch(service, /UPDATE invoice_dispatch_operations|INSERT INTO invoice_dispatch_operations/);
});
test("Step16 BFF checks session, origin and never exposes provider raw response", () => {
  assert.match(bff, /identityUiEnabled\(\)/);
  assert.match(bff, /isSameLocalOrigin\(/);
  assert.match(bff, /ACCESS_COOKIE/);
  assert.match(bff, /submissionEnabled: false/);
  assert.match(bff, /fiscalAccepted: false/);
  assert.match(bff, /Cache-Control.*no-store/);
  assert.doesNotMatch(bff, /return new NextResponse\(raw/);
});
test("Step16 endpoints registered only within development-identity guard", () => {
  const anchor = startup.indexOf("app.MapPhase10InvoiceDispatchDevelopmentEndpoints()");
  assert.ok(anchor > startup.indexOf("if (identityDevelopmentEnabled)", startup.indexOf("var app = builder.Build()")));
  assert.match(startup, /AddScoped<Phase10InvoiceDispatchReadService>/);
});
