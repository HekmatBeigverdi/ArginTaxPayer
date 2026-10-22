"use client";

import { useEffect, useState } from "react";

type Memory = { id: string; memoryIdentifier: string; status: number };
type State = { configured: boolean; generationConfigured: boolean; freshLogin: boolean; fingerprintSha256: string | null };

export default function SigningKeyGeneration({ taxpayerId, memories, loadingMemories }:
  { taxpayerId: string; memories: Memory[]; loadingMemories: boolean }) {
  const [selected, setSelected] = useState("");
  const memoryId = memories.some(item => item.id === selected) ? selected : memories[0]?.id ?? "";
  const [state, setState] = useState<State | null>(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loadingState, setLoadingState] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (!memoryId) return;
    let active = true;
    setState(null); setError(""); setNotice(""); setConsent(false); setLoadingState(true);
    void fetch(`/api/signing/${taxpayerId}/${memoryId}`, {
      cache: "no-store", credentials: "same-origin"
    }).then(async response => {
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || "وضعیت کلید قابل دریافت نیست.");
      if (active) setState(data);
    }).catch(ex => { if (active) setError(ex instanceof Error ? ex.message : "خطا در دریافت وضعیت کلید."); })
      .finally(() => { if (active) setLoadingState(false); });
    return () => { active = false; };
  }, [taxpayerId, memoryId]);

  async function generate() {
    if (busy || !state?.generationConfigured || !state.freshLogin || !consent || !memoryId) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const response = await fetch(`/api/signing/${taxpayerId}/${memoryId}/generate`, {
        method: "POST", cache: "no-store", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ operationId: crypto.randomUUID(), consentVersion: "server-custody-v1", acceptServerCustody: true }),
        signal: AbortSignal.timeout(35000)
      });
      const type = response.headers.get("content-type") || "";
      if (!response.ok) {
        const data = await response.json().catch(() => null);
        const explanations: Record<string, string> = {
          custody_unavailable: "نگهداری امن کلید در دسترس نیست.",
          reauthentication_required: "برای ساخت کلید مجدداً وارد حساب شوید.",
          unauthorized: "نشست ورود معتبر نیست.",
          forbidden: "دسترسی ساخت کلید فقط برای مالک مؤدی مجاز است.",
          key_change_requires_review: "برای این حافظه قبلاً کلید ثبت شده است؛ تعویض به تأیید نیاز دارد.",
          recovery_required: "نتیجه عملیات نامشخص است؛ پیش از اقدام مجدد وضعیت کلید را بازخوانی کنید."
        };
        throw new Error(explanations[data?.error] || "ساخت کلید انجام نشد؛ کد خطا: " + (data?.error || response.status));
      }
      if (type.toLowerCase().startsWith("application/zip")) {
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url; link.download = response.headers.get("content-disposition")?.match(/filename="?([^";]+)"?/i)?.[1] || "signing-keys.zip";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 60000);
        setNotice(`کلیدها دانلود شدند. اثر انگشت: ${response.headers.get("x-signing-key-fingerprint") || "نامشخص"}`);
      } else {
        const data = await response.json();
        setNotice(data.status === "replayed"
          ? "این عملیات قبلاً انجام شده و کلید خصوصی دوباره قابل دانلود نیست؛ برای دریافت بسته جدید عملیات تازه‌ای آغاز کنید."
          : "عملیات انجام شد، اما تحویل اولیه کلید خصوصی در این پاسخ موجود نیست.");
      }
    } catch (ex) {
      setError(ex instanceof Error && (ex.name === "TimeoutError" || ex.name === "AbortError")
        ? "زمان پاسخ ساخت کلید به پایان رسید. پیش از تلاش مجدد، وضعیت کلید را بازخوانی کنید تا کلید تکراری ساخته نشود."
        : ex instanceof Error ? ex.message : "خطا در تولید کلید.");
    }
    finally { setBusy(false); }
  }

  if (loadingMemories) return <p className="field-hint">در حال دریافت حافظه مالیاتی…</p>;
  if (!memoryId) return <p className="field-hint">ابتدا کد حافظه مالیاتی را ثبت کنید.</p>;
  return <div className="signing-key-generation" aria-busy={busy}>
    <label>حافظه مالیاتی
      <select value={memoryId} disabled={busy} onChange={event => setSelected(event.target.value)}>
        {memories.map(item => <option key={item.id} value={item.id}>{item.memoryIdentifier}</option>)}
      </select>
    </label>
    {error && <p role="alert" className="party-error">{error}</p>}
    {notice && <p role="status" className="party-success">{notice}</p>}
    {state?.fingerprintSha256 && <p className="field-hint">اثر انگشت کلید عمومی فعلی: <bdi dir="ltr">{state.fingerprintSha256}</bdi></p>}
    {loadingState && <p role="status" className="field-hint">در حال بررسی تنظیمات ساخت کلید…</p>}
    {!loadingState && state && !state.generationConfigured &&
      <p role="status" className="field-hint">ساخت کلید در API آماده نیست. حالت Database، کلید اصلی رمزگذاری و Migration نگهداری کلید را بررسی کنید.</p>}
    {state && !state.freshLogin && <p className="field-hint">برای تولید کلید دوباره احراز هویت کنید.</p>}
    <label className="signing-custody-consent"><input type="checkbox" checked={consent} disabled={busy}
      onChange={event => setConsent(event.target.checked)} />رضایت می‌دهم کلید جدید تولید و نسخه اولیه خصوصی آن فقط یک‌بار دانلود شود.</label>
    <button type="button" className="identity-primary" disabled={loadingState || busy || !state?.generationConfigured || !state.freshLogin || !consent}
      onClick={() => void generate()}>{busy ? "در حال تولید و آماده‌سازی دانلود…" : "ساخت و دانلود کلید جدید"}</button>
    <p className="field-hint">کلید عمومی را در کارپوشه بارگذاری کنید؛ کد حافظه مالیاتی پس از ثبت توسط سامانه مالیات ارائه می‌شود. کلید خصوصی را فقط در نرم‌افزار مالی امن خود وارد کنید.</p>
  </div>;
}
