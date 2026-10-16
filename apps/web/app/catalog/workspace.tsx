"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";

type Kind = "products" | "services" | "official";
type CatalogRow = {
  id: string; customerUserId?: string; internalCode?: string; code?: string;
  title: string; kind?: number; baseUnitId?: string;
  status?: number; revision?: number; releaseId?: string;
};
type PageResult = { items: CatalogRow[]; hasMore: boolean };
type ErrorBody = { error?: string };
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
  const result = await response.json().catch(() => ({})) as ErrorBody;
  if (response.status === 401) throw new Error("نشست منقضی شده است؛ دوباره وارد شوید.");
  if (response.status === 403) throw new Error("مجوز دسترسی به اطلاعات این مشتری را ندارید.");
  if (response.status === 404) throw new Error("مسیر یا رکورد پیدا نشد؛ محیط توسعه را بررسی کنید.");
  throw new Error(errors[result.error ?? ""] ?? "خطای ثبت/دریافت اطلاعات (" + response.status + ")");
}
const shortId = (id: string) => id.length > 12 ? id.slice(0, 8) + "…" : id;
export default function CatalogWorkspace({ actorId }: { actorId: string }) {
  const [customer, setCustomer] = useState(actorId);
  const [kind, setKind] = useState<Kind>("products");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState<CatalogRow[]>([]);
  const [more, setMore] = useState(false);
  const [loading, setLoading] = useState(false);
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
  const [fromUtc, setFromUtc] = useState("");
  const [history, setHistory] = useState<Array<{
    id: string; officialCode: string; effectiveFromUtc: string;
    effectiveToUtc: string | null; revision: number;
  }>>([]);

  const load = useCallback(async () => {
    if (kind !== "official" && !guid(customer)) { setError("شناسه مشتری باید UUID معتبر باشد."); return; }
    setLoading(true); setError("");
    try {
      const prefix = kind === "official" ? "official" :
        "customers/" + customer + "/" + kind;
      const args = new URLSearchParams({ page: String(page), pageSize: "20", query });
      const data = await catalogCall(prefix + "?" + args) as PageResult;
      setRows(data.items ?? []); setMore(Boolean(data.hasMore));
    } catch (e) { setError(e instanceof Error ? e.message : "خطای دریافت اطلاعات"); }
    finally { setLoading(false); }
  }, [customer, kind, page, query]);

  useEffect(() => { void load(); }, [load]);
  function openForm(row?: CatalogRow) {
    setEdit(row ?? null); setCode(row?.internalCode ?? "");
    setTitle(row?.title ?? ""); setUnitId(row?.baseUnitId ?? "");
    setProductKind(row?.kind === 2 ? 2 : 1); setError(""); setInfo("");
  }
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
    setSelected(row); setHistory([]); setOfficialResult([]);
    try {
      const data = await catalogCall("customers/" + customer + "/items/" + row.id + "/tax-mappings");
      setHistory(data);
    } catch (e) { setError(e instanceof Error ? e.message : "تاریخچه دریافت نشد"); }
  }
  async function findOfficial() {
    try {
      const args = new URLSearchParams({ query: officialCode, page: "1", pageSize: "20",
        kind: selected?.kind === 3 ? "2" : "1" });
      const data = await catalogCall("official?" + args) as PageResult;
      setOfficialResult(data.items ?? []);
    } catch (e) { setError(e instanceof Error ? e.message : "جست‌وجو ناموفق بود"); }
  }
  async function closeMapping(mapping: { id: string; revision: number }) {
    const input = window.prompt("زمان پایان اعتبار (ISO UTC، مثال: 2026-10-16T12:00:00Z)");
    if (!input || !selected) return;
    const at = new Date(input);
    if (Number.isNaN(at.getTime()) || !input.endsWith("Z")) {
      setError("تاریخ پایان باید به شکل معتبر UTC وارد شود."); return;
    }
    setPending(true); setError("");
    try {
      const query = new URLSearchParams({
        expectedRevision: String(mapping.revision), closedAtUtc: at.toISOString(),
      });
      await catalogCall("customers/" + customer + "/tax-mappings/" + mapping.id +
        "/close?" + query, "PUT", {});
      setInfo("بازه قبلی بدون حذف تاریخچه بسته شد."); await chooseItem(selected);
    } catch (e) { setError(e instanceof Error ? e.message : "بستن نگاشت ناموفق بود"); }
    finally { setPending(false); }
  }
  async function assign(official: CatalogRow) {
    if (!selected || !fromUtc) { setError("تاریخ شروع اعتبار را وارد کنید."); return; }
    setPending(true); setError("");
    try {
      const date = new Date(fromUtc);
      if (Number.isNaN(date.getTime())) throw new Error("تاریخ شروع معتبر نیست.");
      await catalogCall("customers/" + customer + "/items/" + selected.id + "/tax-mappings", "POST", {
        officialEntryId: official.id, effectiveFromUtc: date.toISOString(),
      });
      setInfo("نگاشت رسمی ثبت شد."); await chooseItem(selected);
    } catch (e) { setError(e instanceof Error ? e.message : "نگاشت ثبت نشد"); }
    finally { setPending(false); }
  }
  return <div className="catalog-workspace" dir="rtl">
    <section className="catalog-card" aria-label="فیلترهای کاتالوگ">
      <div className="catalog-tabs" role="tablist" aria-label="نوع اطلاعات">
        {([["products","کالاها"],["services","خدمات"],["official","شناسه‌های رسمی"]] as const).map(([value,label]) =>
          <button key={value} type="button" role="tab" aria-selected={kind===value}
            onClick={() => { setKind(value); setPage(1); setRows([]); setSelected(null); setEdit(null); setError(""); }}>{label}</button>)}
      </div>
      <div className="catalog-filters">
        {kind !== "official" && <label>شناسه مشتری
          <input dir="ltr" aria-label="شناسه مشتری" value={customer}
            onChange={e=> {setCustomer(e.target.value.trim()); setPage(1);}} />
        </label>}
        <label>جست‌وجو براساس عنوان یا کد
          <input value={query} onChange={e=> {setQuery(e.target.value);setPage(1);}}
            maxLength={120} placeholder="عنوان یا کد داخلی / رسمی" />
        </label>
        <button type="button" className="identity-secondary" disabled={loading} onClick={()=>void load()}>بازخوانی</button>
      </div>
      {kind !== "official" && <p className="catalog-help">شناسه مشتری پیش‌فرض، شناسه حساب فعلی است. ادمین‌های مجاز می‌توانند شناسه مشتری تحت مدیریت را وارد کنند.</p>}
    </section>
    {error && <p className="form-error" role="alert">{error}</p>}
    {info && <p className="catalog-message" role="status">{info}</p>}
    <section className="catalog-card">
      <div className="catalog-bar"><h2>{kind==="products"?"کالاها":kind==="services"?"خدمات":"مرجع شناسه رسمی"}</h2>
        {kind!=="official" && <button className="identity-primary" type="button" onClick={()=>openForm()}>ثبت {kind==="products"?"کالا":"خدمت"} جدید</button>}
      </div>
      <div className="catalog-scroll">
        <table className="catalog-table">
          <thead><tr><th scope="col">کد</th><th scope="col">عنوان</th><th scope="col">نوع / وضعیت</th><th scope="col">عملیات</th></tr></thead>
          <tbody>{rows.map(row=><tr key={row.id}><td dir="ltr"><bdi>{row.internalCode ?? row.code}</bdi></td>
            <td>{row.title}</td><td>{kind==="official" ? row.kind===1?"کالا":"خدمت" :
              (row.status===4?"بایگانی":row.status===1?"فعال":"غیرفعال")}</td>
            <td>{kind==="official"?<span>نسخه <bdi dir="ltr">{shortId(row.releaseId??"")}</bdi></span>:
              <div className="catalog-actions"><button type="button" onClick={()=>void chooseItem(row)}>نگاشت</button>
                <button type="button" onClick={()=>openForm(row)} disabled={row.status===4}>ویرایش</button>
                <button type="button" disabled={row.status===4||pending} onClick={()=>void archive(row)}>بایگانی</button></div>}
            </td></tr>)}</tbody>
        </table>
        {!loading && rows.length===0 && <p className="catalog-empty">موردی برای نمایش وجود ندارد.</p>}
        {loading && <p role="status">در حال دریافت اطلاعات...</p>}
      </div>
      <nav className="catalog-pages" aria-label="صفحه‌بندی">
        <button disabled={page===1||loading} onClick={()=>setPage(p=>p-1)}>صفحه قبل</button>
        <span>صفحه {page.toLocaleString("fa-IR")}</span>
        <button disabled={!more||loading} onClick={()=>setPage(p=>p+1)}>صفحه بعد</button>
      </nav>
    </section>
    {kind !== "official" && <section className="catalog-card">
      <h2>{edit ? "ویرایش اطلاعات" : "فرم ثبت " + (kind==="products"?"کالا":"خدمت")}</h2>
      <form onSubmit={save} className="catalog-form">
        <label>کد داخلی <input required maxLength={100} dir="ltr" value={code} onChange={e=>setCode(e.target.value)} /></label>
        <label>عنوان <input required minLength={2} maxLength={256} value={title} onChange={e=>setTitle(e.target.value)} /></label>
        <label>شناسه واحد پایه <input required dir="ltr" value={unitId}
          placeholder="UUID واحد مرجع" onChange={e=>setUnitId(e.target.value.trim())} /></label>
        {kind==="products" && <label>نوع کالا <select value={productKind} disabled={Boolean(edit)}
          onChange={e=>setProductKind(Number(e.target.value))}><option value={1}>انبارپذیر</option><option value={2}>غیرانبارپذیر</option></select></label>}
        <div className="catalog-actions"><button className="identity-primary" type="submit" disabled={pending}>ثبت اطلاعات</button>
          <button type="button" onClick={()=>openForm()}>پاک‌کردن فرم</button></div>
      </form>
      <p className="catalog-help">واحد پایه باید شناسه معتبر رکورد واحد اندازه‌گیری باشد. انتخابگر جست‌وجوی واحد، مستلزم API فهرست واحدها است و در این گام هنوز آماده نیست.</p>
    </section>}
    {selected && kind !== "official" && <section className="catalog-card">
      <div className="catalog-bar"><h2>نگاشت شناسه رسمی برای {selected.title}</h2>
        <button type="button" onClick={()=>setSelected(null)}>بستن</button></div>
      <p className="catalog-help">فقط شناسه رسمی تأییدشده و فعال را انتخاب کنید. برای تغییر نگاشت، بازه قبلی ابتدا باید بسته شود.</p>
      <div className="catalog-filters">
        <label>جست‌وجوی شناسه رسمی <input value={officialCode} onChange={e=>setOfficialCode(e.target.value)} /></label>
        <button type="button" onClick={()=>void findOfficial()}>جست‌وجو</button>
        <label>شروع اعتبار (زمان محلی) <input type="datetime-local" value={fromUtc} onChange={e=>setFromUtc(e.target.value)} /></label>
      </div>
      {officialResult.map(item=><div className="catalog-result" key={item.id}><span>{item.title} — <bdi dir="ltr">{item.code}</bdi></span>
        <button disabled={pending} onClick={()=>void assign(item)}>ثبت نگاشت</button></div>)}
      <h3>تاریخچه نگاشت</h3>
      <ul className="catalog-history">{history.map(x=><li key={x.id}>
        <bdi dir="ltr">{x.officialCode}</bdi> — از <time>{new Date(x.effectiveFromUtc).toLocaleString("fa-IR")}</time>
        {" "}تا {x.effectiveToUtc ? new Date(x.effectiveToUtc).toLocaleString("fa-IR") : "باز"}
        {!x.effectiveToUtc && <button type="button" disabled={pending}
          onClick={()=>void closeMapping(x)}>بستن اعتبار</button>}
      </li>)}</ul>
    </section>}
  </div>;
}
