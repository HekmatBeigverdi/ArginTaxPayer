"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

type Tab = "login" | "register";
type Mode = "password" | "otp";
type Step = "details" | "code" | "mfa";
type Purpose = "register" | "login" | "reset";
const captions: Record<Purpose,string> = {
  register: "تأیید شماره همراه", login: "ورود با کد یک‌بارمصرف", reset: "بازیابی رمز عبور"
};

export default function IdentityFlow() {
  const router = useRouter();
  const [tab,setTab] = useState<Tab>("login");
  const [mode,setMode] = useState<Mode>("password");
  const [step,setStep] = useState<Step>("details");
  const [forgot,setForgot] = useState(false);
  const [emailRegistration,setEmailRegistration] = useState(false);
  const [identifier,setIdentifier] = useState("");
  const [password,setPassword] = useState("");
  const [code,setCode] = useState("");
  const [challenge,setChallenge] = useState("");
  const [recovery,setRecovery] = useState(false);
  const [pending,setPending] = useState(false);
  const [error,setError] = useState("");
  const [message,setMessage] = useState("");
  const purpose: Purpose = forgot ? "reset" : tab === "register" ? "register" : "login";
  const phoneOtp = forgot || (tab === "register" && !emailRegistration) || (tab === "login" && mode === "otp");

  function reset() {
    setStep("details"); setIdentifier(""); setPassword(""); setCode(""); setChallenge("");
    setRecovery(false); setError(""); setMessage("");
  }
  function changeTab(next: Tab) { setTab(next); setForgot(false); setMode("password"); setEmailRegistration(false); reset(); }
  function changeMode(next: Mode) { setMode(next);reset(); }
  async function api(path:string, data:unknown) {
    const response = await fetch("/api/identity/"+path, {
      method:"POST",headers:{"Content-Type":"application/json"},cache:"no-store",body:JSON.stringify(data)
    });
    const result = await response.json().catch(()=>({}));
    return {response,result};
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (pending) return;
    setError("");setMessage("");setPending(true);
    try {
      if (step === "mfa") {
        const {response} = await api("mfa/complete",{challenge,code,useRecoveryCode:recovery});
        if (!response.ok) { setError("کد تأیید دومرحله‌ای معتبر نیست یا منقضی شده است.");return; }
        router.replace("/account");router.refresh();return;
      }
      if (phoneOtp) {
        if (step === "details") {
          const {response} = await api("phone/request",{mobile:identifier,purpose});
          if (response.status !== 202) {setError("درخواست تأیید امکان‌پذیر نیست؛ سرویس و تنظیمات پیامک را بررسی کنید.");return;}
          setStep("code");
          setMessage("در صورت مجاز بودن درخواست، کد ارسال می‌شود. اگر پیامکی دریافت نکردید، وضعیت اتصال را بررسی کنید.");
          return;
        }
        const path = purpose === "register" ? "phone/register/complete"
          : purpose === "reset" ? "phone/reset/complete" : "phone/login/complete";
        const body = purpose === "register" ? {mobile:identifier,code,password:password||null}
          : purpose === "reset" ? {mobile:identifier,code,newPassword:password}
          : {mobile:identifier,code};
        const {response} = await api(path,body);
        if (!response.ok) {setError(response.status === 429 ? "تعداد تلاش‌ها زیاد است." : "کد تأیید اشتباه یا منقضی شده است.");return;}
        setPassword("");setCode("");
        if (purpose === "login") {router.replace("/account");router.refresh();return;}
        const wasReset = purpose === "reset";
        setTab("login");setForgot(false);setMode("password");setStep("details");
        setMessage(wasReset?"رمز تغییر کرد. اکنون با رمز جدید وارد شوید.":"ثبت‌نام تکمیل شد؛ اکنون وارد شوید.");
        return;
      }
      if (tab === "register") {
        const {response} = await api("register",{email:identifier,password});
        if (!response.ok) {setError(response.status === 409 ? "این ایمیل قبلاً ثبت شده است.":"ثبت‌نام انجام نشد. ایمیل و رمز را بررسی کنید.");return;}
        setTab("login");setPassword("");setMessage("ثبت‌نام انجام شد. اکنون وارد شوید.");return;
      }
      const {response} = await api("login",{email:identifier,password});
      if (response.ok) {setPassword("");router.replace("/account");router.refresh();return;}
      if (response.status === 401) {
        const challengeResponse = await api("mfa/challenge",{email:identifier,password});
        if (challengeResponse.response.ok && typeof challengeResponse.result.challenge === "string") {
          setChallenge(challengeResponse.result.challenge);setStep("mfa");setCode("");setPassword("");return;
        }
      }
      setError(response.status === 429 ? "تعداد تلاش‌ها بیش از حد مجاز است؛ بعداً دوباره امتحان کنید." :
        "نام کاربری یا رمز عبور نادرست است.");
    } catch {setError("ارتباط با سرور برقرار نشد؛ وضعیت API را بررسی کنید.");}
    finally {setPending(false);}
  }

  return <div className="auth-workflow">
    <div className="identity-tabs identity-main-tabs" role="tablist" aria-label="ورود و ثبت‌نام">
      <button type="button" role="tab" aria-selected={tab==="login"&&!forgot}
        onClick={()=>changeTab("login")}>ورود</button>
      <button type="button" role="tab" aria-selected={tab==="register"&&!forgot}
        onClick={()=>changeTab("register")}>ثبت‌نام</button>
    </div>
    {forgot ? <div className="auth-section-heading">
      <button type="button" className="auth-back" onClick={()=>{setForgot(false);reset();}}>‹ بازگشت به ورود</button>
      <h2>بازیابی رمز عبور</h2>
      <p className="identity-muted">کد بازیابی به شماره همراه تأییدشده ارسال می‌شود.</p>
    </div> : step!=="mfa" && <div className="auth-section-heading">
      <h2>{tab==="login"?"ورود به حساب کاربری":"ایجاد حساب کاربری"}</h2>
      <p className="identity-muted">{tab==="login"?"با رمز عبور یا پیامک وارد شوید.":"شماره موبایل را تأیید کنید یا از ثبت‌نام آزمایشی ایمیلی استفاده کنید."}</p>
    </div>}
    {!forgot && step==="details" && tab==="login" && <div className="auth-choice" role="group" aria-label="روش ورود">
      <button type="button" aria-pressed={mode==="password"} onClick={()=>changeMode("password")}>رمز عبور</button>
      <button type="button" aria-pressed={mode==="otp"} onClick={()=>changeMode("otp")}>کد پیامکی</button>
    </div>}
    {!forgot && step==="details" && tab==="register" && <div className="auth-choice" role="group" aria-label="روش ثبت‌نام">
      <button type="button" aria-pressed={!emailRegistration} onClick={()=>{setEmailRegistration(false);reset();}}>شماره همراه</button>
      <button type="button" aria-pressed={emailRegistration} onClick={()=>{setEmailRegistration(true);reset();}}>ایمیل (آزمایشی)</button>
    </div>}
    {step==="code" && <div className="auth-code-info">
      <strong>{captions[purpose]}</strong>
      <span dir="ltr">{identifier}</span>
      <p>کد تا سه دقیقه اعتبار دارد. دریافت دوباره کد با محدودیت زمانی همراه است.</p>
    </div>}
    {step==="mfa" && <div className="auth-section-heading">
      <h2>تأیید دومرحله‌ای</h2>
      <p className="identity-muted">کد برنامه Authenticator یا یکی از کدهای بازیابی را وارد کنید.</p>
    </div>}
    <form className="identity-form" onSubmit={submit}>
      {step==="details" && <>
        <label htmlFor="auth-identifier">{phoneOtp ? "شماره همراه" : tab==="register" ? "آدرس ایمیل" : "ایمیل یا شماره همراه"}</label>
        <input id="auth-identifier" dir="ltr" type={phoneOtp?"tel":tab==="register"?"email":"text"}
          autoComplete={phoneOtp?"tel":"username"} required value={identifier}
          placeholder={phoneOtp?"09123456789":tab==="register"?"example@email.com":"ایمیل یا شماره همراه"}
          onChange={e=>setIdentifier(e.target.value)} maxLength={320}/>
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
      {error&&<p role="alert" className="form-error">{error}</p>}
      <button className="identity-primary" type="submit" disabled={pending}>
        {pending?"در حال بررسی...":step==="mfa"?"تأیید و ورود":step==="code"?"تأیید کد":
          phoneOtp?"دریافت کد تأیید":tab==="register"?"ایجاد حساب":"ورود به حساب"}
      </button>
      {step==="code"&&<button className="identity-secondary" type="button"
        onClick={()=>{setStep("details");setCode("");setError("");setMessage("");}}>اصلاح شماره / درخواست جدید</button>}
      {!forgot && tab==="login" && step==="details" && <button type="button" className="auth-text-link"
        onClick={()=>{setForgot(true);setStep("details");setCode("");setError("");setMessage("");}}>
        رمز عبور را فراموش کرده‌اید؟
      </button>}
      {step==="mfa"&&<button type="button" className="identity-secondary"
        onClick={()=>{setStep("details");setCode("");setChallenge("");setError("");}}>بازگشت به ورود</button>}
    </form>
    <p className="auth-footer-note">حساب‌های مدیریتی همچنان ملزم به احراز هویت دومرحله‌ای هستند.</p>
  </div>;
}
