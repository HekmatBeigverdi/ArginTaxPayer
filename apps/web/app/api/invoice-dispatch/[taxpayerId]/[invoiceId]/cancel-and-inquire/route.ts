import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../../lib/identity-origin.mjs";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export async function POST(request:NextRequest,context:Context){
 const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 if(!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),request.nextUrl.protocol))
  return fail(403,"origin_not_allowed");
 if(Number(request.headers.get("content-length")??"0")>0)return fail(400,"payload_not_supported");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  // Exactly one backend API call. Backend owns all transitions and inquiry.
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/cancel-and-inquire",{
   method:"POST",cache:"no-store",redirect:"manual",
   signal:AbortSignal.timeout(115000),headers:{Authorization:"Bearer "+token}});
  if(![200,400,401,403,404,409].includes(response.status))return fail(502,"cancel_api_unavailable");
  const raw=await response.text();
  if(raw.length>4096)return fail(502,"invalid_cancellation_response");
  const parsed:unknown=JSON.parse(raw);
  if(!parsed||typeof parsed!=="object"||Array.isArray(parsed))return fail(502,"invalid_cancellation_response");
  const data=parsed as Record<string,unknown>;
  if(!response.ok)return fail(response.status,typeof data.error==="string"?data.error:"cancellation_failed");
  return NextResponse.json({
   invoiceId:typeof data.invoiceId==="string"?data.invoiceId:null,
   status:data.status==="Acknowledged"?"Acknowledged":"Blocked",
   referenceNumber:typeof data.referenceNumber==="string"?data.referenceNumber:null,
   safeCode:typeof data.inquiryCode==="string"?data.inquiryCode:
    typeof data.safeCode==="string"?data.safeCode:"pending"
  },{headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"cancel_api_unavailable");}
}
