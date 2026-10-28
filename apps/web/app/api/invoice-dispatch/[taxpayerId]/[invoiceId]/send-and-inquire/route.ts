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
 if(Number(request.headers.get("content-length")??"0")>0)return fail(400,"payload_not_supported");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 const base=identityApiOrigin();
 const invoicePath="/api/dev/taxpayers/"+taxpayerId+"/invoices/"+invoiceId+"/dispatch/";
 const requestJson=async(path:string,method:"GET"|"POST"="GET",body?:object)=>{
  const response=await fetch(base+path,{method,cache:"no-store",redirect:"manual",
   signal:AbortSignal.timeout(method==="POST"?60000:12000),
   headers:{Authorization:"Bearer "+token,...(body?{"Content-Type":"application/json"}:{})},
   ...(body?{body:JSON.stringify(body)}:{})});
  const raw=await response.text();
  if(raw.length>70000)throw Error("response_too_large");
  let data:Record<string,unknown>={};
  try{const parsed:unknown=JSON.parse(raw);if(parsed&&typeof parsed==="object"&&!Array.isArray(parsed))data=parsed as Record<string,unknown>;}catch{}
  if(!response.ok)throw Error("step_failed_http_"+response.status);
  return data;
 };
 try{
  const initial=await requestJson(invoicePath+"status");
  const initialState=String(initial.status??"NotPrepared");
  if(["Sending","Indeterminate","RetryEligible","FailedTerminal","Unrecognized"].includes(initialState))
   return fail(409,"existing_operation_requires_reconciliation");
  if(initialState==="Acknowledged")
   return NextResponse.json({status:"Acknowledged",referenceNumber:initial.referenceNumber??null,
    safeCode:"already_acknowledged_no_resend",inquiryPerformed:false},{headers:{"Cache-Control":"no-store"}});
  let readiness=await requestJson(invoicePath+"readiness");
  if(readiness.environment!=="Sandbox")return fail(400,"sandbox_required");
  // Internal review is automatic; normal owner-level send requires no extra MFA/admin approval.
  if(readiness.invoiceState==="Draft"){
   await requestJson("/api/dev/taxpayers/"+taxpayerId+"/invoices/"+invoiceId+"/validate-internal",
    "POST",{expectedRevision:readiness.invoiceRevision});
   readiness=await requestJson(invoicePath+"readiness");
  }
  if(readiness.invoiceState==="Validated"){
   const status=await requestJson("/api/dev/invoices/"+invoiceId+"/review/status");
   let reviewState=String(status.state??"");
   let revision=Number(status.revision);
   if(reviewState==="Draft"||reviewState==="ChangesRequested"){
    const submitted=await requestJson("/api/dev/invoices/"+invoiceId+"/review/submit","POST",
     {expectedReviewRevision:revision,idempotencyKey:crypto.randomUUID()});
    reviewState=String(submitted.state??"");revision=Number(submitted.revision);
   }
   if(reviewState==="InReview"){
    const approved=await requestJson("/api/dev/invoices/"+invoiceId+"/review/approve","POST",
     {expectedReviewRevision:revision,idempotencyKey:crypto.randomUUID()});
    reviewState=String(approved.state??"");
   }
   if(reviewState!=="Approved")return fail(409,"review_not_approved");
   await requestJson("/api/dev/taxpayers/"+taxpayerId+"/invoices/"+invoiceId+"/finalize-internal",
    "POST",{expectedRevision:readiness.invoiceRevision});
   readiness=await requestJson(invoicePath+"readiness");
  }
  if(readiness.invoiceState!=="Finalized")return fail(409,"invoice_not_finalized");
  let state=initialState;
  if(state==="NotPrepared"){
   if(Array.isArray(readiness.blockingCodes)&&readiness.blockingCodes.includes("invoice.serial.durable_reservation_required"))
    await requestJson(invoicePath+"serial-reservation","POST",{expectedRevision:readiness.invoiceRevision});
   await requestJson(invoicePath+"prepare-legacy","POST");
   state="Prepared";
  }
  if(state==="Prepared"){
   await requestJson(invoicePath+"arm","POST");
   state="Reserved";
  }
  if(state!=="Reserved")return fail(409,"dispatch_not_reserved");
  // One invoice send only, no automatic retry after an ambiguous response.
  const sent=await requestJson(invoicePath+"send","POST");
  if(sent.status!=="Acknowledged"||typeof sent.referenceNumber!=="string")
   return NextResponse.json({status:sent.status??"Indeterminate",safeCode:sent.safeCode??"send_not_acknowledged",
    inquiryPerformed:false},{headers:{"Cache-Control":"no-store"}});
  let inquiryCode="inquiry_not_completed";
  try{
   const inquiry=await requestJson(invoicePath+"inquiry-by-reference","POST");
   inquiryCode=typeof inquiry.safeCode==="string"?inquiry.safeCode:"inquiry_pending";
  }catch{inquiryCode="inquiry_unavailable";}
  return NextResponse.json({status:"Acknowledged",referenceNumber:sent.referenceNumber,
   inquiryPerformed:inquiryCode!=="inquiry_unavailable",safeCode:inquiryCode},
   {headers:{"Cache-Control":"no-store"}});
 }catch(e){
  return fail(409,e instanceof Error&&/^step_failed_http_\d{3}$/.test(e.message)?e.message:"workflow_stopped_check_status");
 }
}
