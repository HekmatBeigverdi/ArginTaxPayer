"use client";
import {useEffect,useState} from "react";
export default function CheckoutTaxSettings(){
 const [basis,setBasis]=useState(""),[busy,setBusy]=useState(false);
 const [status,setStatus]=useState("");
 useEffect(()=>{fetch("/api/phase12/subscriptions/checkout-tax",{cache:"no-store"})
  .then(async r=>{if(!r.ok)throw Error("HTTP "+r.status);return r.json();})
  .then(x=>setBasis(x.vatBasisPoints===null?"":String(x.vatBasisPoints/100)))
  .catch(()=>setStatus("دریافت تنظیمات ارزش افزوده ناموفق بود."));},[]);
 async function save(){
  const n=Number(basis);
  if(!Number.isFinite(n)||n<0||n>100||!Number.isInteger(n*100)){
   setStatus("درصد باید بین صفر و صد و دارای حداکثر دو رقم اعشار باشد.");return;
  }
  setBusy(true);setStatus("");
  try{
   const r=await fetch("/api/phase12/subscriptions/checkout-tax",{
    method:"PUT",headers:{"Content-Type":"application/json"},
    body:JSON.stringify({vatBasisPoints:Math.round(n*100)})
   });
   if(!r.ok)throw Error("HTTP "+r.status);
   setStatus("نرخ ارزش افزوده خرید نرم‌افزار ذخیره شد.");
  }catch{setStatus("ثبت نرخ ارزش افزوده ناموفق بود.");}
  finally{setBusy(false);}
 }
 return <section style={{marginTop:16,padding:12,border:"1px solid #ceded4",borderRadius:9}}>
  <h3>تنظیمات ارزش افزوده صورتحساب خرید نرم‌افزار</h3>
  <p>نرخ فقط برای سفارش‌های جدید اعمال می‌شود و به سوابق قبلی تسری نمی‌یابد.</p>
  <label>درصد ارزش افزوده <input type="number" min={0} max={100} step=".01"
    value={basis} onChange={e=>setBasis(e.target.value)} style={{width:100,marginInline:9}}/></label>
  <button type="button" disabled={busy||basis===""} onClick={()=>void save()}>ذخیره نرخ</button>
  {status&&<p role="status">{status}</p>}
 </section>;
}
