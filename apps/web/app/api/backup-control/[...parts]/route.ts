import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

type Context={params:Promise<{parts:string[]}>};
const allowed=new Set(["settings","ftps-password","operation","operations/manual","services/start","artifacts/local","artifacts/remote","artifacts/upload","restore/stage-local","restore/stage-remote","restore/activate","recovery","recovery/enable-business"]);
async function handle(req:NextRequest,ctx:Context,method:"GET"|"PUT"|"POST"){
 if(!identityUiEnabled())return NextResponse.json({error:"not_found"},{status:404});
 const {parts}=await ctx.params;const path=parts.join("/");
 const download=/^artifacts\/download\/[a-f0-9-]{36}$/i.test(path);
 const upload=path==="artifacts/upload";
 if((download&&method!=="GET")||(upload&&method!=="POST"))return NextResponse.json({error:"method_not_allowed"},{status:405});
 if(!allowed.has(path)&&!download)return NextResponse.json({error:"not_found"},{status:404});
 const token=req.cookies.get(ACCESS_COOKIE)?.value;if(!token)return NextResponse.json({error:"unauthenticated"},{status:401});
 if(method!=="GET"&&!isSameLocalOrigin(req.headers.get("origin"),req.headers.get("host"),req.nextUrl.protocol))return NextResponse.json({error:"origin_not_allowed"},{status:403});
 let body:string|undefined;
 if(method!=="GET"&&!upload){
  if(!(req.headers.get("content-type")||"").startsWith("application/json"))return NextResponse.json({error:"invalid_type"},{status:415});
  body=await req.text();if(body.length>8192)return NextResponse.json({error:"payload_too_large"},{status:413});
  try{JSON.parse(body)}catch{return NextResponse.json({error:"bad_json"},{status:400});}
 }
 if(upload&&req.headers.get("content-type")!=="application/zip")return NextResponse.json({error:"backup_bundle_zip_required"},{status:415});
 const origin=process.env.ARGIN_BACKUP_CONTROLLER_INTERNAL_URL||"http://127.0.0.1:15092";
 try{
  const longOperation=method==="POST"&&["operations/manual","restore/stage-local","restore/stage-remote","restore/activate","recovery/enable-business"].includes(path);
  const upstream=await fetch(origin+"/api/backup/"+path,{method,cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(upload||download?30*60*1000:path==="artifacts/remote"?120000:longOperation?60000:15000),headers:{Authorization:"Bearer "+token,...(upload?{"Content-Type":"application/zip"}:body?{"Content-Type":"application/json"}:{})},body:upload?req.body:body,...(upload?{duplex:"half"}:{})} as RequestInit);
  if(download&&upstream.ok)return new NextResponse(upstream.body,{status:upstream.status,headers:{"Content-Type":"application/zip","Content-Disposition":upstream.headers.get("content-disposition")||'attachment; filename="backup.zip"',"Cache-Control":"private, no-store","X-Content-Type-Options":"nosniff"}});
  const bytes=await upstream.arrayBuffer();if(bytes.byteLength>2_000_000)return NextResponse.json({error:"oversize_response"},{status:502});
  return new NextResponse(bytes,{status:upstream.status,headers:{"Content-Type":"application/json","Cache-Control":"private, no-store","X-Content-Type-Options":"nosniff"}});
 }catch{return NextResponse.json({error:"backup_controller_unavailable"},{status:503});}
}
export const dynamic="force-dynamic";
export const GET=(r:NextRequest,c:Context)=>handle(r,c,"GET");
export const PUT=(r:NextRequest,c:Context)=>handle(r,c,"PUT");
export const POST=(r:NextRequest,c:Context)=>handle(r,c,"POST");
