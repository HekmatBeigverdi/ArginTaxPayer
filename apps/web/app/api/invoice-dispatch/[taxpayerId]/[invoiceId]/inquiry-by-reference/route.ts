import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../../lib/identity-origin.mjs";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const reject=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
export async function GET(request:NextRequest,context:Context){
 return handle(request,context,"GET");
}
export async function POST(request:NextRequest,context:Context){
 return handle(request,context,"POST");
}
async function handle(request:NextRequest,context:Context,method:"GET"|"POST"){
 if(!identityUiEnabled())return reject(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return reject(404,"not_found");
 if((method==="POST"||request.headers.has("origin"))&&!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),request.nextUrl.protocol))
  return reject(403,"origin_not_allowed");
 if(await hasPayload(request))return reject(400,"payload_not_supported");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return reject(401,"unauthorized");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/inquiry-by-reference",{
   method,cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(35000),
   headers:{Authorization:"Bearer "+token}
  });
  if(upstream.status===401||upstream.status===403||upstream.status===404)
   return reject(upstream.status,"inquiry_access_denied");
  if(![200,400].includes(upstream.status))return reject(503,"inquiry_api_unavailable");
  const raw=await upstream.text();
  if(raw.length>300000)return reject(502,"inquiry_response_invalid");
  let body:unknown;
  try{body=JSON.parse(raw);}catch{return reject(502,"inquiry_response_invalid");}
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
  let authorityResult=null;
  if(result.authorityResult!=null){
   const value=result.authorityResult;
   if(typeof value!=="object"||Array.isArray(value))return reject(502,"inquiry_response_invalid");
   const report=value as Record<string,unknown>;
   const messages=(items:unknown):{code:string;message:string}[]|null=>{
    if(!Array.isArray(items)||items.length>100)return null;
    const output:{code:string;message:string}[]=[];
    for(const item of items){
     if(!item||typeof item!=="object"||Array.isArray(item)||
       typeof item.code!=="string"||item.code.length>80||
       typeof item.message!=="string"||item.message.length>1000)return null;
     output.push({code:item.code,message:item.message});
    }
    return output;
   };
   const errors=messages(report.errors),warnings=messages(report.warnings);
   if(typeof report.status!=="string"||!["FAILED","SUCCESS","PENDING","UNKNOWN"].includes(report.status)||
     !errors||!warnings||typeof report.canPrepareCorrection!=="boolean"||
     typeof report.observedAtUtc!=="string"||report.observedAtUtc.length>80||
     !Number.isFinite(Date.parse(report.observedAtUtc)))return reject(502,"inquiry_response_invalid");
   authorityResult={status:report.status,errors,warnings,
    canPrepareCorrection:report.status==="FAILED"&&report.canPrepareCorrection,
    observedAtUtc:report.observedAtUtc};
  }
  return NextResponse.json({safeCode:result.safeCode,
    referenceNumber:result.referenceNumber??null,authorityResult,
    fiscalAccepted:false,authoritySubmitted:false},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return reject(503,"inquiry_api_unavailable");}
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
