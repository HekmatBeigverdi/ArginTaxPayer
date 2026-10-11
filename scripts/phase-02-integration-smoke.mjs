#!/usr/bin/env node
// Step 20. Local-only, dependency-free cross-platform integration smoke.
// Expects built .NET Release binaries and installed Next.js dependencies.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { resolve, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import net from "node:net";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const web = join(root, "apps", "web");
const apiUrl = "http://127.0.0.1:15088";
const webUrl = "http://127.0.0.1:13019";
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
const children = new Set();
let stopping = false;

function start(executable, args, cwd, env) {
  const child = spawn(executable, args, {
    cwd,
    env: { ...process.env, ...env },
    stdio: ["ignore", "pipe", "pipe"],
    windowsHide: true,
  });
  children.add(child);
  child.on("exit", () => children.delete(child));
  for (const stream of [child.stdout, child.stderr]) {
    stream.setEncoding("utf8");
    stream.on("data", (text) => {
      // Never echo potentially sensitive application output into CI logs.
      if (process.env.ARGIN_SMOKE_VERBOSE === "1") {
        const safe = text.replace(/Password=[^;\s]+/gi, "Password=[REDACTED]");
        process.stdout.write(safe);
      }
    });
  }
  return child;
}

function stop(child) {
  if (child && child.exitCode === null && child.signalCode === null) child.kill("SIGTERM");
}

async function stopAndWait(child) {
  if (!child || child.exitCode !== null || child.signalCode !== null) return;
  const closed = new Promise((done) => child.once("exit", done));
  stop(child);
  await Promise.race([closed, wait(4000)]);
  if (child.exitCode === null && child.signalCode === null) child.kill("SIGKILL");
}

async function ensurePortFree(port) {
  await new Promise((resolveFree, reject) => {
    const server = net.createServer();
    server.once("error", () => reject(new Error(`Port ${port} is occupied; refusing to test an unrelated service.`)));
    server.listen(port, "127.0.0.1", () => server.close(resolveFree));
  });
}

async function request(path, { timeout = 2500 } = {}) {
  return fetch(path, { cache: "no-store", signal: AbortSignal.timeout(timeout) });
}

async function until(name, child, test, timeoutMs = 65000) {
  const end = Date.now() + timeoutMs;
  while (Date.now() < end) {
    if (child.exitCode !== null || child.signalCode !== null)
      throw new Error(`${name} exited before becoming ready.`);
    try {
      if (await test()) return;
    } catch { /* Retry while booting. */ }
    await wait(400);
  }
  throw new Error(`${name} failed readiness within ${timeoutMs}ms.`);
}

function expectLabel(html, label, expected) {
  const expression = new RegExp(`${label}\\s*<strong>${expected}</strong>`);
  assert.match(html, expression);
}

async function main() {
  await ensurePortFree(15088);
  await ensurePortFree(13019);
  const api = start("dotnet", [
    "run", "--no-build", "--configuration", "Release",
    "--no-launch-profile", "--project", "src/ArginTaxPayer.Api",
    "--urls", apiUrl,
  ], root, {
    ASPNETCORE_ENVIRONMENT: "Development",
    ConnectionStrings__ArginTaxPayerDb: "",
    RUN_ARGINTAXPAYER_PG_INTEGRATION: "0",
  });
  await until("API", api, async () =>
    (await request(apiUrl + "/health/live")).status === 200);

  const liveness = await request(apiUrl + "/health/live");
  assert.equal(liveness.status, 200);
  assert.equal((await liveness.json()).status, "Healthy");
  const readiness = await request(apiUrl + "/health/ready");
  assert.equal(readiness.status, 503);
  assert.equal((await readiness.json()).status, "Unhealthy");

  const next = start(process.execPath, [
    "node_modules/next/dist/bin/next", "dev",
    "--hostname", "127.0.0.1", "--port", "13019",
  ], web, {
    NODE_ENV: "development",
    NEXT_TELEMETRY_DISABLED: "1",
    ARGIN_API_INTERNAL_BASE_URL: apiUrl,
  });
  await until("Next.js", next, async () =>
    (await request(webUrl + "/", { timeout: 10000 })).status === 200, 120000);
  const page = await request(webUrl + "/", { timeout: 12000 });
  assert.equal(page.status, 200);
  const html = await page.text();
  assert.match(html, /<html lang="fa" dir="rtl">/);
  expectLabel(html, "اجرای API:", "سالم");
  expectLabel(html, "آمادگی دیتابیس:", "در دسترس نیست");
  console.log("PASS: API 200 / 503; Next.js 200; RTL and independent health states.");

  await stopAndWait(api);
  await wait(1000);
  const offlinePage = await request(webUrl + "/", { timeout: 12000 });
  assert.equal(offlinePage.status, 200);
  const offlineHtml = await offlinePage.text();
  expectLabel(offlineHtml, "اجرای API:", "در دسترس نیست");
  expectLabel(offlineHtml, "آمادگی دیتابیس:", "در دسترس نیست");
  console.log("PASS: Frontend remains available when API is stopped.");
}

process.on("SIGINT", () => {
  stopping = true;
  for (const child of children) stop(child);
  process.exitCode = 130;
});
process.on("SIGTERM", () => {
  stopping = true;
  for (const child of children) stop(child);
  process.exitCode = 143;
});

try {
  await main();
  if (!stopping) console.log("STEP 20 INTEGRATION SMOKE PASSED");
} catch (error) {
  process.exitCode = 1;
  console.error("STEP 20 INTEGRATION SMOKE FAILED:", error instanceof Error ? error.message : "unknown");
} finally {
  await Promise.all([...children].map(stopAndWait));
}
