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
   if(!r.ok){setStatus(`ثبت نرخ ارزش افزوده ناموفق بود (HTTP ${r.status}).`);return;}
   setStatus("نرخ ارزش افزوده خرید نرم‌افزار ذخیره شد.");
  }catch{setStatus("ثبت نرخ ارزش افزوده ناموفق بود.");}
  finally{setBusy(false);}
 }
 return <section className="checkout-tax-settings">
  <h3>تنظیمات ارزش افزوده صورتحساب خرید نرم‌افزار</h3>
  <p>نرخ فقط برای سفارش‌های جدید اعمال می‌شود و به سوابق قبلی تسری نمی‌یابد.</p>
  <div className="checkout-tax-settings__controls">
   <label htmlFor="checkout-vat-rate">درصد ارزش افزوده
    <input id="checkout-vat-rate" type="number" min={0} max={100} step=".01"
     value={basis} onChange={e=>setBasis(e.target.value)}/>
   </label>
   <button className="identity-primary" type="button" disabled={busy||basis===""}
    onClick={()=>void save()}>{busy?"در حال ذخیره…":"ذخیره نرخ"}</button>
  </div>
  {status&&<p className="checkout-tax-settings__status" role="status" aria-live="polite">{status}</p>}
 </section>;
}
