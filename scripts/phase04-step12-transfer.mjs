#!/usr/bin/env node
/**
 * Phase04 controlled mutation acceptance (test-only).
 * Requires a dedicated disposable customer already attributed to Level1.
 * Explicit opt-in PHASE04_ALLOW_MUTATION=YES and current root+partner sessions.
 * Performs: grant partner, reject stale CAS, verify partner read,
 * revoke to root, verify immediate partner 404, confirm history. No deletion.
 *
 * Dedicated fixtures ONLY. The script refuses to run without explicit opt-in.
 */
import assert from "node:assert/strict";
const env = process.env;
if (env.PHASE04_ALLOW_MUTATION !== "YES") {
  console.error("This opt-in test changes commercial ownership. Set PHASE04_ALLOW_MUTATION=YES only for a disposable customer.");
  process.exit(2);
}
for (const k of ["PHASE04_ROOT_TOKEN","PHASE04_PARTNER_TOKEN","PHASE04_FIXTURE_CUSTOMER_ID","PHASE04_FIXTURE_PARTNER_ID"]) {
  if (!env[k]) { console.error("Missing " + k); process.exit(2); }
}
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
for (const key of ["PHASE04_FIXTURE_CUSTOMER_ID","PHASE04_FIXTURE_PARTNER_ID"]) {
  assert.ok(uuid.test(env[key]), "Invalid fixture ID: " + key);
}
const base = new URL(env.PHASE04_API_URL || "http://127.0.0.1:15089");
assert.ok(base.protocol === "http:" && ["localhost","127.0.0.1","[::1]"].includes(base.hostname),
  "Refusing non-loopback server");
const customer = env.PHASE04_FIXTURE_CUSTOMER_ID;
const path = "/api/dev/customer-owners/" + customer;
async function send(token, segment = "", method = "GET", data) {
  const response = await fetch(new URL(path + segment, base), {
    method, cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(10000),
    headers: {
      Authorization: "Bearer " + token,
      ...(data ? { "Content-Type": "application/json" } : {})
    },
    ...(data ? { body: JSON.stringify(data) } : {})
  });
  return {
    status: response.status,
    body: response.headers.get("content-type")?.includes("application/json")
      ? await response.json() : null
  };
}
const root = env.PHASE04_ROOT_TOKEN;
const partner = env.PHASE04_PARTNER_TOKEN;
let current = await send(root);
assert.equal(current.status, 200, "Root must access disposable fixture");
assert.equal(current.body.partnerLevel2UserId, null,
  "Fixture is already owned by partner; refusing to overwrite existing commercial relation");
assert.ok(current.body.revision >= 1, "Fixture must have pre-existing root ownership");
const expected = current.body.revision;
const assignment = {
  partnerLevel2UserId: env.PHASE04_FIXTURE_PARTNER_ID,
  expectedRevision: expected,
  reason: 2,
  explanation: "Automated Phase04 integration test on dedicated disposable customer"
};
let r = await send(root, "/transfer", "POST", assignment);
assert.equal(r.status, 200, "Level1 assignment to Level2 failed");
console.log("PASS Level1 assigned dedicated fixture");

try {
  r = await send(root, "/transfer", "POST", assignment);
  assert.equal(r.status, 409, "Stale revision must be rejected");
  console.log("PASS stale transfer rejected with 409");
  r = await send(partner);
  assert.equal(r.status, 200, "Partner must now see customer");
  r = await send(partner, "/taxpayers");
  assert.equal(r.status, 200, "Partner must now see customer taxpayer collection");
  console.log("PASS scoped partner can read customer and taxpayers");
} finally {
  // Always attempt restoring original direct Level1 ownership.
  const latest = await send(root);
  if (latest.status === 200 &&
      latest.body.partnerLevel2UserId === env.PHASE04_FIXTURE_PARTNER_ID) {
    const restore = await send(root, "/transfer", "POST", {
      partnerLevel2UserId: null,
      expectedRevision: latest.body.revision,
      reason: 2,
      explanation: "End of Phase04 integration test: restore root commercial owner"
    });
    if (restore.status !== 200) {
      console.error("CRITICAL: Could not restore fixture to Level1. Manual review required.");
      process.exitCode = 1;
    }
  }
}
r = await send(partner);
assert.equal(r.status, 404, "Former partner MUST lose access after revocation");
r = await send(partner, "/taxpayers");
assert.equal(r.status, 404, "Former partner MUST lose nested taxpayer access");
r = await send(root, "/history");
assert.equal(r.status, 200);
assert.ok(Array.isArray(r.body) && r.body.length >= 2, "Both assignment and reversal must be audited");
console.log("PASS former partner denied; history persists; fixture returned to Root.");
