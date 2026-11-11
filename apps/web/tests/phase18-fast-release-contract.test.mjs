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
  assert.match(s,/--promote-staged/);
  const helper=read('ops/phase18-deploy/argin-taxpayer-release.py');
  assert.match(helper,/Restoring previous application release/);
  assert.match(helper,/local-health\.sh/);
  assert.match(helper,/lambda: healthy\(probe_mode\)/);
  assert.ok(helper.indexOf('migrate_release(release)\n        promote(')>helper.indexOf('fcntl.flock(lock,'));
  assert.doesNotMatch(s,/dotnet ef database update/);
});
test("GitHub deploy requires main, the production environment and the schema-aware root helper", () => {
  const s=read(".github/workflows/production-release.yml");
  assert.match(s,/workflow_dispatch/);
  assert.match(s,/github\.ref == 'refs\/heads\/main'/);
  assert.match(s,/schema-before-promote-v1/);
  assert.match(s,/tools\/ArginTaxPayer\.Migrations/);
  assert.match(s,/environment: production/);
  assert.match(s,/StrictHostKeyChecking=yes/);
  assert.match(s,/SHA256SUMS/);
  assert.doesNotMatch(s,/StrictHostKeyChecking=no/);
});

test('initial staging startup permits disabled UI only when provider gates remain false', () => {
  const promote=read('scripts/phase18-release-promote.sh');
  const health=read('scripts/phase18-step06-local-health.sh');
  assert.match(promote, /--initial-disabled-ui/);
  assert.match(promote, /ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(promote, /Ghasedak__EnableLiveSending=false/);
  assert.match(promote, /ARGIN_PRODUCTION_UI_ENABLED=false/);
  assert.match(health, /--probe-local-disabled-ui/);
  assert.match(health, /\$webcode.*404/);
});
