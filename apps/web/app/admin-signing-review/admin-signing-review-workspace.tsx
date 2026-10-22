"use client";
import { useEffect, useState } from "react";
import OperationMessageBox from "../taxpayers/operation-message-box";

type Review = {
 id: string; taxpayerName: string; memoryIdentifier: string; kind: number; state: number;
 reasonCode: string; explanation: string; expectedKeyRevision: number; revision: number;
 requestedAtUtc: string; customerUserId: string
};
const labels: Record<number,string> = {
 1:"تعلیق",2:"گزارش افشا",3:"جایگزینی کلید"
};
export default function AdminSigningReviewWorkspace() {
  const [rows,setRows] = useState<Review[]>([]);
  const [busy,setBusy] = useState("");
  const [loading,setLoading] = useState(true);
  const [error,setError] = useState("");
  const [notice,setNotice] = useState("");
  const [reason,setReason] = useState<Record<string,string>>({});
  const [selected,setSelected] = useState<Record<string,boolean>>({});
  const [tick,setTick] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true);setError("");
    void fetch("/api/admin/signing-change-requests",{cache:"no-store",credentials:"same-origin",
      signal:AbortSignal.timeout(20000)})
      .then(async response => {
        if(!response.ok) throw new Error(response.status===403 ?
          "فقط مدیر سطح ۱ با MFA فعال به تأیید درخواست‌ها دسترسی دارد." : "دریافت درخواست‌ها ناموفق بود.");
        const data=await response.json();
        if(active)setRows(data.requests||[]);
      })
      .catch(ex=>{if(active)setError(ex instanceof Error?ex.message:"خطای دریافت اطلاعات");})
      .finally(()=>{if(active)setLoading(false);});
    return ()=>{active=false;};
  },[tick]);

  async function decide(item:Review,approve:boolean) {
    if(busy || (reason[item.id]||"").trim().length<10)return;
    setBusy(item.id);setError("");setNotice("");
    try {
      const response=await fetch("/api/admin/signing-change-requests/"+item.id+"/decision",{
        method:"POST",cache:"no-store",credentials:"same-origin",
        headers:{"Content-Type":"application/json"},
        body:JSON.stringify({approve,expectedRevision:item.revision,decisionReason:reason[item.id].trim()}),
        signal:AbortSignal.timeout(20000)
      });
      if(!response.ok)throw new Error(response.status===409 ?
        "درخواست یا نسخه کلید تغییر کرده است؛ بازخوانی کنید." :
        response.status===403?"مجوز سطح ۱ یا MFA معتبر نیست.":"ثبت تصمیم انجام نشد.");
      setNotice(approve?"درخواست تأیید شد. اعتبار مجوز ۲۴ ساعت است؛ کلید فعلی هنوز جایگزین نشده است.":
        "درخواست با علت ثبت‌شده رد شد؛ کلید فعلی بدون تغییر باقی ماند.");
      setTick(x=>x+1);
    }catch(ex){setError(ex instanceof Error?ex.message:"خطای تصمیم");}
    finally{setBusy("");}
  }
  return <section className="signing-admin-review">
    <div className="signing-panel-heading"><div><h2>درخواست‌های تغییر کلید مؤدیان</h2>
      <p>تأیید یا رد با ثبت علت؛ تأیید به معنی جایگزینی فوری کلید یا صدور توکن نیست.</p></div>
      <button type="button" className="identity-secondary" disabled={loading||Boolean(busy)}
        onClick={()=>setTick(x=>x+1)}>بازخوانی</button>
    </div>
    {error&&<OperationMessageBox key={error} kind="error" title="خطای بررسی درخواست">{error}</OperationMessageBox>}
    {notice&&<OperationMessageBox key={notice} kind="success" title="تصمیم ثبت شد">{notice}</OperationMessageBox>}
    {loading?<p role="status">در حال دریافت درخواست‌ها…</p>:
      rows.length===0?<p className="field-hint">درخواست در انتظار تأییدی وجود ندارد.</p>:
      <div className="signing-admin-review__list">{rows.map(item=>
        <article key={item.id} className="signing-admin-review__item">
          <div className="signing-admin-review__head">
            <strong>{item.taxpayerName} · {labels[item.kind]||"درخواست تغییر"}</strong>
            <span>در انتظار بررسی</span>
          </div>
          <dl>
            <div><dt>حافظه مالیاتی</dt><dd dir="ltr">{item.memoryIdentifier}</dd></div>
            <div><dt>علت</dt><dd>{item.reasonCode}</dd></div>
            <div><dt>زمان درخواست</dt><dd>{new Date(item.requestedAtUtc).toLocaleString("fa-IR")}</dd></div>
            <div><dt>نسخه کلید</dt><dd>{item.expectedKeyRevision}</dd></div>
          </dl>
          <p className="signing-admin-review__explanation">{item.explanation}</p>
          <button type="button" className="identity-secondary"
            onClick={()=>setSelected(s=>({...s,[item.id]:!s[item.id]}))}>
            {selected[item.id]?"بستن بررسی":"بررسی و تصمیم"}
          </button>
          {selected[item.id]&&<div className="signing-admin-review__decision">
            <label>علت تصمیم (حداقل ۱۰ نویسه)
              <textarea rows={3} maxLength={1000} value={reason[item.id]||""}
                onChange={e=>setReason(prev=>({...prev,[item.id]:e.target.value}))}
                disabled={Boolean(busy)} placeholder="دلایل تأیید یا رد را ثبت کنید…"/>
            </label>
            <div className="signing-admin-review__actions">
              <button type="button" disabled={Boolean(busy)||(reason[item.id]||"").trim().length<10}
                className="identity-primary" onClick={()=>void decide(item,true)}>
                {busy===item.id?"در حال ثبت…":"تأیید درخواست"}</button>
              <button type="button" disabled={Boolean(busy)||(reason[item.id]||"").trim().length<10}
                className="identity-secondary" onClick={()=>void decide(item,false)}>رد درخواست</button>
            </div>
          </div>}
        </article>)}</div>}
  </section>;
}
