"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

type View = "login" | "register" | "challenge";
const errorText: Record<string, string> = {
  unauthorized: "ایمیل یا رمز عبور نامعتبر است.",
  too_many_attempts: "تعداد تلاش‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.",
  request_rejected: "درخواست پذیرفته نشد. اطلاعات را بررسی کنید.",
  identity_service_unavailable: "ارتباط با سرویس هویت برقرار نشد.",
  origin_not_allowed: "اعتبارسنجی امنیتی درخواست ناموفق بود."
};

export default function IdentityForms() {
  const router = useRouter();
  const [view, setView] = useState<View>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [challenge, setChallenge] = useState("");
  const [code, setCode] = useState("");
  const [recovery, setRecovery] = useState(false);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFeedback("");
    setBusy(true);
    try {
      const endpoint = view === "challenge" ? ("/api/identity/mfa/complete") :
        view === "register" ? "/api/identity/register" : "/api/identity/login";
      const data = view === "challenge"
        ? { challenge, code, useRecoveryCode: recovery } : { email, password };
      const response = await fetch(endpoint, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data), cache: "no-store"
      });
      const body = await response.json().catch(() => ({}));
      if (response.ok) {
        if (view === "register") {
          setView("login");
          setFeedback("ثبت‌نام انجام شد. اکنون وارد شوید.");
        } else {
          setPassword(""); setCode("");
          router.replace("/account"); router.refresh();
        }
        return;
      }
      if (view === "login" && response.status === 401) {
        const start = await fetch("/api/identity/mfa/challenge", {
          method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, password }), cache: "no-store"
        });
        if (start.ok) {
          const result = await start.json();
          setChallenge(result.challenge);
          setPassword("");
          setView("challenge");
          return;
        }
        if (start.status === 429) {
          setFeedback(errorText.too_many_attempts); return;
        }
      }
      // The proxy deliberately uses a generic error body; preserve the useful
      // HTTP status instead of letting that body hide validation or conflicts.
      const statusFeedback: Record<number, string> = {
        400: view === "register"
          ? "ایمیل معتبر و رمز عبور بین ۸ تا ۱۰۲۴ کاراکتر وارد کنید؛ رمز نباید فقط فاصله باشد."
          : "اطلاعات واردشده معتبر نیست. دوباره بررسی کنید.",
        401: view === "challenge" ? "کد تأیید نامعتبر یا منقضی شده است." : errorText.unauthorized,
        404: "این امکان در سرویس هویت فعال نیست.",
        409: "این ایمیل قبلاً ثبت شده است. از بخش ورود استفاده کنید.",
        429: errorText.too_many_attempts,
        502: "سرویس هویت با خطا مواجه شد. کمی بعد دوباره تلاش کنید.",
        503: errorText.identity_service_unavailable
      };
      setFeedback(statusFeedback[response.status] || errorText[body.error] ||
        "درخواست انجام نشد؛ لطفاً دوباره بررسی کنید.");
    } catch {
      setFeedback(errorText.identity_service_unavailable);
    } finally { setBusy(false); }
  }
  return <div>
    {view !== "challenge" && <div className="identity-tabs" role="group" aria-label="نوع عملیات">
      <button type="button" aria-pressed={view === "login"} onClick={() => {setView("login");setFeedback("");}}>ورود</button>
      <button type="button" aria-pressed={view === "register"} onClick={() => {setView("register");setFeedback("");}}>ثبت‌نام</button>
    </div>}
    <form onSubmit={submit} className="identity-form">
      {view !== "challenge" ? <>
        <label htmlFor="login-email">{view === "login" ? "ایمیل یا شماره همراه" : "ایمیل"}</label>
        <input id="login-email" dir="ltr" type={view === "register" ? "email" : "text"} autoComplete="username"
          value={email} onChange={e=>setEmail(e.target.value)} required maxLength={320}/>
        <label htmlFor="login-password">رمز عبور</label>
        <input id="login-password" dir="ltr" type="password"
          autoComplete={view === "register" ? "new-password" : "current-password"}
          value={password} onChange={e=>setPassword(e.target.value)}
          required minLength={view === "register" ? 8 : undefined}/>
        {view === "register" && <p className="field-hint">حداقل ۸ کاراکتر؛ ترجیحاً از رمز منحصربه‌فرد استفاده کنید.</p>}
      </> : <>
        <h2>تأیید دومرحله‌ای</h2>
        <p className="identity-muted">کد برنامه Authenticator یا یکی از کدهای بازیابی را وارد کنید.</p>
        <label htmlFor="identity-code">{recovery ? "کد بازیابی" : "کد شش‌رقمی"}</label>
        <input id="identity-code" dir="ltr" type="text" inputMode={recovery ? "text" : "numeric"}
          autoComplete="one-time-code" value={code} required
          maxLength={recovery ? 64 : 6} onChange={e=>setCode(e.target.value)}/>
        <label className="inline-check"><input type="checkbox" checked={recovery}
          onChange={e=>setRecovery(e.target.checked)}/> استفاده از کد بازیابی</label>
      </>}
      {feedback && <p role="alert" className="form-error">{feedback}</p>}
      <button className="identity-primary" disabled={busy} type="submit">
        {busy ? "در حال بررسی..." : view === "register" ? "ایجاد حساب" : view === "challenge" ? "تأیید و ورود" : "ورود"}
      </button>
      {view === "challenge" && <button className="identity-secondary" type="button"
        onClick={()=>{setView("login");setChallenge("");setCode("");setFeedback("");}}>بازگشت به ورود</button>}
    </form>
  </div>;
}
