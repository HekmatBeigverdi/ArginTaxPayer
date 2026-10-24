import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../lib/identity";
import {isSameLocalOrigin} from "../../../../../lib/identity-origin.mjs";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function fail(status:number,error:string){
  return NextResponse.json({error,environment:"Sandbox",authenticationConfirmed:false},
    {status,headers:{"Cache-Control":"no-store"}});
}
export async function POST(request:NextRequest,context:Context){
  if(!identityUiEnabled())return fail(404,"unavailable");
  const {taxpayerId}=await context.params;
  if(!uuid.test(taxpayerId))return fail(404,"not_found");
  if(!isSameLocalOrigin(request.headers.get("origin"),
    request.headers.get("host"),request.nextUrl.protocol))
    return fail(403,"origin_not_allowed");
  if(request.headers.get("content-type")||
    Number(request.headers.get("content-length")??"0")>0)
    return fail(400,"request_body_not_allowed");
  const token=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!token)return fail(401,"unauthorized");
  try{
    const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
      taxpayerId+"/sandbox-memory/connection-test",{
      method:"POST",cache:"no-store",redirect:"manual",
      signal:AbortSignal.timeout(30000),
      headers:{Authorization:"Bearer "+token}
    });
    if(![200,400,401,403,404,409,503,504].includes(upstream.status))
      return fail(502,"unexpected_api_status");
    const raw=await upstream.text();
    if(raw.length>4096)return fail(502,"upstream_response_too_large");
    const data:unknown=JSON.parse(raw);
    if(!data||typeof data!=="object"||Array.isArray(data))
      return fail(502,"invalid_response");
    const row=data as Record<string,unknown>;
    const allowed=["Authenticated","AuthenticationRejected","NotAuthorized","KeyNotReady",
      "VaultUnavailable","EndpointUnavailable","TimedOut","UnsupportedProfile",
      "Indeterminate","Unavailable"];
    const status=typeof row.status==="string"&&allowed.includes(row.status)
      ?row.status:"Unavailable";
    const info=row.fiscalInformation&&typeof row.fiscalInformation==="object"&&
      !Array.isArray(row.fiscalInformation)
      ?row.fiscalInformation as Record<string,unknown>:null;
    const bounded=(v:unknown)=>typeof v==="string"&&v.length<=256?v:null;
    return NextResponse.json({
      environment:"Sandbox",status,
      authenticationConfirmed:upstream.ok&&status==="Authenticated"&&
        row.authenticationConfirmed===true,
      portalRegistrationConfirmed:false,fiscalMemoryActivationConfirmed:false,
      testedAtUtc:typeof row.testedAtUtc==="string"&&row.testedAtUtc.length<=40?
        row.testedAtUtc:null,
      error:typeof row.error==="string"&&/^[a-z_]{1,80}$/.test(row.error)?row.error:null,
      fiscalInformation:upstream.ok&&status==="Authenticated"&&info?{
        nameTrade:bounded(info.nameTrade),fiscalStatus:bounded(info.fiscalStatus),
        economicCode:bounded(info.economicCode),nationalId:bounded(info.nationalId)
      }:null
    },{status:upstream.status,headers:{"Cache-Control":"no-store"}});
  }catch{return fail(503,"sandbox_authority_unavailable");}
}
