import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");

test("Customer cash checkout is server-owned and fail-closed for VAT/PSP",()=>{
 const backend=read("app/api/customer-checkout/route.ts");
 const page=read("app/customer-plans/customer-plans.tsx");
 assert.match(backend,/ACCESS_COOKIE/);
 assert.match(backend,/identityApiOrigin/);
 assert.match(backend,/invalid_origin/);
 assert.match(page,/wantsOfficialInvoice:false/);
 assert.match(page,/!cash/);
 assert.match(page,/official/);
 assert.match(page,/customer-checkout/);
});
test("Customer only access to checkout page",()=>{
 const s=read("app/(console)/customer-plans/page.tsx");
 assert.match(s,/currentIdentity/);
 assert.match(s,/roles.includes\("Customer"\)/);
});
test("Subscription view derives from authenticated identity",()=>{
 const source=read("app/api/customer-subscription/route.ts");
 assert.match(source,/ACCESS_COOKIE/);
 assert.match(source,/customer-overview\/me/);
});
