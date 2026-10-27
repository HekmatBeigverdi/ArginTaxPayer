import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const root = new URL("../../../", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");
const api = read("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
const service = read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchReadService.cs");
const bff = read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/route.ts");
const startup = read("src/ArginTaxPayer.Api/Program.cs");

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
  assert.match(service, /FROM invoice_dispatch_operations d\s+WHERE d\.customer_user_id=@owner AND d\.taxpayer_id=@taxpayer AND d\.invoice_id=@invoice/);
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
