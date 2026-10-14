"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import InvoiceParticipantForm from "../parties/invoice-participant-form";

type Taxpayer = { id: string; kind: number; normalizedIdentifier: string; displayName: string; status: number; revision: number };
type Memory = { id: string; taxpayerId: string; memoryIdentifier: string; status: number; revision: number };
type Certificate = { id: string; fingerprintSha256: string; issuer: string | null; notBeforeUtc: string | null; notAfterUtc: string | null; status: number; origin: number };
const memoryLabels: Record<number, string> = { 1: "در انتظار بررسی", 2: "فعال", 3: "تعلیق", 4: "بازنشسته" };
const taxpayerLabels: Record<number, string> = { 1: "فعال", 2: "تعلیق", 3: "بایگانی" };
const certificateLabels: Record<number, string> = { 1: "فقط فراداده", 2: "معتبر", 3: "منقضی", 4: "لغوشده" };

function errorText(status: number) {
  if (status === 401) return "نشست ورود معتبر نیست؛ دوباره وارد شوید.";
  if (status === 403) return "برای این عملیات مجوز کافی ندارید.";
  if (status === 404) return "اطلاعات یافت نشد یا دسترسی شما به آن لغو شده است.";
  if (status === 409) return "اطلاعات تغییر کرده یا عملیات مجاز نیست؛ داده‌ها را تازه‌سازی کنید.";
  if (status === 503) return "سرویس در دسترس نیست؛ بعداً دوباره تلاش کنید.";
  return "عملیات انجام نشد؛ ورودی‌ها را بررسی کنید.";
}
async function request<T>(path: string, method: "GET" | "POST" | "PATCH" = "GET", body?: unknown): Promise<T> {
  const response = await fetch("/api/taxpayers" + path, {
    method, cache: "no-store", credentials: "same-origin",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined
  });
  if (!response.ok) throw new Error(errorText(response.status));
  return response.json() as Promise<T>;
}

export default function TaxpayerWorkspace({ isLevel1, initialTaxpayerId, showCreateForm = true, customerId }:
  { isLevel1: boolean; initialTaxpayerId?: string; showCreateForm?: boolean; customerId?: string }) {
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [details, setDetails] = useState<Taxpayer | null>(null);
  const [memories, setMemories] = useState<Memory[]>([]);
  const [certificates, setCertificates] = useState<Record<string, Certificate[]>>({});
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [search, setSearch] = useState("");
  const [newKind, setNewKind] = useState(2);
  const [newIdentifier, setNewIdentifier] = useState("");
  const [newName, setNewName] = useState("");
  const [newCustomerId, setNewCustomerId] = useState("");
  const [editName, setEditName] = useState("");
  const [newMemory, setNewMemory] = useState("");

  const reload = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const list = await request<Taxpayer[]>("/list");
      setTaxpayers(list);
      setDetails(current => current && list.some(item => item.id === current.id) ? current : null);
      setSelectedId(current => list.some(item => item.id === current) ? current : "");
    }
    catch (ex) { setError((ex as Error).message); setTaxpayers([]); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => {
    void reload();
    const onFocus = () => void reload();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [reload]);

  const show = useCallback(async (id: string) => {
    setError(""); setNotice(""); setSelectedId(id); setDetails(null); setMemories([]); setCertificates({});
    try {
      const profile = await request<Taxpayer>("/" + id);
      const list = isLevel1 ? await request<Memory[]>("/" + id + "/memories") : [];
      setDetails(profile); setEditName(profile.displayName); setMemories(list);
    } catch (ex) { setError((ex as Error).message); }
  }, [isLevel1]);

  useEffect(() => { if (initialTaxpayerId) void show(initialTaxpayerId); }, [initialTaxpayerId, show]);

  async function createTaxpayer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(""); setNotice("");
    try {
      await request("/new", "POST", { kind: newKind, identifier: newIdentifier, displayName: newName, customerUserId: newCustomerId });
      setNotice("مؤدی ثبت شد. شناسه قانونی هنوز به معنی تأیید رسمی سازمان مالیاتی نیست.");
      setNewIdentifier(""); setNewName(""); setNewCustomerId(""); await reload();
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }
  async function rename(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!details || busy) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const updated = await request<Taxpayer>("/" + details.id + "/display-name", "PATCH",
        { displayName: editName, expectedRevision: details.revision });
      setDetails(updated); setNotice("نام نمایشی تغییر کرد و سابقه تغییر ثبت شد.");
      await reload();
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }
  async function addMemory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!details || busy) return;
    setBusy(true); setError(""); setNotice("");
    try {
      await request("/" + details.id + "/memories", "POST", { memoryIdentifier: newMemory });
      setNotice("حافظه مالیاتی به‌عنوان اطلاعات تأییدنشده ثبت شد. این کار به معنی اتصال موفق نیست.");
      setNewMemory("");
      setMemories(await request<Memory[]>("/" + details.id + "/memories"));
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }
  async function revealCertificates(memoryId: string) {
    if (!details) return;
    setBusy(true); setError("");
    try {
      const records = await request<Certificate[]>("/" + details.id + "/memories/" + memoryId + "/certificates");
      setCertificates(current => ({ ...current, [memoryId]: records }));
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }
  const filtered = taxpayers.filter(t => (t.displayName + " " + t.normalizedIdentifier)
    .toLocaleLowerCase("fa").includes(search.toLocaleLowerCase("fa")));
  return <div className="taxpayer-workspace">
    <aside className="taxpayer-panel">
      <div className="taxpayer-section-heading"><h2>{isLevel1 ? "فهرست همه مؤدیان" : "مؤدیان واگذارشده به من"}</h2>
        <button className="identity-secondary" type="button" disabled={loading} onClick={() => void reload()}>بازخوانی</button>
      </div>
      <label htmlFor="taxpayer-search">جست‌وجو در مؤدیان مجاز</label>
      <input id="taxpayer-search" type="search" value={search} onChange={e => setSearch(e.target.value)}
        placeholder="نام یا شناسه قانونی"/>
      {loading ? <p role="status">در حال بارگذاری…</p> :
        filtered.length === 0 ? <p className="identity-muted">مؤدی مجازی برای نمایش وجود ندارد.</p> :
        <ul className="taxpayer-list">{filtered.map(t => <li key={t.id}>
          <button type="button" aria-pressed={t.id === selectedId} onClick={() => void show(t.id)}>
            <strong>{t.displayName}</strong><small dir="ltr">{t.normalizedIdentifier}</small>
            <span>{t.kind === 1 ? "شخص حقیقی" : "شخص حقوقی"} · {taxpayerLabels[t.status] ?? "نامشخص"}</span>
          </button>
        </li>)}</ul>}
    </aside>
    <section className="taxpayer-main" aria-label="مدیریت اطلاعات مؤدی">
      {error && <p role="alert" className="form-error">{error}</p>}
      {notice && <p role="status" className="auth-info">{notice}</p>}
      {!details && <div className="taxpayer-panel"><h2>جزئیات مؤدی</h2>
        <p className="identity-muted">برای مشاهده اطلاعات، یک مؤدی را از فهرست انتخاب کنید.</p></div>}
      {details && <>
        {isLevel1 && customerId && <InvoiceParticipantForm key={details.id} customerId={customerId} subjectId={details.id} role="seller" />}
        <article className="taxpayer-panel">
          <div className="taxpayer-section-heading"><h2>{details.displayName}</h2>
            <button type="button" className="identity-secondary" onClick={() => void show(details.id)}>بازخوانی جزئیات</button></div>
          <dl className="taxpayer-facts"><div><dt>نوع</dt><dd>{details.kind === 1 ? "حقیقی" : "حقوقی"}</dd></div>
            <div><dt>شناسه قانونی</dt><dd dir="ltr">{details.normalizedIdentifier}</dd></div>
            <div><dt>وضعیت</dt><dd>{taxpayerLabels[details.status] ?? "نامشخص"}</dd></div>
            <div><dt>نسخه</dt><dd dir="ltr">{details.revision}</dd></div></dl>
          <form className="identity-form" onSubmit={rename}>
            <label htmlFor="taxpayer-display-name">تغییر نام نمایشی</label>
            <input id="taxpayer-display-name" required maxLength={256} value={editName}
              onChange={e => setEditName(e.target.value)}/>
            <button type="submit" className="identity-primary" disabled={busy || editName.trim() === details.displayName}>ذخیره نام</button>
          </form>
          <p className="field-hint">ویرایش نام به معنای تغییر شناسه رسمی، مالکیت مؤدی یا حافظه مالیاتی نیست.</p>
        </article>
        {isLevel1 && <article className="taxpayer-panel">
          <h2>حافظه‌های مالیاتی</h2>
          {memories.length === 0 ? <p className="identity-muted">هنوز حافظه‌ای ثبت نشده است.</p> :
            <ul className="taxpayer-memory-list">{memories.map(m => <li key={m.id}>
              <div><strong dir="ltr">{m.memoryIdentifier}</strong>
                <span>{memoryLabels[m.status] ?? "نامشخص"} · نسخه {m.revision}</span></div>
              {isLevel1 && <button type="button" className="identity-secondary" disabled={busy}
                onClick={() => void revealCertificates(m.id)}>مشاهده گواهی‌ها</button>}
              {certificates[m.id] && <div className="taxpayer-certificates">
                {certificates[m.id].length === 0 ? <p>گواهی ثبت نشده است.</p> :
                  certificates[m.id].map(c => <dl key={c.id}>
                    <dt>اثر انگشت SHA-256</dt><dd dir="ltr">{c.fingerprintSha256}</dd>
                    <dt>صادرکننده</dt><dd>{c.issuer ?? "نامشخص"}</dd>
                    <dt>وضعیت</dt><dd>{certificateLabels[c.status] ?? "نامشخص"}</dd>
                    <dt>منشأ</dt><dd>{c.origin === 2 ? "کلید موجود" : "کلید تولیدشده"}</dd>
                    <dt>پایان اعتبار</dt><dd>{c.notAfterUtc ? new Date(c.notAfterUtc).toLocaleDateString("fa-IR") : "ثبت نشده"}</dd>
                  </dl>)}
              </div>}
            </li>)}</ul>}
          {isLevel1 && memories.length === 0 && <form className="identity-form" onSubmit={addMemory}>
            <label htmlFor="new-memory">کد حافظه مالیاتی موجود</label>
            <input id="new-memory" dir="ltr" required maxLength={64} pattern="[A-Za-z0-9]+"
              value={newMemory} onChange={e => setNewMemory(e.target.value)}/>
            <button className="identity-primary" disabled={busy}>ثبت اولیه حافظه</button>
            <p className="field-hint">تأیید اتصال واقعی، تعویض حافظه و ورود کلید خصوصی در مراحل آتی فعال می‌شود.</p>
          </form>}
          <p className="taxpayer-warning">تست واقعی ارتباط با سازمان مالیاتی و تأیید توکن هنوز فعال نیست؛ هیچ وضعیت «اتصال موفق» به‌صورت نمایشی ساخته نمی‌شود.</p>
        </article>}
      </>}
      {isLevel1 && showCreateForm && <article className="taxpayer-panel">
        <h2>ثبت مؤدی جدید</h2>
        <form className="identity-form" onSubmit={createTaxpayer}>
          <label htmlFor="new-taxpayer-customer">شناسه حساب مشتری (UUID)</label>
          <input id="new-taxpayer-customer" dir="ltr" required value={newCustomerId}
            onChange={e => setNewCustomerId(e.target.value)}
            pattern="[0-9a-fA-F-]{36}" placeholder="شناسه کاربری مشتری صاحب مؤدی"/>
          <label htmlFor="new-taxpayer-kind">نوع مؤدی</label>
          <select id="new-taxpayer-kind" value={newKind} onChange={e => setNewKind(Number(e.target.value))}>
            <option value={1}>شخص حقیقی</option><option value={2}>شخص حقوقی</option>
          </select>
          <label htmlFor="new-taxpayer-id">کد ملی / شناسه ملی</label>
          <input id="new-taxpayer-id" dir="ltr" required maxLength={20} value={newIdentifier}
            onChange={e => setNewIdentifier(e.target.value)} />
          <label htmlFor="new-taxpayer-name">عنوان مؤدی</label>
          <input id="new-taxpayer-name" required maxLength={256} value={newName}
            onChange={e => setNewName(e.target.value)}/>
          <button className="identity-primary" type="submit" disabled={busy}>ثبت مؤدی</button>
        </form>
      </article>}
    </section>
  </div>;
}
