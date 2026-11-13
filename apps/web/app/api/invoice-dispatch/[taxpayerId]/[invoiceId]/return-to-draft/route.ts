import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../../lib/identity-origin.mjs";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const reject=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});

export async function POST(request:NextRequest,context:Context){
 if(!identityUiEnabled())return reject(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return reject(404,"not_found");
 if(!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),request.nextUrl.protocol))
  return reject(403,"origin_not_allowed");
 if(await hasPayload(request))return reject(400,"payload_not_supported");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return reject(401,"unauthorized");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/return-to-draft",{
   method:"POST",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(20000),
   headers:{Authorization:"Bearer "+token}
  });
  if([401,403,404].includes(upstream.status))return reject(upstream.status,"access_denied");
  if(![200,400,409].includes(upstream.status))return reject(503,"api_unavailable");
  const raw=await upstream.text();
  if(raw.length>10000)return reject(502,"response_invalid");
  let body:unknown;try{body=JSON.parse(raw);}catch{return reject(502,"response_invalid");}
  if(!body||typeof body!=="object"||Array.isArray(body))return reject(502,"response_invalid");
  const result=body as Record<string,unknown>;
  if(!upstream.ok)return reject(upstream.status,typeof result.error==="string"&&
   /^[a-z_]{1,100}$/.test(result.error)?result.error:"return_to_draft_rejected");
  if(typeof result.workspaceId!=="string"||!uuid.test(result.workspaceId)||
   result.authoritySubmitted!==false||typeof result.existingWorkspace!=="boolean")
   return reject(502,"response_invalid");
  return NextResponse.json({workspaceId:result.workspaceId,authoritySubmitted:false,
   existingWorkspace:result.existingWorkspace},{headers:{"Cache-Control":"no-store"}});
 }catch{return reject(503,"api_unavailable");}
}

async function hasPayload(request:NextRequest):Promise<boolean>{
 const length=request.headers.get("content-length");
 if(length!==null&&(!/^\d+$/.test(length)||Number(length)>0))return true;
 if(request.body===null)return false;
 const reader=request.body.getReader();
 try{
  while(true){
   const part=await reader.read();
   if(part.done)return false;
   if(part.value.byteLength>0){await reader.cancel();return true;}
  }
 }catch{return true;}
}
