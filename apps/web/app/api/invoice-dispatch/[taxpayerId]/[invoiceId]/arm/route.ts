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
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/arm",{
    method:"POST",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(15000),
    headers:{Authorization:"Bearer "+token}
  });
  if(upstream.status===401||upstream.status===403||upstream.status===404)
   return fail(upstream.status,"access_denied");
  if(![200,400,409].includes(upstream.status))return fail(502,"arm_upstream_unavailable");
  const raw=await upstream.text();
  if(raw.length>1024)return fail(502,"arm_response_invalid");
  const body:unknown=JSON.parse(raw);
  if(!body||typeof body!=="object"||Array.isArray(body))return fail(502,"arm_response_invalid");
  const data=body as Record<string,unknown>;
  if(upstream.status!==200)return fail(upstream.status,
    typeof data.error==="string"&&/^[a-z_]{1,100}$/.test(data.error)?data.error:"arm_rejected");
  if(data.status!=="Reserved"||data.authoritySubmitted!==false)
   return fail(502,"arm_response_invalid");
  return NextResponse.json({status:"Reserved",authoritySubmitted:false},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"arm_api_unavailable");}
}
