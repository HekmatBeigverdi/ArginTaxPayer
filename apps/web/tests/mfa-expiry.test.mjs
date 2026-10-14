import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const flow = readFileSync(new URL("../app/identity/identity-flow.tsx", import.meta.url), "utf8");
test("MFA challenge expiry uses server-provided UTC deadline rather than OTP code window", () => {
  assert.match(flow, /Date\.parse\(challengeResponse\.result\.expiresAtUtc\)/);
  assert.match(flow, /Date\.now\(\) >= mfaExpiresAt/);
  assert.match(flow, /window\.setInterval\(check, 1000\)/);
  assert.match(flow, /visibilitychange/);
  assert.match(flow, /setStep\("details"\)/);
  assert.match(flow, /مهلت تأیید دومرحله‌ای تمام شد/);
});
