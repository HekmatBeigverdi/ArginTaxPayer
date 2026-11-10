"use client";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import QRCode from "qrcode";

export default function AccountControls({ canEnrollMfa }: { canEnrollMfa: boolean }) {
  const router = useRouter();
  const [enrollment, setEnrollment] = useState<{ secret: string; provisioningUri: string } | null>(null);
  const [totp, setTotp] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [qrCode, setQrCode] = useState("");
  const [mfaPending, setMfaPending] = useState(false);
  const [mfaStatus, setMfaStatus] = useState<{ enabled: boolean; available: boolean } | null>(null);
  const [mfaMessage, setMfaMessage] = useState("");
  const [showRevealForm, setShowRevealForm] = useState(false);
  const [revealCode, setRevealCode] = useState("");
  const [enrollmentMode, setEnrollmentMode] = useState<"setup" | "revealed" | null>(null);

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
      setEnrollmentMode("setup");
      setTotp("");
    } catch { setMfaMessage("دریافت یا نمایش QR ممکن نشد؛ دوباره تلاش کنید."); }
    finally { setMfaPending(false); }
  }
  async function revealMfa() {
    if (mfaPending || !/^[0-9]{6}$/.test(revealCode)) return;
    setMfaPending(true);
    setMfaMessage("");
    try {
      const response = await fetch("/api/identity/mfa/reveal", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ code: revealCode }),
      });
      if (!response.ok) {
        setMfaMessage(response.status === 429 ? "تلاش‌های ناموفق زیاد بود؛ چند دقیقه بعد دوباره امتحان کنید."
          : response.status === 403 ? "نمایش QR فقط برای ادمین سطح ۱ یا ۲ مجاز است."
          : response.status === 401 ? "کد Authenticator معتبر نیست یا نشست شما منقضی شده است. کد تازه‌ای وارد کنید."
          : response.status === 503 ? "سرویس MFA در سرور پیکربندی نشده است."
          : "نمایش مجدد QR انجام نشد؛ وضعیت ورود را بررسی کنید.");
        return;
      }
      const setup = await response.json();
      if (typeof setup.secret !== "string" || typeof setup.provisioningUri !== "string" ||
          !setup.provisioningUri.startsWith("otpauth://totp/")) throw new Error();
      const image = await QRCode.toDataURL(setup.provisioningUri, {
        width: 280, margin: 4, errorCorrectionLevel: "M",
        color: { dark: "#000000", light: "#ffffff" },
      });
      setEnrollment(setup);
      setQrCode(image);
      setEnrollmentMode("revealed");
      setShowRevealForm(false);
      setRevealCode("");
    } catch { setMfaMessage("دریافت یا نمایش QR ممکن نشد؛ دوباره وارد شوید و تلاش کنید."); }
    finally { setMfaPending(false); }
  }
  function closeEnrollment() {
    setEnrollment(null);
    setQrCode("");
    setEnrollmentMode(null);
    setTotp("");
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
      closeEnrollment();
      setMfaStatus({ enabled: true, available: true });
      setMfaMessage("تأیید دومرحله‌ای فعال شد. کدهای بازیابی را نگه دارید و سپس دوباره وارد شوید.");
    } catch { setMfaMessage("ارتباط قطع شد. وضعیت فعال‌سازی را با ورود مجدد بررسی کنید."); }
    finally { setMfaPending(false); }
  }
  return <div className="account-grid">
    {canEnrollMfa && <section className="intro"><h2>تأیید دومرحله‌ای</h2>
      <p>برای ورود مدیریتی از Google Authenticator استفاده کنید.</p>
      {mfaStatus?.enabled && <p role="status">تأیید دومرحله‌ای فعال است.</p>}
      {mfaStatus?.enabled && mfaStatus.available && !enrollment && <button type="button" disabled={mfaPending}
        className="identity-secondary" onClick={() => { setShowRevealForm(true); setMfaMessage(""); }}>
        نمایش مجدد QR برای Authenticator
      </button>}
      {mfaStatus?.enabled && showRevealForm && !enrollment && <div className="identity-form">
        <p>کد تازه شش‌رقمی Authenticator فعلی را برای تأیید وارد کنید. نمایش QR به افراد دیگر، کلید ورود همین حساب را در اختیار آن‌ها می‌گذارد؛ برای هر مدیر حساب جداگانه امن‌تر است.</p>
        <label htmlFor="mfa-reveal-code">کد Authenticator فعلی</label>
        <input dir="ltr" id="mfa-reveal-code" value={revealCode}
          onChange={e => setRevealCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
          inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}/>
        <button type="button" disabled={mfaPending || !/^[0-9]{6}$/.test(revealCode)}
          className="identity-primary" onClick={() => void revealMfa()}>
          {mfaPending ? "در حال بررسی کد…" : "تأیید و نمایش QR"}
        </button>
        <button type="button" className="identity-secondary" disabled={mfaPending}
          onClick={() => { setShowRevealForm(false); setRevealCode(""); }}>انصراف</button>
      </div>}
      {mfaStatus && !mfaStatus.available && <p role="alert">سرویس تأیید دومرحله‌ای در سرور پیکربندی نشده است.</p>}
      {!mfaStatus && !mfaMessage && <p>در حال دریافت وضعیت...</p>}
      {!enrollment && mfaStatus?.available && !mfaStatus.enabled && recoveryCodes.length === 0 &&
        <button type="button" disabled={mfaPending} className="identity-primary" onClick={()=>void startMfa()}>
          {mfaPending ? "در حال آماده‌سازی QR..." : "فعال‌سازی MFA"}
        </button>}
      {enrollment && <div className="identity-form">
        {enrollmentMode === "revealed" ? <p role="alert">این QR کلید فعلی ورود این حساب است. فقط به افراد مجاز نشان دهید. کدی که برای نمایش وارد کردید مصرف شد؛ برای ورود بعدی کد تازه بگیرید.</p>
          : <p>در Google Authenticator گزینهٔ افزودن حساب و اسکن QR را انتخاب کنید. سپس کد شش‌رقمی برنامه را اینجا وارد کنید.</p>}
        {qrCode && <img src={qrCode} width={280} height={280} style={{ maxWidth: "100%", height: "auto" }}
          alt="کد QR راه‌اندازی Google Authenticator"/>}
        {enrollmentMode === "setup" && <p>اسکن QR به‌تنهایی کافی نیست؛ فعال‌سازی پس از تأیید کد انجام می‌شود.</p>}
        <details><summary>ورود دستی کلید به‌جای اسکن</summary>
          <code dir="ltr" className="sensitive-value">{enrollment.secret}</code>
        </details>
        {enrollmentMode === "setup" ? <>
          <label htmlFor="setup-totp">کد شش‌رقمی</label>
          <input dir="ltr" id="setup-totp" value={totp} onChange={e=>setTotp(e.target.value)}
            inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}/>
          <button type="button" disabled={mfaPending || !/^[0-9]{6}$/.test(totp)} className="identity-primary" onClick={()=>void confirmMfa()}>
            {mfaPending ? "در حال تأیید..." : "تأیید و فعال‌سازی"}
          </button>
        </> : <button type="button" className="identity-secondary" onClick={closeEnrollment}>بستن نمایش QR</button>}
      </div>}
      {recoveryCodes.length > 0 && <div>
        <p role="status">کدهای بازیابی فقط همین بار نشان داده می‌شوند؛ آن‌ها را در محل امن نگهداری کنید.</p>
        <ul className="session-list">{recoveryCodes.map(c=><li key={c}><code dir="ltr">{c}</code></li>)}</ul>
        <button type="button" className="identity-secondary" onClick={()=>{setRecoveryCodes([]); router.replace("/identity"); router.refresh();}}>کدها را ذخیره کردم؛ ورود مجدد</button>
      </div>}
      {mfaMessage && <p role="status">{mfaMessage}</p>}
    </section>}
  </div>;
}
