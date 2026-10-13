"use client";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

type SessionResult = { sessionIds: string[] };
export default function AccountControls() {
  const router = useRouter();
  const [sessions, setSessions] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [enrollment, setEnrollment] = useState<{ secret: string; provisioningUri: string } | null>(null);
  const [totp, setTotp] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
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
    const result = await fetch("/api/identity/mfa/enroll", { method:"POST" });
    if (!result.ok) {setMessage("شروع فعال‌سازی ممکن نیست؛ شاید MFA قبلاً فعال شده باشد."); return;}
    setEnrollment(await result.json()); setMessage("");
  }
  async function confirmMfa() {
    const response = await fetch("/api/identity/mfa/confirm", {
      method:"POST", headers:{"Content-Type":"application/json"},
      body:JSON.stringify({ code: totp })
    });
    if (!response.ok) {setMessage("کد معتبر نیست؛ دوباره امتحان کنید."); return;}
    const result = await response.json();
    setRecoveryCodes(result.codes);
    setEnrollment(null); setTotp("");
    setMessage("MFA فعال شد. نشست فعلی باطل شده است. کدهای بازیابی را امن نگه دارید.");
  }
  return <div className="account-grid">
    <section className="intro"><h2>نشست‌های شما</h2>
      {loading ? <p>در حال دریافت...</p> : sessions.length === 0 ? <p>نشست فعالی یافت نشد.</p> :
        <ul className="session-list">{sessions.map(session=><li key={session}>
          <bdi dir="ltr">{session}</bdi>
          <button type="button" onClick={()=>void revoke(session)}>لغو نشست</button></li>)}</ul>}
      <button type="button" className="identity-secondary" onClick={()=>void logout()}>خروج از حساب</button>
    </section>
    <section className="intro"><h2>تأیید دومرحله‌ای</h2>
      <p>برای محافظت بیشتر، برنامه Authenticator را فعال کنید.</p>
      {!enrollment && recoveryCodes.length === 0 &&
        <button type="button" className="identity-primary" onClick={()=>void startMfa()}>فعال‌سازی MFA</button>}
      {enrollment && <div className="identity-form">
        <p>کلید راه‌اندازی را فقط در دستگاه شخصی وارد کنید.</p>
        <code dir="ltr" className="sensitive-value">{enrollment.secret}</code>
        <label htmlFor="setup-totp">کد شش‌رقمی</label>
        <input dir="ltr" id="setup-totp" value={totp} onChange={e=>setTotp(e.target.value)}
          inputMode="numeric" maxLength={6}/>
        <button type="button" className="identity-primary" onClick={()=>void confirmMfa()}>تأیید و فعال‌سازی</button>
      </div>}
      {recoveryCodes.length > 0 && <div>
        <p role="status">کدهای بازیابی فقط همین بار نشان داده می‌شوند؛ آن‌ها را در محل امن نگهداری کنید.</p>
        <ul className="session-list">{recoveryCodes.map(c=><li key={c}><code dir="ltr">{c}</code></li>)}</ul>
        <button type="button" className="identity-secondary" onClick={()=>setRecoveryCodes([])}>کدها را پاک کن</button>
      </div>}
    </section>
    {message && <p role="status" className="form-error">{message}</p>}
  </div>;
}
