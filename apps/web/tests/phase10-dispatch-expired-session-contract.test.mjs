import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
test("dispatch UI distinguishes identity 401 from authority submission failure",()=>{
 const source=readFileSync(resolve(root,"apps/web/app/invoices/invoice-send-readiness-panel.tsx"),"utf8");
 assert.ok(source.includes("response.status===401"));
 assert.ok(source.includes("نشست ورود منقضی شده است"));
 assert.ok(source.includes('dispatch?.status==="Indeterminate"'));
});
