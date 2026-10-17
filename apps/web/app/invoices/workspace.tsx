"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PersianDatePicker } from "../catalog/persian-date-picker";

type Taxpayer = { id: string; displayName: string; status: number };
type Invoice = { id: string; customerUserId: string; taxpayerId: string;
  revision: number; state: string; subject: string; issueDateUtc: string;
  updatedAtUtc: string };
type CatalogItem = { id: string; title: string; internalCode?: string; kind?: number };
type CatalogPage = { items: CatalogItem[] };
type CustomerAccount = { customerUserId: string; companyName?: string | null; personName?: string | null };
const errors: Record<number, string> = {
  400: "ورودی پیش‌نویس معتبر نیست.",
  401: "نشست کاربری معتبر نیست؛ دوباره وارد شوید.",
  403: "شما مجوز دسترسی به این پرونده را ندارید.",
  404: "مؤدی یا صورتحساب در محدوده دسترسی شما یافت نشد.",
  409: "اطلاعات یا نسخه با عملیات دیگری تداخل دارد؛ فهرست را تازه‌سازی کنید.",
  503: "ارتباط با سرویس برقرار نشد."
};
async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    cache: "no-store", credentials: "same-origin", ...init
  });
  if (!response.ok) throw new Error(errors[response.status] ??
    "درخواست انجام نشد (" + response.status + ").");
  return response.json() as Promise<T>;
}
const utcToday = () => new Date().toISOString().slice(0, 10);
const localDate = (date: string) => new Date(date).toLocaleDateString("fa-IR");
const patterns: Record<number, { value: number; label: string }[]> = {
  1: [
    { value: 1, label: "فروش" }, { value: 2, label: "فروش ارز" },
    { value: 3, label: "طلا، جواهر و پلاتین" }, { value: 4, label: "پیمانکاری" },
    { value: 5, label: "قبوض خدماتی" }, { value: 6, label: "بلیت هواپیما" },
    { value: 7, label: "صادرات" }, { value: 8, label: "بارنامه" },
    { value: 9, label: "فروش فرآورده‌های نفتی" },
    { value: 11, label: "بورس مبتنی بر کالا" },
    { value: 13, label: "خدمات بیمه‌ای" }, { value: 14, label: "فروش زنجیره" }
  ],
  2: [
    { value: 1, label: "فروش" }, { value: 3, label: "طلا، جواهر و پلاتین" },
    { value: 9, label: "فروش فرآورده‌های نفتی" },
    { value: 13, label: "خدمات بیمه‌ای" }
  ]
};
type PreparedLine = { id: string; title: string; quantity: string; kind: "products" | "services" };

export default function InvoiceEntryWorkspace({ actorId }: { actorId: string }) {
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerId, setTaxpayerId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selected, setSelected] = useState<Invoice | null>(null);
  const [date, setDate] = useState(utcToday);
  const [hour, setHour] = useState("12:00");
  const [seller, setSeller] = useState("");
  const [buyer, setBuyer] = useState("");
  const [invoiceType, setInvoiceType] = useState(1);
  const [pattern, setPattern] = useState(1);
  const [kind, setKind] = useState<"products" | "services">("products");
  const [catalogQuery, setCatalogQuery] = useState("");
  const [catalogCustomer, setCatalogCustomer] = useState(actorId);
  const [customerAccounts, setCustomerAccounts] = useState<CustomerAccount[]>([]);
  const [catalog, setCatalog] = useState<CatalogItem[]>([]);
  const [catalogBusy, setCatalogBusy] = useState(false);
  const [prepared, setPrepared] = useState<PreparedLine[]>([]);
  const [search, setSearch] = useState("");

  useEffect(() => {
    let active = true;
    getJson<Taxpayer[]>("/api/taxpayers/list").then(data => {
      if (!active) return;
      const allowed = data.filter(x => x.status === 1);
      setTaxpayers(allowed);
      setTaxpayerId(id => allowed.some(t => t.id === id) ? id : allowed[0]?.id ?? "");
    }).catch(e => { if (active) setError((e as Error).message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    getJson<CustomerAccount[]>("/api/customer-owners/accounts")
      .then(rows => { if (active && Array.isArray(rows)) setCustomerAccounts(rows); })
      .catch(() => { /* customer users do not have administration directory permissions */ });
    return () => { active = false; };
  }, []);

  const reload = useCallback(async () => {
    if (!taxpayerId) { setInvoices([]); return; }
    setLoading(true); setError("");
    try {
      const rows = await getJson<Invoice[]>("/api/invoices/" + taxpayerId);
      setInvoices(rows);
    } catch (e) { setError((e as Error).message); setInvoices([]); }
    finally { setLoading(false); }
  }, [taxpayerId]);

  useEffect(() => {
    setSelected(null); setPrepared([]); setCatalog([]);
    void reload();
  }, [reload]);

  useEffect(() => {
    let active = true;
    const params = new URLSearchParams({ page: "1", pageSize: "20", query: catalogQuery });
    setCatalogBusy(true);
    getJson<CatalogPage>("/api/catalog/customers/" +
      catalogCustomer + "/" + kind + "?" + params)
      .then(result => { if (active) setCatalog(result.items ?? []); })
      .catch(() => { if (active) setCatalog([]); })
      .finally(() => { if (active) setCatalogBusy(false); });
    return () => { active = false; };
  }, [catalogCustomer, kind, catalogQuery]);

  const allowedPatterns = patterns[invoiceType] ?? patterns[1];
  function chooseType(next: number) {
    setInvoiceType(next); setPattern((patterns[next] ?? patterns[1])[0].value);
  }
  const filtered = useMemo(() => invoices.filter(item =>
    (item.id + " " + item.subject + " " + item.state).toLowerCase()
      .includes(search.toLowerCase())), [invoices, search]);

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !taxpayerId) return;
    if (invoiceType !== 1 || pattern !== 1 || prepared.length !== 0) {
      setError("فقط پیش‌نویس اولیه نوع اول، الگوی فروش و بدون اقلام در API کنونی قابل ثبت است. پیش‌نمایش نوع/الگو و اقلام هنوز ذخیره نمی‌شود.");
      return;
    }
    const issued = new Date(date + "T" + hour + ":00.000Z");
    if (!date || !/^([01]\d|2[0-3]):[0-5]\d$/.test(hour) || Number.isNaN(issued.getTime()) ||
        issued.toISOString().slice(0, 10) !== date) {
      setError("تاریخ شمسی یا ساعت UTC معتبر انتخاب کنید."); return;
    }
    setBusy(true); setError(""); setNotice("");
    try {
      const invoice = await getJson<Invoice>("/api/invoices/" + taxpayerId, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          issueDateUtc: issued.toISOString(),
          sellerDisplayName: seller.trim(), buyerDisplayName: buyer.trim() || null
        })
      });
      setNotice("پیش‌نویس اولیه ثبت شد؛ اقلام و الگوی کامل هنوز ذخیره و اعتبارسنجی نشده‌اند.");
      setSelected(invoice);
      await reload();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  const ready = invoiceType === 1 && pattern === 1 && prepared.length === 0;

  return <div className="invoice-entry" dir="rtl" lang="fa">
    <div className="invoice-entry__note" role="note">
      <strong>محیط ثبت پیش‌نویس، نه صدور مالیاتی</strong>
      <p>نوع/الگو، انتخاب اقلام و مقدار در این گام فقط پیش‌نمایش محلی‌اند.
        تنها تاریخ، نام فروشنده و نام اختیاری خریدار در پیش‌نویس اولیه ذخیره می‌شود.
        هیچ صورتحسابی به سامانه مؤدیان ارسال نمی‌شود.</p>
    </div>
    {error && <p role="alert" className="form-error">{error}</p>}
    {notice && <p role="status" className="auth-info">{notice}</p>}
    <div className="invoice-entry__layout">
      <section className="taxpayer-panel" aria-labelledby="invoice-create-title">
        <h2 id="invoice-create-title">ایجاد پیش‌نویس</h2>
        <form className="identity-form" onSubmit={create}>
          <label htmlFor="invoice-taxpayer">مؤدی صادرکننده</label>
          <select id="invoice-taxpayer" required value={taxpayerId} disabled={busy || loading}
            onChange={e => setTaxpayerId(e.target.value)}>
            {!taxpayerId && <option value="">انتخاب مؤدی</option>}
            {taxpayers.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
          </select>
          <label htmlFor="invoice-seller">نام فروشنده</label>
          <input id="invoice-seller" value={seller} maxLength={256} required
            autoComplete="organization" onChange={e => setSeller(e.target.value)}
            placeholder="عنوان فروشنده" />
          <label htmlFor="invoice-buyer">نام خریدار (اختیاری)</label>
          <input id="invoice-buyer" value={buyer} maxLength={256}
            onChange={e => setBuyer(e.target.value)} placeholder="عنوان خریدار" />
          <label htmlFor="invoice-date">تاریخ صدور (شمسی)</label>
          <PersianDatePicker id="invoice-date" value={date} onChange={setDate} disabled={busy} />
          <label htmlFor="invoice-hour">ساعت صدور (UTC)</label>
          <input id="invoice-hour" dir="ltr" type="time" value={hour} required
            onChange={e => setHour(e.target.value)} />
          <h3>پیش‌نمایش نوع و الگو — ثبت نمی‌شود</h3>
          <label htmlFor="invoice-type">نوع صورتحساب</label>
          <select id="invoice-type" value={invoiceType} onChange={e => chooseType(Number(e.target.value))}>
            <option value={1}>نوع اول</option><option value={2}>نوع دوم</option>
          </select>
          <label htmlFor="invoice-pattern">الگوی صورتحساب</label>
          <select id="invoice-pattern" value={pattern} onChange={e => setPattern(Number(e.target.value))}>
            {allowedPatterns.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
          </select>
          <p className="field-hint">برای جلوگیری از ثبت ناقص، تنها نوع اول و الگوی فروش در فرم اولیه مجاز است.</p>
          <button className="identity-primary" type="submit"
            disabled={busy || !taxpayerId || !seller.trim() || !date || !ready}>
            {busy ? "در حال ثبت…" : "ثبت پیش‌نویس اولیه"}
          </button>
        </form>
      </section>
      <section className="taxpayer-panel" aria-labelledby="invoice-list-title">
        <div className="taxpayer-section-heading">
          <h2 id="invoice-list-title">پیش‌نویس‌ها و صورتحساب‌های داخلی</h2>
          <button type="button" className="identity-secondary" disabled={loading}
            onClick={() => void reload()}>بازخوانی</button>
        </div>
        <label htmlFor="invoice-search">جست‌وجو در فهرست</label>
        <input id="invoice-search" type="search" value={search}
          onChange={e => setSearch(e.target.value)} placeholder="شناسه یا وضعیت" />
        {loading ? <p role="status">در حال دریافت اطلاعات…</p> :
          filtered.length === 0 ? <p className="identity-muted">صورتحسابی برای این مؤدی ثبت نشده است.</p> :
          <div className="invoice-entry__list">
            {filtered.map(item => <button type="button" key={item.id}
              aria-pressed={selected?.id === item.id} onClick={() => setSelected(item)}>
              <bdi dir="ltr">{item.id.slice(0, 8)}…</bdi>
              <span>نسخه {item.revision} · {item.state === "Draft" ? "پیش‌نویس" : item.state}</span>
              <small>{localDate(item.issueDateUtc)}</small>
            </button>)}
          </div>}
        {selected && <div className="invoice-entry__detail">
          <h3>جزئیات داخلی صورتحساب انتخاب‌شده</h3>
          <dl>
            <dt>شناسه داخلی</dt><dd dir="ltr">{selected.id}</dd>
            <dt>Revision</dt><dd>{selected.revision}</dd>
            <dt>وضعیت داخلی</dt><dd>{selected.state}</dd>
            <dt>موضوع</dt><dd>{selected.subject}</dd>
          </dl>
          <p className="field-hint">وضعیت داخلی به معنی پذیرش یا ثبت در سامانه مؤدیان نیست.</p>
        </div>}
      </section>
    </div>
    <section className="taxpayer-panel" aria-labelledby="invoice-items-title">
      <h2 id="invoice-items-title">آماده‌سازی اقلام (فقط پیش‌نمایش محلی)</h2>
      <p className="field-hint">اقلام انتخابی در دیتابیس صورتحساب ذخیره نمی‌شوند؛ این بخش برای آماده‌سازی فرم ورود اقلام در مراحل بعد است.</p>
      <div className="invoice-entry__toolbar">
        {customerAccounts.length > 0 && <>
          <label htmlFor="invoice-catalog-owner">کاتالوگ مشتری مجاز</label>
          <select id="invoice-catalog-owner" value={catalogCustomer}
            onChange={e => { setCatalogCustomer(e.target.value); setPrepared([]); }}>
            <option value={actorId}>حساب فعلی</option>
            {customerAccounts.filter(a => a.customerUserId !== actorId).map(a =>
              <option key={a.customerUserId} value={a.customerUserId}>
                {a.companyName || a.personName || "مشتری مجاز"}
              </option>)}
          </select>
        </>}
        <label htmlFor="invoice-item-kind">نوع قلم</label>
        <select id="invoice-item-kind" value={kind} onChange={e => setKind(e.target.value as "products" | "services")}>
          <option value="products">کالا</option><option value="services">خدمت</option>
        </select>
        <label htmlFor="invoice-item-search">جست‌وجوی کاتالوگ</label>
        <input id="invoice-item-search" type="search" value={catalogQuery}
          onChange={e => setCatalogQuery(e.target.value)} placeholder="نام یا کد داخلی" />
      </div>
      {catalogBusy && <p role="status">در حال جست‌وجوی کالا و خدمت…</p>}
      <div className="invoice-entry__catalog">
        {catalog.map(item => <button key={item.id} type="button"
          disabled={prepared.some(line => line.id === item.id)}
          onClick={() => setPrepared(current => current.concat({
            id: item.id, title: item.title, quantity: "1", kind
          }))}>
          <strong>{item.title}</strong><small>{item.internalCode || "—"}</small>
          <span>افزودن به پیش‌نمایش</span>
        </button>)}
      </div>
      {prepared.length > 0 && <div className="invoice-entry__prepared">
        <h3>اقلام آماده‌شده — ثبت نشده</h3>
        {prepared.map(line => <div className="invoice-entry__line" key={line.id}>
          <span>{line.title}</span>
          <label htmlFor={"quantity-" + line.id}>مقدار</label>
          <input id={"quantity-" + line.id} type="number" min="0.00000001" step="any"
            dir="ltr" value={line.quantity}
            onChange={e => setPrepared(current => current.map(x =>
              x.id === line.id ? { ...x, quantity: e.target.value } : x))}/>
          <button type="button" className="identity-secondary"
            onClick={() => setPrepared(current => current.filter(x => x.id !== line.id))}>حذف</button>
        </div>)}
      </div>}
      <p className="field-hint">برای مدیریت اصلی کالا و خدمات از <Link href="/catalog">کاتالوگ</Link> استفاده کنید.</p>
      <p className="field-hint">شناسه حساب فعال: <bdi dir="ltr">{actorId.slice(0, 8)}…</bdi></p>
    </section>
  </div>;
}
