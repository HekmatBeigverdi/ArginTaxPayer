"use client";

import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { PersianDatePicker } from "../catalog/persian-date-picker";

type Taxpayer = { id: string; displayName: string; status: number };
type Invoice = { id: string; customerUserId: string; taxpayerId: string; revision: number;
  state: string; subject: string; issueDateUtc: string; updatedAtUtc: string };
type CatalogItem = { id: string; title: string; internalCode?: string };
type CatalogPage = { items: CatalogItem[] };
type Account = { customerUserId: string; companyName?: string | null; personName?: string | null };
type Row = { key: number; productId: string; name: string; quantity: string;
  unitPrice: string; discount: string; vatRate: string };
type Step = 0 | 1 | 2;
const headings = ["سربرگ صورتحساب", "اقلام صورتحساب", "جمع‌بندی صورتحساب"] as const;
const errorCodes: Record<number, string> = {
  400: "ورودی پیش‌نویس معتبر نیست.", 401: "نشست منقضی شده است؛ دوباره وارد شوید.",
  403: "برای این پرونده دسترسی ندارید.", 404: "مؤدی یا صورتحساب یافت نشد.",
  409: "تغییر هم‌زمان رخ داده است؛ فهرست را بازخوانی کنید.",
  503: "سرویس فعلاً در دسترس نیست."
};
const patterns: Record<number, { code: number; title: string }[]> = {
  1: [{code:1,title:"فروش"},{code:2,title:"فروش ارز"},{code:3,title:"طلا و جواهر"},
    {code:4,title:"پیمانکاری"},{code:5,title:"قبوض خدماتی"},{code:6,title:"بلیت هواپیما"},
    {code:7,title:"صادرات"},{code:8,title:"بارنامه"},{code:9,title:"فرآورده‌های نفتی"},
    {code:11,title:"بورس کالا"},{code:13,title:"خدمات بیمه‌ای"},{code:14,title:"فروش زنجیره"}],
  2: [{code:1,title:"فروش"},{code:3,title:"طلا و جواهر"},{code:9,title:"فرآورده‌های نفتی"},
    {code:13,title:"خدمات بیمه‌ای"}]
};
async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { credentials: "same-origin", cache: "no-store", ...init });
  if (!response.ok) throw new Error(errorCodes[response.status] || "خطای سرویس (" + response.status + ")");
  return response.json() as Promise<T>;
}
const todayUtc = () => new Date().toISOString().slice(0, 10);
const toFaDate = (value: string) => new Date(value).toLocaleDateString("fa-IR-u-ca-persian", {timeZone:"UTC"});
const amount = (value: string) => {
  const n = Number(value);
  return Number.isFinite(n) && n >= 0 ? n : 0;
};
const fmt = (value: number) => value.toLocaleString("fa-IR", { maximumFractionDigits: 2 });

export default function InvoiceEntryWorkspace({actorId}: {actorId: string}) {
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerId, setTaxpayerId] = useState("");
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [search, setSearch] = useState("");
  const [modal, setModal] = useState(false);
  const [step, setStep] = useState<Step>(0);
  const dialog = useRef<HTMLDivElement>(null);
  const openButton = useRef<HTMLButtonElement>(null);
  const [date, setDate] = useState(todayUtc);
  const [hour, setHour] = useState("12:00");
  const [seller, setSeller] = useState("");
  const [buyer, setBuyer] = useState("");
  const [invoiceType, setInvoiceType] = useState(1);
  const [pattern, setPattern] = useState(1);
  const [kind, setKind] = useState<"products" | "services">("products");
  const [catalogQuery, setCatalogQuery] = useState("");
  const [catalogCustomer, setCatalogCustomer] = useState(actorId);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [catalog, setCatalog] = useState<CatalogItem[]>([]);
  const [catalogBusy, setCatalogBusy] = useState(false);
  const [rows, setRows] = useState<Row[]>([]);
  const nextKey = useRef(0);
  const [detail, setDetail] = useState<Invoice | null>(null);

  useEffect(() => {
    let live = true;
    getJson<Taxpayer[]>("/api/taxpayers/list")
      .then(list => {
        if (!live) return;
        const active = list.filter(t => t.status === 1);
        setTaxpayers(active);
        setTaxpayerId(current => active.some(t => t.id === current) ? current : active[0]?.id ?? "");
      })
      .catch(e => { if (live) setError((e as Error).message); })
      .finally(() => { if (live) setLoading(false); });
    getJson<Account[]>("/api/customer-owners/accounts")
      .then(list => { if (live && Array.isArray(list)) setAccounts(list); })
      .catch(() => {});
    return () => { live = false; };
  }, []);

  const reload = useCallback(async () => {
    if (!taxpayerId) { setInvoices([]); return; }
    setLoading(true);
    try {
      const list = await getJson<Invoice[]>("/api/invoices/" + taxpayerId);
      setInvoices(list); setError("");
    } catch (e) { setInvoices([]); setError((e as Error).message); }
    finally { setLoading(false); }
  }, [taxpayerId]);
  useEffect(() => { setDetail(null); void reload(); }, [reload]);

  useEffect(() => {
    if (!modal || step !== 1) return;
    let live = true;
    setCatalogBusy(true);
    const query = new URLSearchParams({page:"1",pageSize:"20",query:catalogQuery});
    getJson<CatalogPage>("/api/catalog/customers/" + catalogCustomer + "/" + kind + "?" + query)
      .then(data => { if (live) setCatalog(data.items ?? []); })
      .catch(() => { if (live) setCatalog([]); })
      .finally(() => { if (live) setCatalogBusy(false); });
    return () => { live = false; };
  }, [modal, step, kind, catalogCustomer, catalogQuery]);

  useEffect(() => {
    if (!modal) return;
    const before = document.activeElement;
    dialog.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) { setModal(false); return; }
      if (event.key !== "Tab" || !dialog.current) return;
      const elements = [...dialog.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href]'
      )].filter(el => el.getClientRects().length > 0);
      if (!elements.length) return;
      const first = elements[0], last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault(); last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault(); first.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      if (before instanceof HTMLElement && before.isConnected) before.focus();
      else openButton.current?.focus();
    };
  }, [modal, busy]);

  function open() {
    setDetail(null);
    setStep(0); setDate(todayUtc()); setHour("12:00");
    setSeller(""); setBuyer(""); setInvoiceType(1); setPattern(1);
    setRows([]); setError(""); setNotice(""); setModal(true);
  }
  function close() { if (!busy) setModal(false); }
  function addRow(item?: CatalogItem) {
    nextKey.current++;
    setRows(current => current.concat({
      key: nextKey.current, productId: item?.id ?? "", name: item?.title ?? "",
      quantity:"1", unitPrice:"0", discount:"0", vatRate:"0"
    }));
  }
  function updateRow(key: number, changes: Partial<Row>) {
    setRows(current => current.map(row => row.key === key ? {...row,...changes} : row));
  }
  const calculated = useMemo(() => {
    const base = rows.reduce((total, row) =>
      total + amount(row.quantity) * amount(row.unitPrice), 0);
    const discount = rows.reduce((total, row) =>
      total + Math.min(amount(row.discount), amount(row.unitPrice)) * amount(row.quantity), 0);
    const vat = rows.reduce((total, row) =>
      total + Math.max(0, amount(row.unitPrice) - amount(row.discount)) *
      amount(row.quantity) * amount(row.vatRate) / 100, 0);
    return {base, discount, vat, total: base - discount + vat};
  }, [rows]);
  const filtered = invoices.filter(row =>
    (row.id + " " + row.state + " " + row.subject).toLowerCase().includes(search.toLowerCase()));
  const persistableHeader = invoiceType === 1 && pattern === 1;

  async function saveHeader(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (step !== 2 || busy || !taxpayerId || !persistableHeader) return;
    const issued = new Date(date + "T" + hour + ":00.000Z");
    if (!date || !/^([01]\d|2[0-3]):[0-5]\d$/.test(hour) ||
        Number.isNaN(issued.getTime()) || issued.toISOString().slice(0,10) !== date) {
      setError("تاریخ یا ساعت معتبر انتخاب کنید."); return;
    }
    setBusy(true); setError("");
    try {
      const created = await getJson<Invoice>("/api/invoices/" + taxpayerId, {
        method:"POST", headers:{"Content-Type":"application/json"},
        body: JSON.stringify({
          issueDateUtc: issued.toISOString(),
          sellerDisplayName:seller.trim(), buyerDisplayName:buyer.trim() || null
        })
      });
      setModal(false); setDetail(created);
      setNotice("فقط سربرگ اولیه ذخیره شد؛ ردیف‌ها و محاسبات صرفاً پیش‌نمایش بوده و ذخیره نشده‌اند.");
      await reload();
    } catch(e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  return <div className="invoice-entry invoice-register" dir="rtl" lang="fa">
    <div className="invoice-register__toolbar">
      <div className="invoice-register__filters">
        <label htmlFor="invoice-list-taxpayer">مؤدی</label>
        <select id="invoice-list-taxpayer" value={taxpayerId} disabled={loading}
          onChange={e => setTaxpayerId(e.target.value)}>
          <option value="">انتخاب مؤدی</option>
          {taxpayers.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
        </select>
        <label htmlFor="invoice-list-search">جست‌وجو</label>
        <input id="invoice-list-search" type="search" value={search}
          onChange={e => setSearch(e.target.value)} placeholder="شناسه / وضعیت" />
      </div>
      <div className="invoice-register__actions">
        <button type="button" className="identity-secondary" disabled={loading}
          onClick={() => void reload()}>بازخوانی</button>
        <button ref={openButton} type="button" className="identity-primary"
          disabled={!taxpayerId || loading} onClick={open}>+ افزودن صورتحساب</button>
      </div>
    </div>
    {error && !modal && <p role="alert" className="form-error">{error}</p>}
    {notice && <p role="status" className="auth-info">{notice}</p>}
    <div className="invoice-register__table-wrap">
      <table className="invoice-register__table">
        <thead><tr><th>شناسه</th><th>تاریخ صدور</th><th>موضوع</th>
          <th>وضعیت داخلی</th><th>نسخه</th><th>عملیات</th></tr></thead>
        <tbody>{loading ? <tr><td colSpan={6}>در حال دریافت…</td></tr> :
          filtered.length === 0 ? <tr><td colSpan={6}>صورتحسابی برای نمایش وجود ندارد.</td></tr> :
          filtered.map(item => <tr key={item.id}>
            <td><bdi dir="ltr" title={item.id}>{item.id.slice(0,8)}…</bdi></td>
            <td>{toFaDate(item.issueDateUtc)}</td><td>{item.subject}</td>
            <td>{item.state === "Draft" ? "پیش‌نویس" : item.state}</td>
            <td>{item.revision}</td>
            <td><button type="button" className="identity-secondary"
              onClick={() => setDetail(item)}>مشاهده</button></td>
          </tr>)}</tbody>
      </table>
    </div>
    {detail && <div className="invoice-register__overlay" onMouseDown={e => {
      if (e.currentTarget === e.target) setDetail(null);
    }}>
      <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
        aria-label="جزئیات صورتحساب">
        <div className="invoice-register__dialog-header">
          <h2>جزئیات صورتحساب</h2>
          <button type="button" className="identity-secondary" onClick={() => setDetail(null)}
            aria-label="بستن جزئیات">×</button>
        </div>
        <dl className="invoice-register__totals">
          <dt>شناسه داخلی</dt><dd dir="ltr">{detail.id}</dd>
          <dt>تاریخ</dt><dd>{toFaDate(detail.issueDateUtc)}</dd>
          <dt>موضوع</dt><dd>{detail.subject}</dd>
          <dt>وضعیت داخلی</dt><dd>{detail.state}</dd>
          <dt>نسخه</dt><dd>{detail.revision}</dd>
        </dl>
        <p className="field-hint">این وضعیت به معنی تأیید سازمان امور مالیاتی نیست.</p>
      </div>
    </div>}

    {modal && <div className="invoice-register__overlay" onMouseDown={e => {
      if (e.target === e.currentTarget) close();
    }}>
      <div ref={dialog} tabIndex={-1} className="invoice-register__dialog"
        role="dialog" aria-modal="true" aria-labelledby="invoice-dialog-title">
        <header className="invoice-register__dialog-header">
          <div><h2 id="invoice-dialog-title">افزودن صورتحساب</h2>
            <p>پیش‌نویس داخلی — بدون ارسال مالیاتی</p></div>
          <button className="identity-secondary" type="button" aria-label="بستن پنجره"
            disabled={busy} onClick={close}>×</button>
        </header>
        <nav className="invoice-register__steps" aria-label="مراحل ورود صورتحساب">
          {headings.map((title,index) => <span key={title}
            aria-current={step === index ? "step" : undefined}
            className={step === index ? "active" : ""}>{index+1}. {title}</span>)}
        </nav>
        <form className="invoice-register__form" onSubmit={saveHeader}>
          <div className="invoice-register__dialog-body">
            {error && <p role="alert" className="form-error">{error}</p>}
            {step === 0 && <section aria-label="سربرگ صورتحساب" className="invoice-register__fields">
              <label htmlFor="invoice-taxpayer">مؤدی صادرکننده</label>
              <select id="invoice-taxpayer" value={taxpayerId} required
                onChange={e => setTaxpayerId(e.target.value)}>
                {taxpayers.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
              </select>
              <label htmlFor="invoice-seller">فروشنده</label>
              <input id="invoice-seller" required maxLength={256}
                value={seller} onChange={e => setSeller(e.target.value)} />
              <label htmlFor="invoice-buyer">خریدار (اختیاری)</label>
              <input id="invoice-buyer" maxLength={256} value={buyer}
                onChange={e => setBuyer(e.target.value)}/>
              <label htmlFor="invoice-date">تاریخ صدور شمسی</label>
              <PersianDatePicker id="invoice-date" value={date} onChange={setDate} floating/>
              <label htmlFor="invoice-hour">ساعت UTC</label>
              <input id="invoice-hour" type="time" dir="ltr" required value={hour}
                onChange={e => setHour(e.target.value)}/>
              <label htmlFor="invoice-type">نوع صورتحساب</label>
              <select id="invoice-type" value={invoiceType} onChange={e => {
                const next = Number(e.target.value);
                setInvoiceType(next); setPattern(patterns[next][0].code);
              }}><option value={1}>نوع اول</option><option value={2}>نوع دوم</option></select>
              <label htmlFor="invoice-pattern">الگوی صورتحساب</label>
              <select id="invoice-pattern" value={pattern}
                onChange={e => setPattern(Number(e.target.value))}>
                {patterns[invoiceType].map(p => <option key={p.code} value={p.code}>{p.title}</option>)}
              </select>
              <p className="invoice-register__hint">نوع/الگو صرفاً پیش‌نمایش هستند؛ API فعلی فقط سربرگ اولیه نوع اول، الگوی فروش را می‌پذیرد.</p>
            </section>}
            {step === 1 && <section aria-label="اقلام صورتحساب">
              <p className="invoice-register__hint">ردیف‌ها صرفاً در حافظه این پنجره آماده می‌شوند؛ هنوز API ثبت ردیف وجود ندارد.</p>
              <div className="invoice-register__row-tools">
                {accounts.length > 0 && <>
                  <label htmlFor="invoice-catalog-owner">کاتالوگ مشتری</label>
                  <select id="invoice-catalog-owner" value={catalogCustomer}
                    onChange={e => {setCatalogCustomer(e.target.value);setCatalog([]);}}>
                    <option value={actorId}>حساب فعلی</option>
                    {accounts.filter(a => a.customerUserId !== actorId).map(a =>
                      <option key={a.customerUserId} value={a.customerUserId}>
                        {a.companyName || a.personName || "مشتری مجاز"}
                      </option>)}
                  </select>
                </>}
                <label htmlFor="invoice-kind">نوع قلم</label>
                <select id="invoice-kind" value={kind}
                  onChange={e => setKind(e.target.value as "products"|"services")}>
                  <option value="products">کالا</option><option value="services">خدمت</option>
                </select>
                <label htmlFor="invoice-catalog-search">جست‌وجوی اقلام</label>
                <input id="invoice-catalog-search" type="search" value={catalogQuery}
                  onChange={e => setCatalogQuery(e.target.value)}/>
              </div>
              <div className="invoice-register__catalog">
                {catalogBusy ? "در حال بارگذاری…" : catalog.length === 0 ?
                  "قلمی در کاتالوگ مجاز یافت نشد." : catalog.map(item =>
                    <button key={item.id} type="button" className="identity-secondary"
                      onClick={() => addRow(item)}>+ {item.title}</button>)}
              </div>
              <div className="invoice-register__row-heading">
                <h3>ردیف‌های صورتحساب ({rows.length})</h3>
                <button type="button" className="identity-secondary" onClick={() => addRow()}>
                  + ردیف جدید</button>
              </div>
              {rows.length === 0 ? <p className="identity-muted">هنوز ردیفی اضافه نشده است.</p> :
                rows.map((row,index) => <div className="invoice-register__item" key={row.key}>
                  <strong>ردیف {index+1}</strong>
                  <label>شرح<input value={row.name} maxLength={256}
                    onChange={e => updateRow(row.key,{name:e.target.value})}/></label>
                  <label>مقدار<input dir="ltr" type="number" min="0.00000001" step="any"
                    value={row.quantity} onChange={e => updateRow(row.key,{quantity:e.target.value})}/></label>
                  <label>فی واحد (ریال)<input dir="ltr" type="number" min="0" step="any"
                    value={row.unitPrice} onChange={e => updateRow(row.key,{unitPrice:e.target.value})}/></label>
                  <label>تخفیف واحد<input dir="ltr" type="number" min="0" step="any"
                    value={row.discount} onChange={e => updateRow(row.key,{discount:e.target.value})}/></label>
                  <label>نرخ VAT (%)<input dir="ltr" type="number" min="0" step="any"
                    value={row.vatRate} onChange={e => updateRow(row.key,{vatRate:e.target.value})}/></label>
                  <button type="button" className="identity-secondary"
                    onClick={() => setRows(current => current.filter(x => x.key !== row.key))}>حذف ردیف</button>
                </div>)}
              <p className="field-hint">برای مدیریت کاتالوگ به <Link href="/catalog">کالاها و خدمات</Link> مراجعه کنید.</p>
            </section>}
            {step === 2 && <section aria-label="جمع‌بندی صورتحساب">
              <p className="invoice-register__hint">مبالغ زیر فقط جمع‌بندی نمایشی ردیف‌های واردشده است؛ مالیات واقعی باید با قوانین و نرخ معتبر سرور محاسبه شود.</p>
              <dl className="invoice-register__totals">
                <dt>مبلغ ناخالص</dt><dd>{fmt(calculated.base)} ریال</dd>
                <dt>تخفیف</dt><dd>{fmt(calculated.discount)} ریال</dd>
                <dt>ارزش افزوده نمایشی</dt><dd>{fmt(calculated.vat)} ریال</dd>
                <dt>جمع نمایشی</dt><dd><strong>{fmt(calculated.total)} ریال</strong></dd>
              </dl>
              <div className="invoice-register__restriction">
                <strong>محدوده ثبت در نسخه فعلی</strong>
                <p>با تأیید نهایی فقط سربرگ اولیه (تاریخ، نام فروشنده و خریدار) ذخیره می‌شود.
                  {rows.length > 0 ? " ردیف‌های آماده‌شده و فوتر ذخیره نخواهند شد." : " برای این پیش‌نویس هنوز ردیفی ثبت نشده است."}
                  نوع/الگو و نرخ مالیات نیز ذخیره یا تأیید نمی‌شوند.</p>
                {!persistableHeader && <p role="alert">برای ذخیره سربرگ اولیه، نوع اول و الگوی فروش را انتخاب کنید.</p>}
              </div>
            </section>}
          </div>
          <footer className="invoice-register__dialog-footer">
            <button type="button" className="identity-secondary" onClick={close}
              disabled={busy}>انصراف</button>
            {step > 0 && <button type="button" className="identity-secondary"
              onClick={() => {setError("");setStep((step-1) as Step);}} disabled={busy}>مرحله قبل</button>}
            {step < 2 ? <button type="button" className="identity-primary"
              disabled={step === 0 && (!seller.trim() || !taxpayerId || !date)}
              onClick={() => {setError("");setStep((step+1) as Step);}}>مرحله بعد</button> :
              <button type="submit" className="identity-primary"
                disabled={busy || !persistableHeader || !seller.trim() || !taxpayerId}>
                {busy ? "در حال ثبت…" : "ذخیره فقط سربرگ پیش‌نویس"}</button>}
          </footer>
        </form>
      </div>
    </div>}
  </div>;
}
