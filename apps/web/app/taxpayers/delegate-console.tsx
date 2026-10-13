"use client";
import { FormEvent, useCallback, useEffect, useState } from "react";

type Grant = { id: string; taxpayerId: string; delegateUserId: string; grantedByLevel1UserId: string; grantedAtUtc: string; revokedAtUtc: string | null; revokedByLevel1UserId: string | null; revocationReason: string | null; revision: number };
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
function readable(status: number) {
  return status === 401 ? "نشست شما معتبر نیست؛ دوباره وارد شوید." :
    status === 403 ? "مجوز مدیریت واگذاری‌ها وجود ندارد." :
    status === 404 ? "مؤدی یا واگذاری یافت نشد." :
    status === 409 ? "مجوز قبلاً ثبت، لغو یا هم‌زمان تغییر کرده است؛ بازخوانی کنید." :
    "عملیات انجام نشد. اتصال سرویس و اطلاعات ورودی را بررسی کنید.";
}
export default function DelegateConsole({ taxpayerId, taxpayerName, onChanged }:
  { taxpayerId: string; taxpayerName: string; onChanged?: () => void }) {
  const [history, setHistory] = useState<Grant[]>([]);
  const [delegateId, setDelegateId] = useState("");
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const response = await fetch("/api/taxpayers/" + taxpayerId + "/delegates/history",
        { cache: "no-store", credentials: "same-origin" });
      if (!response.ok) throw new Error(readable(response.status));
      setHistory(await response.json() as Grant[]);
    } catch (e) { setError((e as Error).message); setHistory([]); }
    finally { setLoading(false); }
  }, [taxpayerId]);
  useEffect(() => { void refresh(); }, [refresh]);

  async function grant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy || !uuid.test(delegateId.trim())) return;
    setBusy(true); setError(""); setMessage("");
    try {
      const response = await fetch("/api/taxpayers/" + taxpayerId + "/delegates", {
        method: "POST", headers: { "Content-Type": "application/json" }, credentials: "same-origin",
        body: JSON.stringify({ delegateUserId: delegateId.trim() })
      });
      if (!response.ok) throw new Error(readable(response.status));
      setDelegateId(""); setMessage("واگذاری ثبت شد."); await refresh(); onChanged?.();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function revoke(grant: Grant) {
    const reason = (reasons[grant.id] || "").trim();
    if (busy || !reason) return;
    if (!window.confirm("دسترسی این ادمین به مؤدی لغو شود؟")) return;
    setBusy(true); setError(""); setMessage("");
    try {
      const response = await fetch("/api/taxpayers/" + taxpayerId + "/delegates/" + grant.id + "/revoke", {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedRevision: grant.revision, reason })
      });
      if (!response.ok) throw new Error(readable(response.status));
      setMessage("دسترسی لغو شد؛ تغییر در درخواست‌های بعدی ادمین سطح ۲ اعمال می‌شود.");
      await refresh(); onChanged?.();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <article className="taxpayer-panel delegate-console">
    <div className="taxpayer-section-heading"><h2>مدیریت واگذاری‌ها — {taxpayerName}</h2>
      <button className="identity-secondary" type="button" disabled={loading || busy}
        onClick={() => void refresh()}>بازخوانی تاریخچه</button></div>
    <p className="identity-muted">فقط ادمین سطح ۱ می‌تواند یک مؤدی را به ادمین سطح ۲ فعال دارای MFA واگذار کند یا مجوز قبلی را لغو کند. شناسه کاربر مقصد را از حساب او دریافت کنید.</p>
    <form className="identity-form" onSubmit={grant}>
      <label htmlFor="delegate-target">شناسه کاربری ادمین سطح ۲ (UUID)</label>
      <input id="delegate-target" dir="ltr" value={delegateId} required
        placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
        onChange={e => setDelegateId(e.target.value)}/>
      <button className="identity-primary" type="submit" disabled={busy || !uuid.test(delegateId.trim())}>ثبت واگذاری</button>
    </form>
    {error && <p className="form-error" role="alert">{error}</p>}
    {message && <p className="auth-info" role="status">{message}</p>}
    <h3>سوابق واگذاری</h3>
    {loading ? <p role="status">در حال بازخوانی…</p> :
      history.length === 0 ? <p className="identity-muted">هنوز واگذاری ثبت نشده است.</p> :
      <ul className="delegate-history">{history.map(item => <li key={item.id}>
        <div className="delegate-row">
          <div><strong>{item.revokedAtUtc ? "لغوشده" : "فعال"}</strong>
            <p><bdi dir="ltr">{item.delegateUserId}</bdi></p>
            <small>صدور: {new Date(item.grantedAtUtc).toLocaleString("fa-IR")}</small>
            {item.revokedAtUtc && <p>لغو: {new Date(item.revokedAtUtc).toLocaleString("fa-IR")} — {item.revocationReason}</p>}
          </div>
          {!item.revokedAtUtc && <div className="delegate-revoke">
            <label htmlFor={"revoke-" + item.id}>علت لغو دسترسی</label>
            <input id={"revoke-" + item.id} value={reasons[item.id] || ""} maxLength={500}
              onChange={e => setReasons(prev => ({ ...prev, [item.id]: e.target.value }))}/>
            <button type="button" disabled={busy || !(reasons[item.id] || "").trim()}
              onClick={() => void revoke(item)}>لغو واگذاری</button>
          </div>}
        </div>
      </li>)}</ul>}
  </article>;
}
