import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";
export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
export async function GET(request:NextRequest,context:Context){
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/print-preview",{
   cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(10000),
   headers:{Authorization:"Bearer "+token}});
  if(!response.ok)return fail([400,401,403,404].includes(response.status)?response.status:502,"preview_unavailable");
  const raw=await response.text();
  if(raw.length>120000)return fail(502,"preview_too_large");
  const value:unknown=JSON.parse(raw);
  if(!value||typeof value!=="object"||Array.isArray(value))return fail(502,"invalid_preview");
  const data=value as Record<string,unknown>;
  if(!Array.isArray(data.lines)||data.lines.length>150||
     typeof data.invoiceId!=="string"||!uuid.test(data.invoiceId)||
     typeof data.totalRial!=="number")return fail(502,"invalid_preview");
  return NextResponse.json(data,{headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"preview_unavailable");}
}
