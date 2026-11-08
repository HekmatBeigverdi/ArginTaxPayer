"use client";
import { useEffect, useState } from "react";
import OperationMessageBox from "./operation-message-box";

type Memory = { id: string; memoryIdentifier: string };
type Result = { status: string; testedAtUtc: string; oneTimeTest?: boolean; environmentPersisted?: boolean; memoryActivated?: boolean;
  testEnvironment?: "Production" | "Sandbox" | null; fiscalInformation?: {
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
const httpErrors: Record<string, string> = {
  unauthorized: "نشست شما منقضی شده است؛ دوباره وارد شوید.",
  forbidden: "دسترسی به این حافظه مجاز نیست.",
  origin_not_allowed: "درخواست از مبدأ مجاز ارسال نشده است؛ صفحه را تازه‌سازی کنید.",
  not_found: "مودی یا حافظه مالیاتی پیدا نشد.",
  invalid_request: "درخواست آزمون معتبر نبود؛ صفحه را تازه‌سازی و دوباره امتحان کنید.",
  request_too_large: "درخواست آزمون نامعتبر بود؛ صفحه را تازه‌سازی و دوباره امتحان کنید.",
  service_unavailable: "سرویس برنامه در دسترس نیست؛ چند لحظه بعد دوباره امتحان کنید.",
  authority_timeout: "پاسخی از سرویس مؤدیان در مهلت مقرر دریافت نشد.",
  request_rejected: "سرویس برنامه درخواست آزمون را نپذیرفت.",
  invalid_response: "پاسخ سرویس برنامه برای آزمون اتصال معتبر نبود؛ نسخه سرویس را بررسی کنید.",
  unavailable: "آزمون اتصال در این سرویس فعال نیست."
};
export default function SigningConnectionTest({ taxpayerId, memories, loadingMemories, hideMemorySelector = false,
  oneTimeEnvironment, onAuthenticated, onReenterPrivateKey, onTestCompleted }:
 { taxpayerId: string; memories: Memory[]; loadingMemories: boolean; hideMemorySelector?: boolean;
   oneTimeEnvironment?: "Production" | "Sandbox"; onAuthenticated?: (authenticated: boolean) => void;
   onReenterPrivateKey?: () => void; onTestCompleted?: (authenticated: boolean) => void }) {
  const [selected, setSelected] = useState("");
  const memoryId = memories.some(m => m.id === selected) ? selected : memories[0]?.id ?? "";
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => { setResult(null); setError(""); }, [taxpayerId, memoryId]);
  async function testConnection() {
    if (!memoryId || busy) return;
    setBusy(true); setResult(null); setError(""); onAuthenticated?.(false);
    try {
      const body = oneTimeEnvironment ? JSON.stringify({ testEnvironment: oneTimeEnvironment }) : undefined;
      const res = await fetch(`/api/signing/${taxpayerId}/${memoryId}/connection-test`, {
        method: "POST", credentials: "same-origin", cache: "no-store",
        ...(body ? { headers: { "Content-Type": "application/json" }, body } : {}),
        signal: AbortSignal.timeout(35000)
      });
      if (!res.ok) {
        const payload = await res.json().catch(() => null);
        const code = payload && typeof payload.error === "string" &&
          Object.prototype.hasOwnProperty.call(httpErrors, payload.error) ? payload.error : "";
        const message = code ? httpErrors[code] : res.status === 401 ? httpErrors.unauthorized :
          res.status === 403 ? httpErrors.forbidden : "آزمون ارتباط انجام نشد.";
        setError(`${message} (HTTP ${res.status}${code ? `، کد ${code}` : ""})`);
        return;
      }
      const value = await res.json();
      if (typeof value.status !== "string" || !Object.prototype.hasOwnProperty.call(labels, value.status)) {
        setError("نتیجه آزمون نامعتبر است."); return;
      }
      setResult(value as Result);
      onTestCompleted?.(value.status === "Authenticated" && !!value.fiscalInformation);
      onAuthenticated?.((!value.oneTimeTest || value.environmentPersisted === true) &&
        value.status === "Authenticated" && !!value.fiscalInformation);
    } catch { setError("آزمون ارتباط در دسترس نیست."); }
    finally { setBusy(false); }
  }
  return <section aria-busy={busy} className="taxpayer-connection-test">
    <strong>آزمون ارتباط فقط‌خواندنی</strong>
    {loadingMemories ? <p className="field-hint">در حال دریافت حافظه‌ها…</p> :
      memories.length === 0 ? <p className="field-hint">ابتدا حافظه مالیاتی ثبت کنید.</p> :
      <>{!hideMemorySelector && <label>حافظه مالیاتی<select value={memoryId} disabled={busy}
        onChange={e => setSelected(e.target.value)}>
        {memories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}</option>)}
      </select></label>}
      <button type="button" className="identity-secondary" disabled={busy}
        onClick={() => void testConnection()}>{busy ? "در حال بررسی…" : "تست اتصال"}</button></>}
    {error && <OperationMessageBox key={error} kind="error" title="آزمون اتصال انجام نشد">{error}</OperationMessageBox>}
    {result && <OperationMessageBox key={result.testedAtUtc + result.status}
      kind={result.status === "Authenticated" ? "success" : result.status === "KeyNotReady" ? "warning" : "error"}
      title={result.status === "Authenticated" ? "ارتباط با سامانه مؤدیان برقرار شد" : "نتیجه آزمون ارتباط"}
      detail={Number.isNaN(Date.parse(result.testedAtUtc)) ? undefined : new Date(result.testedAtUtc).toLocaleString("fa-IR")}>
      {result.status === "Authenticated" && result.fiscalInformation && result.oneTimeTest
        ? result.memoryActivated
          ? "آزمون موفق بود؛ محیط و فعال‌بودن حافظه با پاسخ سازمان تأیید شد و حافظه برای استفاده در صورتحساب آماده است."
          : "اطلاعات حافظه دریافت شد، اما فعال‌سازی خودکار انجام نشد؛ وضعیت فعال حافظه و تطابق شناسه مؤدی را بررسی کنید."
        : result.status === "Authenticated" && result.fiscalInformation
        ? result.memoryActivated
          ? "اتصال موفق بود؛ فعال‌بودن حافظه با پاسخ سازمان تأیید شد و حافظه برای استفاده در صورتحساب آماده است."
          : "احراز هویت و دریافت اطلاعات حافظه انجام شد، اما سازمان فعال‌بودن حافظه و تطابق شناسه مؤدی را تأیید نکرد."
        : labels[result.status]}
    </OperationMessageBox>}
    {(error || result && result.status !== "Authenticated") && onReenterPrivateKey && <button type="button"
      className="identity-secondary" onClick={onReenterPrivateKey}>
      ورود دوبارهٔ کلید خصوصی همین حافظه
    </button>}
    {result?.status === "Authenticated" && result.fiscalInformation && <dl className="signing-fiscal-info">
      <div><dt>نام تجاری</dt><dd>{result.fiscalInformation.nameTrade}</dd></div>
      <div><dt>وضعیت حافظه</dt><dd>{result.fiscalInformation.fiscalStatus}</dd></div>
      <div><dt>کد اقتصادی</dt><dd>{result.fiscalInformation.economicCode}</dd></div>
      <div><dt>شناسه ملی</dt><dd>{result.fiscalInformation.nationalId}</dd></div>
    </dl>}
  </section>;
}
