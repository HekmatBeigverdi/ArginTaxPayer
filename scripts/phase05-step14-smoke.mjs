#!/usr/bin/env node
/**
 * Phase05 Step14 READ-ONLY, authenticated Development API smoke.
 * Real Level1 + two distinct Level2 sessions; one active completed customer assigned
 * to Level2-A under the chosen Level1; buyer and seller belonging to that customer.
 * Does NOT create/transfer/archive anything and never displays bearer tokens.
 *
 * PHASE05_ROOT_TOKEN, PHASE05_PARTNER_TOKEN, PHASE05_OTHER_TOKEN,
 * PHASE05_CUSTOMER_ID, PHASE05_BUYER_ID, PHASE05_TAXPAYER_ID (all required).
 * PHASE05_API_URL defaults to http://127.0.0.1:15089 (loopback only).
 * Run again AFTER an audited Level1 transfer to Level2-B with PARTNER/OTHER
 * tokens swapped to assert the new owner sees the customer.
 */
import assert from "node:assert/strict";

const vars = ["PHASE05_ROOT_TOKEN", "PHASE05_PARTNER_TOKEN", "PHASE05_OTHER_TOKEN",
  "PHASE05_CUSTOMER_ID", "PHASE05_BUYER_ID", "PHASE05_TAXPAYER_ID"];
const missing = vars.filter(name => !process.env[name]?.trim());
if (missing.length) {
  console.error("Missing Step14 smoke variables: " + missing.join(", ") +
    ". Use existing test users; no account or database mutations occur.");
  process.exit(2);
}
const root = process.env.PHASE05_ROOT_TOKEN;
const assigned = process.env.PHASE05_PARTNER_TOKEN;
const outsider = process.env.PHASE05_OTHER_TOKEN;
const customer = process.env.PHASE05_CUSTOMER_ID;
const buyer = process.env.PHASE05_BUYER_ID;
const seller = process.env.PHASE05_TAXPAYER_ID;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
for (const value of [customer, buyer, seller])
  assert.ok(uuid.test(value), "Invalid test fixture UUID");
assert.notEqual(root, assigned);
assert.notEqual(assigned, outsider);
assert.notEqual(root, outsider);

const url = new URL(process.env.PHASE05_API_URL ?? "http://127.0.0.1:15089");
if (url.protocol !== "http:" || !["localhost", "127.0.0.1", "[::1]"].includes(url.hostname) ||
    url.username || url.password || url.search || url.hash)
  throw new Error("Refusing any non-loopback or credential-bearing API URL");
const fetchJson = async (token, path) => {
  const response = await fetch(new URL(path, url), {
    method: "GET", redirect: "manual", cache: "no-store",
    headers: token ? { Authorization: "Bearer " + token } : {},
    signal: AbortSignal.timeout(12000)
  });
  const text = await response.text();
  let body;
  try { body = text ? JSON.parse(text) : null; } catch { body = null; }
  return { status: response.status, body };
};
let passes = 0;
async function expect(label, token, path, expected, verify) {
  const r = await fetchJson(token, path);
  const choices = Array.isArray(expected) ? expected : [expected];
  assert.ok(choices.includes(r.status), label + ": unexpected HTTP " + r.status);
  if (verify) verify(r.body);
  console.log("PASS " + label + " HTTP " + r.status);
  passes++;
}
const fiscal = "/api/dev/fiscal-participants?page=1&pageSize=100";
const parties = "/api/dev/customers/" + customer + "/parties";
const buyerDetail = parties + "/" + buyer + "/details";
const taxpayer = "/api/dev/taxpayers/" + seller;
const nested = "/api/dev/customer-owners/" + customer + "/taxpayers";
const ownedRows = body => {
  assert.ok(body && Array.isArray(body.items), "Expected paginated JSON");
  assert.ok(body.items.every(item => typeof item.id === "string"), "Malformed rows");
};
await expect("anonymous fiscal directory", null, fiscal, 401);
await expect("anonymous party listing", null, parties, 401);
await expect("invalid paging rejected", root,
  "/api/dev/fiscal-participants?page=0&pageSize=500", 400);
await expect("root scoped fiscal directory", root, fiscal, 200, ownedRows);
await expect("assigned partner fiscal directory", assigned, fiscal, 200, ownedRows);
await expect("other partner fiscal directory hides chosen customer", outsider, fiscal, 200, body => {
  ownedRows(body);
  assert.ok(body.items.every(item => item.customerUserId !== customer),
    "CROSS-OWNER FISCAL RECORD LEAK");
});
await expect("root party directory", root, parties, 200, data => {
  assert.ok(Array.isArray(data?.items));
  assert.ok(Number.isInteger(data.total) && data.total >= 1, "Expected linked buyers");
});
await expect("assigned partner party directory", assigned, parties, 200);
await expect("other partner party directory denied", outsider, parties, [403, 404]);
await expect("root buyer details", root, buyerDetail, 200);
await expect("assigned partner buyer details", assigned, buyerDetail, 200);
await expect("other partner buyer details denied", outsider, buyerDetail, [403, 404]);
await expect("assigned partner seller details", assigned, taxpayer, 200);
await expect("other partner seller denied", outsider, taxpayer, [403, 404]);
await expect("assigned partner seller list", assigned, nested, 200, values => {
  assert.ok(Array.isArray(values));
  assert.ok(values.some(v => v.id === seller), "Fixture taxpayer missing");
});
await expect("other partner seller list denied", outsider, nested, [403, 404]);
await expect("unknown buyer UUID never leaks", assigned,
  parties + "/" + crypto.randomUUID() + "/details", [403, 404]);
console.log("Phase05 Step14 READ-ONLY HTTP checks passed: " + passes +
  ". Transfer race and rollback-only PostgreSQL tests are separate required gates.");
