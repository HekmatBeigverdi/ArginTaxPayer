"use client";

import { useEffect, useState, type FormEvent } from "react";
import OperationMessageBox from "./operation-message-box";

type Item = { id: string; kind: number; state: number; reasonCode: string; explanation: string;
  expectedKeyRevision: number; revision: number; requestedAtUtc: string };
const stateLabels: Record<number, string> = {
  1: "در انتظار تأیید سطح ۱", 2: "تأییدشده", 3: "ردشده", 4: "استفاده‌شده", 5: "منقضی‌شده"
};
const kindLabels: Record<number, string> = {
  1: "تعلیق کلید", 2: "گزارش افشای کلید", 3: "درخواست جایگزینی کلید"
};
const codeLabels: Record<string, string> = {
  compromised: "احتمال افشای کلید",
  ownership_change: "تغییر مسئول/مالک",
  replaced_in_portal: "تغییر کلید در کارپوشه",
  technical_issue: "مشکل فنی"
};
const errorMessages: Record<string, string> = {
  invalid_request: "اطلاعات درخواست کامل یا معتبر نیست.",
  forbidden: "ثبت درخواست فقط توسط مالک مؤدی مجاز است.",
  key_not_ready: "ابتدا کلید فعلی را ثبت و نهایی کنید.",
  conflict: "نسخه کلید تغییر کرده یا درخواست قبلی هنوز باز است. وضعیت را بازخوانی کنید.",
  second_memory_requires_plan_review: "تغییر حافظه دوم بدون بررسی مجوز طرح امکان‌پذیر نیست.",
  service_unavailable: "ثبت یا دریافت درخواست فعلاً ممکن نیست."
};

export default function SigningChangeRequestPanel({ taxpayerId, memoryId }: {
  taxpayerId: string; memoryId: string
}) {
  const [items, setItems] = useState<Item[]>([]);
  const [revision, setRevision] = useState<number | null>(null);
  const [kind, setKind] = useState(3);
  const [reasonCode, setReasonCode] = useState("technical_issue");
  const [explanation, setExplanation] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [tick, setTick] = useState(0);
  useEffect(() => {
    if (!memoryId) return;
    let active = true;
    setLoading(true); setError("");
    void fetch(`/api/signing/${taxpayerId}/${memoryId}/change-requests`, {
      credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(20000)
    }).then(async res => {
      const data = await res.json();
      if (!res.ok) throw new Error(errorMessages[data?.error] || "دریافت درخواست‌ها ناموفق بود.");
      if (active) {
        setItems(data.requests || []);
        setRevision(data.currentKeyRevision);
      }
    }).catch(ex => { if (active) setError(ex instanceof Error ? ex.message : "خطای دریافت اطلاعات"); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [taxpayerId, memoryId, tick]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !revision || explanation.trim().length < 10 || items.some(x => x.state === 1 || x.state === 2)) return;
    setBusy(true); setError(""); setNotice("");
    const requestId = crypto.randomUUID();
    try {
      const res = await fetch(`/api/signing/${taxpayerId}/${memoryId}/change-requests`, {
        method: "POST", credentials: "same-origin", cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ requestId, expectedKeyRevision: revision, kind, reasonCode,
          explanation: explanation.trim().replace(/\s+/g, " ") }), signal: AbortSignal.timeout(20000)
      });
      const data = await res.json();
      if (!res.ok) throw new Error(errorMessages[data?.error] || "درخواست ثبت نشد.");
      setNotice("درخواست تغییر ثبت شد. بررسی و تأیید آن در گام بعد توسط مدیر سطح ۱ انجام می‌شود؛ کلید فعلی تغییری نکرده است.");
      setExplanation("");
      setTick(x => x + 1);
    } catch (ex) { setError(ex instanceof Error ? ex.message : "ثبت درخواست ناموفق بود."); }
    finally { setBusy(false); }
  }

  if (!memoryId) return null;
  const pending = items.some(x => x.state === 1 || x.state === 2);
  return <section className="signing-change-requests" aria-busy={loading || busy}>
    <div className="signing-panel-heading">
      <div><h3>درخواست تغییر کلید حافظه</h3>
        <p>درخواست تعلیق، اعلام افشا یا جایگزینی کلید فقط برای حافظه فعلی ثبت می‌شود. کلید موجود خودکار تغییر نمی‌کند.</p></div>
      <button type="button" className="identity-secondary" disabled={loading || busy}
        onClick={() => setTick(x => x + 1)}>بازخوانی درخواست‌ها</button>
    </div>
    {error && <OperationMessageBox key={error} kind="error" title="خطای درخواست تغییر">{error}</OperationMessageBox>}
    {notice && <OperationMessageBox key={notice} kind="success" title="درخواست ثبت شد">{notice}</OperationMessageBox>}
    {items.some(x => x.state === 1) && <OperationMessageBox kind="warning" title="درخواست در انتظار بررسی است">
      درخواست شما منتظر بررسی مدیر سطح ۱ است.
    </OperationMessageBox>}
    {items.some(x => x.state === 2) && <OperationMessageBox kind="info" title="درخواست تأیید شده است">
      نیازی به درخواست دوباره نیست. برای ادامه، تب «ساخت کلید جدید» را باز کنید و با ورود تازه، ساخت و دانلود را انجام دهید. مجوز تأییدشده ۲۴ ساعت اعتبار دارد و در فهرست انتظار مدیر نمایش داده نمی‌شود.
    </OperationMessageBox>}
    <form className="signing-change-form" onSubmit={submit}>
      <label>نوع درخواست<select value={kind} disabled={busy || pending} onChange={e => setKind(Number(e.target.value))}>
        {Object.entries(kindLabels).map(([id,label]) => <option key={id} value={id}>{label}</option>)}
      </select></label>
      <label>علت<select value={reasonCode} disabled={busy || pending} onChange={e => setReasonCode(e.target.value)}>
        {Object.entries(codeLabels).map(([id,label]) => <option key={id} value={id}>{label}</option>)}
      </select></label>
      <label className="signing-change-form__reason">شرح درخواست
        <textarea value={explanation} onChange={e => setExplanation(e.target.value)}
          rows={3} minLength={10} maxLength={1000} disabled={busy || pending}
          placeholder="علت دقیق تغییر را در حداقل ۱۰ نویسه توضیح دهید…" required/>
      </label>
      <div className="signing-change-form__actions">
        <span>نسخه کلید فعلی: {revision ?? "ثبت نشده"}</span>
        <button className="identity-primary" type="submit" disabled={busy || loading || pending ||
          !revision || explanation.trim().length < 10}>{busy ? "در حال ثبت…" : "ثبت درخواست بررسی"}</button>
      </div>
    </form>
    {items.length > 0 && <div className="signing-change-history">
      <strong>سوابق درخواست‌های این حافظه</strong>
      <ul>{items.map(item => <li key={item.id}>
        <span>{kindLabels[item.kind] || "تغییر کلید"} · {codeLabels[item.reasonCode] || item.reasonCode}</span>
        <span>{stateLabels[item.state] || "نامشخص"}</span>
        <time>{new Date(item.requestedAtUtc).toLocaleString("fa-IR")}</time>
      </li>)}</ul>
    </div>}
  </section>;
}
