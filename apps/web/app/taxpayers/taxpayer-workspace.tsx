"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import InvoiceParticipantForm from "../parties/invoice-participant-form";
import SectionLoading from "../components/section-loading";
import ConsoleLoader from "../components/console-loader";
import TaxpayerConnectionPanel from "./taxpayer-connection-panel";

type Taxpayer = { id: string; kind: number; normalizedIdentifier: string; displayName: string; status: number; revision: number };
type Memory = { id: string; taxpayerId: string; memoryIdentifier: string; status: number; revision: number;
  authorityEnvironment: 1 | 2 | null; lastConnectionTestSucceeded: boolean | null; lastConnectionTestAtUtc: string | null;
  canVerifyAndActivate?: boolean };
type Certificate = { id: string; fingerprintSha256: string; issuer: string | null; notBeforeUtc: string | null; notAfterUtc: string | null; status: number; origin: number };
const memoryLabels: Record<number, string> = { 1: "در انتظار بررسی", 2: "فعال", 3: "تعلیق", 4: "بازنشسته" };
const memoryEnvironmentLabels: Record<number, string> = { 1: "Production (اصلی)", 2: "Sandbox (آزمایشی)" };
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
  if (!response.ok) {
    if (method === "POST" && path.endsWith("/memories")) {
      const data = await response.json().catch(() => null);
      if (data?.error === "binding_review_required")
        throw new Error("برای این مؤدی حافظه‌ای ثبت شده است. وضعیت را بازخوانی کنید و برای تغییر حافظه از مسیر بررسی درخواست اقدام کنید.");
      if (data?.error === "memory_identifier_conflict")
        throw new Error("ثبت این کد با یک رکورد دیگر تداخل دارد. وضعیت را بازخوانی کنید و بررسی کنید که کد قبلاً ثبت نشده باشد.");
    }
    throw new Error(errorText(response.status));
  }
  return response.json() as Promise<T>;
}

export default function TaxpayerWorkspace({ isLevel1, initialTaxpayerId, showCreateForm = true, customerId, showTaxpayerList = true }:
  { isLevel1: boolean; initialTaxpayerId?: string; showCreateForm?: boolean; customerId?: string; showTaxpayerList?: boolean }) {
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [details, setDetails] = useState<Taxpayer | null>(null);
  const [memories, setMemories] = useState<Memory[]>([]);
  const [certificates, setCertificates] = useState<Record<string, Certificate[]>>({});
  const [detailsLoading, setDetailsLoading] = useState(Boolean(initialTaxpayerId));
  const [detailsError, setDetailsError] = useState("");
  const [importLoading, setImportLoading] = useState(false);
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
  const [editIdentifier, setEditIdentifier] = useState("");
  const [preparedKey, setPreparedKey] = useState<{ memoryId: string; publicKey: string } | null>(null);

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
    if (!showTaxpayerList) return;
    void reload();
    const onFocus = () => void reload();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [reload, showTaxpayerList]);

  const show = useCallback(async (id: string) => {
    setDetailsLoading(true); setDetailsError("");
    setError(""); setNotice(""); setSelectedId(id); setDetails(null); setMemories([]); setCertificates({});
    try {
      const [profile, list] = await Promise.all([
        request<Taxpayer>("/" + id), request<Memory[]>("/" + id + "/memories")
      ]);
      setDetails(profile); setEditName(profile.displayName);
      setEditIdentifier(profile.normalizedIdentifier); setMemories(list);
    } catch (ex) { setDetailsError((ex as Error).message); }
    finally { setDetailsLoading(false); }
  }, []);

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
  async function changeIdentifier(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!details || busy) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const updated = await request<Taxpayer>("/" + details.id + "/legal-identifier", "PATCH",
        { identifier: editIdentifier, expectedRevision: details.revision });
      setDetails(updated); setEditIdentifier(updated.normalizedIdentifier);
      setNotice("شناسه قانونی مؤدی تغییر کرد؛ این تغییر فقط برای مؤدی بدون صورتحساب صادرشده مجاز است.");
      await reload();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }
  async function addMemory(event: FormEvent<HTMLFormElement>, publicKey?: string) {
    event.preventDefault(); if (!details || busy) return;
    const memoryIdentifier = String(new FormData(event.currentTarget).get("memoryIdentifier") ?? "").trim();
    setBusy(true); setError(""); setNotice("");
    try {
      const memory = await request<Memory>("/" + details.id + "/memories", "POST",
        { memoryIdentifier, authorityEnvironment: 1 });
      setPreparedKey(publicKey ? { memoryId: memory.id, publicKey } : null);
      setNotice("کد حافظه ثبت شد. کلید خصوصی همین حافظه را وارد کنید و سپس اتصال را آزمایش کنید.");
      setMemories(current => current.some(item => item.id === memory.id) ?
        current.map(item => item.id === memory.id ? memory : item) : [...current, memory]);
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
  return <div className={"taxpayer-workspace" + (showTaxpayerList ? "" : " taxpayer-workspace-single")}>
    {showTaxpayerList && <aside className="taxpayer-panel">
      <div className="taxpayer-section-heading"><h2>{isLevel1 ? "فهرست همه مؤدیان" : "مؤدیان واگذارشده به من"}</h2>
        <button className="identity-secondary" type="button" disabled={loading} onClick={() => void reload()}>بازخوانی</button>
      </div>
      <label htmlFor="taxpayer-search">جست‌وجو در مؤدیان مجاز</label>
      <input id="taxpayer-search" type="search" value={search} onChange={e => setSearch(e.target.value)}
        placeholder="نام یا شناسه قانونی"/>
      {loading ? <ConsoleLoader label="در حال دریافت پرونده‌های مالیاتی…" /> :
        filtered.length === 0 ? <p className="identity-muted">مؤدی مجازی برای نمایش وجود ندارد.</p> :
        <ul className="taxpayer-list">{filtered.map(t => <li key={t.id}>
          <button type="button" aria-pressed={t.id === selectedId} onClick={() => void show(t.id)}>
            <strong>{t.displayName}</strong><small dir="ltr">{t.normalizedIdentifier}</small>
            <span>{t.kind === 1 ? "شخص حقیقی" : "شخص حقوقی"} · {taxpayerLabels[t.status] ?? "نامشخص"}</span>
          </button>
        </li>)}</ul>}
    </aside>}
    <section className="taxpayer-main" aria-label="مدیریت اطلاعات مؤدی">
      {error && <p role="alert" className="form-error">{error}</p>}
      {notice && <p role="status" className="auth-info">{notice}</p>}
      {!details && !initialTaxpayerId && !selectedId && <div className="taxpayer-panel"><h2>جزئیات مؤدی</h2>
        <p className="identity-muted">برای مشاهده اطلاعات، یک مؤدی را از فهرست انتخاب کنید.</p></div>}
      {(details || initialTaxpayerId || selectedId) && <>
        {details && showTaxpayerList && customerId && <InvoiceParticipantForm key={details.id} customerId={customerId} subjectId={details.id} role="seller" personKind={details.kind} />}
        <div className="taxpayer-sections-grid">
        {details && showTaxpayerList && <article className="taxpayer-panel">
          <div className="taxpayer-section-heading"><h2>{details.displayName}</h2>
            <button type="button" className="identity-secondary" onClick={() => void show(details.id)}>بازخوانی جزئیات</button></div>
          <dl className="taxpayer-facts"><div><dt>نوع</dt><dd>{details.kind === 1 ? "حقیقی" : "حقوقی"}</dd></div>
            <div><dt>شناسه قانونی</dt><dd dir="ltr">{details.normalizedIdentifier}</dd></div>
            <div><dt>وضعیت</dt><dd>{taxpayerLabels[details.status] ?? "نامشخص"}</dd></div>
            <div><dt>نسخه</dt><dd dir="ltr">{details.revision}</dd></div></dl>
          {isLevel1 && <form className="identity-form" onSubmit={rename}>
            <label htmlFor="taxpayer-display-name">تغییر نام نمایشی</label>
            <input id="taxpayer-display-name" required maxLength={256} value={editName}
              onChange={e => setEditName(e.target.value)}/>
            <button type="submit" className="identity-primary" disabled={busy || editName.trim() === details.displayName}>ذخیره نام</button>
          </form>}
          <p className="field-hint">شناسه قانونی با شماره اقتصادی بخش اطلاعات مالیاتی متفاوت است.</p>
          {isLevel1 && <form className="identity-form" onSubmit={changeIdentifier}>
            <label htmlFor="taxpayer-legal-identifier">ویرایش شناسه قانونی مؤدی</label>
            <input id="taxpayer-legal-identifier" dir="ltr" inputMode="numeric" required maxLength={11}
              value={editIdentifier} onChange={e => setEditIdentifier(e.target.value)}/>
            <small>این تغییر فقط برای مؤدی‌ای مجاز است که هنوز صورتحساب صادرشده ندارد.</small>
            <button type="submit" className="identity-primary"
              disabled={busy || editIdentifier.trim() === details.normalizedIdentifier}>ذخیره شناسه قانونی</button>
          </form>}
        </article>}
        <details name="fiscal-sections" className="fiscal-section">
          <summary>حافظه مالیاتی و اتصال{(importLoading||detailsLoading)&&<SectionLoading/>}</summary>
          <div className="fiscal-section-content taxpayer-token-panel">
            {detailsError?<p role="alert">{detailsError}</p>:<TaxpayerConnectionPanel key={initialTaxpayerId || selectedId}
              taxpayerId={initialTaxpayerId || selectedId} memories={memories}
              loadingMemories={detailsLoading} onLoadingChange={setImportLoading} isLevel1={isLevel1}
              preparedKey={preparedKey}
              memoryRegistration={!memories.some(m=>m.authorityEnvironment===1||m.authorityEnvironment==null) ?
                publicKey => <form className="identity-form" onSubmit={event => void addMemory(event, publicKey)}>
                  <label htmlFor="new-memory">کد حافظه مالیاتی Production</label>
                  <input id="new-memory" name="memoryIdentifier" dir="ltr" required maxLength={64}
                    pattern="[A-Za-z0-9]+" disabled={busy}/>
                  <button className="identity-primary" disabled={busy}>ثبت حافظهٔ Production</button>
                </form> : undefined}
              onMemoriesChanged={async()=>setMemories(await request<Memory[]>("/"+(initialTaxpayerId || selectedId)+"/memories"))}/>}
            {memories.length>0&&<details className="connection-history-details"><summary>سوابق ثبت و جزئیات حافظه‌ها</summary>
              <p className="field-hint">این سوابق مربوط به ثبت‌های مستقل حافظه‌اند؛ وضعیت اتصال جاری در کارت همان محیط نمایش داده می‌شود.</p>
              {memories.map(m=><article key={m.id} className="connection-history-row"><strong dir="ltr">{m.memoryIdentifier}</strong>
                <span>{m.authorityEnvironment===null?"محیط تأییدنشده":memoryEnvironmentLabels[m.authorityEnvironment]} · {memoryLabels[m.status]} · نسخه {m.revision}</span>
                {m.lastConnectionTestSucceeded!==null&&<p className="field-hint">آزمون قبلی این ثبت: {m.lastConnectionTestSucceeded?"موفق":"ناموفق"}
                  {m.lastConnectionTestAtUtc&&<> · {new Date(m.lastConnectionTestAtUtc).toLocaleString("fa-IR")}</>}</p>}
                {isLevel1&&<button type="button" className="identity-secondary" onClick={()=>void revealCertificates(m.id)}>مشاهده گواهی‌ها</button>}
                {certificates[m.id]?.map(c=><p key={c.id} className="field-hint">{certificateLabels[c.status]} · <bdi dir="ltr">{c.fingerprintSha256}</bdi></p>)}
              </article>)}
            </details>}
          </div>
        </details>
        </div>
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

