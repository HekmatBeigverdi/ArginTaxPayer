"use client";

import { useEffect, useState } from "react";

type Finding = { key:string; severity:"error"|"warning"; fieldPath:string; message:string };
type Evaluation = { draftId:string; revision:number; edition:string;
  isSubmissionAuthorized:boolean; findings:Finding[] };
type Ref = { id:string; subject:string; state:string; issueDateUtc:string;
  revision:number; fiscalIdentifierVerified:boolean; fiscalIdentifier:string|null };
const subjectNames = [
  {value:"Original",label:"اصلی"},
  {value:"Correction",label:"اصلاحی"},
  {value:"Cancellation",label:"ابطالی"},
  {value:"SalesReturn",label:"برگشت از فروش"}
] as const;

async function readJson<T>(url:string):Promise<T> {
  const result=await fetch(url,{cache:"no-store",credentials:"same-origin"});
  if(!result.ok) throw new Error(result.status===401?
    "نشست کاربری منقضی شده است." : result.status===404?
    "پیش‌نویس یا مرجع در محدوده دسترسی شما یافت نشد." :
    "دریافت وضعیت اعتبارسنجی ممکن نشد.");
  return result.json() as Promise<T>;
}

export default function InvoiceValidationPanel({
  draftId,taxpayerId,revision,dirty,onSave
}:{draftId:string;taxpayerId:string;revision:number;dirty:boolean;onSave:()=>Promise<boolean>}) {
  const [report,setReport]=useState<Evaluation|null>(null);
  const [references,setReferences]=useState<Ref[]>([]);
  const [subject,setSubject]=useState("Original");
  const [selectedRef,setSelectedRef]=useState("");
  const [loading,setLoading]=useState(false);
  const [checkingAfterSave,setCheckingAfterSave]=useState(false);
  const [error,setError]=useState("");
  async function refresh() {
    if(dirty) {setError("ابتدا تغییرات فرم را ذخیره کنید.");return;}
    setLoading(true);setError("");
    try {
      const [checks,source]=await Promise.all([
        readJson<Evaluation>("/api/invoices/workspaces/"+draftId+"/validation"),
        readJson<Ref[]>("/api/invoices/references/"+taxpayerId)
      ]);
      if(checks.revision!==revision) {
        setReport(null);setError("نسخه پیش‌نویس تغییر کرده است؛ آن را مجدداً باز کنید.");return;
      }
      setReport(checks);setReferences(source);
    }catch(e){setError((e as Error).message);}
    finally{setLoading(false);}
  }
  useEffect(()=>{
    setReport(null);setReferences([]);setSubject("Original");setSelectedRef("");
  },[draftId,revision,taxpayerId]);
  useEffect(()=>{
    if(checkingAfterSave && !dirty){setCheckingAfterSave(false);void refresh();}
  },[revision,dirty,checkingAfterSave]);
  const issues=report?.findings ?? [];
  return <section className="invoice-validation" aria-labelledby="invoice-validation-title">
    <div className="invoice-validation__heading">
      <div><h3 id="invoice-validation-title">پیش‌بررسی و سوابق ارجاع</h3>
        <p>این بررسی برای تکمیل فرم است؛ مجوز ارسال مالیاتی صادر نمی‌کند.</p></div>
      <button type="button" className="identity-secondary"
        disabled={loading||checkingAfterSave} onClick={()=>{
          if(dirty){setCheckingAfterSave(true);void onSave().then(saved=>{
            if(!saved){setCheckingAfterSave(false);setError("ذخیره تغییرات انجام نشد؛ خطای نمایش‌داده‌شده در فرم را بررسی کنید.");}
          }).catch(e=>{setCheckingAfterSave(false);setError((e as Error).message);});}
          else void refresh();
        }}>
        {loading||checkingAfterSave?"در حال ذخیره/بررسی…":dirty?"ذخیره تغییرات و بررسی":"بررسی پیش‌نویس ذخیره‌شده"}
      </button>
    </div>
    {dirty && <p role="status" className="invoice-validation__warning">
      فرم از آخرین نسخه ذخیره‌شده تغییر کرده است؛ ابتدا ذخیره کنید.
    </p>}
    {error && <p role="alert" className="form-error">{error}</p>}
    {report && <div className="invoice-validation__findings">
      <div className="invoice-validation__summary">
        <strong>ویرایش {report.revision} · قواعد {report.edition}</strong>
        <span>{issues.filter(f=>f.severity==="error").length} خطا ·
          {" "}{issues.filter(f=>f.severity==="warning").length} هشدار</span>
      </div>
      {issues.length===0 ? <p>در این پیش‌بررسی خطایی پیدا نشد؛ تأیید رسمی نیست.</p> :
        <ul>{issues.map((f,i)=><li key={f.key+"-"+i}
          className={"invoice-validation__"+f.severity}>
          <strong>{f.severity==="error"?"خطا":"هشدار"}</strong>
          <span>{f.message}</span>
          <code dir="ltr">{f.fieldPath}</code>
        </li>)}</ul>}
      <p className="invoice-validation__warning">
        <strong>وضعیت: پیش‌نویس داخلی، نه صورتحساب قطعی قابل ارسال.</strong>
        <span> حتی اگر خطای فرم صفر باشد، صدور قطعی مستلزم شناسه رسمی ۱۳ رقمی تمام اقلام، حافظه معتبر در محیط انتخابی، تولید شناسه یکتای مالیاتی صورتحساب، اعتبارسنجی قواعد معتبر و آماده‌سازی بسته امضاشده است. امکان ارسال Sandbox با تست اتصال حافظه به‌تنهایی ایجاد نمی‌شود.</span>
      </p>
    </div>}
    <div className="invoice-validation__reference">
      <label htmlFor="invoice-reference-subject">موضوع صورتحساب برای بررسی ارجاع</label>
      <select id="invoice-reference-subject" value={subject}
        onChange={e=>{setSubject(e.target.value);setSelectedRef("");}}>
        {subjectNames.map(x=><option key={x.value} value={x.value}>{x.label}</option>)}
      </select>
      {subject!=="Original" && <>
        <label htmlFor="invoice-reference-candidate">صورتحساب مرتبط (فقط سابقه داخلی)</label>
        <select id="invoice-reference-candidate" value={selectedRef}
          onChange={e=>setSelectedRef(e.target.value)}>
          <option value="">مرجع تأییدشده‌ای در دسترس نیست</option>
          {references.map(r=><option key={r.id} value={r.id} disabled>
            {r.id.slice(0,8)} · {r.subject} · {r.state}
          </option>)}
        </select>
        <p className="invoice-validation__warning">
          مرجع فقط با شناسه مالیاتی معتبر، سابقه ثبت رسمی و بررسی زنجیره قابل انتخاب است.
          رکوردهای محلی فعلی چنین شواهدی ندارند و عمداً غیرفعال‌اند.
        </p>
        {references.length>0 && <div className="invoice-validation__history">
          <strong>سوابق داخلی همین مؤدی ({references.length} رکورد)</strong>
          <ul>{references.map(r=><li key={r.id}>
            <bdi dir="ltr">{r.id.slice(0,8)}…</bdi>
            <span>{r.subject} · {r.state} · نسخه {r.revision}</span>
            <small>شناسه مالیاتی رسمی: تأیید نشده</small>
          </li>)}</ul>
        </div>}
      </>}
    </div>
  </section>;
}
