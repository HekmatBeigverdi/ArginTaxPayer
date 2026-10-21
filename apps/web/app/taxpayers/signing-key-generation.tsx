"use client";

import { useEffect, useState } from "react";

type Memory = { id: string; memoryIdentifier: string; status: number };
type State = { configured: boolean; freshLogin: boolean; fingerprintSha256: string | null };

export default function SigningKeyGeneration({ taxpayerId, memories, loadingMemories }:
  { taxpayerId: string; memories: Memory[]; loadingMemories: boolean }) {
  const [selected, setSelected] = useState("");
  const memoryId = memories.some(item => item.id === selected) ? selected : memories[0]?.id ?? "";
  const [state, setState] = useState<State | null>(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (!memoryId) return;
    let active = true;
    setState(null); setError(""); setNotice(""); setConsent(false);
    void fetch(`/api/signing/${taxpayerId}/${memoryId}`, {
      cache: "no-store", credentials: "same-origin"
    }).then(async response => {
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || "وضعیت کلید قابل دریافت نیست.");
      if (active) setState(data);
    }).catch(ex => { if (active) setError(ex instanceof Error ? ex.message : "خطا در دریافت وضعیت کلید."); });
    return () => { active = false; };
  }, [taxpayerId, memoryId]);

  async function generate() {
    if (busy || !state?.configured || !state.freshLogin || !consent || !memoryId) return;
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
        throw new Error(data?.error || "تولید کلید انجام نشد.");
      }
      if (type.toLowerCase().startsWith("application/zip")) {
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url; link.download = response.headers.get("content-disposition")?.match(/filename="?([^";]+)"?/i)?.[1] || "signing-keys.zip";
        link.click(); URL.revokeObjectURL(url);
        setNotice(`کلیدها دانلود شدند. اثر انگشت: ${response.headers.get("x-signing-key-fingerprint") || "نامشخص"}`);
      } else {
        const data = await response.json();
        setNotice(data.status === "replayed"
          ? "این عملیات قبلاً انجام شده و کلید خصوصی دوباره قابل دانلود نیست؛ برای دریافت بسته جدید عملیات تازه‌ای آغاز کنید."
          : "عملیات انجام شد، اما تحویل اولیه کلید خصوصی در این پاسخ موجود نیست.");
      }
    } catch (ex) { setError(ex instanceof Error ? ex.message : "خطا در تولید کلید."); }
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
    {!state?.configured && <p className="field-hint">Vault هنوز آماده نیست؛ تولید کلید غیرفعال است.</p>}
    {state && !state.freshLogin && <p className="field-hint">برای تولید کلید دوباره احراز هویت کنید.</p>}
    <label className="signing-custody-consent"><input type="checkbox" checked={consent} disabled={busy}
      onChange={event => setConsent(event.target.checked)} />رضایت می‌دهم کلید جدید تولید و نسخه اولیه خصوصی آن فقط یک‌بار دانلود شود.</label>
    <button type="button" className="identity-primary" disabled={busy || !state?.configured || !state.freshLogin || !consent}
      onClick={() => void generate()}>{busy ? "در حال تولید و آماده‌سازی دانلود…" : "ساخت و دانلود کلید جدید"}</button>
    <p className="field-hint">کلید عمومی را در کارپوشه بارگذاری کنید؛ کد حافظه مالیاتی پس از ثبت توسط سامانه مالیات ارائه می‌شود. کلید خصوصی را فقط در نرم‌افزار مالی امن خود وارد کنید.</p>
  </div>;
}
