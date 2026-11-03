#!/usr/bin/env node
// Phase15 Step16: non-mutating, authenticated, localhost-only role acceptance.
// Never print tokens or response payloads; test identities must be independent.
const origin=process.env.PHASE15_BASE_URL??"http://127.0.0.1:15089";
const url=new URL(origin);
if(url.protocol!=="http:"||!["localhost","127.0.0.1","[::1]"].includes(url.hostname)||
   url.username||url.password||url.pathname!=="/"||url.search||url.hash){
 throw Error("REFUSED: use the loopback-only HTTP development API");
}
const actors=[
 ["Level1",process.env.PHASE15_LEVEL1_TOKEN],
 ["Level2",process.env.PHASE15_LEVEL2_TOKEN],
 ["Customer",process.env.PHASE15_CUSTOMER_TOKEN]
];
if(actors.some(([,token])=>typeof token!=="string"||token.length<10)||
 new Set(actors.map(([,token])=>token)).size!==3){
 throw Error("Set three distinct PHASE15_LEVEL1_TOKEN, PHASE15_LEVEL2_TOKEN and PHASE15_CUSTOMER_TOKEN test-account tokens");
}
const checks=[
 ["overview","?period=30d",{Level1:200,Level2:200,Customer:200}],
 ["trends","?period=30d",{Level1:200,Level2:200,Customer:200}],
 ["attention","?period=30d",{Level1:200,Level2:200,Customer:200}],
 ["recent","?period=30d",{Level1:200,Level2:200,Customer:200}],
 ["finance","?period=30d",{Level1:200,Level2:200,Customer:403}],
 ["financial-trends","?period=30d",{Level1:200,Level2:200,Customer:403}],
 ["root","?period=30d",{Level1:200,Level2:403,Customer:403}],
 ["partner","",{Level1:403,Level2:200,Customer:403}],
 ["subscriptions-watch","",{Level1:200,Level2:200,Customer:403}],
 ["root","?partnerUserId=00000000-0000-0000-0000-000000000001",
  {Level1:400,Level2:400,Customer:400}],
 ["subscriptions-watch","?period=30d",{Level1:400,Level2:400,Customer:400}]
];
let failures=0;
for(const [role,token] of actors){
 for(const [section,query,expected] of checks){
  const controller=new AbortController();
  const abort=setTimeout(()=>controller.abort(),10000);
  let actual=0,valid=false;
  try{
   const res=await fetch(new URL("/api/dev/dashboard/"+section+query,url),{
    method:"GET",headers:{Authorization:"Bearer "+token,Accept:"application/json"},
    redirect:"manual",cache:"no-store",signal:controller.signal
   });
   actual=res.status;
   if(actual===200){
    const payload=await res.json();
    valid=payload?.schemaVersion===1&&
      (payload.role===undefined||payload.role===role)&&
      !("password" in payload)&&!("accessToken" in payload);
   }else{
    // No response content or identifiers should be printed.
    await res.arrayBuffer();
    valid=true;
   }
  }catch{actual=0;valid=false;}
  finally{clearTimeout(abort);}
  const ok=actual===expected[role]&&valid;
  if(!ok)failures++;
  console.log((ok?"[PASS] ":"[FAIL] ")+role+" "+section+" status="+actual+" expected="+expected[role]);
 }
}
if(failures){
 console.error("Step16 role smoke FAILED; "+failures+" checks failed. Check API and test identities before release.");
 process.exitCode=1;
}else console.log("Step16 role smoke PASS for the three authenticated TEST actors; browser and ledger checks still manual.");
