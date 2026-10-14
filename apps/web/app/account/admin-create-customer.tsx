"use client";

import { FormEvent, useState } from "react";

export default function AdminCreateCustomer({ onCreated }: { onCreated?: (mobile: string) => void } = {}) {
  const [mobile,setMobile] = useState("");
  const [email,setEmail] = useState("");
  const [password,setPassword] = useState("");
  const [busy,setBusy] = useState(false);
  const [message,setMessage] = useState("");
  const [failed,setFailed] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);setMessage("");setFailed(false);
    try {
      const response = await fetch("/api/identity/admin/customers", {
        method:"POST",headers:{"Content-Type":"application/json"},
        body:JSON.stringify({mobile,email:email.trim()||null,password})
      });
      if (response.status===201) {
        onCreated?.(mobile);
        setMobile("");setEmail("");setPassword("");
        setMessage("مشتری ایجاد شد. شماره ثبت‌شده به روش اداری تأیید شده و کد پیامکی ارسال نشد.");
      } else {
        setFailed(true);
        setMessage(response.status===409?"این شماره همراه یا ایمیل قبلاً ثبت شده است.":
          response.status===401||response.status===403?"مجوز لازم برای افزودن مشتری وجود ندارد.":
          response.status===400?"شماره همراه، ایمیل اختیاری یا رمز معتبر نیست.":
          "ثبت مشتری انجام نشد؛ وضعیت سرور را بررسی کنید.");
      }
    } catch {setFailed(true);setMessage("ارتباط با سرویس برقرار نشد.");}
    finally {setBusy(false);}
  }
  return <section className="intro">
    <h2>افزودن مشتری جدید</h2>
    <p className="identity-muted">ویژه مدیر سطح ۱ و ۲ با MFA فعال. شماره همراه الزامی، ایمیل اختیاری است. نیازی به ارسال OTP نیست.</p>
    <form onSubmit={submit} className="identity-form">
      <label htmlFor="admin-customer-mobile">شماره همراه مشتری</label>
      <input id="admin-customer-mobile" type="tel" dir="ltr" autoComplete="off" required
        placeholder="09123456789" value={mobile} onChange={e=>setMobile(e.target.value)}/>
      <label htmlFor="admin-customer-email">ایمیل (اختیاری)</label>
      <input id="admin-customer-email" type="email" dir="ltr" autoComplete="off"
        value={email} onChange={e=>setEmail(e.target.value)}/>
      <label htmlFor="admin-customer-password">رمز عبور اولیه</label>
      <input id="admin-customer-password" type="password" dir="ltr" autoComplete="new-password"
        required minLength={8} value={password} onChange={e=>setPassword(e.target.value)}/>
      <p className="field-hint">پیش از ثبت، مطمئن شوید شماره همراه متعلق به مشتری است؛ این عملیات تأیید پیامکی را با مجوز مدیر جایگزین می‌کند.</p>
      {message&&<p role={failed?"alert":"status"} className={failed?"form-error":"auth-info"}>{message}</p>}
      <button className="identity-primary" type="submit" disabled={busy} aria-busy={busy}>
        {busy?"در حال ثبت مشتری...":"ایجاد مشتری"}
      </button>
    </form>
  </section>;
}
