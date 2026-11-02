"use client";
import {useEffect,useState,type FormEvent} from "react";
export default function SmsServiceSettings(){
 const [fee,setFee]=useState(""),[ceiling,setCeiling]=useState(""),[customerBillingEnabled,setCustomerBillingEnabled]=useState(true),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 useEffect(()=>{let active=true;fetch("/api/phase12/subscriptions/sms-settings",{cache:"no-store"}).then(r=>r.ok?r.json():null).then(x=>{if(active&&x){setFee(String(x.serviceFeeBasisPoints/100));setCeiling(String(x.reservationCeilingIrr));setCustomerBillingEnabled(x.customerBillingEnabled!==false);}}).catch(()=>{});return()=>{active=false}},[]);
 async function save(e:FormEvent){e.preventDefault();setBusy(true);setMessage("");try{const r=await fetch("/api/phase12/subscriptions/sms-settings",{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({serviceFeeBasisPoints:Math.round(Number(fee)*100),reservationCeilingIrr:Number(ceiling),customerBillingEnabled})});if(!r.ok)throw new Error("ذخیره تنظیمات انجام نشد (HTTP "+r.status+")");setMessage("تنظیمات ذخیره شد.")}catch(e){setMessage(e instanceof Error?e.message:"خطای ارتباط")}finally{setBusy(false)}}
 return <section className="checkout-tax-settings">
  <h3>هزینه سرویس پیامک</h3>
  <p>درصد روی هزینه واقعی قاصدک افزوده می‌شود. هزینهٔ واقعی سرویس در هر دو حالت در گزارش ثبت خواهد شد.</p>
  <form onSubmit={save}>
   <div className="checkout-tax-settings__controls">
    <label>درصد سرویس
     <input required type="number" min="0" max="100" step="0.01" value={fee} onChange={e=>setFee(e.target.value)}/>
    </label>
    <label>سقف رزرو هر پیامک (ریال)
     <input required type="number" min="1" step="1" value={ceiling} onChange={e=>setCeiling(e.target.value)}/>
    </label>
    <button className="identity-primary" type="submit" disabled={busy}>{busy?"در حال ذخیره…":"ذخیره تنظیمات"}</button>
   </div>
   <label className="sms-billing-toggle">
    <input type="checkbox" checked={customerBillingEnabled} onChange={e=>setCustomerBillingEnabled(e.target.checked)}/>
    <span><strong>کنترل و کسر اعتبار پیامک کاربران</strong><small>با خاموش‌کردن، کمبود اعتبار مانع ارسال نمی‌شود و مبلغی از کیف پول کسر نخواهد شد.</small></span>
   </label>
  </form>
  {message&&<p className="checkout-tax-settings__status" role="status" aria-live="polite">{message}</p>}
 </section>
}
