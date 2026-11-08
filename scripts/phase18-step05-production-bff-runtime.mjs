#!/usr/bin/env node
// Phase18: real Next.js Production BFF negative/positive cookie plumbing test.
// A local synthetic API listens ONLY on 127.0.0.1:15090. This is NOT the
// real fiscal/identity backend, and it never calls external services.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import http from "node:http";
import net from "node:net";
import { existsSync } from "node:fs";
import { resolve } from "node:path";

const webRoot = resolve(import.meta.dirname, "../apps/web");
const buildId = resolve(webRoot, ".next/BUILD_ID");
assert.ok(existsSync(buildId), "Production Next.js build missing. Run phase18 build-release first.");
const nextPort = 15994;
const backendPort = 15090;
const host = "127.0.0.1";
const publicOrigin = "https://tax.argin.ir";
const testAccess = "phase18-local-fake-access-token";
const testRefresh = "phase18-local-fake-refresh-token";
let frontend;
let backend;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

async function freePort(port) {
  const probe = net.createServer();
  try {
    await new Promise((ok, bad) => {
      probe.once("error", bad);
      probe.listen(port, host, ok);
    });
  } finally {
    await new Promise(ok => probe.close(ok));
  }
}
async function request(path, method = "GET", headers = {}, body) {
  return new Promise((ok, bad) => {
    const req = http.request({
      hostname: host, port: nextPort, path, method,
      headers: { Host: "tax.argin.ir", ...headers }
    }, res => {
      const chunks = [];
      res.on("data", c => chunks.push(c));
      res.on("end", () => ok({
        status: res.statusCode,
        headers: res.headers,
        text: Buffer.concat(chunks).toString("utf8")
      }));
    });
    req.setTimeout(5000, () => req.destroy(new Error("request timeout")));
    req.on("error", bad);
    if (body !== undefined) req.write(JSON.stringify(body));
    req.end();
  });
}
try {
  await freePort(nextPort);
  await freePort(backendPort);
  // Strict loopback-only synthetic backend. No actual identity data or log output.
  backend = http.createServer((req, res) => {
    res.setHeader("Cache-Control", "no-store");
    if (req.url === "/api/dev/identity/login" && req.method === "POST") {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ accessToken: testAccess, refreshToken: testRefresh }));
    } else if (req.url === "/api/dev/identity/authorization/me" &&
               req.headers.authorization === "Bearer " + testAccess) {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ userId: "00000000-0000-0000-0000-000000000001", roles: ["Customer"] }));
    } else if (req.url === "/api/dev/identity/logout") {
      res.writeHead(204);
      res.end();
    } else {
      res.writeHead(404, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: "fixture_missing" }));
    }
  });
  await new Promise((ok, bad) => {
    backend.once("error", bad);
    backend.listen(backendPort, host, ok);
  });
  frontend = spawn(process.execPath, [
    "node_modules/next/dist/bin/next", "start", "-H", host, "-p", String(nextPort)
  ], {
    cwd: webRoot,
    env: {
      ...process.env,
      NODE_ENV: "production",
      ARGIN_IDENTITY_UI_ENABLED: "true",
      ARGIN_PRODUCTION_UI_ENABLED: "true",
      ARGIN_PUBLIC_BASE_URL: publicOrigin,
      ARGIN_IDENTITY_API_INTERNAL_BASE_URL: "http://127.0.0.1:15090"
    },
    stdio: "ignore"
  });
  let started = false;
  for (let i = 0; i < 100; i++) {
    if (frontend.exitCode !== null) throw new Error("Next.js exited before readiness.");
    try {
      const resp = await request("/api/identity/me");
      if (resp.status === 401) { started = true; break; }
    } catch { /* still starting */ }
    await sleep(150);
  }
  assert.ok(started, "Next.js production route did not become ready.");

  const payload = { email: "phase18@example.invalid", password: "test-only" };
  for (const [label, headers] of [
    ["missing origin", {}],
    ["cross origin", { Origin: "https://malicious.example" }],
    ["insecure origin", { Origin: "http://tax.argin.ir" }],
    ["forged host", { Origin: publicOrigin, Host: "other.example" }]
  ]) {
    const denied = await request("/api/identity/login", "POST",
      { "Content-Type": "application/json", ...headers }, payload);
    assert.equal(denied.status, 403, label);
    assert.equal(denied.headers["set-cookie"], undefined, label);
    console.log("[PASS] " + label + " denied 403 without cookies");
  }
  const login = await request("/api/identity/login", "POST", {
    Origin: publicOrigin, "Content-Type": "application/json"
  }, payload);
  assert.equal(login.status, 200, "Synthetic login via Production BFF failed");
  assert.ok(!login.text.includes(testAccess) && !login.text.includes(testRefresh),
    "BFF leaked token in body");
  const cookies = login.headers["set-cookie"] || [];
  assert.equal(cookies.length, 2, "Expected access + refresh cookies");
  for (const c of cookies) {
    assert.match(c, /HttpOnly/i);
    assert.match(c, /Secure/i);
    assert.match(c, /SameSite=Strict/i);
    assert.match(c, /Path=\//i);
  }
  assert.ok(cookies.some(c => c.startsWith("argin_identity_access=")));
  assert.ok(cookies.some(c => c.startsWith("argin_identity_refresh=")));
  console.log("[PASS] Production login emits 2 Secure/HttpOnly/SameSite=Strict cookies");

  const cookieHeader = cookies.map(c => c.split(";")[0]).join("; ");
  const me = await request("/api/identity/me", "GET", { Cookie: cookieHeader });
  assert.equal(me.status, 200);
  assert.ok(me.text.includes("Customer"));
  console.log("[PASS] BFF resolves synthetic identity through HttpOnly cookie");

  const forbiddenLogout = await request("/api/identity/logout", "POST", {
    Cookie: cookieHeader, Origin: "https://other.example"
  }, {});
  assert.equal(forbiddenLogout.status, 403);
  assert.equal(forbiddenLogout.headers["set-cookie"], undefined);
  console.log("[PASS] forged Origin cannot log out a session");

  const logout = await request("/api/identity/logout", "POST", {
    Cookie: cookieHeader, Origin: publicOrigin
  }, {});
  assert.equal(logout.status, 200);
  assert.ok((logout.headers["set-cookie"] || []).length >= 2,
    "Logout must clear access and refresh cookies");
  console.log("[PASS] logout clears local cookie credentials");

  console.log("[DONE] Browser-facing Production BFF QA PASS; no real accounts or tax authority calls.");
} finally {
  if (frontend) {
    frontend.kill("SIGTERM");
    await Promise.race([
      new Promise(ok => frontend.once("exit", ok)),
      sleep(4000).then(() => frontend.kill("SIGKILL"))
    ]);
  }
  if (backend) await new Promise(ok => backend.close(ok));
}
