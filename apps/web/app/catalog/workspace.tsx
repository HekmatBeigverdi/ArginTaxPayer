"use client";

import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import ConsoleLoader from "../components/console-loader";
import { PersianDatePicker } from "./persian-date-picker";
import CatalogUnitCombobox, { type CatalogUnitOption } from "./unit-combobox";

type Kind = "products" | "services" | "official";
type CatalogRow = {
  id: string; customerUserId?: string; internalCode?: string; code?: string; officialCode?: string | null;
  title: string; kind?: number; baseUnitId?: string;
  status?: number; revision?: number; releaseId?: string;
};
type PageResult = { items: CatalogRow[]; hasMore: boolean };
type OwnerAccount = {customerUserId: string; personName?: string|null; companyName?: string|null; isActive?: boolean; partnerLevel2UserId?: string|null; rootLevel1UserId?: string|null};
type ErrorBody = { error?: string };
const accountName = (account: OwnerAccount): string =>
  (account.companyName?.trim() || account.personName?.trim() || "");
const errors: Record<string, string> = {
  product_conflict: "کد کالا تکراری است یا نسخه اطلاعات تغییر کرده است.",
  service_conflict: "کد خدمت تکراری است یا نسخه اطلاعات تغییر کرده است.",
  Conflict: "تداخل داده یا تغییر هم‌زمان رخ داده است.",
  UnitUnavailable: "واحد انتخاب‌شده معتبر یا قابل استفاده نیست.",
  WrongKind: "نوع کالا یا خدمت با شناسه رسمی سازگار نیست.",
  UnverifiedOfficialCode: "شناسه رسمی هنوز تأیید معتبر ندارد.",
  Overlap: "تاریخ اعتبار نگاشت با نگاشت قبلی هم‌پوشانی دارد.",
  Invalid: "اطلاعات واردشده نامعتبر است.",
  api_unavailable: "ارتباط با API برقرار نشد.",
};
function guid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
async function catalogCall(path: string, method = "GET", body?: object) {
  const response = await fetch("/api/catalog/" + path, {
    method,
    credentials: "same-origin", cache: "no-store",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (response.ok) return response.json();
  const result = await response.json().catch(() => ({})) as ErrorBody & {
    title?: string; detail?: string; errors?: Record<string, string[]>;
  };
  if (response.status === 401) throw new Error("نشست منقضی شده است؛ دوباره وارد شوید.");
  if (response.status === 403) throw new Error("مجوز دسترسی به اطلاعات این مشتری را ندارید.");
  if (response.status === 404) throw new Error("مسیر یا رکورد پیدا نشد؛ محیط توسعه را بررسی کنید.");
  const validation = Object.values(result.errors ?? {}).flat().filter(Boolean).join(" ");
  const message = errors[result.error ?? ""] || result.detail || validation || result.title;
  throw new Error(message || "خطای ثبت/دریافت اطلاعات (" + response.status + ")");
}
const shortId = (id: string) => id.length > 12 ? id.slice(0, 8) + "…" : id;
const normalizeDigits = (value: string) => value
  .replace(/[۰-۹]/g, digit => String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit)))
  .replace(/\D/g, "")
  .slice(0, 13);
export default function CatalogWorkspace({ actorId }: { actorId: string }) {
  const [customer, setCustomer] = useState(actorId);
  const [accounts, setAccounts] = useState<OwnerAccount[]>([]);
  const [actorLabel, setActorLabel] = useState("حساب واردشده");
  const tableVersion = useRef(0);
  const [closeDate, setCloseDate] = useState("");
  const [kind, setKind] = useState<Kind>("products");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState<CatalogRow[]>([]);
  const [more, setMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [info, setInfo] = useState("");
  const [edit, setEdit] = useState<CatalogRow | null>(null);
  const [title, setTitle] = useState("");
  const [code, setCode] = useState("");
  const [unitId, setUnitId] = useState("");
  const [productKind, setProductKind] = useState(1);
  const [pending, setPending] = useState(false);
  const [selected, setSelected] = useState<CatalogRow | null>(null);
  const [officialCode, setOfficialCode] = useState("");
  const [officialResult, setOfficialResult] = useState<CatalogRow[]>([]);
  const [selectedOfficialId, setSelectedOfficialId] = useState("");
  const [mappingSearchOpen, setMappingSearchOpen] = useState(false);
  const [officialSearchQuery, setOfficialSearchQuery] = useState("");
  const [fromUtc, setFromUtc] = useState("");
  const [history, setHistory] = useState<Array<{
    id: string; officialCode: string; effectiveFromUtc: string;
    effectiveToUtc: string | null; revision: number;
  }>>([]);

  useEffect(() => {
    let live = true;
    async function loadAccounts() {
      try {
        const result = await fetch("/api/customer-owners/accounts", {
          credentials: "same-origin", cache: "no-store"
        });
        if (result.ok) {
          const data: unknown = await result.json();
          if (live && Array.isArray(data)) setAccounts(data as OwnerAccount[]);
        }
      } catch { /* Customer users may not have access to the administrator directory. */ }
      try {
        const result = await fetch("/api/software-profile/me", {
          credentials: "same-origin", cache: "no-store"
        });
        if (result.ok) {
          const value = await result.json() as {
            firstName?: string; lastName?: string; personName?: string; companyName?: string;
          };
          const name = value.companyName || value.personName ||
            [value.firstName, value.lastName].filter(Boolean).join(" ");
          if (live && name) setActorLabel(name);
        }
      } catch { /* Keep a human-readable account fallback instead of exposing UUID. */ }
    }
    void loadAccounts();
    return () => { live = false; };
  }, []);
  const customerLabel = customer === actorId ? actorLabel :
    (accountName(accounts.find(a => a.customerUserId === customer) ?? { customerUserId: customer }) || "حساب انتخاب‌شده");
  const load = useCallback(async () => {
    if (kind !== "official" && !guid(customer)) { setError("شناسه مشتری باید UUID معتبر باشد."); return; }
    const version = ++tableVersion.current;
    setLoading(true); setError("");
    try {
      const prefix = kind === "official" ? "official" :
        "customers/" + customer + "/" + kind;
      const args = new URLSearchParams({ page: String(page), pageSize: "20", query });
      const data = await catalogCall(prefix + "?" + args) as PageResult;
      if (version !== tableVersion.current) return;
      setRows(data.items ?? []); setMore(Boolean(data.hasMore));
    } catch (e) { if (version === tableVersion.current) {
      setRows([]); setMore(false); setError(e instanceof Error ? e.message : "خطای دریافت اطلاعات");
    } }
    finally { if (version === tableVersion.current) setLoading(false); }
  }, [customer, kind, page, query]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    setSelected(null); setHistory([]); setOfficialResult([]); setSelectedOfficialId("");
    setMappingSearchOpen(false); setOfficialSearchQuery(""); setEdit(null);
    setCode(""); setTitle(""); setUnitId(""); setInfo("");
    setFromUtc(""); setCloseDate("");
  }, [customer]);
  function openForm(row?: CatalogRow) {
    setEdit(row ?? null); setCode(row?.internalCode ?? "");
    setTitle(row?.title ?? ""); setUnitId(row?.baseUnitId ?? "");
    setProductKind(row?.kind === 2 ? 2 : 1); setError(""); setInfo("");
  }
  const searchUnits = useCallback(async (query: string): Promise<CatalogUnitOption[]> => {
    const params = new URLSearchParams({ page: "1", pageSize: "50", query });
    const response = await catalogCall("units?" + params) as { items: CatalogUnitOption[] };
    return response.items ?? [];
  }, []);
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (kind === "official" || !guid(customer) || !guid(unitId)) {
      setError("شناسه مشتری و واحد پایه باید معتبر باشند."); return;
    }
    if (code.trim().length < 1 || title.trim().length < 2) {
      setError("کد داخلی و عنوان معتبر وارد کنید."); return;
    }
    setPending(true); setError("");
    try {
      const root = "customers/" + customer + "/" + kind;
      const payload = {
        internalCode: code, title, baseUnitId: unitId,
        ...(kind === "products" ? { kind: productKind } : {}),
        expectedRevision: edit?.revision ?? 0, discontinue: false,
      };
      await catalogCall(root + (edit ? "/" + edit.id : ""), edit ? "PUT" : "POST", payload);
      setInfo("اطلاعات با موفقیت ثبت شد."); setEdit(null);
      setCode(""); setTitle(""); setUnitId(""); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "ثبت ناموفق بود"); }
    finally { setPending(false); }
  }
  async function archive(row: CatalogRow) {
    if (!window.confirm("این مورد بدون حذف سوابق غیرفعال شود؟")) return;
    setPending(true); setError("");
    try {
      await catalogCall("customers/" + customer + "/" + kind + "/" + row.id, "PUT", {
        internalCode: row.internalCode, title: row.title,
        baseUnitId: row.baseUnitId, ...(kind === "products" ? { kind: row.kind } : {}),
        expectedRevision: row.revision, discontinue: true,
      });
      setInfo("مورد انتخابی بایگانی شد."); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "عملیات ناموفق بود"); }
    finally { setPending(false); }
  }
  async function chooseItem(row: CatalogRow) {
    setSelected(row); setHistory([]); setOfficialResult([]); setSelectedOfficialId("");
    try {
      const data = await catalogCall("customers/" + customer + "/items/" + row.id + "/tax-mappings");
      setHistory(data);
    } catch (e) { setError(e instanceof Error ? e.message : "تاریخچه دریافت نشد"); }
  }
  async function findOfficial() {
    try {
      const args = new URLSearchParams({ query: officialSearchQuery, page: "1", pageSize: "20",
        kind: selected?.kind === 3 ? "2" : "1" });
      const data = await catalogCall("official?" + args) as PageResult;
      setOfficialResult(data.items ?? []); setSelectedOfficialId("");
    } catch (e) { setError(e instanceof Error ? e.message : "جست‌وجو ناموفق بود"); }
  }
  async function closeMapping(mapping: { id: string; revision: number }) {
    if (!closeDate || !selected) { setError("تاریخ پایان اعتبار را انتخاب کنید."); return; }
    const at = new Date(closeDate + "T00:00:00.000Z");
    if (Number.isNaN(at.getTime())) { setError("تاریخ پایان معتبر نیست."); return; }
    setPending(true); setError("");
    try {
      const query = new URLSearchParams({
        expectedRevision: String(mapping.revision), closedAtUtc: at.toISOString(),
      });
      await catalogCall("customers/" + customer + "/tax-mappings/" + mapping.id +
        "/close?" + query, "PUT", {});
      setInfo("بازه قبلی بدون حذف تاریخچه بسته شد."); await chooseItem(selected); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "بستن نگاشت ناموفق بود"); }
    finally { setPending(false); }
  }
  async function saveMapping(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || !officialCodeIsValid) {
      setError("شناسه رسمی باید دقیقاً ۱۳ رقم باشد."); return;
    }
    if (!fromUtc) { setError("تاریخ شروع اعتبار را وارد کنید."); return; }
    setPending(true); setError("");
    try {
      const official = selectedOfficialId
        ? officialResult.find(item => item.id === selectedOfficialId)
        : null;
      if (selectedOfficialId && !official) throw new Error("شناسه رسمی انتخاب‌شده یافت نشد.");
      const date = new Date(fromUtc + "T00:00:00.000Z");
      if (Number.isNaN(date.getTime())) throw new Error("تاریخ شروع معتبر نیست.");
      const payload = official
        ? { officialEntryId: official.id, effectiveFromUtc: date.toISOString() }
        : { officialCode, effectiveFromUtc: date.toISOString() };
      await catalogCall("customers/" + customer + "/items/" + selected.id + "/tax-mappings", "POST", payload);
      setInfo("نگاشت رسمی ثبت شد."); setSelectedOfficialId(""); await chooseItem(selected); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "نگاشت ثبت نشد"); }
    finally { setPending(false); }
  }
  const officialCodeIsValid = officialCode.length === 13;
  return <div className="catalog-workspace catalog-workspace-compact" dir="rtl">
    <section className="catalog-card" aria-label="فیلترهای کاتالوگ">
      <div className="catalog-tabs" role="tablist" aria-label="نوع اطلاعات">
        {([["products","کالاها"],["services","خدمات"],["official","شناسه‌های رسمی"]] as const).map(([value,label]) =>
          <button key={value} type="button" role="tab" aria-selected={kind===value}
            onClick={() => { setKind(value); setPage(1); setRows([]); setSelected(null); setEdit(null); setError(""); }}>{label}</button>)}
      </div>
      <div className="catalog-filters">
        {kind !== "official" && <label>حساب / مشتری هدف
          <select aria-label="انتخاب مشتری بر اساس نام" value={customer}
            onChange={e=> {tableVersion.current++; setCustomer(e.target.value); setPage(1); setRows([]); setSelected(null);}}>
            <option value={actorId}>{actorLabel} (حساب فعلی)</option>
            {accounts.filter(a=>a.customerUserId!==actorId && a.isActive !== false && accountName(a).length>0).map(a=><option
              key={a.customerUserId} value={a.customerUserId}>
              {accountName(a)}
            </option>)}
          </select>
        </label>}
        <label>جست‌وجو براساس عنوان یا کد
          <input value={query} onChange={e=> {setQuery(e.target.value);setPage(1);}}
            maxLength={120} placeholder="عنوان یا کد داخلی / رسمی" />
        </label>
        <button type="button" className="identity-secondary" disabled={loading} onClick={()=>void load()}>بازخوانی</button>
      </div>
      {kind !== "official" && <p className="catalog-help">نمایش و ثبت فقط برای حساب انتخاب‌شده: <strong>{customerLabel}</strong>. «کد داخلی» برای مدیریت کالا در ArginTaxPayer است. برای صورتحساب مؤدیان، پس از ثبت کالا در جدول روی «نگاشت» کلیک کنید و شناسه رسمی ۱۳ رقمی را از مرجع فعال انتخاب کنید. دسترسی توسط سرور و مالکیت تجاری کنترل می‌شود.</p>}
    </section>
    {error && <p className="form-error" role="alert">{error}</p>}
    {info && <p className="catalog-message" role="status">{info}</p>}
    <div className="catalog-split-layout">
    {kind !== "official" && <section className={"catalog-card catalog-editor-panel" +
      (selected ? " catalog-mapping-editor" : "")}>
      {selected ? <>
        <header className="catalog-editor-header">
          <div>
            <h2>نگاشت شناسه رسمی</h2>
            <p>برای «{selected.title}»</p>
          </div>
          <button type="button" className="identity-secondary" disabled={pending}
            onClick={()=>setSelected(null)}>بستن</button>
        </header>
        <form onSubmit={saveMapping} className="catalog-form catalog-mapping-form">
          <p className="catalog-help catalog-mapping-help">
            شناسه رسمی ۱۳ رقمی را جست‌وجو و انتخاب کنید، سپس تاریخ شروع اعتبار را وارد و ذخیره کنید.
            این شناسه با کد داخلی کالا متفاوت است.
          </p>
          <label>شناسه رسمی ۱۳ رقمی
            <div className="catalog-inline-field">
              <input value={officialCode} dir="ltr" inputMode="numeric" maxLength={13}
                onChange={e=>{setOfficialCode(normalizeDigits(e.target.value));setSelectedOfficialId("");}}
                placeholder="کد ۱۳ رقمی را وارد کنید" />
              <button type="button" className="identity-secondary" disabled={pending}
                onClick={()=>{setOfficialSearchQuery(officialCode);setOfficialResult([]);setMappingSearchOpen(true);}}>
                جست‌وجو در مرجع
              </button>
            </div>
            <small className="catalog-help">کد را می‌توانید مستقیم وارد و ذخیره کنید؛ برای انتخاب از فهرست مرجع از دکمه جست‌وجو استفاده کنید.</small>
          </label>
          <label>شروع اعتبار نگاشت (شمسی)
            <PersianDatePicker value={fromUtc} onChange={setFromUtc}/>
          </label>
          <label>پایان اعتبار نگاشت قبلی (شمسی)
            <PersianDatePicker value={closeDate} onChange={setCloseDate}/>
            <small className="catalog-help">فقط برای بستن نگاشت فعال قبلی استفاده می‌شود.</small>
          </label>
          <div className="catalog-modal-footer">
            <button type="button" className="identity-secondary" disabled={pending}
              onClick={()=>setSelected(null)}>انصراف</button>
            <button type="submit" className="identity-primary"
              disabled={pending || !officialCodeIsValid || !fromUtc}>
              {pending ? "در حال ذخیره…" : "ذخیره نگاشت"}
            </button>
          </div>
        </form>
        <div className="catalog-mapping-history">
          <h3>تاریخچه نگاشت</h3>
          <ul className="catalog-history">{history.map(x=><li key={x.id}>
            <bdi dir="ltr">{x.officialCode}</bdi> — از <time>{new Date(x.effectiveFromUtc).toLocaleString("fa-IR")}</time>
            {" "}تا {x.effectiveToUtc ? new Date(x.effectiveToUtc).toLocaleString("fa-IR") : "باز"}
            {!x.effectiveToUtc && <button type="button" disabled={pending}
              onClick={()=>void closeMapping(x)}>بستن اعتبار</button>}
          </li>)}</ul>
        </div>
        {mappingSearchOpen && <div className="catalog-mapping-search-modal" role="dialog"
          aria-modal="true" aria-labelledby="catalog-mapping-search-title">
          <div className="catalog-mapping-search-dialog">
            <header className="catalog-editor-header">
              <div>
                <h2 id="catalog-mapping-search-title">جست‌وجوی شناسه رسمی</h2>
                <p>انتخاب از مرجع شناسه‌های فعال</p>
              </div>
              <button type="button" className="identity-secondary"
                onClick={()=>setMappingSearchOpen(false)}>بستن</button>
            </header>
            <form className="catalog-inline-field" onSubmit={event=>{event.preventDefault();void findOfficial();}}>
              <input value={officialSearchQuery} dir="ltr" inputMode="numeric" maxLength={13}
                onChange={e=>setOfficialSearchQuery(normalizeDigits(e.target.value))}
                placeholder="کد یا عنوان شناسه رسمی" autoFocus />
              <button type="submit" className="identity-primary" disabled={pending || !officialSearchQuery.trim()}>
                جست‌وجو
              </button>
            </form>
            <div className="catalog-official-results" aria-live="polite">
              {officialResult.length === 0 ? <p className="catalog-help">برای نمایش نتایج، جست‌وجو را اجرا کنید.</p> :
                officialResult.map(item=><button type="button" key={item.id}
                  className={"catalog-official-option" + (selectedOfficialId === item.id ? " is-selected" : "")}
                  onClick={()=>{setSelectedOfficialId(item.id);setOfficialCode(item.code ?? "");setMappingSearchOpen(false);}}>
                  <span>{item.title}</span><bdi dir="ltr">{item.code}</bdi>
                </button>)}
            </div>
          </div>
        </div>}
      </> : <>
        <div className="catalog-form-dialog" aria-labelledby="catalog-dialog-title">
          <header className="catalog-editor-header">
            <div><h2 id="catalog-dialog-title">{edit?"ویرایش":"افزودن"} {kind==="products"?"کالا":"خدمت"}</h2>
              <p>اطلاعات پایه مشتری</p></div>
            <button type="button" className="identity-secondary"
              disabled={pending} onClick={()=>openForm()}>فرم جدید</button>
          </header>
          <div className="catalog-editor-body">
            <form onSubmit={save} className="catalog-form">
              <label>کد داخلی (صرفاً شناسه داخل نرم‌افزار؛ کد رسمی ۱۳ رقمی نیست)
                <input required maxLength={100} dir="ltr" value={code} onChange={e=>setCode(e.target.value)}/></label>
              <label>عنوان
                <input required minLength={2} maxLength={256} value={title} onChange={e=>setTitle(e.target.value)}/></label>
              <label>واحد اندازه‌گیری پایه
                <CatalogUnitCombobox value={unitId} onChange={setUnitId}
                  search={searchUnits} disabled={pending}/></label>
              {kind==="products" && <label>نوع کالا
                <select value={productKind} disabled={Boolean(edit)}
                  onChange={e=>setProductKind(Number(e.target.value))}>
                  <option value={1}>انبارپذیر</option><option value={2}>غیرانبارپذیر</option></select></label>}
              <p className="catalog-help">واحدهای مرجع پیش‌نویس فقط برای ثبت داخلی هستند و تأیید مالیاتی محسوب نمی‌شوند.</p>
              <div className="catalog-modal-footer">
                <button type="button" className="identity-secondary" disabled={pending}
                  onClick={()=>openForm()}>پاک‌کردن فرم</button>
                <button type="submit" className="identity-primary" disabled={pending||!unitId}>
                  {pending?"در حال ذخیره…":"ذخیره اطلاعات"}</button>
              </div>
            </form>
          </div>
        </div>
      </>}
    </section>}

    <section className="catalog-card">
      <div className="catalog-bar"><h2>{kind==="products"?"کالاها":kind==="services"?"خدمات":"مرجع شناسه رسمی"}</h2>
        
      </div>
      <div className="catalog-scroll catalog-grid-loading-host">
        <table className="catalog-table">
          <thead><tr><th scope="col">شناسه ۱۳ رقمی</th>{kind !== "official" && <th scope="col">کد داخلی</th>}<th scope="col">عنوان</th><th scope="col">نوع / وضعیت</th><th scope="col">عملیات</th></tr></thead>
          <tbody>{rows.map(row=><tr key={row.id}>
            <td>{(kind === "official" ? row.code : row.officialCode)
              ? <bdi dir="ltr">{kind === "official" ? row.code : row.officialCode}</bdi>
              : <span className="catalog-help">فاقد نگاشت فعال</span>}</td>
            {kind !== "official" && <td><bdi dir="ltr">{row.internalCode}</bdi></td>}
            <td className="catalog-title-cell">{row.title}</td><td>{kind==="official" ? row.kind===1?"کالا":"خدمت" :
              (row.status===4?"بایگانی":row.status===1?"فعال":"غیرفعال")}</td>
            <td>{kind==="official"?<span>نسخه <bdi dir="ltr">{shortId(row.releaseId??"")}</bdi></span>:
              <div className="catalog-actions"><button type="button" onClick={()=>void chooseItem(row)}>نگاشت</button>
                <button type="button" onClick={()=>openForm(row)} disabled={row.status===4}>ویرایش</button>
                <button type="button" disabled={row.status===4||pending} onClick={()=>void archive(row)}>بایگانی</button></div>}
            </td></tr>)}</tbody>
        </table>
        {!loading && rows.length===0 && <p className="catalog-empty">موردی برای نمایش وجود ندارد.</p>}
        {loading && <ConsoleLoader label="در حال بارگذاری کالاها و خدمات…" full />}
      </div>
      <nav className="catalog-pages" aria-label="صفحه‌بندی">
        <button disabled={page===1||loading} onClick={()=>setPage(p=>p-1)}>صفحه قبل</button>
        <span>صفحه {page.toLocaleString("fa-IR")}</span>
        <button disabled={!more||loading} onClick={()=>setPage(p=>p+1)}>صفحه بعد</button>
      </nav>
    </section>
    </div>
  </div>;
}
