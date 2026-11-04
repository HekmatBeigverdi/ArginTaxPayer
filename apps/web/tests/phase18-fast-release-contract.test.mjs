import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const repoRoot = fileURLToPath(new URL("../../../", import.meta.url));
const read = (p) => readFileSync(resolve(repoRoot, p), "utf8");
test("release staging verifies SHA256 and never activates services", () => {
  const s=read("scripts/phase18-release-stage.sh");
  assert.match(s,/sha256sum -c --status SHA256SUMS/);
  assert.match(s,/build-info\.txt/);
  assert.match(s,/npm ci --omit=dev/);
  assert.doesNotMatch(s,/systemctl (?:restart|start|enable)/);
  assert.doesNotMatch(s,/dotnet ef database update/);
  assert.doesNotMatch(s,/docker (?:rm|stop|compose down)/);
});
test("release promotion requires explicit approval and activation marker", () => {
  const s=read("scripts/phase18-release-promote.sh");
  assert.match(s,/PHASE18_OWNER_APPROVED_RELEASE/);
  assert.match(s,/phase18-activation-approved/);
  assert.match(s,/phase18-step06-local-health.sh/);
  assert.match(s,/Rolling back application only/);
  assert.doesNotMatch(s,/dotnet ef database update/);
});
test("GitHub workflow never deploys automatically on push", () => {
  const s=read(".github/workflows/phase18-production-release.yml");
  assert.match(s,/workflow_dispatch/);
  assert.match(s,/github\.event_name == 'workflow_dispatch'/);
  assert.match(s,/environment: production/);
  assert.match(s,/StrictHostKeyChecking=yes/);
  assert.match(s,/SHA256SUMS/);
  assert.doesNotMatch(s,/StrictHostKeyChecking=no/);
});
