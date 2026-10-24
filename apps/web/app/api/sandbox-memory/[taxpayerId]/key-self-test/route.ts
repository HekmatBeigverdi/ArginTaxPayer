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
  if(request.headers.get("content-type")||Number(request.headers.get("content-length")??"0")>0)
    return fail(400,"body_not_allowed");
  const token=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!token)return fail(401,"unauthorized");
  try{
    const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
      taxpayerId+"/sandbox-memory/key-self-test",{
      method:"POST",cache:"no-store",redirect:"manual",
      signal:AbortSignal.timeout(15000),
      headers:{Authorization:"Bearer "+token}
    });
    if(![200,400,401,403,404,409,503].includes(response.status))
      return fail(502,"unexpected_api_status");
    const raw=await response.text();
    if(raw.length>2048)return fail(502,"response_too_large");
    const value:unknown=JSON.parse(raw);
    if(!value||typeof value!=="object"||Array.isArray(value))
      return fail(502,"invalid_api_response");
    const r=value as Record<string,unknown>;
    return NextResponse.json({environment:"Sandbox",
      offlineKeyVerified:response.status===200&&r.status==="OfflineKeyVerified",
      authorityAuthenticated:false,
      error:typeof r.error==="string"&&/^[a-z_]{0,80}$/.test(r.error)
        ?r.error:"offline_test_failed"
    },{status:response.status,headers:{"Cache-Control":"no-store"}});
  }catch{return fail(503,"api_unavailable");}
}
