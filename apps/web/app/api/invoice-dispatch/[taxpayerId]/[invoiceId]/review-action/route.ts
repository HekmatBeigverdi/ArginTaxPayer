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
 let data:Record<string,unknown>;
 try{
  if(Number(request.headers.get("content-length")??"0")>1024)return fail(413,"request_too_large");
  const payload:unknown=await request.json();
  if(!payload||typeof payload!=="object"||Array.isArray(payload))return fail(400,"invalid_request");
  data=payload as Record<string,unknown>;
 }catch{return fail(400,"invalid_request");}
 const action=data.action;
 const revision=data.expectedReviewRevision;
 const key=data.idempotencyKey;
 if((action!=="submit"&&action!=="approve")||
  !Number.isSafeInteger(revision)||Number(revision)<1||
  typeof key!=="string"||!uuid.test(key))
  return fail(400,"invalid_review_action");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/invoices/"+invoiceId+
   "/review/"+action,{method:"POST",cache:"no-store",redirect:"manual",
   signal:AbortSignal.timeout(15000),
   headers:{Authorization:"Bearer "+token,"Content-Type":"application/json"},
   body:JSON.stringify({expectedReviewRevision:revision,idempotencyKey:key})
  });
  if(upstream.status===401||upstream.status===403||upstream.status===404)
   return fail(upstream.status,"review_access_denied");
  if(![200,400,409].includes(upstream.status))return fail(502,"review_upstream_failure");
  const raw=await upstream.text();
  if(raw.length>1024)return fail(502,"review_response_too_large");
  const result:unknown=JSON.parse(raw);
  if(!result||typeof result!=="object"||Array.isArray(result))
   return fail(502,"invalid_review_response");
  const object=result as Record<string,unknown>;
  if(upstream.status===200){
   if(!["InReview","Approved"].includes(String(object.state))||
    !Number.isSafeInteger(object.revision))return fail(502,"invalid_review_response");
   return NextResponse.json({state:object.state,revision:object.revision},
    {headers:{"Cache-Control":"no-store"}});
  }
  const error=typeof object.error==="string"&&/^[a-z_]{1,100}$/.test(object.error)
   ?object.error:"review_request_rejected";
  return fail(upstream.status,error);
 }catch{return fail(503,"review_api_unavailable");}
}
