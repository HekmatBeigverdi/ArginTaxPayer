"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type Tab = "login" | "register";
type Mode = "password" | "otp";
type Step = "details" | "code" | "mfa";
type Purpose = "register" | "login" | "reset";
const purposeCaptions: Record<Purpose, string> = {
  register: "تأیید شماره همراه",
  login: "ورود با کد یک‌بارمصرف",
  reset: "بازیابی رمز عبور",
};

function getPurpose(tab: Tab, forgot: boolean): Purpose {
  if (forgot) return "reset";
  return tab === "register" ? "register" : "login";
}

function usesPhoneOtp(
  tab: Tab,
  mode: Mode,
  forgot: boolean,
  emailRegistration: boolean,
) {
  return (
    forgot ||
    (tab === "register" && !emailRegistration) ||
    (tab === "login" && mode === "otp")
  );
}

function getOtpCompletionRequest(
  purpose: Purpose,
  identifier: string,
  code: string,
  password: string,
) {
  if (purpose === "register") {
    return {
      path: "phone/register/complete",
      body: { mobile: identifier, code, password: password || null },
    };
  }

  if (purpose === "reset") {
    return {
      path: "phone/reset/complete",
      body: { mobile: identifier, code, newPassword: password },
    };
  }

  return {
    path: "phone/login/complete",
    body: { mobile: identifier, code },
  };
}

function getOtpError(status: number) {
  return status === 429
    ? "تعداد تلاش‌ها زیاد است."
    : "کد تأیید اشتباه یا منقضی شده است.";
}

function getPasswordError(status: number) {
  if (status === 429) {
    return "تعداد تلاش‌ها بیش از حد مجاز است؛ بعداً دوباره امتحان کنید.";
  }

  return "نام کاربری یا رمز عبور نادرست است.";
}

export default function IdentityFlow() {
  const router = useRouter();
  const [tab, setTab] = useState<Tab>("login");
  const [mode, setMode] = useState<Mode>("password");
  const [step, setStep] = useState<Step>("details");
  const [forgot, setForgot] = useState(false);
  const [emailRegistration, setEmailRegistration] = useState(false);
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [challenge, setChallenge] = useState("");
  const [mfaExpiresAt, setMfaExpiresAt] = useState<number | null>(null);
  const [mfaSecondsLeft, setMfaSecondsLeft] = useState(0);
  const [recovery, setRecovery] = useState(false);
  const [pending, setPending] = useState(false);
  const [navigating, setNavigating] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [retryAfter, setRetryAfter] = useState(0);

  const purpose = getPurpose(tab, forgot);
  const phoneOtp = usesPhoneOtp(tab, mode, forgot, emailRegistration);

  useEffect(() => {
    if (retryAfter <= 0) return;
    const tick = setInterval(() => {
      setRetryAfter((value) => Math.max(0, value - 1));
    }, 1000);
    return () => clearInterval(tick);
  }, [retryAfter > 0]);

  useEffect(() => {
    if (step !== "mfa" || mfaExpiresAt === null) return;
    const check = () => {
      const remaining = Math.max(0, Math.ceil((mfaExpiresAt - Date.now()) / 1000));
      setMfaSecondsLeft(remaining);
      if (remaining === 0) {
        setStep("details"); setChallenge(""); setCode(""); setRecovery(false);
        setMfaExpiresAt(null); setPassword("");
        setError("مهلت تأیید دومرحله‌ای تمام شد. لطفاً مجدداً وارد شوید.");
      }
    };
    check();
    const id = window.setInterval(check, 1000);
    window.addEventListener("focus", check);
    document.addEventListener("visibilitychange", check);
    return () => { window.clearInterval(id); window.removeEventListener("focus", check); document.removeEventListener("visibilitychange", check); };
  }, [step, mfaExpiresAt]);

  function reset() {
    setRetryAfter(0);
    setStep("details");
    setIdentifier("");
    setPassword("");
    setCode("");
    setChallenge("");
    setMfaExpiresAt(null);
    setMfaSecondsLeft(0);
    setRecovery(false);
    setError("");
    setMessage("");
  }
  function changeTab(next: Tab) {
    setTab(next);
    setForgot(false);
    setMode("password");
    setEmailRegistration(false);
    reset();
  }

  function changeMode(next: Mode) {
    setMode(next);
    reset();
  }

  async function api(path: string, data: unknown) {
    const response = await fetch(`/api/identity/${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      cache: "no-store",
      body: JSON.stringify(data),
    });
    const result = await response.json().catch(() => ({}));
    return { response, result };
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (pending || navigating) return;
    setError("");
    setMessage("");
    setPending(true);
    let leavingForAccount = false;
    try {
      if (step === "mfa") {
        if (mfaExpiresAt === null || Date.now() >= mfaExpiresAt) {
          reset(); setError("مهلت تأیید دومرحله‌ای تمام شد. لطفاً مجدداً وارد شوید."); return;
        }
        const {response} = await api("mfa/complete",{challenge,code,useRecoveryCode:recovery});
        if (!response.ok) {
          if (mfaExpiresAt !== null && Date.now() >= mfaExpiresAt) {
            reset(); setError("مهلت تأیید دومرحله‌ای تمام شد. لطفاً مجدداً وارد شوید.");
          } else setError("کد تأیید دومرحله‌ای معتبر نیست یا منقضی شده است.");
          return;
        }
        leavingForAccount = true;setNavigating(true);setNavigating(true);router.replace("/dashboard");router.refresh();return;
      }
      if (phoneOtp) {
        if (step === "details") {
          const {response,result} = await api("phone/request",{mobile:identifier,purpose});
          if (response.status === 409 && result.error === "mobile_already_registered") {
            setError("این شماره همراه قبلاً ثبت‌نام شده است. از گزینه ورود استفاده کنید.");
            return;
          }
          if (response.status === 429) {
            const seconds = typeof result.retryAfterSeconds === "number"
              && Number.isFinite(result.retryAfterSeconds)
              ? Math.max(1, Math.min(900, Math.ceil(result.retryAfterSeconds))) : 60;
            setRetryAfter(seconds);
            setError("otp_cooldown");
            return;
          }
          if (response.status !== 202) {
            setError(response.status === 503
              ? "ارسال کد تأیید انجام نشد. اتصال سرویس پیامک را بررسی کنید."
              : "درخواست تأیید معتبر نیست؛ شماره همراه را بررسی کنید.");
            return;
          }
          setRetryAfter(0);
          setStep("code");
          setMessage("درخواست کد تأیید پذیرفته شد. کد دریافت‌شده را وارد کنید.");
          return;
        }
        const request = getOtpCompletionRequest(purpose, identifier, code, password);
        const { response } = await api(request.path, request.body);
        if (!response.ok) {
          setError(getOtpError(response.status));
          return;
        }
        setPassword("");
        setCode("");
        if (purpose === "login") {leavingForAccount = true;setNavigating(true);router.replace("/dashboard");router.refresh();return;}
        const wasReset = purpose === "reset";
        setTab("login");
        setForgot(false);
        setMode("password");
        setStep("details");
        setMessage(
          wasReset
            ? "رمز تغییر کرد. اکنون با رمز جدید وارد شوید."
            : "ثبت‌نام تکمیل شد؛ اکنون وارد شوید.",
        );
        return;
      }
      if (tab === "register") {
        const {response} = await api("register",{email:identifier,password});
        if (!response.ok) {setError(response.status === 409 ? "این ایمیل قبلاً ثبت شده است.":"ثبت‌نام انجام نشد. ایمیل و رمز را بررسی کنید.");return;}
        setTab("login");setPassword("");setMessage("ثبت‌نام انجام شد. اکنون وارد شوید.");return;
      }
      const {response} = await api("login",{email:identifier,password});
      if (response.ok) {setPassword("");leavingForAccount = true;setNavigating(true);router.replace("/dashboard");router.refresh();return;}
      if (response.status === 401) {
        const challengeResponse = await api("mfa/challenge",{email:identifier,password});
        if (challengeResponse.response.ok && typeof challengeResponse.result.challenge === "string") {
          const expiresAt = Date.parse(challengeResponse.result.expiresAtUtc);
          if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
            setError("مهلت درخواست ورود به پایان رسیده است. مجدداً تلاش کنید."); return;
          }
          setMfaExpiresAt(expiresAt);
          setChallenge(challengeResponse.result.challenge);setStep("mfa");setCode("");setPassword("");return;
        }
      }
      setError(getPasswordError(response.status));
    } catch {
      setError("ارتباط با سرور برقرار نشد؛ وضعیت API را بررسی کنید.");
    } finally {
      if (!leavingForAccount) setPending(false);
    }
  }

  return <div className="auth-workflow auth-simplified" aria-busy={pending || navigating}>
    {navigating && <div className="auth-navigation-lock" role="status" aria-live="assertive"><span className="auth-spinner" aria-hidden="true" /><strong>در حال ورود به داشبورد…</strong><small>لطفاً منتظر تکمیل انتقال بمانید.</small></div>}
    <fieldset className="auth-lock-fieldset" disabled={pending || navigating}>
    {forgot ? <div className="auth-section-heading">
      <button type="button" className="auth-back" onClick={()=>{setForgot(false);reset();}}>‹ بازگشت به ورود</button>
      <h2>بازیابی رمز عبور</h2>
      <p className="identity-muted">کد بازیابی به شماره همراه تأییدشده ارسال می‌شود.</p>
    </div> : step!=="mfa" && <div className="auth-section-heading">
      <h2>{tab==="login"?"ورود به حساب کاربری":"ایجاد حساب کاربری"}</h2>
      <p className="identity-muted">{tab==="login"?"با رمز عبور یا پیامک وارد شوید.":"شماره موبایل را تأیید کنید یا از ثبت‌نام آزمایشی ایمیلی استفاده کنید."}</p>
    </div>}
    {step==="code" && <div className="auth-code-info">
      <strong>{purposeCaptions[purpose]}</strong>
      <span dir="ltr">{identifier}</span>
      <p>کد تا سه دقیقه اعتبار دارد. دریافت دوباره کد با محدودیت زمانی همراه است.</p>
    </div>}
    {step==="mfa" && <div className="auth-section-heading">
      <h2>تأیید دومرحله‌ای</h2>
      <p className="identity-muted">کد برنامه Authenticator یا یکی از کدهای بازیابی را وارد کنید.</p>
      <p role="timer" className="identity-muted">زمان باقی‌مانده برای ورود: <strong dir="ltr">{Math.floor(mfaSecondsLeft / 60).toString().padStart(2, "0")}:{(mfaSecondsLeft % 60).toString().padStart(2, "0")}</strong></p>
    </div>}
    <form className="identity-form" onSubmit={submit}>
      {step==="details" && <>
        <label htmlFor="auth-identifier">{phoneOtp ? "شماره همراه" : tab==="register" ? "آدرس ایمیل" : "ایمیل یا شماره همراه"}</label>
        <input id="auth-identifier" dir="ltr" type={phoneOtp?"tel":tab==="register"?"email":"text"}
          autoComplete={phoneOtp?"tel":"username"} required value={identifier}
          placeholder={phoneOtp?"09123456789":tab==="register"?"example@email.com":"ایمیل یا شماره همراه"}
          onChange={e=>{setIdentifier(e.target.value);setRetryAfter(0);setError("");}} maxLength={320}/>
        {!phoneOtp && <>
          <label htmlFor="auth-password">رمز عبور</label>
          <input id="auth-password" dir="ltr" type="password" required
            autoComplete={tab==="register"?"new-password":"current-password"}
            minLength={tab==="register"?8:undefined} value={password}
            onChange={e=>setPassword(e.target.value)}/>
        </>}
      </>}
      {step==="code" && <>
        <label htmlFor="auth-code">کد شش‌رقمی تأیید</label>
        <input id="auth-code" dir="ltr" type="text" inputMode="numeric" autoComplete="one-time-code"
          pattern="[0-9]{6}" maxLength={6} required value={code} onChange={e=>setCode(e.target.value)}/>
        {(purpose==="reset"||purpose==="register") && <>
          <label htmlFor="auth-new-password">{purpose==="reset"?"رمز عبور جدید":"رمز عبور (اختیاری)"}</label>
          <input id="auth-new-password" dir="ltr" type="password" autoComplete="new-password"
            required={purpose==="reset"} minLength={8} value={password}
            onChange={e=>setPassword(e.target.value)}/>
        </>}
      </>}
      {step==="mfa" && <>
        <label htmlFor="auth-mfa-code">{recovery?"کد بازیابی":"کد برنامه Authenticator"}</label>
        <input id="auth-mfa-code" dir="ltr" required type="text" value={code}
          inputMode={recovery?"text":"numeric"} maxLength={recovery?64:6}
          autoComplete="one-time-code" onChange={e=>setCode(e.target.value)}/>
        <label className="inline-check"><input type="checkbox" checked={recovery}
          onChange={e=>setRecovery(e.target.checked)}/>استفاده از کد بازیابی</label>
      </>}
      {message&&<p role="status" className="auth-info">{message}</p>}
      {error && <p role="alert" className="form-error">
        {error === "otp_cooldown" ? (retryAfter > 0 ? <>
          کاربر گرامی، لطفاً پس از <strong dir="ltr" className="otp-countdown">
            {Math.floor(retryAfter/60).toString().padStart(2,"0")}:{(retryAfter%60).toString().padStart(2,"0")}
          </strong> مجدداً تلاش کنید.
        </> : "اکنون می‌توانید مجدداً درخواست کد تأیید بدهید.") : error}
      </p>}
      <button className="identity-primary" type="submit" disabled={pending || navigating || (phoneOtp && step==="details" && retryAfter > 0)} aria-busy={pending || navigating}>
        {navigating?"در حال ورود به پنل...":pending?"در حال بررسی...":phoneOtp && step==="details" && retryAfter > 0?"امکان درخواست مجدد پس از پایان شمارش":step==="mfa"?"تأیید و ورود":step==="code"?"تأیید کد":
          phoneOtp?"دریافت کد تأیید":tab==="register"?"ایجاد حساب":"ورود به حساب"}
      </button>
      {step==="code"&&<button className="identity-secondary" type="button"
        onClick={()=>{setStep("details");setCode("");setError("");setMessage("");}}>اصلاح شماره / درخواست جدید</button>}
      {!forgot && tab==="login" && step==="details" && <button type="button" className="auth-text-link"
        onClick={()=>{setForgot(true);setStep("details");setCode("");setError("");setMessage("");}}>
        رمز عبور را فراموش کرده‌اید؟
      </button>}
      {step==="mfa"&&<button type="button" className="identity-secondary"
        onClick={()=>{reset();}}>بازگشت به ورود</button>}
    </form>
    <p className="auth-footer-note">حساب‌های مدیریتی همچنان ملزم به احراز هویت دومرحله‌ای هستند.</p>
    </fieldset>
    {step === "details" && !pending && !navigating && <div className="auth-secondary-links">
      {tab === "login" && !forgot && <button type="button" onClick={() => changeMode(mode === "password" ? "otp" : "password")}>{mode === "password" ? "ورود با کد پیامکی" : "ورود با رمز عبور"}</button>}
      {!forgot && <button type="button" onClick={() => changeTab(tab === "login" ? "register" : "login")}>{tab === "login" ? "ایجاد حساب جدید" : "حساب دارید؟ ورود"}</button>}
      {tab === "register" && <button type="button" onClick={() => {setEmailRegistration(v => !v);reset();}}>{emailRegistration ? "ثبت‌نام با موبایل" : "ثبت‌نام با ایمیل (آزمایشی)"}</button>}
    </div>}
  </div>;
}
