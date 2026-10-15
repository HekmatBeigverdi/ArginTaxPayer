import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");
test("Mac release candidate is clean-tree guarded and never merges or migrates", () => {
  const script = read("../../scripts/phase05-step15-verify.zsh");
  assert.match(script, /phase\/05-parties-customers/);
  assert.match(script, /git status --porcelain/);
  assert.match(script, /phase05-step14-verify\.zsh/);
  assert.match(script, /--pg/);
  assert.doesNotMatch(script, /dotnet ef database update/);
  assert.doesNotMatch(script, /git tag\s/);
  assert.doesNotMatch(script, /git merge\s/);
});
test("Ubuntu handoff fails closed unless test DB and migrations are verified", () => {
  const script = read("../../scripts/phase05-step15-ubuntu.sh");
  assert.match(script, /argintaxpayer_test/);
  assert.match(script, /SELECT current_database\(\)/);
  assert.match(script, /__EFMigrationsHistory/);
  assert.match(script, /20261015190000_AddSoftwareUserProfiles/);
  assert.match(script, /ubuntu-dev\.sh verify/);
  assert.doesNotMatch(script, /dotnet ef database update/);
});
test("Phase05 formal closure is distinct from step14 owner signoff", () => {
  const doc = read("../../docs/phases/phase-05.md");
  assert.match(doc, /Step14 explicitly approved/);
  assert.match(doc, /Step15 is \*\*implemented, not owner-approved\*\*/);
});
