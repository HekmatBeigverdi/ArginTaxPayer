"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

type Purpose = "register" | "login" | "reset";
const labels: Record<Purpose, string> = {
  register: "ثبت‌نام با شماره همراه",
  login: "ورود بدون رمز با پیامک",
  reset: "بازیابی رمز با پیامک"
};

export default function PhoneForms() {
  const router = useRouter();
  const [purpose, setPurpose] = useState<Purpose>("register");
  const [mobile, setMobile] = useState("");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [started, setStarted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("");
  const [failure, setFailure] = useState(false);

  function switchPurpose(next: Purpose) {
    setPurpose(next);
    setCode(""); setPassword(""); setStarted(false); setStatus(""); setFailure(false);
  }
  async function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setFailure(false); setStatus("");
    try {
      const result = await fetch("/api/identity/phone/request", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mobile, purpose })
      });
      if (result.status === 202) {
        setStarted(true);
        setStatus("اگر این روش برای حساب شما فعال باشد، کد تأیید ارسال می‌شود.");
      } else {
        setFailure(true); setStatus("درخواست کد ممکن نیست؛ بعداً دوباره تلاش کنید.");
      }
    } catch {
      setFailure(true); setStatus("ارتباط با سرویس تأیید برقرار نشد.");
    } finally { setBusy(false); }
  }
  async function complete(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setStatus(""); setFailure(false);
    const endpoint = purpose === "register" ? "register/complete" :
      purpose === "login" ? "login/complete" : "reset/complete";
    const payload = purpose === "login" ? { mobile, code } :
      purpose === "register" ? { mobile, code, password: password || null } :
      { mobile, code, newPassword: password };
    try {
      const result = await fetch("/api/identity/phone/" + endpoint, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (result.ok) {
        setCode(""); setPassword("");
        if (purpose === "login") { router.replace("/account"); router.refresh(); return; }
        setStarted(false);
        setStatus(purpose === "register" ? "حساب ایجاد شد. اکنون وارد شوید." :
          "رمز تغییر کرد؛ با رمز جدید وارد شوید.");
        return;
      }
      setFailure(true); setStatus(result.status === 429
        ? "تعداد تلاش‌ها بیش از حد مجاز است." : "کد یا اطلاعات درخواست معتبر نیست.");
    } catch {
      setFailure(true); setStatus("ارتباط با سرور برقرار نشد.");
    } finally { setBusy(false); }
  }

  return <section className="phone-auth" aria-labelledby="phone-title">
    <h2 id="phone-title">احراز هویت با شماره همراه</h2>
    <div className="identity-tabs" role="group" aria-label="روش تأیید همراه">
      {(["register","login","reset"] as const).map(option => <button key={option}
        type="button" aria-pressed={purpose === option}
        onClick={() => switchPurpose(option)}>{labels[option]}</button>)}
    </div>
    <form className="identity-form" onSubmit={started ? complete : send}>
      <label htmlFor="phone-number">شماره همراه</label>
      <input id="phone-number" dir="ltr" type="tel" autoComplete="tel"
        placeholder="09123456789" value={mobile} required
        onChange={event => setMobile(event.target.value)} disabled={started}/>
      {started && <>
        <label htmlFor="phone-code">کد شش‌رقمی</label>
        <input id="phone-code" dir="ltr" type="text" inputMode="numeric"
          autoComplete="one-time-code" value={code} required
          minLength={6} maxLength={6} pattern="[0-9]{6}"
          onChange={event => setCode(event.target.value)}/>
        {purpose !== "login" && <>
          <label htmlFor="phone-password">{purpose === "reset" ? "رمز جدید" :
            "رمز عبور (اختیاری)"}</label>
          <input id="phone-password" type="password" dir="ltr"
            autoComplete="new-password" minLength={8}
            required={purpose === "reset"} value={password}
            onChange={event => setPassword(event.target.value)}/>
        </>}
      </>}
      {status && <p role={failure ? "alert" : "status"} className={failure ? "form-error" : "field-hint"}>{status}</p>}
      <button type="submit" disabled={busy} className="identity-primary">
        {busy ? "در حال بررسی..." : started ? "تأیید کد" : "دریافت کد تأیید"}
      </button>
      {started && <button type="button" className="identity-secondary"
        onClick={() => {setStarted(false);setCode("");setStatus("");}}>بازگشت</button>}
    </form>
    <p className="field-hint">ورود با ایمیل و رمز عبور نیز همچنان فعال است. ارسال پیامک فقط پس از فعال‌سازی امن سرویس قاصدک انجام می‌شود.</p>
  </section>;
}
