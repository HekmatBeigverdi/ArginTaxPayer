import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const css=readFileSync(new URL("../app/(console)/dashboard/dashboard-design.css",import.meta.url),"utf8");
const page=readFileSync(new URL("../app/(console)/dashboard/page.tsx",import.meta.url),"utf8");

test("phase15 design keeps existing Argin design tokens and RTL isolated",()=>{
 assert.match(css,/--argin-primary/);
 assert.match(css,/direction:rtl/);
 assert.match(css,/\.argin-dashboard__card/);
 assert.doesNotMatch(css,/(^|\n)body\s*\{/);
 assert.doesNotMatch(css,/(^|\n):root\s*\{/);
});
test("design covers responsive four-two-one KPI layout and chart collapse",()=>{
 assert.match(css,/repeat\(4,minmax\(0,1fr\)\)/);
 assert.match(css,/max-width:1100px/);
 assert.match(css,/max-width:640px/);
 assert.match(css,/max-width:380px/);
 assert.match(css,/\.argin-dashboard__charts/);
});
test("design includes accessible focus, reduced motion and explicit loading/error/empty states",()=>{
 assert.match(css,/:focus-visible/);
 assert.match(css,/prefers-reduced-motion:reduce/);
 for(const name of ["empty","error","loading","status","table-wrap","filters"])
   assert.ok(css.includes(".argin-dashboard__"+name));
});
test("step05 does not prematurely replace the existing dashboard or operational links",()=>{
 assert.match(page,/SubscriptionStatus/);
 assert.match(page,/admin \? adminModules : customerModules/);
 assert.match(page,/href: "\/invoices"/);
});
