import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../lib/identity-origin.mjs";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function fail(status:number,error:string){
  return NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
}
export async function POST(request:NextRequest,context:Context){
  if(!identityUiEnabled())return fail(404,"unavailable");
  const {taxpayerId}=await context.params;
  if(!uuid.test(taxpayerId))return fail(404,"not_found");
  if(!isSameLocalOrigin(request.headers.get("origin"),request.headers.get("host"),
    request.nextUrl.protocol))return fail(403,"origin_not_allowed");
  if(!(request.headers.get("content-type")??"").toLowerCase().startsWith("application/json"))
    return fail(415,"json_required");
  const bearer=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!bearer)return fail(401,"unauthorized");
  if(Number(request.headers.get("content-length")??"0")>16000)
    return fail(413,"payload_too_large");
  let body:string;
  try {
    body=await request.text();
    if(!body||body.length>16000)return fail(400,"invalid_payload");
    const json:unknown=JSON.parse(body);
    if(!json||typeof json!=="object"||Array.isArray(json))
      return fail(400,"invalid_payload");
    const v=json as Record<string,unknown>;
    if(Object.keys(v).length!==1||typeof v.privateKeyPem!=="string"||
      v.privateKeyPem.length<100||v.privateKeyPem.length>11999||
      !v.privateKeyPem.startsWith("-----BEGIN PRIVATE KEY-----"))
      return fail(400,"invalid_private_key_input");
  }catch{return fail(400,"invalid_payload");}
  try {
    const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
      "/sandbox-memory/private-key",{
        method:"POST",cache:"no-store",redirect:"manual",
        signal:AbortSignal.timeout(20000),
        headers:{Authorization:"Bearer "+bearer,"Content-Type":"application/json"},
        body
      });
    if(![200,400,401,403,404,409,503].includes(response.status))
      return fail(502,"unexpected_api_status");
    const raw=await response.text();
    if(raw.length>4096)return fail(502,"response_too_large");
    const parsed:unknown=JSON.parse(raw);
    if(!parsed||typeof parsed!=="object"||Array.isArray(parsed))
      return fail(502,"invalid_api_response");
    const r=parsed as Record<string,unknown>;
    const entry=r.data&&typeof r.data==="object"&&!Array.isArray(r.data)
      ?r.data as Record<string,unknown>:null;
    return NextResponse.json({
      environment:"Sandbox",
      error:typeof r.error==="string"&&/^[a-z_]{0,80}$/.test(r.error)
        ?r.error:"request_rejected",
      keyCustodyReady:response.ok&&entry?.keyCustodyReady===true,
      connectionTestReady:false
    },{status:response.status,headers:{"Cache-Control":"no-store"}});
  }catch{return fail(503,"sandbox_key_storage_unavailable");}
}
