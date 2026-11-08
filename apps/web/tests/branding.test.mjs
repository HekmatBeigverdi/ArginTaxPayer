import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { test } from "node:test";
import React from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {loadTsx} from "./helpers/load-tsx.mjs";
const read = (path) => readFileSync(new URL("../"+path,import.meta.url),"utf8");
test("owner-supplied Argin logo is available as a nonempty PNG", () => {
  for (const path of ["public/brand/argin-logo.png", "app/icon.png",
    "public/favicon.png", "public/apple-touch-icon.png"]) {
    const file = new URL("../"+path,import.meta.url);
    assert.ok(existsSync(file),path+" missing");
    const data = readFileSync(file);
    assert.ok(data.length > 300);
    assert.equal(data.subarray(0,8).toString("hex"),"89504e470d0a1a0a");
  }
});
test("same logo is used for login, sidebar, header and metadata", () => {
  assert.match(read("app/layout.tsx"), /icons:/);
  assert.match(read("app/layout.tsx"), /\/brand\/argin-logo\.png/);
  const login = read("app/identity/identity-screen.tsx");
  const shell = read("app/components/console-shell.tsx");
  assert.match(login, /src="\/brand\/argin-logo\.png"/);
  assert.match(shell, /src="\/brand\/argin-logo\.png"/);
  assert.equal((login.match(/src="\/brand\/argin-logo\.png"/g) ?? []).length, 1);
  assert.equal((shell.match(/src="\/brand\/argin-logo\.png"/g) ?? []).length, 2);
  assert.match(login, /identity-screen__brand-logo/);
  assert.match(shell, /argin-sidebar-logo/);
  assert.match(shell, /argin-header-logo/);
  assert.equal((login.match(/unoptimized/g) ?? []).length, 2);
  assert.equal((shell.match(/unoptimized/g) ?? []).length, 2);
  assert.match(read("app/globals.css"), /\.argin-brand-logo\{/);
});

test("all branding images have a transparent PNG color and alpha definition", () => {
  for (const path of ["public/brand/argin-logo.png", "app/icon.png",
    "public/favicon.png", "public/apple-touch-icon.png"]) {
    const png = readFileSync(new URL("../"+path,import.meta.url));
    const transparencyChunk = png.includes(Buffer.from("tRNS","ascii"));
    const rgbaColorType = png[25] === 6;
    assert.ok(transparencyChunk || rgbaColorType, path+" must have transparent background");
  }
});
test("dashboard renders role-scoped analytics without the retired welcome banner",async()=>{
  for(const [role,title] of [["Customer","وضعیت صورتحساب‌های شما"],
    ["Level1","نمای مدیریتی کل سامانه"],["Level2","عملکرد مشتریان زیرمجموعه"]]){
    const Page=loadTsx(new URL("../app/(console)/dashboard/page.tsx",import.meta.url),{
      modules:{
        "./dashboard-design.css":{},
        "../../../lib/identity":{currentIdentity:async()=>({roles:[role]})},
        "next/link":{default:({href,children,...props})=>React.createElement("a",{href,...props},children)}
      }
    }).default;
    const html=renderToStaticMarkup(await Page());
    assert.ok(html.includes(title),role);
    assert.ok(html.includes('class="argin-dashboard"'),role);
    assert.ok(!html.includes("app-welcome-graphic"),role);
    assert.ok(!html.includes("argin-welcome-logo"),role);
  }
});
