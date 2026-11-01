import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");

test("Admin settings link resides in account dropdown for Level1 only",()=>{
 const shell=read("app/components/console-shell.tsx");
 assert.match(shell,/roles\.includes\("Level1"\) && <Link role="menuitem" href="\/admin-settings"/);
 assert.match(shell,/href="\/account"/);
 assert.match(shell,/خروج از حساب/);
 const sidebar=shell.slice(shell.indexOf("const base:"),shell.indexOf("export default function ConsoleShell"));
 assert.doesNotMatch(sidebar,/\/admin-settings/);
});
test("Admin settings page checks server-side Level1 role",()=>{
 const page=read("app/(console)/admin-settings/page.tsx");
 assert.match(page,/currentIdentity\(\)/);
 assert.match(page,/!actor\.roles\.includes\("Level1"\)/);
 assert.match(page,/CheckoutTaxSettings/);
});
test("VAT editor removed from billing plan page",()=>{
 const workspace=read("app/subscriptions/subscription-workspace.tsx");
 assert.doesNotMatch(workspace,/CheckoutTaxSettings/);
 assert.match(workspace,/PlanCatalog/);
});
