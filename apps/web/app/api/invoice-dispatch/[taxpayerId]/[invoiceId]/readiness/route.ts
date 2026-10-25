import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const codes=new Set([
 "invoice.snapshot.invalid",
 "invoice.snapshot.not_internal_promoted",
 "invoice.sandbox.memory_not_bound",
 "invoice.finalization.not_finalized",
 "invoice.rules.not_submission_verified",
 "invoice.fiscal_identifier.not_issued",
 "invoice.serial.durable_reservation_required",
 "invoice.sandbox.certificate_chain_not_verified",
 "invoice.v2.protocol_not_verified_for_sandbox",
 "invoice.legacy.transport_not_registered",
 "invoice.signed_packet.not_prepared",
 "invoice.sandbox_outbox.not_connected",
 "invoice.transport.disabled",
 "invoice.owner_live_send_approval.required"
]);
const error=(status:number,code:string)=>NextResponse.json({error:code},
 {status,headers:{"Cache-Control":"no-store"}});
export async function GET(request:NextRequest,context:Context){
 if(!identityUiEnabled())return error(404,"unavailable");
 const {taxpayerId,invoiceId}=await context.params;
 if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return error(404,"not_found");
 const token=request.cookies.get(ACCESS_COOKIE)?.value;
 if(!token)return error(401,"unauthorized");
 try{
  const response=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+
   taxpayerId+"/invoices/"+invoiceId+"/dispatch/readiness",{
   method:"GET",cache:"no-store",redirect:"manual",
   signal:AbortSignal.timeout(10000),
   headers:{Authorization:"Bearer "+token}});
  if(![200,400,401,403,404].includes(response.status))
   return error(502,"invalid_upstream_status");
  if(!response.ok)return error(response.status,"readiness_unavailable");
  const raw=await response.text();
  if(raw.length>4096)return error(502,"response_too_large");
  const value:unknown=JSON.parse(raw);
  if(!value||typeof value!=="object"||Array.isArray(value))
   return error(502,"invalid_response");
  const data=value as Record<string,unknown>;
  if(data.readyForLiveSend!==false||data.submissionEnabled!==false||
   data.signedPacketAvailable!==false||data.fiscalAccepted!==false||
   typeof data.hasImmutableSnapshot!=="boolean"||
   !Number.isSafeInteger(data.invoiceRevision)||Number(data.invoiceRevision)<1||
   (data.sandboxFiscalMemoryId!==null&&typeof data.sandboxFiscalMemoryId!=="string")||
   !["Sandbox","Unverified"].includes(String(data.environment))||
   !["Draft","Validated","Finalized"].includes(String(data.invoiceState))||
   !Array.isArray(data.blockingCodes)||data.blockingCodes.length>20||
   data.blockingCodes.some((item:unknown)=>typeof item!=="string"||!codes.has(item)))
   return error(502,"invalid_readiness_policy");
  return NextResponse.json({
   environment:data.environment,invoiceState:data.invoiceState,
   hasImmutableSnapshot:data.hasImmutableSnapshot,
   invoiceRevision:data.invoiceRevision,
   sandboxFiscalMemoryId:data.sandboxFiscalMemoryId,
   readyForLiveSend:false,submissionEnabled:false,
   signedPacketAvailable:false,fiscalAccepted:false,
   blockingCodes:data.blockingCodes
  },{headers:{"Cache-Control":"no-store"}});
 }catch{return error(503,"readiness_unavailable");}
}
