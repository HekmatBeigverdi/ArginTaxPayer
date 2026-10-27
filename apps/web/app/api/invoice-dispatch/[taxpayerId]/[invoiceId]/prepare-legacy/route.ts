import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../../lib/identity-origin.mjs";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail=(status:number,error:string)=>NextResponse.json({error},
 {status,headers:{"Cache-Control":"no-store"}});
export async function POST(request:NextRequest,context:Context){
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 if(!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),request.nextUrl.protocol))
  return fail(403,"origin_not_allowed");
 const bearer=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!bearer)return fail(401,"unauthorized");
 if(Number(request.headers.get("content-length")??"0")>0)
  return fail(400,"unexpected_payload");
 try{
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/prepare-legacy",{
   method:"POST",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(30000),
   headers:{Authorization:"Bearer "+bearer}
  });
  if(response.status===401)return fail(401,"unauthorized");
  if(response.status===403)return fail(403,"forbidden");
  if(response.status===404)return fail(404,"not_found");
  if(![200,400,409].includes(response.status))
   return fail(503,"preparation_api_unavailable");
  const raw=await response.text();
  if(raw.length>65536)return fail(502,"response_too_large");
  const payload:unknown=JSON.parse(raw);
  if(!payload||typeof payload!=="object"||Array.isArray(payload))
   return fail(502,"invalid_preparation_response");
  const value=payload as Record<string,unknown>;
  const code=response.ok?value.status:value.error;
  if(typeof code!=="string"||!/^[a-z0-9_]{1,100}$/.test(code))
   return fail(502,"invalid_preparation_response");
  if(!response.ok)return fail(response.status,code);
  if(value.authoritySubmitted!==false||
     typeof value.operationId!=="string"||!uuid.test(value.operationId)||
     typeof value.wirePreviewJson!=="string"||value.wirePreviewJson.length>60000)
   return fail(502,"invalid_preparation_response");
  let wire:unknown;
  try{wire=JSON.parse(value.wirePreviewJson);}catch{return fail(502,"invalid_preparation_response");}
  if(!wire||typeof wire!=="object"||Array.isArray(wire)||
     !("header" in wire)||!("body" in wire))return fail(502,"invalid_preparation_response");
  return NextResponse.json({status:code,operationId:value.operationId,
   environment:"Sandbox",authoritySubmitted:false,wirePreviewJson:value.wirePreviewJson},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"preparation_api_unavailable");}
}
