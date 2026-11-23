"use client";

import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import ConsoleLoader from "../components/console-loader";
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
  product_conflict: "نسخه اطلاعات کالا تغییر کرده است یا واحد انتخاب‌شده در دسترس نیست.",
  service_conflict: "نسخه اطلاعات خدمت تغییر کرده است یا واحد انتخاب‌شده در دسترس نیست.",
  Conflict: "تداخل داده یا تغییر هم‌زمان رخ داده است.",
  UnitUnavailable: "واحد انتخاب‌شده معتبر یا قابل استفاده نیست.",
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
  if (result.error === "origin_not_allowed")
    throw new Error("آدرس مرورگر برای ثبت تغییرات معتبر نیست. پنل را از آدرس رسمی سامانه باز کنید.");
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
export default function CatalogWorkspace({ actorId, isAdmin }: { actorId: string; isAdmin: boolean }) {
  const [customer, setCustomer] = useState(isAdmin ? "" : actorId);
  const [accounts, setAccounts] = useState<OwnerAccount[]>([]);
  const [actorLabel, setActorLabel] = useState("حساب واردشده");
  const tableVersion = useRef(0);
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
  const [officialCode, setOfficialCode] = useState("");

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
  const customerLabel = !customer ? "مشتری انتخاب نشده" : customer === actorId ? actorLabel :
    (accountName(accounts.find(a => a.customerUserId === customer) ?? { customerUserId: customer }) || "حساب انتخاب‌شده");
  const load = useCallback(async () => {
    if (kind !== "official" && !customer && isAdmin) {
      setRows([]); setMore(false); setLoading(false); setError(""); return;
    }
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
  }, [customer, isAdmin, kind, page, query]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    setEdit(null);
    setCode(""); setTitle(""); setUnitId(""); setInfo("");
    setOfficialCode("");
  }, [customer]);
  function openForm(row?: CatalogRow) {
    setEdit(row ?? null); setCode(row?.internalCode ?? ""); setOfficialCode(row?.officialCode ?? "");
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
        internalCode: code, title, baseUnitId: unitId, officialCode,
        ...(kind === "products" ? { kind: productKind } : {}),
        expectedRevision: edit?.revision ?? 0, discontinue: false,
      };
      await catalogCall(root + (edit ? "/" + edit.id : ""), edit ? "PUT" : "POST", payload);
      setInfo("اطلاعات با موفقیت ثبت شد."); setEdit(null);
      setCode(""); setOfficialCode(""); setTitle(""); setUnitId(""); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "ثبت ناموفق بود"); }
    finally { setPending(false); }
  }
  async function archive(row: CatalogRow) {
    if (!window.confirm("این مورد بدون حذف سوابق غیرفعال شود؟")) return;
    setPending(true); setError("");
    try {
      await catalogCall("customers/" + customer + "/" + kind + "/" + row.id, "PUT", {
        internalCode: row.internalCode, officialCode: row.officialCode, title: row.title,
        baseUnitId: row.baseUnitId, ...(kind === "products" ? { kind: row.kind } : {}),
        expectedRevision: row.revision, discontinue: true,
      });
      setInfo("مورد انتخابی بایگانی شد."); await load();
    } catch (e) { setError(e instanceof Error ? e.message : "عملیات ناموفق بود"); }
    finally { setPending(false); }
  }

  const officialCodeIsValid = officialCode.length === 13;
  return <div className="catalog-workspace catalog-workspace-compact" dir="rtl">
    <section className="catalog-card" aria-label="فیلترهای کاتالوگ">
      <div className="catalog-tabs" role="tablist" aria-label="نوع اطلاعات">
        {([["products","کالاها"],["services","خدمات"],["official","شناسه‌های رسمی"]] as const).map(([value,label]) =>
          <button key={value} type="button" role="tab" aria-selected={kind===value}
            onClick={() => { setKind(value); setPage(1); setRows([]); setEdit(null); setError(""); }}>{label}</button>)}
      </div>
      <div className="catalog-filters">
        {kind !== "official" && isAdmin && <label>حساب / مشتری هدف
          <select aria-label="انتخاب مشتری بر اساس نام" value={customer}
            onChange={e=> {tableVersion.current++; setCustomer(e.target.value); setPage(1); setRows([]);}}>
            <option value="">انتخاب مشتری زیرمجموعه</option>
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
      {kind !== "official" && <p className="catalog-help">{isAdmin ? "نمایش و ثبت فقط برای مشتری انتخاب‌شده" : "نمایش و ثبت در حساب شما"}: <strong>{customerLabel}</strong>. کد داخلی برای مدیریت کالا در ArginTaxPayer است و همراه با شناسه رسمی ۱۳ رقمی در مشخصات کالا ذخیره می‌شود. دسترسی توسط سرور و مالکیت تجاری کنترل می‌شود.</p>}
    </section>
    {error && <p className="form-error" role="alert">{error}</p>}
    {info && <p className="catalog-message" role="status">{info}</p>}
    <div className="catalog-split-layout">
    {kind !== "official" && <section className="catalog-card catalog-editor-panel">
      <div className="catalog-form-dialog" aria-labelledby="catalog-dialog-title">
        <header className="catalog-editor-header">
          <div><h2 id="catalog-dialog-title">{edit?"ویرایش":"افزودن"} {kind==="products"?"کالا":"خدمت"}</h2>
            <p>اطلاعات پایه مشتری</p></div>
          <button type="button" className="identity-secondary"
            disabled={pending} onClick={()=>openForm()}>فرم جدید</button>
        </header>
        <div className="catalog-editor-body">
          <form onSubmit={save} className="catalog-form">
            <label>شناسه رسمی ۱۳ رقمی
              <input required inputMode="numeric" maxLength={13} dir="ltr" value={officialCode}
                onChange={e=>setOfficialCode(normalizeDigits(e.target.value))}
                placeholder="شناسه رسمی کالا" />
            </label>
            <label>کد داخلی (صرفاً شناسه داخل نرم‌افزار)
              <input required maxLength={100} dir="ltr" value={code} onChange={e=>setCode(e.target.value)}/></label>
            <label>عنوان
              <input required minLength={2} maxLength={256} value={title} onChange={e=>setTitle(e.target.value)}/></label>
            {kind==="products" && <label>نوع کالا
              <select value={productKind} disabled={Boolean(edit)}
                onChange={e=>setProductKind(Number(e.target.value))}>
                <option value={1}>انبارپذیر</option><option value={2}>غیرانبارپذیر</option></select></label>}
            <label>واحد اندازه‌گیری پایه
              <CatalogUnitCombobox value={unitId} onChange={setUnitId}
                search={searchUnits} disabled={pending}/></label>
              <p className="catalog-help">شناسه رسمی و کد داخلی جداگانه ثبت می‌شوند؛ کد داخلی می‌تواند برای چند کالای مشابه یکسان باشد.</p>
            <div className="catalog-modal-footer">
              <button type="button" className="identity-secondary" disabled={pending}
                onClick={()=>openForm()}>پاک‌کردن فرم</button>
              <button type="submit" className="identity-primary" disabled={pending||!unitId||!officialCodeIsValid}>
                {pending?"در حال ذخیره…":"ذخیره اطلاعات"}</button>
            </div>
          </form>
        </div>
      </div>
    </section>}

    <section className="catalog-card">
      <div className="catalog-bar"><h2>{kind==="products"?"کالاها":kind==="services"?"خدمات":"مرجع شناسه رسمی"}</h2>
        
      </div>
      <div className="catalog-scroll catalog-grid-loading-host">
        <table className="catalog-table">
          <thead><tr><th scope="col">شناسه رسمی ۱۳ رقمی</th>{kind !== "official" && <th scope="col">کد داخلی</th>}<th scope="col">عنوان و نوع</th><th scope="col">{kind==="official"?"نوع":kind==="products"||kind==="services"?"وضعیت":"نوع / وضعیت"}</th><th scope="col">عملیات</th></tr></thead>
          <tbody>{rows.map(row=><tr key={row.id}>
            <td>{(kind === "official" ? row.code : row.officialCode)
              ? <bdi dir="ltr">{kind === "official" ? row.code : row.officialCode}</bdi>
              : <span className="catalog-help">ثبت نشده</span>}</td>
            {kind !== "official" && <td><bdi dir="ltr">{row.internalCode}</bdi></td>}
            <td className="catalog-title-cell"><span className="catalog-item-title">{row.title}</span>
              {kind==="products" && <span className="catalog-item-kind">{row.kind===2?"غیرانبارپذیر":"انبارپذیر"}</span>}</td>
            <td>{kind==="official" ? row.kind===1?"کالا":"خدمت" :
              (row.status===4?"بایگانی":row.status===1?"فعال":"غیرفعال")}</td>
            <td>{kind==="official"?<span>نسخه <bdi dir="ltr">{shortId(row.releaseId??"")}</bdi></span>:
              <div className="catalog-actions">
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
