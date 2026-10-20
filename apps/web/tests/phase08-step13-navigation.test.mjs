import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
test("moving from lines to summary saves lines without finalizing settlement",()=>{
  assert.match(ui,/const persistedStep: Step = !exit && step===1 && completeStep===2 \? 1 : completeStep/);
  assert.match(ui,/lastCompletedStep:persistedStep/);
  assert.match(ui,/if \(persistedStep===2 && settlement===0\)/);
  assert.match(ui,/else if\(!preserveVisibleStep\) setStep\(completeStep\)/);
});
test("final summary completion retains required settlement choice",()=>{
  assert.match(ui,/onClick=\{\(\) => void saveWorkspace\(2,true\)\}/);
  assert.match(ui,/settlementError \|\| settlement===0/);
});
