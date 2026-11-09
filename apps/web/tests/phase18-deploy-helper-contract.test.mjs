import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join, resolve } from "node:path";
import test from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("../../../", import.meta.url));
const corePath = resolve(repoRoot, "ops/phase18-deploy/release-core.sh");
const sha = "a".repeat(40);
const source = (path) => existsSync(resolve(repoRoot, path)) ? readFileSync(resolve(repoRoot, path), "utf8") : "";

function runCore(command) {
  return spawnSync("bash", ["-c", `set -Eeuo pipefail\nsource "$PHASE18_CORE"\n${command}`], {
    encoding: "utf8",
    env: { ...process.env, PHASE18_CORE: corePath },
  });
}

function makeBundle() {
  const root = mkdtempSync(join(tmpdir(), "argin-phase18-release-"));
  const stage = join(root, "stage");
  mkdirSync(join(stage, "api"), { recursive: true });
  mkdirSync(join(stage, "web", ".next"), { recursive: true });
  mkdirSync(join(stage, "web", "node_modules", "next", "dist", "bin"), { recursive: true });
  writeFileSync(join(stage, "api", "ArginTaxPayer.Api.dll"), "api");
  writeFileSync(join(stage, "web", ".next", "BUILD_ID"), "build-id");
  writeFileSync(join(stage, "web", "package-lock.json"), "{}\n");
  writeFileSync(join(stage, "web", "node_modules", "next", "dist", "bin", "next"), "next");
  const apiTar = spawnSync("tar", ["-czf", join(root, "api.tar.gz"), "api"], { cwd: stage });
  const webTar = spawnSync("tar", ["-czf", join(root, "web.tar.gz"), "web"], { cwd: stage });
  assert.equal(apiTar.status, 0, apiTar.stderr?.toString());
  assert.equal(webTar.status, 0, webTar.stderr?.toString());
  writeFileSync(join(root, "build-info.txt"), `sha=${sha}\nutc=20261105T000000Z\n`);
  const sums = spawnSync("sha256sum", ["api.tar.gz", "web.tar.gz"], { cwd: root, encoding: "utf8" });
  assert.equal(sums.status, 0, sums.stderr);
  writeFileSync(join(root, "SHA256SUMS"), sums.stdout);
  return { root, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}

test("release core accepts only a full lowercase Git SHA", () => {
  assert.equal(runCore(`phase18_validate_sha '${sha}'`).status, 0);
  assert.notEqual(runCore("phase18_validate_sha 'ABC123'").status, 0);
});

test("release core verifies a matching immutable bundle", (t) => {
  const bundle = makeBundle();
  t.after(bundle.cleanup);
  const result = runCore(`phase18_verify_release_bundle '${bundle.root}' '${sha}'`);
  assert.equal(result.status, 0, result.stderr);
});

test("release core rejects a bundle with a different embedded source SHA", (t) => {
  const bundle = makeBundle();
  t.after(bundle.cleanup);
  writeFileSync(join(bundle.root, "build-info.txt"), `sha=${"b".repeat(40)}\n`);
  const result = runCore(`phase18_verify_release_bundle '${bundle.root}' '${sha}'`);
  assert.notEqual(result.status, 0);
});

test("release core rejects a checksum mismatch and symlinked archives", (t) => {
  const bundle = makeBundle();
  t.after(bundle.cleanup);
  writeFileSync(join(bundle.root, "web.tar.gz"), "tampered");
  assert.notEqual(runCore(`phase18_verify_release_bundle '${bundle.root}' '${sha}'`).status, 0);

  const symlinkBundle = makeBundle();
  t.after(symlinkBundle.cleanup);
  const archive = join(symlinkBundle.root, "api.tar.gz");
  const target = join(symlinkBundle.root, "api-real.tar.gz");
  const original = readFileSync(archive);
  writeFileSync(target, original);
  rmSync(archive);
  symlinkSync(basename(target), archive);
  assert.notEqual(runCore(`phase18_verify_release_bundle '${symlinkBundle.root}' '${sha}'`).status, 0);
});

test("release core rejects parent traversal entries even with a matching checksum", (t) => {
  const bundle = makeBundle();
  t.after(bundle.cleanup);
  const traversalRoot = join(bundle.root, "traversal-source");
  mkdirSync(join(traversalRoot, "api"), { recursive: true });
  writeFileSync(join(traversalRoot, "api", "outside.txt"), "outside");
  const malicious = spawnSync("tar", ["--transform=s,^api/,api/../,", "-czf", join(bundle.root, "api.tar.gz"), "-C", traversalRoot, "api"], { encoding: "utf8" });
  assert.equal(malicious.status, 0, malicious.stderr);
  const sums = spawnSync("sha256sum", ["api.tar.gz", "web.tar.gz"], { cwd: bundle.root, encoding: "utf8" });
  assert.equal(sums.status, 0, sums.stderr);
  writeFileSync(join(bundle.root, "SHA256SUMS"), sums.stdout);
  const result = runCore(`phase18_verify_release_bundle '${bundle.root}' '${sha}'`);
  assert.notEqual(result.status, 0);
});

test("root helper accepts only the fixed SHA interface and deployment identity", () => {
  const helper = source("ops/phase18-deploy/phase18-release-root.sh");
  assert.ok(helper.length > 0, "root helper must exist");
  assert.match(helper, /SUDO_USER.*argin-deploy/);
  assert.match(helper, /--sha/);
  assert.match(helper, /\/var\/lib\/argin-taxpayer-deploy\/incoming/);
  assert.match(helper, /phase18_verify_release_bundle/);
  assert.match(helper, /processing_root/);
  assert.match(helper, /mv -- "\$bundle" "\$processing_root\/\$sha"/);
  assert.match(helper, /chown -R root:root/);
  assert.doesNotMatch(helper, /eval\s|bash\s+-c|systemctl\s+\$|--bundle-dir.*\$[0-9]/);
});

test("installer grants sudo only to the root-owned release wrapper", () => {
  const installer = source("scripts/phase18-install-deploy-helper.sh");
  assert.ok(installer.length > 0, "installer must exist");
  assert.match(installer, /deploy_user=argin-deploy/);
  assert.match(installer, /wrapper=\/usr\/local\/sbin\/argin-taxpayer-release/);
  assert.match(installer, /sudoers_line="\$deploy_user ALL=\(root\) NOPASSWD: \$wrapper --sha \*"/);
  assert.match(installer, /visudo -cf/);
  assert.match(installer, /\/usr\/local\/lib\/argin-taxpayer-deploy/);
  assert.doesNotMatch(installer, /NOPASSWD:ALL|systemctl\s+(?:start|restart|enable)|dotnet ef database update/);
});

test("stage reuses shared validation and idempotently accepts the exact staged SHA", () => {
  const stage = source("scripts/phase18-release-stage.sh");
  assert.match(stage, /phase18_verify_release_bundle/);
  assert.match(stage, /PHASE18_ROOT_MODE/);
  assert.match(stage, /SHA256SUMS/);
  assert.match(stage, /already staged/);
  assert.match(stage, /web\/node_modules\/next\/dist\/bin\/next/);
  assert.doesNotMatch(stage, /npm\s+(?:--prefix|ci)|phase18_as_build_user/);
  assert.doesNotMatch(stage, /systemctl\s+(?:restart|start|enable)/);
});

test("promotion runs through the privilege adapter and keeps an atomic rollback symlink", () => {
  const promote = source("scripts/phase18-release-promote.sh");
  assert.match(promote, /PHASE18_ROOT_MODE/);
  assert.match(promote, /\.current-next-\$\$/);
  assert.match(promote, /phase18_as_root mv -Tf/);
  assert.match(promote, /Rolling back application only/);
  assert.doesNotMatch(promote, /sudo\s+(?:ln|mv|systemctl)/);
});
