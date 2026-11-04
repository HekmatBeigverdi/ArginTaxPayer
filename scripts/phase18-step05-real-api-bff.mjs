#!/usr/bin/env node
// Phase18 QA only: actual Production .NET API + actual Production Next.js BFF.
// No Nginx, DNS, SMS, tax authority or customer data. A disposable test user is
// loaded by the guarded Ubuntu wrapper from the isolated VerifyFull database.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { resolve } from "node:path";
import http from "node:http";
import net from "node:net";

const project = resolve(import.meta.dirname, "..");
const web = resolve(project, "apps/web");
const publishedApi = resolve(project, "artifacts/phase18-step05-e2e/api/ArginTaxPayer.Api.dll");
const email = process.env.PHASE18_QA_USER_EMAIL;
const password = process.env.PHASE18_QA_USER_PASSWORD;
const database = process.env.PHASE18_DISPOSABLE_DB_CONNECTION;
assert.ok(email?.startsWith("PHASE18-") && email.endsWith("@EXAMPLE.INVALID"),
  "Refusing non-synthetic identity");
assert.equal(password, "Phase18-Test-Tenancy-Only-Password!", "Not a Phase18 synthetic credential");
assert.ok(database?.includes("Database=argin_phase18_acceptance;"));
assert.ok(database.includes("Username=phase18_acceptance;"));
assert.ok(database.includes("Port=15433;"));
assert.ok(database.includes("SSL Mode=VerifyFull;"));
assert.ok(existsSync(publishedApi), "Published QA API DLL missing");
assert.ok(existsSync(resolve(web, ".next/BUILD_ID")), "Next.js Production build missing");

const host = "127.0.0.1";
const apiPort = 15090;
const webPort = 15994;
const origin = "https://tax.argin.ir";
const wait = ms => new Promise(ok => setTimeout(ok, ms));
let api;
let next;

async function assertFree(port) {
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

async function request(port, path, method = "GET", headers = {}, data) {
  return await new Promise((ok, bad) => {
    const req = http.request({
      host, port, path, method,
      headers: { Host: port === webPort ? "tax.argin.ir" : "127.0.0.1:" + apiPort, ...headers }
    }, res => {
      const chunks = [];
      res.on("data", x => chunks.push(x));
      res.on("end", () => ok({
        status: res.statusCode, headers: res.headers,
        body: Buffer.concat(chunks).toString("utf8")
      }));
    });
    req.on("error", bad);
    req.setTimeout(7000, () => req.destroy(new Error("QA HTTP timeout")));
    if (data !== undefined) req.end(JSON.stringify(data)); else req.end();
  });
}

async function ready(port, path, expected, child, tag) {
  for (let i=0; i<100; i++) {
    if (child.exitCode !== null) throw new Error(tag + " terminated early");
    try {
      const result = await request(port, path);
      if (result.status === expected) return;
    } catch { /* startup */ }
    await wait(200);
  }
  throw new Error(tag + " did not reach expected ready state");
}

async function stop(child) {
  if (!child) return;
  if (child.exitCode !== null) return;
  child.kill("SIGTERM");
  await Promise.race([
    new Promise(ok => child.once("exit", ok)),
    wait(3500).then(() => child.kill("SIGKILL"))
  ]);
}

try {
  await assertFree(apiPort);
  await assertFree(webPort);
  api = spawn("dotnet", [publishedApi], {
    cwd: resolve(project, "artifacts/phase18-step05-e2e/api"),
    env: {
      PATH: process.env.PATH || "/usr/local/bin:/usr/bin:/bin",
      HOME: process.env.HOME || "/tmp",
      ...(process.env.DOTNET_ROOT ? { DOTNET_ROOT: process.env.DOTNET_ROOT } : {}),
      ASPNETCORE_ENVIRONMENT: "Production",
      DOTNET_ENVIRONMENT: "Production",
      ASPNETCORE_URLS: "http://127.0.0.1:15090",
      ConnectionStrings__ArginTaxPayerDb: database,
      ArginTaxPayer__PublicBaseUrl: origin,
      ArginTaxPayer__EnableOperationalEndpoints: "true",
      Identity__AllowDevelopmentRegistration: "false",
      Identity__OtpDeliveryMode: "Ghasedak",
      Ghasedak__EnableLiveSending: "false",
      Phase11__EnableReadOnlyQueueObserver: "false",
      SandboxInvoiceLegacy__Enabled: "false",
      SandboxInvoiceLegacy__OperatorApprovedEndpoint: "false",
      SigningConnectionTest__Enabled: "false",
      SigningKeyImport__Enabled: "false",
      SigningKeyGeneration__Enabled: "false"
    },
    stdio: "ignore"
  });
  await ready(apiPort, "/health/live", 200, api, "Production API");
  assert.equal((await request(apiPort, "/health/ready")).status, 200,
    "Production API database readiness failed");
  assert.equal((await request(apiPort, "/health/dev-identity")).status, 404);
  assert.equal((await request(apiPort, "/api/dev/identity/register", "POST",
    { "Content-Type": "application/json" }, { email: "unapproved@example.invalid", password })).status, 404);

  next = spawn(process.execPath, [
    "node_modules/next/dist/bin/next", "start", "-H", host, "-p", String(webPort)
  ], {
    cwd: web,
    env: {
      PATH: process.env.PATH || "/usr/local/bin:/usr/bin:/bin",
      HOME: process.env.HOME || "/tmp",
      NODE_ENV: "production",
      ARGIN_PRODUCTION_UI_ENABLED: "true",
      ARGIN_IDENTITY_UI_ENABLED: "true",
      ARGIN_PUBLIC_BASE_URL: origin,
      ARGIN_IDENTITY_API_INTERNAL_BASE_URL: "http://127.0.0.1:15090"
    },
    stdio: "ignore"
  });
  await ready(webPort, "/api/identity/me", 401, next, "Production BFF");

  const payload = { email, password };
  const bad = await request(webPort, "/api/identity/login", "POST",
    { Origin: "https://foreign.example", "Content-Type": "application/json" }, payload);
  assert.equal(bad.status, 403);
  assert.equal(bad.headers["set-cookie"], undefined);
  const login = await request(webPort, "/api/identity/login", "POST",
    { Origin: origin, "Content-Type": "application/json" }, payload);
  assert.equal(login.status, 200, "Synthetic QA login failed with actual Production API");
  assert.ok(!login.body.includes("accessToken") && !login.body.includes("refreshToken"));
  const cookies = login.headers["set-cookie"] || [];
  assert.equal(cookies.length, 2);
  for (const c of cookies) {
    assert.match(c, /Secure/i);
    assert.match(c, /HttpOnly/i);
    assert.match(c, /SameSite=Strict/i);
  }
  const cookieHeader = cookies.map(c => c.split(";")[0]).join("; ");
  assert.equal((await request(webPort, "/api/identity/me", "GET",
    { Cookie: cookieHeader })).status, 200);
  assert.equal((await request(webPort, "/api/identity/sessions", "GET",
    { Cookie: cookieHeader })).status, 200);
  const denied = await request(webPort, "/api/identity/logout", "POST",
    { Cookie: cookieHeader, Origin: "https://foreign.example" }, {});
  assert.equal(denied.status, 403);
  const logout = await request(webPort, "/api/identity/logout", "POST",
    { Cookie: cookieHeader, Origin: origin }, {});
  assert.equal(logout.status, 200);
  assert.ok((logout.headers["set-cookie"] || []).length >= 2);
  console.log("[PASS] actual Production API + real Next.js BFF, synthetic customer login, sessions, secure cookies, CSRF and logout");
  console.log("[SAFE] only loopback, no actual taxpayer data/authority/network publication");
} finally {
  await stop(next);
  await stop(api);
}
