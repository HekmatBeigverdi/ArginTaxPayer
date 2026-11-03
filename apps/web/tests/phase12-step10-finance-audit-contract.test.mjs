import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const root=new URL("../../../",import.meta.url);
const read=path=>readFileSync(new URL(path,root),"utf8");
test("Step10 audit permits approved unpaid orders while reconciling true receipts",()=>{
 const audit=read("scripts/phase12-step10-finance-audit.sql");
 assert.match(audit,/commercial_collections/);
 assert.match(audit,/collected_amount_irr<>c\.verified_irr/);
 assert.doesNotMatch(audit,/o\.status<>3/);
 assert.doesNotMatch(audit,/o\.price_version_id<>s\.price_version_id/);
 assert.match(audit,/subscription_credit_accounts/);
 assert.match(audit,/commercial_order_activation_expiries/);
});
