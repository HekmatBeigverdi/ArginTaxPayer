"use client";
import { useEffect,useState } from "react";
type History={orderId:string;planCode:string;planKind:number;amountIrr:number;collectedIrr:number;status:number;createdAtUtc:string;invoiceNumber:string;subscriptionId:string|null;expiresAtUtc:string|null};
const kind:Record<number,string>={1:"سالانه",2:"تعدادی",3:"ماهانه",4:"سه‌ماهه",5:"شش‌ماهه"};
const fmt=(n:number)=>new Intl.NumberFormat("fa-IR").format(n)+" ریال";
export default function CustomerFinancialHistory(){
 const [rows,setRows]=useState<History[]>([]),[loading,setLoading]=useState(true),[error,setError]=useState("");
 const [query,setQuery]=useState("");
 useEffect(()=>{const ctrl=new AbortController();
  fetch("/api/customer-finance",{cache:"no-store",signal:ctrl.signal}).then(async r=>{
   if(!r.ok)throw Error("دریافت گزارش مالی ناموفق بود (HTTP "+r.status+")");
   setRows(await r.json() as History[]);
  }).catch(e=>{if(!ctrl.signal.aborted)setError(e instanceof Error?e.message:"خطا");})
  .finally(()=>{if(!ctrl.signal.aborted)setLoading(false);});
  return()=>ctrl.abort();
 },[]);
 const filtered=rows.filter(x=>(x.planCode+" "+x.invoiceNumber).toLowerCase().includes(query.toLowerCase()));
 return <section className="console-settings-card" dir="rtl">
  <div className="console-settings-card-title"><h2>سوابق مالی من</h2><p>پرداخت‌ها و اشتراک‌های فعال‌شده بر اساس اطلاعات سرور</p></div>
  <input aria-label="جستجوی سوابق مالی" placeholder="جستجوی نام پلن یا شماره صورتحساب..." value={query} onChange={e=>setQuery(e.target.value)}
   style={{padding:9,borderRadius:8,border:"1px solid #b9c9c1",maxWidth:340,width:"100%"}}/>
  {error&&<p role="alert">{error}</p>}
  <div style={{overflowX:"auto",marginTop:12}}><table style={{width:"100%",fontSize:13,borderCollapse:"collapse"}}>
   <thead><tr><th>تاریخ</th><th>پلن</th><th>صورتحساب داخلی</th><th>مبلغ</th><th>وصول‌شده</th><th>وضعیت</th><th>اعتبار تا</th></tr></thead>
   <tbody>{loading?<tr><td colSpan={7}>در حال دریافت...</td></tr>:
    filtered.length?filtered.map(x=><tr key={x.orderId}>
     <td>{new Date(x.createdAtUtc).toLocaleDateString("fa-IR")}</td>
     <td>{kind[x.planKind]??"—"} – {x.planCode.split("-").slice(1).join("-")||x.planCode}</td>
     <td dir="ltr">{x.invoiceNumber}</td><td>{fmt(x.amountIrr)}</td><td>{fmt(x.collectedIrr)}</td>
     <td>{x.subscriptionId?"فعال‌شده":x.status===3?"پرداخت‌شده، در انتظار فعال‌سازی":x.status===4?"باطل‌شده":"در انتظار پرداخت"}</td>
     <td>{x.expiresAtUtc?new Date(x.expiresAtUtc).toLocaleDateString("fa-IR"):"—"}</td>
    </tr>):<tr><td colSpan={7}>سابقه‌ای وجود ندارد.</td></tr>}</tbody>
  </table></div>
  <p style={{fontSize:12,marginTop:10}}>این گزارش تنها سفارش‌های ثبت‌شده حساب شما را نشان می‌دهد. طبقه‌بندی خرید اولیه، تمدید و بسته اضافه پس از ثبت نوع سفارش در Backend نمایش داده خواهد شد.</p>
 </section>;
}
