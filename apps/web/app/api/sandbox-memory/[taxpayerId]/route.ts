import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../lib/identity-origin.mjs";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function error(status:number,code:string) {
  return NextResponse.json({error:code},{status,headers:{"Cache-Control":"no-store"}});
}
async function handle(request:NextRequest,context:Context,method:"GET"|"POST"){
  if(!identityUiEnabled())return error(404,"unavailable");
  const {taxpayerId}=await context.params;
  if(!uuid.test(taxpayerId))return error(404,"not_found");
  const token=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!token)return error(401,"unauthorized");
  let body:string|undefined;
  if(method==="POST"){
    if(!isSameLocalOrigin(request.headers.get("origin"),
      request.headers.get("host"),request.nextUrl.protocol))return error(403,"origin_not_allowed");
    if(!(request.headers.get("content-type")??"").toLowerCase().startsWith("application/json"))
      return error(415,"json_required");
    if(Number(request.headers.get("content-length")??"0")>12000)return error(413,"payload_too_large");
    try {
      body=await request.text();
      if(!body||body.length>12000)return error(400,"invalid_payload");
      const data:unknown=JSON.parse(body);
      if(!data||typeof data!=="object"||Array.isArray(data))return error(400,"invalid_payload");
      const row=data as Record<string,unknown>;
      if(Object.keys(row).some(x=>!["memoryIdentifier","publicKeyPem"].includes(x)) ||
        typeof row.memoryIdentifier!=="string"||
        !/^[A-Z0-9]{6,32}$/.test(row.memoryIdentifier)||
        typeof row.publicKeyPem!=="string"||
        row.publicKeyPem.length<100||row.publicKeyPem.length>9999||
        !row.publicKeyPem.startsWith("-----BEGIN PUBLIC KEY-----"))
        return error(400,"invalid_sandbox_metadata");
    }catch{return error(400,"invalid_payload");}
  }
  try {
    const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
      taxpayerId+"/sandbox-memory",{
      method,cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(15000),
      headers:{Authorization:"Bearer "+token,...(body?{"Content-Type":"application/json"}:{})},
      ...(body?{body}:{})
    });
    if(![200,201,400,401,403,404,409,503].includes(response.status))
      return error(502,"unexpected_api_status");
    const raw=await response.text();
    if(raw.length>4096)return error(502,"response_too_large");
    const decoded:unknown=JSON.parse(raw);
    if(!decoded||typeof decoded!=="object"||Array.isArray(decoded))
      return error(502,"invalid_api_response");
    const source=decoded as Record<string,unknown>;
    const detail=source.data&&typeof source.data==="object"&&!Array.isArray(source.data)
      ?source.data as Record<string,unknown>:null;
    return NextResponse.json({
      environment:"Sandbox",
      error:typeof source.error==="string" && /^[a-z_]{0,80}$/.test(source.error)
        ?source.error:"request_rejected",
      data:detail&&typeof detail.memoryIdentifier==="string"&&
        /^[A-Z0-9]{6,32}$/.test(detail.memoryIdentifier)&&
        typeof detail.publicFingerprintSha256==="string"&&
        /^[0-9a-f]{64}$/.test(detail.publicFingerprintSha256)
        ?{memoryIdentifier:detail.memoryIdentifier,
          publicFingerprintSha256:detail.publicFingerprintSha256,
          revision:detail.revision,keyCustodyReady:false,connectionTestReady:false}
        :null,
      connectionTestReady:false
    },{status:response.status,headers:{"Cache-Control":"no-store"}});
  }catch{return error(503,"api_unavailable");}
}
export async function GET(request:NextRequest,context:Context){return handle(request,context,"GET");}
export async function POST(request:NextRequest,context:Context){return handle(request,context,"POST");}
