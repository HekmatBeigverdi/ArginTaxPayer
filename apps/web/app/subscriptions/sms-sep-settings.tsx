"use client";
import {useEffect,useState,type FormEvent} from "react";
type Admin={userId:string;display:string};
export default function SmsSepSettings(){
 const [terminal,setTerminal]=useState(""),[admin,setAdmin]=useState(""),[options,setOptions]=useState<Admin[]>([]),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
 useEffect(()=>{let active=true;Promise.all([fetch("/api/phase12/subscriptions/sms-sep-settings",{cache:"no-store"}),fetch("/api/phase12/subscriptions/sms-sep-admins",{cache:"no-store"})]).then(async([s,a])=>{if(!s.ok||!a.ok)throw new Error();const config=await s.json(),list=await a.json() as Admin[];if(active){setTerminal(config.terminalNumber?String(config.terminalNumber):"");setAdmin(config.assignedAdminUserId??"");setOptions(list)}}).catch(()=>{if(active)setMessage("بارگذاری تنظیمات SEP انجام نشد.")});return()=>{active=false}},[]);
 async function save(e:FormEvent){e.preventDefault();setBusy(true);setMessage("");try{const r=await fetch("/api/phase12/subscriptions/sms-sep-settings",{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({terminalNumber:Number(terminal),assignedAdminUserId:admin})});if(!r.ok)throw new Error("ذخیره انجام نشد (HTTP "+r.status+")");setMessage("تنظیمات ترمینال SEP ذخیره شد.")}catch(e){setMessage(e instanceof Error?e.message:"خطای ارتباط")}finally{setBusy(false)}}
 return <section className="checkout-tax-settings">
  <h3>درگاه SEP برای شارژ پیامک</h3>
  <p>ترمینال و ادمین مسئول برای مشتریان این مجموعه ثبت می‌شود. فعال‌سازی پرداخت زنده به تنظیم callback عمومی HTTPS در سرور نیاز دارد.</p>
  <form onSubmit={save}>
   <div className="checkout-tax-settings__controls">
    <label>شماره ترمینال
     <input required type="text" inputMode="numeric" pattern="[0-9]{1,10}" value={terminal} onChange={e=>setTerminal(e.target.value)}/>
    </label>
    <label>ادمین مسئول درگاه
     <select required value={admin} onChange={e=>setAdmin(e.target.value)}><option value="">انتخاب ادمین</option>{options.map(x=><option key={x.userId} value={x.userId}>{x.display}</option>)}</select>
    </label>
    <button className="identity-primary" type="submit" disabled={busy||!options.length}>{busy?"در حال ذخیره…":"ذخیره تنظیمات SEP"}</button>
   </div>
  </form>
  {message&&<p className="checkout-tax-settings__status" role="status" aria-live="polite">{message}</p>}
 </section>
}
