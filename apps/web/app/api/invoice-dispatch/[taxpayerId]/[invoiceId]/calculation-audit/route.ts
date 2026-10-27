import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const codes=new Set(["0204501","0304401","0200201","00010","00026"]);
const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});

export async function GET(request:NextRequest,context:Context){
 if(!identityUiEnabled())return fail(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return fail(401,"unauthorized");
 try{
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
   "/invoices/"+invoiceId+"/dispatch/calculation-audit",{
   method:"GET",cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(10000),
   headers:{Authorization:"Bearer "+token}});
  if(![200,400,401,403,404].includes(response.status))return fail(502,"invalid_upstream_status");
  if(!response.ok)return fail(response.status,"calculation_audit_unavailable");
  const raw=await response.text();
  if(raw.length>12000)return fail(502,"response_too_large");
  const value=JSON.parse(raw) as Record<string,unknown>;
  const revision=value.invoiceRevision;
  const findings=value.findings;
  if(!Number.isSafeInteger(revision)||Number(revision)<1||!Array.isArray(findings)||findings.length>20)
   return fail(502,"invalid_calculation_audit");
  const safe=findings.map(item=>{
   if(!item||typeof item!=="object"||Array.isArray(item))throw Error("finding");
   const row=item as Record<string,unknown>;
   if(typeof row.code!=="string"||!codes.has(row.code)||typeof row.field!=="string"||
      row.field.length>120||typeof row.message!=="string"||row.message.length>500)throw Error("finding");
   const optional=(key:string)=>row[key]===null||row[key]===undefined?null:
    typeof row[key]==="string"&&row[key].length<=120?row[key]:undefined;
   const expected=optional("expected");const actual=optional("actual");
   if(expected===undefined||actual===undefined)throw Error("finding");
   return {code:row.code,field:row.field,message:row.message,expected,actual};
  });
  const canResend=value.canResend===true&&safe.length===0;
  return NextResponse.json({invoiceRevision:revision,canResend,findings:safe},
   {headers:{"Cache-Control":"no-store"}});
 }catch{return fail(503,"calculation_audit_unavailable");}
}
