"use client";
import {useEffect,useState} from "react";
type Entry={revision:number;actorUserId:string;action:string;reason:string;occurredAtUtc:string};
export default function InvoiceHistoryPanel({workspaceId}:{workspaceId:string}){
 const [rows,setRows]=useState<Entry[]>([]);
 const [error,setError]=useState("");
 const [loading,setLoading]=useState(true);
 useEffect(()=>{
  let active=true;setLoading(true);setError("");
  fetch("/api/invoices/workspaces/"+workspaceId+"/history",{cache:"no-store",credentials:"same-origin"})
   .then(async response=>{
    if(!response.ok)throw Error("دریافت تاریخچه مجاز نیست یا موقتاً در دسترس نیست.");
    return response.json() as Promise<Entry[]>;
   }).then(data=>{if(active)setRows(data);})
   .catch(e=>{if(active)setError((e as Error).message);})
   .finally(()=>{if(active)setLoading(false);});
  return ()=>{active=false;};
 },[workspaceId]);
 return <section aria-label="تاریخچه نسخه‌های پیش‌نویس">
   <h3>تاریخچه تغییرات پیش‌نویس</h3>
   {loading&&<p role="status">در حال دریافت تاریخچه…</p>}
   {error&&<p role="alert" className="form-error">{error}</p>}
   {!loading&&!error&&rows.length===0&&<p className="field-hint">برای این پیش‌نویس هنوز سابقه‌ای در جدول تاریخچه موجود نیست.</p>}
   {rows.length>0&&<div className="console-table-scroll"><table className="console-table">
    <thead><tr><th>نسخه</th><th>عملیات</th><th>تاریخ UTC</th><th>کاربر ثبت‌کننده</th><th>شرح</th></tr></thead>
    <tbody>{rows.map(item=><tr key={item.revision}>
     <td>{item.revision}</td><td>{item.action==="create"?"ایجاد":"ذخیره تغییرات"}</td>
     <td><bdi dir="ltr">{item.occurredAtUtc}</bdi></td>
     <td><bdi dir="ltr">{item.actorUserId}</bdi></td><td>{item.reason}</td>
    </tr>)}</tbody>
   </table></div>}
   <p className="field-hint">این تاریخچه صرفاً ثبت نسخه‌های پیش‌نویس است و به معنای تأیید یا ارسال مالیاتی نیست.</p>
  </section>;
}
