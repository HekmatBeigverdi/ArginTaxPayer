import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const repoRoot = fileURLToPath(new URL("../../../", import.meta.url));
const read = (p) => readFileSync(resolve(repoRoot, p), "utf8");
test("release staging verifies SHA256 and never activates services", () => {
  const s=read("scripts/phase18-release-stage.sh");
  const core=read("ops/phase18-deploy/release-core.sh");
  assert.match(s,/phase18_verify_release_bundle/);
  assert.match(core,/sha256sum -c --status SHA256SUMS/);
  assert.match(s,/build-info\.txt/);
  assert.match(s,/web\/node_modules\/next\/dist\/bin\/next/);
  assert.doesNotMatch(s,/npm\s+(?:--prefix|ci)/);
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
test("GitHub workflow deploys only approved main releases", () => {
  const s=read(".github/workflows/phase18-production-release.yml");
  assert.match(s,/github\.event_name == 'push' && github\.ref == 'refs\/heads\/main'/);
  assert.match(s,/environment: production/);
  assert.match(s,/StrictHostKeyChecking=yes/);
  assert.match(s,/SHA256SUMS/);
  assert.doesNotMatch(s,/StrictHostKeyChecking=no/);
  assert.doesNotMatch(s,/workflow_dispatch/);
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
