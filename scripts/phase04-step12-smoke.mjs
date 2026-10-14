#!/usr/bin/env node
/**
 * Phase 04 Step 12 — authenticated, READ-ONLY API authorization smoke.
 *
 * Requires existing test accounts and migrated argintaxpayer_test DB.
 * Never prints session tokens, does not create accounts and never changes ownership.
 *
 * PHASE04_ROOT_TOKEN       Level1 session token
 * PHASE04_PARTNER_TOKEN    Level2 partner session token currently assigned to customer
 * PHASE04_OTHER_TOKEN      DIFFERENT Level2 partner (no access to selected customer)
 * PHASE04_CUSTOMER_ID      UUID of a customer assigned to PHASE04_PARTNER_TOKEN
 * PHASE04_TAXPAYER_ID     optional taxpayer UUID of that customer
 * PHASE04_LEGACY_CUSTOMER_ID optional unassigned legacy customer (root should see)
 * PHASE04_API_URL          default http://127.0.0.1:15089 (loopback required)
 */
import assert from "node:assert/strict";
import process from "node:process";

const required = ["PHASE04_ROOT_TOKEN","PHASE04_PARTNER_TOKEN","PHASE04_OTHER_TOKEN","PHASE04_CUSTOMER_ID"];
const missing = required.filter(k => !process.env[k]?.trim());
if (missing.length) {
  console.error("Missing required test variables: " + missing.join(", ") +
    "\nUse distinct Level1 and two Level2 test sessions. No database changes will be made.");
  process.exit(2);
}
const root = process.env.PHASE04_ROOT_TOKEN;
const partner = process.env.PHASE04_PARTNER_TOKEN;
const other = process.env.PHASE04_OTHER_TOKEN;
const customer = process.env.PHASE04_CUSTOMER_ID;
const taxpayer = process.env.PHASE04_TAXPAYER_ID;
const legacy = process.env.PHASE04_LEGACY_CUSTOMER_ID;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
for (const id of [customer,taxpayer,legacy].filter(Boolean)) {
  if (!uuid.test(id)) throw new Error("Invalid fixture UUID");
}
assert.notEqual(root, partner, "Level1 and Level2 must use distinct sessions");
assert.notEqual(partner, other, "Partner and stranger must use distinct sessions");
const base = new URL(process.env.PHASE04_API_URL || "http://127.0.0.1:15089");
if (!["127.0.0.1","localhost","[::1]"].includes(base.hostname) || base.protocol !== "http:")
  throw new Error("Safety guard: smoke only supports loopback HTTP Development API");

let passed = 0;
async function check(label, token, path, status, validate) {
  const res = await fetch(new URL(path, base), {
    headers: token ? { Authorization: "Bearer " + token } : {},
    cache: "no-store", redirect: "manual",
    signal: AbortSignal.timeout(8000)
  });
  assert.equal(res.status, status, label + " unexpectedly returned HTTP " + res.status);
  if (validate) validate(await res.json());
  console.log("PASS " + label + " — HTTP " + status);
  passed++;
}
function recordsArray(rows) {
  assert.ok(Array.isArray(rows), "Endpoint must return JSON array");
}
const accounts = "/api/dev/customer-owners/accounts";
const nested = "/api/dev/customer-owners/" + customer + "/taxpayers";
const owner = "/api/dev/customer-owners/" + customer;
const partners = "/api/dev/customer-owners/partners";

await check("anonymous customer directory denied", null, accounts, 401);
await check("root customer directory", root, accounts, 200, recordsArray);
await check("partner scoped customer directory", partner, accounts, 200, rows => {
  recordsArray(rows);
  assert.ok(rows.some(r => r.customerUserId === customer), "Assigned customer missing");
  assert.ok(rows.every(r => r.partnerLevel2UserId && r.partnerLevel2UserId ===
    rows[0]?.partnerLevel2UserId), "Partner list contains another owner's customer");
});
await check("unassigned Level2 directory cannot reveal target", other, accounts, 200, rows => {
  recordsArray(rows);
  assert.ok(!rows.some(r => r.customerUserId === customer), "CROSS-PARTNER CUSTOMER LEAK");
});
await check("root sees chosen customer", root, owner, 200, v => {
  assert.equal(v.customerUserId, customer);
});
await check("assigned Level2 sees chosen customer", partner, owner, 200, v => {
  assert.equal(v.customerUserId, customer);
});
await check("other Level2 cannot guess customer UUID", other, owner, 404);
await check("other Level2 cannot guess customer's taxpayer collection", other, nested, 404);
await check("assigned Level2 can list customer's taxpayers", partner, nested, 200, recordsArray);
await check("root may list customer's taxpayers", root, nested, 200, recordsArray);
await check("only root may list eligible partners", root, partners, 200, recordsArray);
await check("partner cannot enumerate eligible partners", partner, partners, 403);
await check("unknown customer UUID does not enumerate existence", partner,
  "/api/dev/customer-owners/" + crypto.randomUUID() + "/taxpayers", 404);
if (taxpayer) {
  await check("partner scoped taxpayer detail", partner, "/api/dev/taxpayers/" + taxpayer, 200);
  await check("unassigned partner taxpayer detail denied", other,
    "/api/dev/taxpayers/" + taxpayer, 404);
}
if (legacy) {
  await check("root sees legacy account after migration", root,
    "/api/dev/customer-owners/accounts", 200, rows => {
      assert.ok(rows.some(r => r.customerUserId === legacy && !r.rootLevel1UserId));
  });
}
console.log("Phase04 Step12 readonly HTTP smoke: " + passed + " checks passed. " +
  "Mutation, concurrency, UI browser and revocation checks remain separate gates.");
