"use client";
import { useEffect, useState } from "react";
import OperationMessageBox from "./operation-message-box";

type Memory = { id: string; memoryIdentifier: string };
type Result = { status: string; testedAtUtc: string; fiscalInformation?: {
  nameTrade: string; fiscalStatus: string; economicCode: string; nationalId: string
} | null };
const labels: Record<string, string> = {
  Authenticated: "احراز هویت موفق است؛ ثبت کلید و فعال بودن حافظه تأیید نشده است.",
  AuthenticationRejected: "احراز هویت رد شد.",
  KeyNotReady: "کلید آماده نیست.",
  VaultUnavailable: "سرویس نگهداری امن کلید در دسترس نیست.",
  EndpointUnavailable: "سرویس مؤدیان در دسترس نیست.",
  TimedOut: "مهلت آزمون تمام شد.",
  UnsupportedProfile: "پروفایل رسمی اتصال هنوز فعال نیست؛ درخواست زنده‌ای ارسال نشده است.",
  Indeterminate: "نتیجه نامشخص است.",
  NotAuthorized: "دسترسی مجاز نیست."
};
export default function SigningConnectionTest({ taxpayerId, memories, loadingMemories }:
 { taxpayerId: string; memories: Memory[]; loadingMemories: boolean }) {
  const [selected, setSelected] = useState("");
  const memoryId = memories.some(m => m.id === selected) ? selected : memories[0]?.id ?? "";
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => { setResult(null); setError(""); }, [taxpayerId, memoryId]);
  async function testConnection() {
    if (!memoryId || busy) return;
    setBusy(true); setResult(null); setError("");
    try {
      const res = await fetch(`/api/signing/${taxpayerId}/${memoryId}/connection-test`, {
        method: "POST", credentials: "same-origin", cache: "no-store",
        signal: AbortSignal.timeout(35000)
      });
      if (!res.ok) { setError(res.status === 403 ? "دسترسی به این حافظه مجاز نیست." :
        res.status === 401 ? "دوباره وارد شوید." : "آزمون ارتباط انجام نشد."); return; }
      const value = await res.json();
      if (typeof value.status !== "string" || !Object.prototype.hasOwnProperty.call(labels, value.status)) {
        setError("نتیجه آزمون نامعتبر است."); return;
      }
      setResult(value as Result);
    } catch { setError("آزمون ارتباط در دسترس نیست."); }
    finally { setBusy(false); }
  }
  return <section aria-busy={busy} className="taxpayer-connection-test">
    <strong>آزمون ارتباط فقط‌خواندنی</strong>
    {loadingMemories ? <p className="field-hint">در حال دریافت حافظه‌ها…</p> :
      memories.length === 0 ? <p className="field-hint">ابتدا حافظه مالیاتی ثبت کنید.</p> :
      <><label>حافظه مالیاتی<select value={memoryId} disabled={busy}
        onChange={e => setSelected(e.target.value)}>
        {memories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}</option>)}
      </select></label>
      <button type="button" className="identity-secondary" disabled={busy}
        onClick={() => void testConnection()}>{busy ? "در حال بررسی…" : "تست اتصال"}</button></>}
    {error && <OperationMessageBox key={error} kind="error" title="آزمون اتصال انجام نشد">{error}</OperationMessageBox>}
    {result && <OperationMessageBox key={result.testedAtUtc + result.status}
      kind={result.status === "Authenticated" ? "success" : result.status === "KeyNotReady" ? "warning" : "error"}
      title={result.status === "Authenticated" ? "ارتباط با سامانه مؤدیان برقرار شد" : "نتیجه آزمون ارتباط"}
      detail={Number.isNaN(Date.parse(result.testedAtUtc)) ? undefined : new Date(result.testedAtUtc).toLocaleString("fa-IR")}>
      {result.status === "Authenticated" && result.fiscalInformation
        ? "احراز هویت و دریافت اطلاعات حافظه انجام شد."
        : labels[result.status]}
    </OperationMessageBox>
    {result?.status === "Authenticated" && result.fiscalInformation && <dl className="signing-fiscal-info">
      <div><dt>نام تجاری</dt><dd>{result.fiscalInformation.nameTrade}</dd></div>
      <div><dt>وضعیت حافظه</dt><dd>{result.fiscalInformation.fiscalStatus}</dd></div>
      <div><dt>کد اقتصادی</dt><dd>{result.fiscalInformation.economicCode}</dd></div>
      <div><dt>شناسه ملی</dt><dd>{result.fiscalInformation.nationalId}</dd></div>
    </dl>}
    <p className="field-hint">این آزمون صورتحساب ارسال نمی‌کند و تأیید حافظه نیست.</p>
  </section>;
}
