import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../../lib/identity-origin.mjs";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
export async function POST(request:NextRequest,context:Context){
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 if(!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),request.nextUrl.protocol))
  return fail(403,"origin_not_allowed");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 let input:unknown;
 try{
  if(Number(request.headers.get("content-length")??"0")>512)return fail(413,"request_too_large");
  input=await request.json();
 }catch{return fail(400,"invalid_request");}
 if(!input||typeof input!=="object"||Array.isArray(input))return fail(400,"invalid_request");
 const revision=(input as Record<string,unknown>).expectedRevision;
 if(!Number.isSafeInteger(revision)||Number(revision)<1)return fail(400,"invalid_revision");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/serial-reservation",{
   method:"POST",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(15000),
   headers:{Authorization:"Bearer "+token,"Content-Type":"application/json"},
   body:JSON.stringify({expectedRevision:revision})
  });
  if(upstream.status>=500)return fail(503,"upstream_server_error");
  if(![200,400,401,403,404,409].includes(upstream.status))return fail(502,"invalid_upstream_status");
  // ASP.NET Core Results.Unauthorized/Forbid/NotFound commonly have empty bodies.
  // Preserve their meaning instead of misclassifying them as malformed JSON.
  if(upstream.status===401)return fail(401,"unauthorized");
  if(upstream.status===403)return fail(403,"forbidden");
  if(upstream.status===404)return fail(404,"not_found");
  const raw=await upstream.text();
  if(raw.length>1024)return fail(502,"response_too_large");
  let value:unknown;
  try{value=JSON.parse(raw);}catch{return fail(502,"invalid_upstream_response");}
  if(!value||typeof value!=="object"||Array.isArray(value))return fail(502,"invalid_upstream_response");
  const record=value as Record<string,unknown>;
  const code=upstream.ok?record.status:record.error;
  if(typeof code!=="string"||!(upstream.ok?["Reserved","IdenticalReplay"].includes(code):/^[a-z_]{1,100}$/.test(code)))
   return fail(502,"invalid_upstream_response");
  return NextResponse.json(upstream.ok?{status:code,authoritySubmitted:false,
   fiscalTaxId:typeof record.fiscalTaxId==="string"&&/^[A-Z0-9]{22}$/.test(record.fiscalTaxId)?record.fiscalTaxId:null}:{error:code},
   {status:upstream.status,headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"api_unavailable");}
}
