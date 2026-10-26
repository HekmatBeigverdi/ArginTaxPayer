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
 if(Number(request.headers.get("content-length")??"0")>0)return reject(400,"payload_not_supported");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return reject(401,"unauthorized");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/inquiry-by-reference",{
   method:"POST",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(35000),
   headers:{Authorization:"Bearer "+token}
  });
  if(upstream.status===401||upstream.status===403||upstream.status===404)
   return reject(upstream.status,"inquiry_access_denied");
  if(![200,400].includes(upstream.status))return reject(503,"inquiry_api_unavailable");
  const raw=await upstream.text();
  if(raw.length>1024)return reject(502,"inquiry_response_invalid");
  const body:unknown=JSON.parse(raw);
  if(!body||typeof body!=="object"||Array.isArray(body))
   return reject(502,"inquiry_response_invalid");
  const result=body as Record<string,unknown>;
  if(upstream.status===400)return reject(400,typeof result.error==="string"&&
    /^[a-z_]{1,100}$/.test(result.error)?result.error:"inquiry_rejected");
  if(result.authoritySubmitted!==false||result.fiscalAccepted!==false||
   typeof result.safeCode!=="string"||!/^[a-z0-9_.]{1,100}$/.test(result.safeCode))
   return reject(502,"inquiry_response_invalid");
  if(result.referenceNumber!=null &&
    (typeof result.referenceNumber!=="string"||
     !/^[A-Za-z0-9_-]{1,128}$/.test(result.referenceNumber)))
   return reject(502,"inquiry_response_invalid");
  return NextResponse.json({safeCode:result.safeCode,
    referenceNumber:result.referenceNumber??null,
    fiscalAccepted:false,authoritySubmitted:false},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return reject(503,"inquiry_api_unavailable");}
}
