"use client";
import {useCallback,useEffect,useState,type FormEvent} from "react";
import {amount} from "./finance-format";

type Balance={isUnlimited:boolean;availableIrr:number|null};
type Topup={id:string;amountIrr:number;state:string;createdAtUtc:string;verifiedAtUtc:string|null};
type Start={topupId:string;token:string;paymentUrl:string;returnUrl:string};

export default function SmsTopupPanel({customer}:{customer:boolean}){
 const [balance,setBalance]=useState<Balance|null>(null),[amountToman,setAmountToman]=useState("50000"),[busy,setBusy]=useState(false),[reconciling,setReconciling]=useState(false),[error,setError]=useState(""),[message,setMessage]=useState(""),[topup,setTopup]=useState<Topup|null>(null);
 const refresh=useCallback(async()=>{if(!customer)return;const r=await fetch("/api/sms-credit",{cache:"no-store"});if(r.ok)setBalance(await r.json() as Balance)},[customer]);
 useEffect(()=>{void refresh();const params=new URLSearchParams(window.location.search);const id=params.get("smsTopupId");const status=params.get("smsTopupStatus");if(status)setMessage(status==="success"?"پرداخت تأیید شد و اعتبار پیامک شارژ شد.":status==="pending"?"وضعیت پرداخت هنوز قطعی نیست؛ پس از بررسی دوباره وضعیت را بازخوانی کنید.":"پرداخت تأیید نشد.");if(id){fetch("/api/phase12/subscriptions/sms-topups/"+encodeURIComponent(id),{cache:"no-store"}).then(async r=>{if(r.ok){setTopup(await r.json() as Topup);await refresh()}}).catch(()=>{})}},[refresh]);
 async function start(e:FormEvent){e.preventDefault();if(busy)return;setBusy(true);setError("");setMessage("");try{
  const response=await fetch("/api/phase12/subscriptions/sms-topups/start",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({amountIrr:Number(amountToman)*10})});
  const result=await response.json().catch(()=>null) as Start|null;
  if(!response.ok||!result?.token||!result.paymentUrl||!result.topupId)throw new Error(response.status===409?"درگاه SEP آماده نیست؛ تنظیمات ترمینال و callback باید توسط مدیر تکمیل شود.":"شروع پرداخت انجام نشد (HTTP "+response.status+").");
  const form=document.createElement("form");form.method="POST";form.action=result.paymentUrl;form.style.display="none";
  const input=document.createElement("input");input.type="hidden";input.name="Token";input.value=result.token;form.append(input);document.body.append(form);form.submit();
 }catch(e){setError(e instanceof Error?e.message:"اتصال به درگاه انجام نشد.");setBusy(false)}}
 async function reloadTopup(){if(!topup)return;setReconciling(true);setError("");try{const status=await fetch(`/api/phase12/subscriptions/sms-topups/${encodeURIComponent(topup.id)}`,{cache:"no-store"});if(!status.ok)throw new Error("بازخوانی وضعیت انجام نشد (HTTP "+status.status+")");setTopup(await status.json() as Topup);await refresh()}catch(e){setError(e instanceof Error?e.message:"خطای بازخوانی وضعیت")}finally{setReconciling(false)}}
 async function reconcile(){if(!topup||reconciling)return;setReconciling(true);setError("");try{const r=await fetch(`/api/phase12/subscriptions/sms-topups/${encodeURIComponent(topup.id)}/reconcile`,{method:"POST",headers:{"Content-Type":"application/json"},body:"{}"});if(!r.ok)throw new Error("تطبیق انجام نشد (HTTP "+r.status+")");const status=await fetch(`/api/phase12/subscriptions/sms-topups/${encodeURIComponent(topup.id)}`,{cache:"no-store"});if(status.ok)setTopup(await status.json() as Topup);setMessage("وضعیت پرداخت از SEP دوباره استعلام شد.");await refresh()}catch(e){setError(e instanceof Error?e.message:"خطای استعلام SEP")}finally{setReconciling(false)}}
 return <section className="console-settings-card sms-credit-card" dir="rtl">
  <div className="console-settings-card-title">
   <h2>اعتبار پیامک</h2>
   <p>شارژ از طریق درگاه امن SEP انجام می‌شود و پس از تأیید سرور به سرور، اعتبار به کیف پول اضافه خواهد شد.</p>
  </div>
  <section className="checkout-tax-settings sms-credit-panel">
   <h3>شارژ کیف پول پیامک</h3>
   {customer&&balance&&<p className="sms-credit-balance">مانده اعتبار: <strong>{Math.floor((balance.availableIrr??0)/10).toLocaleString("fa-IR")} تومان</strong></p>}
   {customer&&<form onSubmit={start}><div className="checkout-tax-settings__controls">
    <label>مبلغ شارژ به تومان<input required type="number" min={1000} max={10000000000} step={1000} value={amountToman} onChange={e=>setAmountToman(e.target.value)}/></label>
    <button className="identity-primary" type="submit" disabled={busy}>{busy?"در حال اتصال به درگاه…":"پرداخت و شارژ"}</button>
   </div></form>}
   {message&&<p className="checkout-tax-settings__status" role="status" aria-live="polite">{message}</p>}
   {error&&<p role="alert" className="form-error">{error}</p>}
   {topup&&<div className="sms-credit-topup-status"><p>مبلغ درخواست: {amount(topup.amountIrr)}</p><p>وضعیت: {topup.state==="verified"?"تأیید و شارژ شده":topup.state==="failed"?"ناموفق":topup.state==="unknown"?"در حال بررسی":"در انتظار تأیید"}</p>
    {(topup.state==="unknown"||topup.state==="callback_received"||topup.state==="token_issued")&&<button className="identity-secondary" type="button" onClick={()=>void reloadTopup()} disabled={reconciling}>{reconciling?"در حال بازخوانی…":"بازخوانی وضعیت پرداخت"}</button>}</div>}
   {!customer&&topup?.state==="unknown"&&<button className="identity-secondary" type="button" onClick={()=>void reconcile()} disabled={reconciling}>{reconciling?"در حال استعلام…":"استعلام دوباره از SEP"}</button>}
   {customer&&<button className="identity-secondary" type="button" onClick={()=>void refresh()}>بازخوانی مانده</button>}
  </section>
 </section>;
}
