"use client";

import { FormEvent, useState } from "react";

export default function AdminNomination() {
  const [mobile, setMobile] = useState("");
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState("");

  async function nominate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    setPending(true);
    setMessage("");
    try {
      const response = await fetch("/api/identity/roles/level2/nominate", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mobile }),
      });
      setMessage(response.ok
        ? "کاربر معرفی شد. باید با رمز وارد حساب شود و تأیید دومرحله‌ای را تکمیل کند تا دسترسی ادمین سطح ۲ فعال شود."
        : response.status === 404 ? "کاربر فعال با این شماره یافت نشد."
        : response.status === 409 ? "این کاربر قبلاً نقش مدیریتی یا دعوت فعال دارد."
        : response.status === 403 ? "این عملیات فقط برای ادمین سطح ۱ مجاز است؛ کاربر مقصد نیز باید رمز عبور داشته باشد."
        : "معرفی کاربر انجام نشد. دوباره وارد حساب شوید و تلاش کنید.");
    } catch { setMessage("ارتباط با سرور برقرار نشد."); }
    finally { setPending(false); }
  }

  return <section className="intro">
    <h2>معرفی ادمین سطح ۲</h2>
    <p>کاربر باید حساب فعال و رمز عبور داشته باشد. دسترسی مدیریتی پس از تأیید Authenticator فعال می‌شود.</p>
    <form className="identity-form" onSubmit={nominate}>
      <label htmlFor="admin-mobile">شماره همراه کاربر</label>
      <input id="admin-mobile" dir="ltr" type="tel" required value={mobile}
        onChange={event => setMobile(event.target.value)}/>
      <button className="identity-primary" disabled={pending}>
        {pending ? "در حال ثبت..." : "معرفی برای مدیریت"}
      </button>
    </form>
    {message && <p role="status">{message}</p>}
  </section>;
}
