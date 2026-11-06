import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";

const read=p=>readFileSync(new URL(`../../../${p}`,import.meta.url),"utf8");

test("only the explicit legacy wire profile is active; unknown SDK versions fail closed",()=>{
  const profile=read("src/ArginTaxPayer.Infrastructure/Signing/Connection/AuthorityProtocolProfile.cs");
  const tests=read("tests/ArginTaxPayer.Tests/Phase18AuthorityProtocolProfileTests.cs");
  const catalog=read("docs/references/tax-authority-sdk-catalog.md");
  assert.match(profile,/LegacyFamily = "legacy-self-tsp"/);
  assert.match(profile,/LegacyVersion = "0\.0\.x"/);
  assert.match(profile,/source-0\.0\.30;package-0\.0\.34/);
  assert.match(profile,/if \(!string\.Equals\(family, LegacyFamily/);
  assert.match(tests,/Unknown|Unsupported|unsupported/);
  assert.match(catalog,/modern-rest\/2\.0\.32` family remains a distinct, inactive adapter/);
});
