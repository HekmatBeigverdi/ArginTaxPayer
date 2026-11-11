"use client";
import { useEffect, useState, type FormEvent } from "react";
import type { ConnectionHistory } from "../../lib/fiscal-connection-status";

type Workspace={subscriptionId:string;planKind:number;boundTaxpayerId:string;boundMemoryId:string;
  requests:{id:string;currentMemoryId:string;proposedMemoryId:string;reason:string;status:number;requestedAtUtc:string;decisionReason:string|null}[]};
const statusLabels:Record<number,string>={1:"در انتظار بررسی مدیر سطح ۱",2:"تأیید شده؛ در حال اعمال",3:"رد شده",4:"تغییر اعمال شد"};
const planLabels:Record<number,string>={1:"یک‌ساله",3:"یک‌ماهه",4:"سه‌ماهه",5:"شش‌ماهه"};

export default function FiscalMemoryChangeRequestPanel({taxpayerId,memoryId,memoryIdentifier,history}:
  {taxpayerId:string;memoryId:string;memoryIdentifier:string;history:ConnectionHistory}){
  const [workspace,setWorkspace]=useState<Workspace|null>(null);const [reason,setReason]=useState("");
  const [busy,setBusy]=useState(false);const [error,setError]=useState("");const [notice,setNotice]=useState("");
  const [tick,setTick]=useState(0);
  useEffect(()=>{let active=true;fetch(`/api/phase12/subscriptions/binding-exceptions/mine?taxpayerId=${encodeURIComponent(taxpayerId)}`,{cache:"no-store"})
    .then(async r=>{if(!r.ok)throw new Error();const value=await r.json() as Workspace;if(active)setWorkspace(value);})
    .catch(()=>{if(active)setWorkspace(null);});return()=>{active=false};},[taxpayerId,tick]);
  async function submit(event:FormEvent){event.preventDefault();if(!workspace||busy||reason.trim().length<10)return;
    setBusy(true);setError("");setNotice("");try{const response=await fetch(`/api/phase12/subscriptions/${workspace.subscriptionId}/binding-exceptions`,{
      method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({proposedTaxpayerId:taxpayerId,proposedMemoryId:memoryId,reason:reason.trim()})});
      if(!response.ok)throw new Error(response.status===409?"درخواست مجاز نیست؛ حافظه باید Production باشد و آزمون موفق ثبت‌شده داشته باشد.":"ثبت درخواست انجام نشد.");
      setReason("");setNotice("درخواست تغییر حافظه برای همین مودی ثبت شد و تا تأیید مدیر، اتصال پلن تغییر نمی‌کند.");setTick(v=>v+1);
    }catch(e){setError(e instanceof Error?e.message:"خطای ثبت درخواست.");}finally{setBusy(false);}}
  if(!workspace||![1,3,4,5].includes(workspace.planKind))return null;
  const pending=workspace.requests.some(r=>r.status===1||r.status===2);
  const sameTaxpayer=workspace.boundTaxpayerId===taxpayerId;
  const bound=workspace.boundMemoryId===memoryId;
  const hasSuccess=history.lastSuccess?.status==="Authenticated";
  return <section className="fiscal-memory-change-request" aria-busy={busy}>
    <h3>اتصال حافظه به پلن {planLabels[workspace.planKind]??"زمانی"}</h3>
    <p>{sameTaxpayer&&bound?"این حافظه به پلن تک‌مودی متصل است؛ تغییر آن پس از آزمون اتصال فقط با تأیید مدیر سطح ۱ انجام می‌شود.":"پلن تک‌مودی فقط به یک مودی Production متصل می‌ماند؛ Sandbox از این قاعده مستقل است."}</p>
    {workspace.requests.map(item=><p key={item.id} role="status">درخواست حافظه {item.proposedMemoryId===memoryId?memoryIdentifier:item.proposedMemoryId}: {statusLabels[item.status]??"وضعیت نامشخص"}
      {item.decisionReason&&<> — {item.decisionReason}</>}</p>)}
    {sameTaxpayer&&!bound&&hasSuccess&&!pending&&<form onSubmit={submit}>
      <label>علت درخواست تغییر حافظه<textarea required minLength={10} maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
      <button type="submit" className="identity-primary" disabled={busy||reason.trim().length<10}>{busy?"در حال ثبت…":"درخواست اتصال این حافظه به پلن"}</button>
    </form>}
    {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
  </section>;
}
