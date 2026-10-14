"use client";

import { FormEvent, useEffect, useState } from "react";
type OwnerChange = { id: string; previousPartnerLevel2UserId: string | null;
  newPartnerLevel2UserId: string | null; explanation: string; changedAtUtc: string;
  approvedByLevel1UserId: string; fromRevision: number; toRevision: number };
type CustomerOwner = {
  customerUserId: string; rootLevel1UserId: string; partnerLevel2UserId: string | null;
  acquisitionChannel: number; revision: number;
};
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const errorText = (status: number) => status === 401 ? "نشست منقضی شده است." :
  status === 403 ? "برای این عملیات مجوز ندارید." :
  status === 404 ? "مشتری در محدوده مجاز شما یافت نشد." :
  status === 409 ? "مالکیت مشتری هم‌زمان تغییر کرده است؛ فهرست را بازخوانی کنید." :
  "درخواست ناموفق بود؛ ورودی یا اتصال را بررسی کنید.";

export default function CustomerOwnershipConsole({ level1 }: { level1: boolean }) {
  const [customers, setCustomers] = useState<CustomerOwner[]>([]);
  const [selection, setSelection] = useState("");
  const [targetPartner, setTargetPartner] = useState("");
  const [reason, setReason] = useState("");
  const [reasonCode, setReasonCode] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [history, setHistory] = useState<OwnerChange[] | null>(null);

  async function refresh() {
    setBusy(true); setError("");
    try {
      const response = await fetch("/api/customer-owners/list", { cache: "no-store", credentials: "same-origin" });
      if (!response.ok) throw new Error(errorText(response.status));
      const records = await response.json() as CustomerOwner[];
      setCustomers(records);
      if (!records.some(item => item.customerUserId === selection)) setSelection("");
    } catch (ex) { setError((ex as Error).message); setCustomers([]); }
    finally { setBusy(false); }
  }
  useEffect(() => { void refresh(); }, []);
  const current = customers.find(c => c.customerUserId === selection);
  async function loadHistory(customerId: string) {
    setBusy(true); setError("");
    try {
      const response = await fetch("/api/customer-owners/" + customerId + "/history",
        { cache: "no-store", credentials: "same-origin" });
      if (!response.ok) throw new Error(errorText(response.status));
      setHistory(await response.json() as OwnerChange[]);
    } catch (ex) { setError((ex as Error).message); setHistory(null); }
    finally { setBusy(false); }
  }


  async function transfer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!level1 || busy || !current || !reason.trim() ||
      (targetPartner.trim() && !uuid.test(targetPartner.trim()))) return;
    if (!window.confirm("مالک تجاری این مشتری تغییر کند؟ تراکنش‌های مالی تاریخی تغییر نخواهند کرد.")) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const response = await fetch("/api/customer-owners/" + current.customerUserId + "/transfer", {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          partnerLevel2UserId: targetPartner.trim() || null,
          expectedRevision: current.revision,
          reason: reasonCode,
          explanation: reason.trim()
        })
      });
      if (!response.ok) throw new Error(errorText(response.status));
      setNotice("مالک تجاری مشتری تغییر کرد؛ تاریخچه انتقال محفوظ است.");
      setReason(""); setHistory(null); await refresh();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }

  return <section className="intro" aria-label="مالکیت تجاری مشتریان">
    <div className="taxpayer-section-heading">
      <h2>{level1 ? "مدیریت مالکیت مشتریان و شرکا" : "مشتریان زیرمجموعه من"}</h2>
      <button className="identity-secondary" type="button" disabled={busy} onClick={() => void refresh()}>بازخوانی</button>
    </div>
    <p className="identity-muted">{level1 ?
      "هر مشتری یک مالک تجاری دارد و می‌تواند چند مؤدی مالیاتی داشته باشد. انتساب مشتری به شریک فقط با تأیید ادمین سطح ۱ انجام می‌شود." :
      "تنها حساب‌های مشتریانی نمایش داده می‌شوند که به شما تعلق دارند. تسویه مالی طبق قرارداد مشارکت در فاز ۱۲ اضافه می‌شود."}</p>
    {error && <p role="alert" className="form-error">{error}</p>}
    {notice && <p role="status" className="auth-info">{notice}</p>}
    {busy && <p role="status">در حال دریافت یا ثبت اطلاعات…</p>}
    {!busy && customers.length === 0 && <p className="identity-muted">مشتری ثبت‌شده‌ای در محدوده شما یافت نشد.</p>}
    {customers.length > 0 && <>
      <div className="taxpayer-panel">
        <label htmlFor="commercial-customer-select">حساب مشتری</label>
        <select id="commercial-customer-select" value={selection}
          onChange={e => {
            const id = e.target.value; setSelection(id);
            const selected = customers.find(item => item.customerUserId === id);
            setTargetPartner(selected?.partnerLevel2UserId || ""); setReason(""); setHistory(null);
          }}>
          <option value="">انتخاب مشتری</option>
          {customers.map(c => <option key={c.customerUserId} value={c.customerUserId}>
            {c.customerUserId} — {c.partnerLevel2UserId ? "شریک سطح ۲" : "مستقیم سطح ۱"}
          </option>)}
        </select>
        {current && <dl className="taxpayer-facts">
          <div><dt>شناسه مشتری</dt><dd dir="ltr">{current.customerUserId}</dd></div>
          <div><dt>مالک تجاری فعلی</dt><dd dir="ltr">{current.partnerLevel2UserId || "ادمین سطح ۱"}</dd></div>
          <div><dt>نسخه انتساب</dt><dd>{current.revision}</dd></div>
        </dl>}
      </div>
      {level1 && current && <section className="taxpayer-panel">
        <button type="button" className="identity-secondary" disabled={busy}
          onClick={() => void loadHistory(current.customerUserId)}>مشاهده تاریخچه انتقال مشتری</button>
        {history && (history.length ? <ul className="delegate-history">{history.map(change =>
          <li key={change.id}>
            <p>از: <bdi dir="ltr">{change.previousPartnerLevel2UserId || "سطح ۱"}</bdi></p>
            <p>به: <bdi dir="ltr">{change.newPartnerLevel2UserId || "سطح ۱"}</bdi></p>
            <p>علت: {change.explanation}</p>
            <p>زمان: {new Date(change.changedAtUtc).toLocaleString("fa-IR")}</p>
            <p>تأییدکننده: <bdi dir="ltr">{change.approvedByLevel1UserId}</bdi></p>
          </li>)}</ul> : <p className="identity-muted">تغییر مالکیت ثبت نشده است.</p>)}
      </section>}
      {level1 && current && <form className="identity-form" onSubmit={transfer}>
        <label htmlFor="commercial-partner">شناسه ادمین سطح ۲ (برای مشتری مستقیم، خالی بگذارید)</label>
        <input id="commercial-partner" dir="ltr" value={targetPartner}
          onChange={e => setTargetPartner(e.target.value)} placeholder="UUID شریک تجاری"/>
        <label htmlFor="commercial-reason-kind">نوع تغییر</label>
        <select id="commercial-reason-kind" value={reasonCode} onChange={e => setReasonCode(Number(e.target.value))}>
          <option value={1}>معرفی مشتری و تأیید سطح ۱</option>
          <option value={2}>اصلاح انتساب اشتباه</option>
          <option value={3}>غیرفعال‌شدن شریک قبلی</option>
          <option value={4}>سایر دلایل</option>
        </select>
        <label htmlFor="commercial-reason">شرح و مستندات علت تغییر</label>
        <textarea id="commercial-reason" required maxLength={1000} rows={3}
          value={reason} onChange={e => setReason(e.target.value)}/>
        <button className="identity-primary" disabled={busy || !reason.trim() ||
          (targetPartner.trim() !== "" && !uuid.test(targetPartner.trim())) ||
          targetPartner.trim() === (current.partnerLevel2UserId || "")}>
          تأیید انتساب مشتری به شریک
        </button>
      </form>}
    </>}
    <p className="field-hint">تغییر مالکیت مشتری هرگز به‌تنهایی مانده کیف پول، تراکنش‌های گذشته، مؤدیِ قفل‌شده در پلن سالیانه یا تسویه انجام‌شده را تغییر نمی‌دهد.</p>
  </section>;
}
