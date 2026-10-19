import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const server=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
test("catalog unit and code are copied to the draft and restored",()=>{
  assert.match(ui,/unitId:item\?\.baseUnitId/);
  assert.match(ui,/catalogCodeSnapshot:item\?\.internalCode/);
  assert.match(ui,/unitId:line\.unitId/);
  assert.match(server,/CatalogCodeSnapshot/);
  assert.match(server,/Lines = canonicalLines\.ToArray\(\)/);
});
test("server enforces catalog ownership and immutable code and unit snapshots",()=>{
  assert.match(server,/c\.CustomerUserId == access\.Item2/);
  assert.match(server,/item\.Status != 1 && previous is null/);
  assert.match(server,/previous\?\.UnitId \?\? item\.BaseUnitId/);
  assert.match(server,/previous\?\.CatalogCodeSnapshot \?\? item\.InternalCode/);
  assert.match(server,/requestedUnit != unit/);
});
