#!/usr/bin/env node
/**
 * Phase07 read-only HTTP isolation smoke, DEV LOOPBACK ONLY.
 * All sessions must be dedicated synthetic test identities.
 * Does not create, modify, delete or issue invoices.
 * Required env:
 * PHASE07_API_ORIGIN=http://127.0.0.1:15089
 * PHASE07_OWNER_TOKEN, PHASE07_OTHER_TOKEN
 * PHASE07_OWNER_TAXPAYER_ID, PHASE07_FOREIGN_TAXPAYER_ID
 */
import assert from "node:assert/strict";

const required = ["PHASE07_API_ORIGIN","PHASE07_OWNER_TOKEN",
  "PHASE07_OTHER_TOKEN","PHASE07_OWNER_TAXPAYER_ID",
  "PHASE07_FOREIGN_TAXPAYER_ID"];
const missing=required.filter(key=>!process.env[key]);
if(missing.length){
  console.error("Missing test environment variables: "+missing.join(", "));
  console.error("Use two distinct authorized development test sessions. No DB writes occur.");
  process.exit(2);
}
const url=new URL(process.env.PHASE07_API_ORIGIN);
if(!["127.0.0.1","localhost","::1"].includes(url.hostname) ||
    !["http:","https:"].includes(url.protocol) || url.username || url.password ||
    url.pathname!=="/" || url.search || url.hash) {
  console.error("Refusing non-loopback, credential-bearing or path-scoped API origin.");
  process.exit(2);
}
const uuid=/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
for(const name of ["PHASE07_OWNER_TAXPAYER_ID","PHASE07_FOREIGN_TAXPAYER_ID"])
  assert.match(process.env[name],uuid,name);
assert.notEqual(process.env.PHASE07_OWNER_TOKEN,process.env.PHASE07_OTHER_TOKEN,
  "Use distinct owner and foreign sessions.");
assert.notEqual(process.env.PHASE07_OWNER_TAXPAYER_ID,
  process.env.PHASE07_FOREIGN_TAXPAYER_ID,"Test taxpayers must be distinct.");

async function get(route,token){
  const response=await fetch(new URL(route,url),{
    headers:token?{Authorization:"Bearer "+token}:{},
    signal:AbortSignal.timeout(12000),redirect:"manual"
  });
  const body=await response.text();
  return {status:response.status,body};
}
const own=process.env.PHASE07_OWNER_TAXPAYER_ID;
const foreign=process.env.PHASE07_FOREIGN_TAXPAYER_ID;
const ownerToken=process.env.PHASE07_OWNER_TOKEN;
const outsider=process.env.PHASE07_OTHER_TOKEN;
const cases=[
  ["no token reference",`/api/dev/invoice-references/${own}`,undefined,401],
  ["no token overview","/api/dev/invoices",undefined,401],
  ["owner reference history",`/api/dev/invoice-references/${own}`,ownerToken,200],
  ["outsider cannot read owner references",`/api/dev/invoice-references/${own}`,outsider,404],
  ["owner cannot read other taxpayer",`/api/dev/invoice-references/${foreign}`,ownerToken,404],
  ["owner scoped invoice overview",`/api/dev/invoices?taxpayerId=${own}`,ownerToken,200],
  ["owner cannot read foreign invoice overview",`/api/dev/invoices?taxpayerId=${foreign}`,ownerToken,200]
];
for(const [label,route,token,expected] of cases){
  const {status,body}=await get(route,token);
  assert.equal(status,expected,`${label}: HTTP ${status} (expected ${expected})`);
  if(label==="owner reference history"){
    const items=JSON.parse(body);
    assert.ok(Array.isArray(items),label);
    assert.ok(items.every(item=>item.fiscalIdentifierVerified===false &&
      item.fiscalIdentifier===null), "Tax identifiers must not be invented.");
  }
  if(label==="owner cannot read foreign invoice overview")
    assert.deepEqual(JSON.parse(body),[],
      "Foreign taxpayer filter must reveal no invoice rows.");
  console.log("PASS: "+label+" (HTTP "+status+")");
}
console.log("Read-only Phase07 scoped smoke complete. No data modified.");
