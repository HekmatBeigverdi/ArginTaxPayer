import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (name) => JSON.parse(readFileSync(new URL(`../${name}`, import.meta.url), "utf8"));

test("lockfile exactly matches direct manifest dependency declarations", () => {
  const manifest = read("package.json");
  const lock = read("package-lock.json");
  const root = lock.packages?.[""];

  assert.equal(lock.lockfileVersion, 3);
  assert.equal(lock.name, manifest.name);
  assert.equal(lock.version, manifest.version);
  assert.deepEqual(root?.dependencies, manifest.dependencies);
  assert.deepEqual(root?.devDependencies, manifest.devDependencies);
});

test("all installed direct dependencies are pinned by lockfile package metadata", () => {
  const manifest = read("package.json");
  const lock = read("package-lock.json");
  for (const name of [...Object.keys(manifest.dependencies), ...Object.keys(manifest.devDependencies)]) {
    const entry = lock.packages?.[`node_modules/${name}`];
    assert.ok(entry?.version, `Missing locked version for ${name}`);
    assert.ok(entry?.integrity, `Missing integrity digest for ${name}`);
  }
});

test("frontend must remain a private package without secret-bearing scripts", () => {
  const manifest = read("package.json");
  assert.equal(manifest.private, true);
  assert.equal(manifest.scripts.typecheck, "tsc --noEmit");
  assert.ok(!Object.keys(manifest.scripts).some((script) => script.startsWith("postinstall")));
});
