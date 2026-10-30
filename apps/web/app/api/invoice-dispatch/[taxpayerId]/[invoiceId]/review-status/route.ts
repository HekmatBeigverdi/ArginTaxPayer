import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
export async function GET(request:NextRequest,context:Context){
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/review-status",{
   method:"GET",cache:"no-store",redirect:"manual",
   signal:AbortSignal.timeout(10000),headers:{Authorization:"Bearer "+token}
  });
  if(upstream.status===401||upstream.status===403||upstream.status===404)
   return fail(upstream.status,"review_unavailable");
  if(upstream.status!==200)return fail(502,"invalid_upstream_status");
  const raw=await upstream.text();
  if(raw.length>512)return fail(502,"response_too_large");
  const value:unknown=JSON.parse(raw);
  if(!value||typeof value!=="object"||Array.isArray(value))
   return fail(502,"invalid_review_response");
  const data=value as Record<string,unknown>;
  if(!["Draft","InReview","ChangesRequested","Approved"].includes(String(data.state))||
     !Number.isSafeInteger(data.revision)||Number(data.revision)<1)
   return fail(502,"invalid_review_response");
  return NextResponse.json({state:data.state,revision:data.revision},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"review_api_unavailable");}
}
