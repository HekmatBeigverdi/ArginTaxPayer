"use client";
import {useEffect,useState} from "react";
type Plan={planId:string;code:string;kind:number;validityDays:number;invoiceUnitLimit:number|null;priceIrr:number};
const kinds:Record<number,string>={1:"سالانه",2:"تعدادی",3:"ماهانه",4:"سه‌ماهه",5:"شش‌ماهه"};
export default function CustomerPlans(){
 const [plans,setPlans]=useState<Plan[]>([]);
 const [busy,setBusy]=useState(false),[error,setError]=useState(""),[notice,setNotice]=useState("");
 const [selected,setSelected]=useState(""),[official,setOfficial]=useState(false),[cash,setCash]=useState(true);
 const [operation,setOperation]=useState(()=>crypto.randomUUID());
 useEffect(()=>{const ctrl=new AbortController();
  fetch("/api/customer-checkout",{cache:"no-store",signal:ctrl.signal}).then(async r=>{
   if(!r.ok)throw Error("دریافت پلن‌ها ناموفق بود (HTTP "+r.status+")");
   setPlans(await r.json() as Plan[]);
  }).catch(e=>{if(!ctrl.signal.aborted)setError(String(e));});
  return()=>ctrl.abort();
 },[]);
 async function buy(){
  if(!selected||busy||!cash)return;
  setBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/customer-checkout",{method:"POST",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({planId:selected,wantsOfficialInvoice:official,operationKey:operation})});
   if(!response.ok)throw Error("ثبت درخواست ناموفق بود (HTTP "+response.status+")");
   const result=await response.json() as {orderId:string};
   setNotice("درخواست پرداخت نقدی ثبت شد. شماره سفارش: "+result.orderId+
     " — پس از پرداخت وجه و تأیید مدیر، اشتراک فعال می‌شود.");
  }catch(e){setError(e instanceof Error?e.message:"خطا");}
  finally{setBusy(false);}
 }
 return <section className="console-settings-card" dir="rtl">
  <div className="console-settings-card-title"><h2>انتخاب پلن نرم‌افزار</h2>
   <p>فقط پلن‌ها و قیمت‌های فعال مدیر تجاری شما نمایش داده می‌شوند.</p></div>
  {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
  <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(190px,1fr))",gap:10}}>
   {plans.map(p=><label key={p.planId} style={{padding:12,border:"1px solid #cbded3",borderRadius:10}}>
    <input type="radio" name="plan" checked={selected===p.planId} onChange={()=>{setSelected(p.planId);setOperation(crypto.randomUUID());}}/>
    <strong>{kinds[p.kind]||"پلن"} — {p.code.split("-").slice(1).join("-")||p.code}</strong>
    <p>{p.kind===2?p.invoiceUnitLimit+" صورتحساب":"صورتحساب نامحدود"}</p>
    <p>{p.priceIrr.toLocaleString("fa-IR")} ریال</p>
   </label>)}
  </div>
  <div style={{marginTop:16,display:"flex",gap:14,flexWrap:"wrap"}}>
   <label><input type="checkbox" checked={official} onChange={e=>setOfficial(e.target.checked)}/> درخواست فاکتور رسمی خرید نرم‌افزار</label>
   <label><input type="radio" checked={cash} onChange={()=>setCash(true)}/> پرداخت نقدی</label>
   <label><input type="radio" checked={!cash} onChange={()=>setCash(false)}/> پرداخت از درگاه</label>
  </div>
  {official&&<p>ارزش افزوده بر اساس تنظیمات مدیر اصلی روی سرور محاسبه می‌شود؛ مبلغ قطعی در پاسخ سفارش ثبت خواهد شد.</p>}
  {!cash&&<p role="alert">درگاه آنلاین هنوز آماده تأیید سروری نیست؛ امکان شروع پرداخت غیرفعال است.</p>}
  <button type="button" disabled={!selected||busy||!cash} onClick={()=>void buy()}
    style={{padding:"10px 18px",border:0,borderRadius:9,marginTop:14,background:"#176c4e",color:"white"}}>
    {busy?"در حال ثبت...":"ثبت درخواست خرید نقدی"}
  </button>
  <p style={{marginTop:12,fontSize:12}}>ارسال درخواست به‌معنی پرداخت یا فعال‌سازی اشتراک نیست. برای هر درخواست جدید، ابتدا یک پلن انتخاب کنید.</p>
 </section>;
}
