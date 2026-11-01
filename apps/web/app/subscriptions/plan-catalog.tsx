"use client";
import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
type Plan={id:string;code:string;kind:number;validityDays:number;invoiceUnitLimit:number|null;isActive:boolean;createdAtUtc:string};
const labels:Record<number,string>={1:"سالانه",2:"تعدادی",3:"ماهانه",4:"سه‌ماهه",5:"شش‌ماهه"};
const days:Record<number,number>={1:365,2:365,3:30,4:90,5:182};
const api="/api/phase12/subscriptions/plans";
type Mode="create"|"edit"|"view"|"archive";
export default function PlanCatalog({level1}:{level1:boolean}){
 const [items,setItems]=useState<Plan[]>([]);
 const [loading,setLoading]=useState(true);
 const [saving,setSaving]=useState(false);
 const [error,setError]=useState("");
 const [query,setQuery]=useState("");
 const [mode,setMode]=useState<Mode|null>(null);
 const [selected,setSelected]=useState<Plan|null>(null);
 const [code,setCode]=useState("");
 const [kind,setKind]=useState(1);
 const [validity,setValidity]=useState(365);
 const [units,setUnits]=useState(100);
 const [price,setPrice]=useState("");
 const [pricePlan,setPricePlan]=useState("");
 const [activation,setActivation]=useState("");
 const [notice,setNotice]=useState("");
 const refresh=useCallback(async()=>{
   setLoading(true);setError("");
   try{const r=await fetch(api,{cache:"no-store"});if(!r.ok)throw Error("خطای دریافت پلن‌ها: "+r.status);
     const result=await r.json() as Plan[];setItems(result);}
   catch(e){setError(e instanceof Error?e.message:"خطای ناشناخته");}
   finally{setLoading(false);}
 },[]);
 useEffect(()=>{void refresh();},[refresh]);
 const filtered=useMemo(()=>items.filter(x=>
   (x.code+" "+labels[x.kind]).toLocaleLowerCase().includes(query.toLocaleLowerCase())),[items,query]);
 function open(next:Mode,plan?:Plan){
   setSelected(plan??null);setMode(next);setError("");
   setCode(plan?.code??"");setKind(plan?.kind??1);
   setValidity(plan?.validityDays??365);setUnits(plan?.invoiceUnitLimit??100);
 }
 async function action(url:string,method:"POST"|"PUT"|"DELETE",body?:object){
   const r=await fetch(url,{method,credentials:"same-origin",cache:"no-store",
     headers:body?{"Content-Type":"application/json"}:undefined,
     body:body?JSON.stringify(body):undefined});
   if(!r.ok)throw Error("عملیات ناموفق بود (HTTP "+r.status+")");
   return r.status===204?null:r.json();
 }
 async function save(e:FormEvent){
   e.preventDefault();if(saving)return;setSaving(true);setError("");
   try{
     if(mode==="archive"&&selected){
       await action(api+"/"+selected.id,"DELETE");
       setNotice("پلن بایگانی شد؛ اطلاعات مالی پیشین محفوظ است.");
     }else if(mode==="create"||mode==="edit"){
       await action(mode==="create"?api:api+"/"+selected!.id,mode==="create"?"POST":"PUT",{
         code,kind,validityDays:validity,invoiceUnitLimit:kind===2?units:null
       });
       setNotice(mode==="create"?"پلن جدید ثبت شد.":"پلن به‌روزرسانی شد.");
     }
     setMode(null);await refresh();
   }catch(e){setError(e instanceof Error?e.message:"خطای ذخیره‌سازی");}
   finally{setSaving(false);}
 }
 async function finance(e:FormEvent,which:"price"|"activation"){
   e.preventDefault();setSaving(true);setError("");
   try{
     if(which==="price"){
       await action("/api/phase12/subscriptions/prices","POST",{
         planId:pricePlan,priceIrr:Number(price),
         effectiveFromUtc:new Date().toISOString(),effectiveUntilUtc:null
       });setNotice("تعرفه اختصاصی ثبت شد.");
     }else{
       await action(api+"/activate","POST",{planId:activation});
       setNotice("پلن فعال شد.");await refresh();
     }
   }catch(e){setError(e instanceof Error?e.message:"خطا");}
   finally{setSaving(false);}
 }
 return <section className="phase12-plan-catalog" aria-label="فهرست پلن‌ها">
  <div className="phase12-plan-toolbar">
   <div><h3>پلن‌های اشتراک</h3><small>نمایش {filtered.length} پلن</small></div>
   <input aria-label="جست‌وجوی پلن" placeholder="جست‌وجوی نام یا نوع پلن..." value={query} onChange={e=>setQuery(e.target.value)}/>
   <button type="button" onClick={()=>open("create")}>+ افزودن پلن</button>
   <button type="button" onClick={()=>void refresh()} disabled={loading}>بازخوانی</button>
  </div>
  {notice&&<p role="status" className="phase12-plan-success">{notice}</p>}
  {error&&!mode&&<p role="alert">{error}</p>}
  <div className="phase12-plan-table"><table><thead><tr><th>کد پلن</th><th>نوع</th><th>اعتبار</th><th>سهمیه</th><th>وضعیت</th><th>عملیات</th></tr></thead>
  <tbody>{loading?<tr><td colSpan={6}>در حال بارگذاری...</td></tr>:
   filtered.length===0?<tr><td colSpan={6}>پلنی یافت نشد.</td></tr>:filtered.map(p=><tr key={p.id}>
    <td>{p.code}</td><td>{labels[p.kind]??"نامشخص"}</td><td>{p.validityDays} روز</td>
    <td>{p.kind===2?p.invoiceUnitLimit?.toLocaleString("fa-IR"):"نامحدود"}</td>
    <td>{p.isActive?"فعال":"غیرفعال"}</td><td className="phase12-plan-actions">
     <button type="button" onClick={()=>open("view",p)}>مشاهده</button>
     <button type="button" onClick={()=>open("edit",p)}>ویرایش</button>
     <button type="button" onClick={()=>open("archive",p)} disabled={!p.isActive}>حذف</button>
    </td>
   </tr>)}</tbody></table></div>
  <details className="phase12-plan-extra"><summary>تعرفه و فعال‌سازی پلن</summary>
   <form onSubmit={e=>void finance(e,"price")}>
    <label>پلن<select required value={pricePlan} onChange={e=>setPricePlan(e.target.value)}>
     <option value="">انتخاب پلن</option>{items.map(p=><option key={p.id} value={p.id}>{p.code}</option>)}
    </select></label><label>قیمت (ریال)<input type="number" required min={0} value={price} onChange={e=>setPrice(e.target.value)}/></label>
    <button disabled={saving}>ثبت تعرفه مستقل</button>
   </form>
   {level1&&<form onSubmit={e=>void finance(e,"activation")}>
    <label>پلن<select required value={activation} onChange={e=>setActivation(e.target.value)}>
     <option value="">انتخاب پلن</option>{items.filter(p=>!p.isActive).map(p=><option key={p.id} value={p.id}>{p.code}</option>)}
    </select></label><button disabled={saving}>فعال‌سازی پلن</button>
   </form>}
  </details>
  {mode&&<div className="phase12-plan-overlay" role="presentation" onMouseDown={e=>{if(e.target===e.currentTarget&&!saving)setMode(null);}}>
   <div className="phase12-plan-modal" role="dialog" aria-modal="true" aria-label={mode==="create"?"افزودن پلن":mode==="edit"?"ویرایش پلن":mode==="view"?"مشاهده پلن":"حذف پلن"}>
    <div className="phase12-plan-modal-header"><h3>{mode==="create"?"افزودن پلن جدید":mode==="edit"?"ویرایش پلن":mode==="view"?"مشخصات پلن":"بایگانی پلن"}</h3>
     <button type="button" aria-label="بستن" disabled={saving} onClick={()=>setMode(null)}>×</button></div>
    <form onSubmit={e=>void save(e)}>
     {mode==="archive"?<p>پلن «{selected?.code}» غیرفعال می‌شود؛ اطلاعات سفارش‌ها و اشتراک‌های قبلی باقی می‌مانند. ادامه می‌دهید؟</p>:<>
      <label>کد پلن<input required readOnly={mode==="view"} value={code} maxLength={50} onChange={e=>setCode(e.target.value)}/></label>
      <label>نوع پلن<select disabled={mode==="view"} value={kind} onChange={e=>{let k=Number(e.target.value);setKind(k);setValidity(days[k]);}}>
       {[3,4,5,1,2].map(k=><option key={k} value={k}>{labels[k]}</option>)}</select></label>
      <label>اعتبار (روز)<input type="number" required readOnly={mode==="view"} min={1} max={366} value={validity} onChange={e=>setValidity(Number(e.target.value))}/></label>
      {kind===2&&<label>سهمیه صورتحساب<input type="number" min={1} required readOnly={mode==="view"} value={units} onChange={e=>setUnits(Number(e.target.value))}/></label>}
      {selected&&<p>شناسه: <code dir="ltr">{selected.id}</code></p>}
     </>}
     {error&&<p role="alert">{error}</p>}
     <div className="phase12-plan-modal-footer"><button type="button" onClick={()=>setMode(null)} disabled={saving}>بستن</button>
      {mode!=="view"&&<button type="submit" disabled={saving}>{saving?"در حال ثبت...":mode==="archive"?"تأیید حذف":"ذخیره"}</button>}</div>
    </form>
   </div></div>}
 </section>;
}
