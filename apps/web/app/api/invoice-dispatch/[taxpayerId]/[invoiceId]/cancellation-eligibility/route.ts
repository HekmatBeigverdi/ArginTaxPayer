import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export async function GET(request:NextRequest,context:Context){
 const fail=(status:number,error:string)=>NextResponse.json({eligible:false,error},{status,headers:{"Cache-Control":"no-store"}});
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  const upstream=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/cancellation-eligibility",{
   cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(12000),
   headers:{Authorization:"Bearer "+token}});
  if(![200,401,403,404,409].includes(upstream.status))return fail(502,"preflight_unavailable");
  const raw=await upstream.text();
  if(raw.length>4096)return fail(502,"preflight_invalid");
  const parsed:unknown=JSON.parse(raw);
  if(!parsed||typeof parsed!=="object"||Array.isArray(parsed))return fail(502,"preflight_invalid");
  const v=parsed as Record<string,unknown>;
  return NextResponse.json({
   eligible:upstream.ok&&v.eligible===true,
   fiscalTaxId:typeof v.fiscalTaxId==="string"?v.fiscalTaxId:null,
   code:typeof v.code==="string"?v.code:"not_eligible"
  },{status:upstream.status,headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"preflight_unavailable");}
}
