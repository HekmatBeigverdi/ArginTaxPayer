import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const paths=[
 "../app/(console)/dashboard/page.tsx",
 "../app/components/console-shell.tsx",
 "../app/identity/page.tsx"
];
test("logo uses Next public URL instead of static PNG module imports on Ubuntu",()=>{
 for(const path of paths){
  const code=readFileSync(new URL(path,import.meta.url),"utf8");
  assert.match(code,/src="\/brand\/argin-logo\.png"/,path);
  assert.doesNotMatch(code,/import\s+arginLogo\s+from\s+["'][^"']+\.png["']/,path);
 }
});
