import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const repoRoot = fileURLToPath(new URL("../../../", import.meta.url));
const workflow = readFileSync(resolve(repoRoot, ".github/workflows/phase18-production-release.yml"), "utf8");

test("pull requests to main run package verification", () => {
  assert.match(workflow, /pull_request:\s*\n\s*branches:\s*\[main, phase\/18-production-deployment\]/);
  assert.match(workflow, /name: Run API regression tests/);
  assert.match(workflow, /npm --prefix apps\/web test/);
  assert.match(workflow, /npm --prefix apps\/web run typecheck/);
});

test("only a push to main can enter the production deployment gate", () => {
  assert.match(workflow, /if: github\.event_name == 'push' && github\.ref == 'refs\/heads\/main'/);
  assert.match(workflow, /environment: production/);
  assert.doesNotMatch(workflow, /workflow_dispatch/);
});

test("deployment consumes the same SHA artifact and does not rebuild or migrate", () => {
  assert.match(workflow, /name: argintaxpayer-release-\$\{\{ github\.sha \}\}/);
  assert.match(workflow, /printf 'sha=%s\\n' "\$GITHUB_SHA"/);
  assert.match(workflow, /sha256sum api\.tar\.gz web\.tar\.gz/);
  assert.match(workflow, /actions\/download-artifact@v4/);
  assert.doesNotMatch(workflow, /dotnet ef database update|ConnectionStrings__ArginTaxPayerDb/);
});

test("SSH deployment pins the host key and calls only the fixed helper", () => {
  assert.match(workflow, /StrictHostKeyChecking=yes/);
  assert.match(workflow, /\/var\/lib\/argin-taxpayer-deploy\/incoming\/\$SOURCE_SHA/);
  assert.match(workflow, /argin-taxpayer-release --sha/);
  assert.doesNotMatch(workflow, /StrictHostKeyChecking=no/);
  assert.doesNotMatch(workflow, /sudo .*systemctl|ssh .*bash -c/);
});
