import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/invoice-history-panel.tsx",import.meta.url),"utf8");
const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
test("Jalali history uses Tehran time and actor display name",()=>{
 assert.match(ui,/fa-IR-u-ca-persian/);
 assert.match(ui,/Asia\/Tehran/);
 assert.match(ui,/actorDisplayName/);
 assert.doesNotMatch(ui,/<td><bdi dir="ltr">\{item\.actorUserId\}/);
});
test("history ordered newest first on backend and in UI",()=>{
 assert.match(service,/OrderByDescending\(h=>h\.OccurredAtUtc\)/);
 assert.match(service,/ThenByDescending\(h=>h\.Revision\)/);
 assert.match(ui,/Date\.parse\(b\.occurredAtUtc\)-Date\.parse\(a\.occurredAtUtc\)/);
 assert.match(service,/db\.SoftwareUserProfiles/);
});
