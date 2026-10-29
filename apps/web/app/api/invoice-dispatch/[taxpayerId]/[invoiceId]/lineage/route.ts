import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export async function GET(request:NextRequest,context:Context){
 const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/lineage",{
   cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(12000),
   headers:{Authorization:"Bearer "+token}});
  if(!response.ok)return fail([401,403,404].includes(response.status)?response.status:502,"lineage_unavailable");
  const raw=await response.text();
  if(raw.length>30000)return fail(502,"invalid_lineage");
  const parsed:unknown=JSON.parse(raw);
  if(!parsed||typeof parsed!=="object"||Array.isArray(parsed))return fail(502,"invalid_lineage");
  const body=parsed as Record<string,unknown>;
  if(typeof body.hasDispatchAttempt!=="boolean"||!Array.isArray(body.nodes)||body.nodes.length>100)
   return fail(502,"invalid_lineage");
  return NextResponse.json(body,{headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"lineage_unavailable");}
}
