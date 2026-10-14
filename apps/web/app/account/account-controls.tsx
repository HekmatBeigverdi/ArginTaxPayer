"use client";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import QRCode from "qrcode";

type SessionResult = { sessionIds: string[] };
export default function AccountControls({ canEnrollMfa }: { canEnrollMfa: boolean }) {
  const router = useRouter();
  const [sessions, setSessions] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [enrollment, setEnrollment] = useState<{ secret: string; provisioningUri: string } | null>(null);
  const [totp, setTotp] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [qrCode, setQrCode] = useState("");
  const [mfaPending, setMfaPending] = useState(false);
  const [mfaStatus, setMfaStatus] = useState<{ enabled: boolean; available: boolean } | null>(null);
  const [mfaMessage, setMfaMessage] = useState("");

  useEffect(() => {
    if (!canEnrollMfa) return;
    let cancelled = false;
    async function loadMfaStatus() {
      try {
        const response = await fetch("/api/identity/mfa/status", { cache: "no-store" });
        if (!response.ok) throw new Error();
        const status = await response.json();
        if (!cancelled) setMfaStatus(status);
      } catch {
        if (!cancelled) setMfaMessage("دریافت وضعیت تأیید دومرحله‌ای ممکن نیست؛ صفحه را تازه کنید یا دوباره وارد شوید.");
      }
    }
    void loadMfaStatus();
    return () => { cancelled = true; };
  }, [canEnrollMfa]);
  async function load() {
    try {
      const res = await fetch("/api/identity/sessions", { cache: "no-store" });
      if (res.status === 401) { router.replace("/identity"); return; }
      if (res.ok) setSessions(((await res.json()) as SessionResult).sessionIds);
    } catch { setMessage("دریافت نشست‌ها امکان‌پذیر نیست."); }
    finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, []);
  async function logout() {
    await fetch("/api/identity/logout", { method:"POST" });
    router.replace("/identity"); router.refresh();
  }
  async function revoke(sessionId:string) {
    const response = await fetch("/api/identity/sessions/revoke", {
      method:"POST", headers:{"Content-Type":"application/json"},
      body:JSON.stringify({sessionId})
    });
    if (response.ok) { await load(); router.refresh(); }
    else setMessage("لغو نشست انجام نشد.");
  }
  async function startMfa() {
    if (mfaPending) return;
    setMfaPending(true);
    setMfaMessage("");
    try {
      const result = await fetch("/api/identity/mfa/enroll", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: "{}",
      });
      if (!result.ok) {
        setMfaMessage(result.status === 401 ? "نشست شما منقضی شده است؛ دوباره وارد شوید."
          : result.status === 403 ? "فعال‌سازی فقط برای کاربران معرفی‌شده جهت مدیریت مجاز است."
          : result.status === 503 ? "سرویس تأیید دومرحله‌ای در سرور پیکربندی نشده است."
          : "فعال‌سازی انجام نشد؛ وضعیت حساب را با تازه‌کردن صفحه بررسی کنید.");
        return;
      }
      const setup = await result.json();
      if (typeof setup.secret !== "string" || typeof setup.provisioningUri !== "string" ||
          !setup.provisioningUri.startsWith("otpauth://totp/")) throw new Error();
      const image = await QRCode.toDataURL(setup.provisioningUri, {
        width: 280, margin: 4, errorCorrectionLevel: "M",
        color: { dark: "#000000", light: "#ffffff" },
      });
      setEnrollment(setup);
      setQrCode(image);
      setTotp("");
    } catch { setMfaMessage("دریافت یا نمایش QR ممکن نشد؛ دوباره تلاش کنید."); }
    finally { setMfaPending(false); }
  }
  async function confirmMfa() {
    if (mfaPending || !/^[0-9]{6}$/.test(totp)) return;
    setMfaPending(true);
    setMfaMessage("");
    try {
      const response = await fetch("/api/identity/mfa/confirm", {
        method:"POST", headers:{"Content-Type":"application/json"},
        body:JSON.stringify({ code: totp })
      });
      if (!response.ok) {
        setMfaMessage(response.status === 503 ? "سرویس تأیید دومرحله‌ای در دسترس نیست."
          : "تأیید انجام نشد؛ کد جدید Google Authenticator را وارد کنید. اگر نشست منقضی شده، دوباره وارد شوید.");
        return;
      }
      const result = await response.json();
      setRecoveryCodes(result.codes);
      setEnrollment(null); setQrCode(""); setTotp("");
      setMfaStatus({ enabled: true, available: true });
      setMfaMessage("تأیید دومرحله‌ای فعال شد. کدهای بازیابی را نگه دارید و سپس دوباره وارد شوید.");
    } catch { setMfaMessage("ارتباط قطع شد. وضعیت فعال‌سازی را با ورود مجدد بررسی کنید."); }
    finally { setMfaPending(false); }
  }
  return <div className="account-grid">
    <section className="intro"><h2>نشست‌های شما</h2>
      {loading ? <p>در حال دریافت...</p> : sessions.length === 0 ? <p>نشست فعالی یافت نشد.</p> :
        <ul className="session-list">{sessions.map(session=><li key={session}>
          <bdi dir="ltr">{session}</bdi>
          <button type="button" onClick={()=>void revoke(session)}>لغو نشست</button></li>)}</ul>}
      <button type="button" className="identity-secondary" onClick={()=>void logout()}>خروج از حساب</button>
    </section>
    {canEnrollMfa && <section className="intro"><h2>تأیید دومرحله‌ای</h2>
      <p>برای ورود مدیریتی از Google Authenticator استفاده کنید.</p>
      {mfaStatus?.enabled && <p role="status">تأیید دومرحله‌ای فعال است.</p>}
      {mfaStatus && !mfaStatus.available && <p role="alert">سرویس تأیید دومرحله‌ای در سرور پیکربندی نشده است.</p>}
      {!mfaStatus && !mfaMessage && <p>در حال دریافت وضعیت...</p>}
      {!enrollment && mfaStatus?.available && !mfaStatus.enabled && recoveryCodes.length === 0 &&
        <button type="button" disabled={mfaPending} className="identity-primary" onClick={()=>void startMfa()}>
          {mfaPending ? "در حال آماده‌سازی QR..." : "فعال‌سازی MFA"}
        </button>}
      {enrollment && <div className="identity-form">
        <p>در Google Authenticator گزینهٔ افزودن حساب و اسکن QR را انتخاب کنید. سپس کد شش‌رقمی برنامه را اینجا وارد کنید.</p>
        {qrCode && <img src={qrCode} width={280} height={280} style={{ maxWidth: "100%", height: "auto" }}
          alt="کد QR راه‌اندازی Google Authenticator"/>}
        <p>اسکن QR به‌تنهایی کافی نیست؛ فعال‌سازی پس از تأیید کد انجام می‌شود.</p>
        <details><summary>ورود دستی کلید به‌جای اسکن</summary>
          <code dir="ltr" className="sensitive-value">{enrollment.secret}</code>
        </details>
        <label htmlFor="setup-totp">کد شش‌رقمی</label>
        <input dir="ltr" id="setup-totp" value={totp} onChange={e=>setTotp(e.target.value)}
          inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}/>
        <button type="button" disabled={mfaPending || !/^[0-9]{6}$/.test(totp)} className="identity-primary" onClick={()=>void confirmMfa()}>
          {mfaPending ? "در حال تأیید..." : "تأیید و فعال‌سازی"}
        </button>
      </div>}
      {recoveryCodes.length > 0 && <div>
        <p role="status">کدهای بازیابی فقط همین بار نشان داده می‌شوند؛ آن‌ها را در محل امن نگهداری کنید.</p>
        <ul className="session-list">{recoveryCodes.map(c=><li key={c}><code dir="ltr">{c}</code></li>)}</ul>
        <button type="button" className="identity-secondary" onClick={()=>{setRecoveryCodes([]); router.replace("/identity"); router.refresh();}}>کدها را ذخیره کردم؛ ورود مجدد</button>
      </div>}
      {mfaMessage && <p role="status">{mfaMessage}</p>}
    </section>}
    {message && <p role="status" className="form-error">{message}</p>}
  </div>;
}
