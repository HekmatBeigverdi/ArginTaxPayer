import {NextRequest,NextResponse} from "next/server";
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from "../../../../../../lib/identity";

export const dynamic="force-dynamic";
type Context={params:Promise<{taxpayerId:string;invoiceId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function fail(status:number,error:string){
  return NextResponse.json({error},{status,headers:{"Cache-Control":"no-store"}});
}
export async function GET(request:NextRequest,context:Context){
  if(!identityUiEnabled())return fail(404,"unavailable");
  const {taxpayerId,invoiceId}=await context.params;
  if(!uuid.test(taxpayerId)||!uuid.test(invoiceId))return fail(404,"not_found");
  const token=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!token)return fail(401,"unauthorized");
  try{
    const res=await fetch(identityApiOrigin()+"/api/dev/taxpayers/"+taxpayerId+
      "/invoices/"+invoiceId+"/dispatch/history",{
      method:"GET",cache:"no-store",redirect:"manual",
      signal:AbortSignal.timeout(10000),
      headers:{Authorization:"Bearer "+token}
    });
    if(![200,400,401,403,404,503].includes(res.status))
      return fail(502,"unexpected_api_status");
    if(!res.ok)return fail(res.status,"history_unavailable");
    const raw=await res.text();
    if(raw.length>16384)return fail(502,"response_too_large");
    const value:unknown=JSON.parse(raw);
    if(!value||typeof value!=="object"||Array.isArray(value))
      return fail(502,"invalid_payload");
    const row=value as Record<string,unknown>;
    if(row.environment!=="Mock"||row.submissionEnabled!==false||
      row.fiscalAccepted!==false||row.signedPreviewAvailable!==false||
      typeof row.state!=="string"||!/^[A-Za-z]{1,30}$/.test(row.state)||
      !Array.isArray(row.attempts)||!Array.isArray(row.receipts)||
      row.attempts.length>30||row.receipts.length>30)
      return fail(502,"invalid_history_policy");
    const date=(x:unknown)=>typeof x==="string"&&x.length<=40&&!Number.isNaN(Date.parse(x))?x:null;
    const attempts=row.attempts.map((a:unknown)=>{
      if(!a||typeof a!=="object"||Array.isArray(a))throw Error("bad attempt");
      const v=a as Record<string,unknown>;
      if(!Number.isInteger(v.number)||Number(v.number)<1||!date(v.startedAtUtc))
        throw Error("bad attempt");
      return {number:v.number,startedAtUtc:date(v.startedAtUtc),
        endedAtUtc:date(v.endedAtUtc),
        result:typeof v.result==="string"&&["success","acknowledged","failed",
          "timeout","indeterminate","Pending","Recorded"].includes(v.result)
          ?v.result:"Recorded"};
    });
    const receipts=row.receipts.map((a:unknown)=>{
      if(!a||typeof a!=="object"||Array.isArray(a))throw Error("bad receipt");
      const v=a as Record<string,unknown>;
      if(!["InitialReceipt","InquiryEvidence"].includes(String(v.kind))||
        !date(v.observedAtUtc))throw Error("bad receipt");
      return {kind:v.kind,observedAtUtc:date(v.observedAtUtc),
        hasAuthorityReference:v.hasAuthorityReference===true};
    });
    const allowedRecovery=["CompleteDraft","AwaitPreparation","RefreshOnly",
      "AwaitReconciliation","ManualReview","NoFurtherSend"];
    const recoveryAction=typeof row.recoveryAction==="string"&&
      allowedRecovery.includes(row.recoveryAction)?row.recoveryAction:"ManualReview";
    const recoveryCode=typeof row.recoveryCode==="string"&&
      /^invoice\\.recovery\\.[a-z_]{2,60}$/.test(row.recoveryCode)
      ?row.recoveryCode:"invoice.recovery.unrecognized_stop";
    if(row.mayRetry!==false||row.mayInquire!==false)
      return fail(502,"invalid_recovery_policy");
    return NextResponse.json({
      environment:"Mock",submissionEnabled:false,fiscalAccepted:false,
      signedPreviewAvailable:false,
      invoiceRevision:typeof row.invoiceRevision==="number"&&
        Number.isSafeInteger(row.invoiceRevision)&&row.invoiceRevision>0?
        row.invoiceRevision:null,
      state:row.state,lastUpdatedAtUtc:date(row.lastUpdatedAtUtc),
      attempts,receipts,recoveryAction,recoveryCode,mayRetry:false,mayInquire:false
    },{headers:{"Cache-Control":"no-store"}});
  }catch{return fail(502,"history_invalid_or_unavailable");}
}
