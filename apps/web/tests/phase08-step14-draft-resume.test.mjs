import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
test("draft edit resumes after the persisted completed step",()=>{
 assert.match(ui,/saved\.lastCompletedStep\+1/);
 assert.match(ui,/setSavedSnapshot\(draftSnapshot\)/);
});
test("unsaved changes are confirmed before modal dismissal and navigation",()=>{
 assert.match(ui,/const unsavedChanges = modal/);
 assert.match(ui,/window\.confirm\("تغییرات ذخیره‌نشده/);
 assert.match(ui,/window\.addEventListener\("beforeunload"/);
});
test("server keeps revision CAS and client explains concurrent modification",()=>{
 assert.match(service,/x\.Revision == input\.ExpectedRevision/);
 assert.match(ui,/نسخه این پیش‌نویس در محل دیگری تغییر کرده است/);
});
