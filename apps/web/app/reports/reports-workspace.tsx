"use client";

import { useEffect, useState, type FormEvent } from "react";
import { PersianDatePicker } from "../catalog/persian-date-picker";
import SearchableSelect from "../components/searchable-select";

type Filters = { customerId: string; period: string; from: string; to: string; subject: string; environment: string };
type Account = { customerUserId: string; isActive: boolean; personName?: string; companyName?: string; mobile?: string };
type Totals = {
  count: number; submittedCount: number; missingVatCount: number; missingTotalCount: number;
  totalRial: string; vatRial: string;
};
type Row = {
  id: string; subject: number; issueDateUtc: string; serial: number | null; taxId: string | null;
  seller: string | null; buyer: string | null; status: string; environment: string;
  totalRial: string | null; vatRial: string | null;
};
type Report = {
  customerName: string;
  period: { fromUtc: string; toUtcExclusive: string };
  totals: Totals; groups: { subject: number; totals: Totals }[]; items: Row[];
};
type View = "summary" | "details";

const subjects: Record<number, string> = { 1: "اصلی", 2: "اصلاحی", 3: "ابطالی", 4: "برگشت از فروش" };
const statuses: Record<string, string> = {
  registered: "ثبت داخلی", attempted: "ارسال بدون تأیید دریافت", submitted: "ارسال‌شده",
  accepted: "تأییدشده", rejected: "ردشده"
};
const environments: Record<string, string> = { production: "اصلی", sandbox: "آزمایشی", local: "داخلی" };
const periods = [
  { value: "7d", label: "۷ روز اخیر" }, { value: "30d", label: "۳۰ روز اخیر" },
  { value: "90d", label: "۹۰ روز اخیر" }, { value: "year", label: "سال شمسی جاری" },
  { value: "custom", label: "بازه دلخواه" }
];
const subjectOptions = [{ value: "all", label: "همه موضوع‌ها" },
  ...Object.entries(subjects).map(([value, label]) => ({ value, label }))];
const environmentOptions = [{ value: "all", label: "همه محیط‌ها" },
  ...Object.entries(environments).map(([value, label]) => ({ value, label }))];
const initial: Filters = { customerId: "", period: "30d", from: "", to: "", subject: "all", environment: "all" };
const pageSize = 25;
const date = new Intl.DateTimeFormat("fa-IR-u-ca-persian", { dateStyle: "short", timeZone: "Asia/Tehran" });
const count = new Intl.NumberFormat("fa-IR");

function money(value: string | null) {
  if (value === null) return "—";
  const [integer, fraction] = value.split(".");
  const sign = value.startsWith("-") ? "−" : "";
  const formatted = sign + count.format(BigInt(integer.replace(/^-/, "")));
  return fraction ? formatted + "٫" + fraction.replace(/[0-9]/g, digit => "۰۱۲۳۴۵۶۷۸۹"[Number(digit)]) : formatted;
}

function SummaryTable({ report }: { report: Report }) {
  const cells = (totals: Totals) => <>
    <td>{count.format(totals.count)}</td><td>{count.format(totals.submittedCount)}</td>
    <td>{money(totals.missingTotalCount > 0 && totals.missingTotalCount === totals.count ? null : totals.totalRial)}</td>
    <td>{money(totals.missingVatCount > 0 && totals.missingVatCount === totals.count ? null : totals.vatRial)}</td>
  </>;
  return <>
    <div className="reports-table-wrap"><table className="console-table reports-table">
      <thead><tr><th scope="col">موضوع</th><th scope="col">تعداد ثبت‌شده</th><th scope="col">دارای سابقه ارسال</th>
        <th scope="col">جمع مبلغ کل (ریال)</th><th scope="col">جمع ارزش افزوده (ریال)</th></tr></thead>
      <tbody>{report.groups.map(group => <tr key={group.subject}>
        <th scope="row">{subjects[group.subject]}</th>{cells(group.totals)}</tr>)}</tbody>
      <tfoot><tr><th scope="row">جمع کل</th>{cells(report.totals)}</tr></tfoot>
    </table></div>
    <p className="reports-footnote">مبالغ هر سند به همان شکل ثبت‌شده جمع شده‌اند. جمع جدول، خالص فروش یا مالیات پرداختنی نیست.</p>
    {(report.totals.missingVatCount > 0 || report.totals.missingTotalCount > 0) &&
      <p className="reports-footnote">تعداد سند بدون مبلغ کل: {count.format(report.totals.missingTotalCount)}. تعداد سند بدون ارزش افزوده: {count.format(report.totals.missingVatCount)}. جمع‌ها بر اساس مبالغ موجود هستند.</p>}
  </>;
}

function DetailTable({ rows, offset }: { rows: Row[]; offset: number }) {
  return <div className="reports-table-wrap"><table className="console-table reports-table reports-detail-table">
    <thead><tr>{["ردیف", "شماره فاکتور", "شماره منحصر به فرد مالیاتی", "تاریخ صدور", "موضوع",
      "وضعیت / محیط", "فروشنده", "خریدار", "مبلغ کل (ریال)", "ارزش افزوده (ریال)"]
      .map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead>
    <tbody>{rows.map((row, index) => <tr key={row.id}>
      <td>{count.format(offset + index + 1)}</td><td>{row.serial === null ? "—" : count.format(row.serial)}</td>
      <td><bdi dir="ltr">{row.taxId ?? "—"}</bdi></td>
      <td>{date.format(new Date(row.issueDateUtc))}</td><td>{subjects[row.subject]}</td>
      <td>{statuses[row.status]}<small>{environments[row.environment]}</small></td>
      <td>{row.seller ?? "—"}</td><td>{row.buyer ?? "—"}</td>
      <td>{money(row.totalRial)}</td><td>{money(row.vatRial)}</td>
    </tr>)}
    {!rows.length && <tr><td colSpan={10}>صورتحسابی در این بازه پیدا نشد.</td></tr>}</tbody>
  </table></div>;
}

export default function ReportsWorkspace({ isAdmin }: { isAdmin: boolean }) {
  const [filters, setFilters] = useState(initial);
  const [applied, setApplied] = useState<Filters | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [accountsLoading, setAccountsLoading] = useState(isAdmin);
  const [accountsError, setAccountsError] = useState("");
  const [report, setReport] = useState<Report | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [view, setView] = useState<View>("summary");
  const [preview, setPreview] = useState(false);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!isAdmin) return;
    const controller = new AbortController();
    fetch("/api/customer-owners/accounts", { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error();
        const data = await response.json() as Account[];
        if (!controller.signal.aborted) setAccounts(data.filter(account => account.isActive));
      })
      .catch(() => {
        if (!controller.signal.aborted) setAccountsError("دریافت فهرست کاربران ممکن نشد.");
      })
      .finally(() => { if (!controller.signal.aborted) setAccountsLoading(false); });
    return () => controller.abort();
  }, [isAdmin]);

  useEffect(() => {
    if (!applied) return;
    const controller = new AbortController();
    const query = new URLSearchParams({ period: applied.period });
    if (applied.customerId) query.set("customerId", applied.customerId);
    if (applied.period === "custom") {
      query.set("from", applied.from);
      query.set("to", applied.to);
    }
    if (applied.subject !== "all") query.set("subject", applied.subject);
    if (applied.environment !== "all") query.set("environment", applied.environment);
    fetch("/api/reports/invoices?" + query, { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (response.status === 401) throw new Error("نشست شما منقضی شده است.");
        if ([403, 404].includes(response.status)) throw new Error("دسترسی به گزارش این کاربر ممکن نیست.");
        if (response.status === 400) throw new Error("فیلترها یا بازه تاریخ معتبر نیستند.");
        if (!response.ok) throw new Error("دریافت گزارش ممکن نشد. دوباره تلاش کنید.");
        const data = await response.json() as Report;
        if (!controller.signal.aborted) setReport(data);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "دریافت گزارش ممکن نشد.");
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [applied]);

  function change(key: keyof Filters, value: string) {
    setFilters(current => ({ ...current, [key]: value }));
    setError("");
    if (key === "customerId") {
      setApplied(null);
      setReport(null);
      setLoading(false);
      setPreview(false);
    }
  }

  function generate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isAdmin && !filters.customerId) return;
    if (filters.period === "custom" && (!filters.from || !filters.to || filters.from > filters.to ||
      event.currentTarget.querySelector(".ui-persian-date__input[aria-invalid=true]"))) {
      setError("تاریخ ابتدا و انتهای بازه را به‌درستی وارد کنید.");
      return;
    }
    setError("");
    setReport(null);
    setPreview(false);
    setPage(1);
    setLoading(true);
    setApplied({ ...filters });
  }

  const accountOptions = accounts.map(account => ({
    value: account.customerUserId,
    label: account.companyName?.trim() || account.personName?.trim() || account.mobile || account.customerUserId,
    description: account.mobile
  }));
  const title = view === "summary" ? "خلاصه صورتحساب‌ها" : "گزارش تفصیلی صورتحساب‌ها";
  const pageCount = Math.ceil((report?.items.length ?? 0) / pageSize);
  const offset = (page - 1) * pageSize;

  return <section className="reports-workspace" data-preview={preview}>
    <form className="reports-toolbar reports-no-print" onSubmit={generate}>
      {isAdmin && <div className="reports-filter"><label htmlFor="reports-customer">کاربر نرم‌افزار</label>
        <SearchableSelect id="reports-customer" value={filters.customerId} options={accountOptions} required
          onChange={value => change("customerId", value)} loading={accountsLoading} error={accountsError}
          placeholder="کاربر را انتخاب کنید…" emptyMessage="کاربر مجازی پیدا نشد." /></div>}
      <div className="reports-filter"><label htmlFor="reports-period">بازه زمانی</label>
        <SearchableSelect id="reports-period" value={filters.period} options={periods} clearable={false}
          onChange={value => change("period", value || initial.period)} /></div>
      {filters.period === "custom" && <div className="reports-date-range">
        <div><label htmlFor="reports-from">از تاریخ</label>
          <PersianDatePicker id="reports-from" value={filters.from} onChange={value => change("from", value)} floating /></div>
        <div><label htmlFor="reports-to">تا تاریخ</label>
          <PersianDatePicker id="reports-to" value={filters.to} onChange={value => change("to", value)} floating /></div>
      </div>}
      <div className="reports-filter"><label htmlFor="reports-subject">موضوع صورتحساب</label>
        <SearchableSelect id="reports-subject" value={filters.subject} options={subjectOptions}
          onChange={value => change("subject", value || "all")} /></div>
      <div className="reports-filter"><label htmlFor="reports-environment">محیط</label>
        <SearchableSelect id="reports-environment" value={filters.environment} options={environmentOptions}
          onChange={value => change("environment", value || "all")} /></div>
      <button type="submit" className="identity-primary" disabled={loading || (isAdmin && !filters.customerId)}>
        {loading ? "در حال دریافت…" : "تهیه گزارش"}</button>
    </form>

    {error && <p className="reports-error reports-no-print" role="alert">{error}</p>}
    {!report && !error && <p className="reports-empty" role="status">{loading ? "در حال دریافت گزارش…" :
      isAdmin && !filters.customerId ? "برای گزارش‌گیری ابتدا کاربر نرم‌افزار را انتخاب کنید." : "برای مشاهده گزارش، دکمه تهیه گزارش را بزنید."}</p>}

    {report && applied && <>
      <div className="reports-actions reports-no-print">
        <div className="reports-tabs" role="group" aria-label="نوع گزارش">
          <button type="button" aria-pressed={view === "summary"} onClick={() => setView("summary")}>خلاصه</button>
          <button type="button" aria-pressed={view === "details"} onClick={() => setView("details")}>تفصیلی</button>
        </div>
        <div className="reports-tabs">
          <button type="button" aria-pressed={preview} onClick={() => setPreview(!preview)}>
            {preview ? "بستن پیش‌نمایش" : "پیش‌نمایش چاپ"}</button>
          {preview && <button type="button" className="identity-primary" onClick={() => window.print()}>چاپ A4</button>}
        </div>
      </div>
      <article className="reports-sheet" data-print-layout={view === "summary" ? "portrait" : "landscape"}>
        <header className="reports-sheet-heading">
          <h2>{title}</h2>
          <p>{report.customerName} · {date.format(new Date(report.period.fromUtc))} تا {date.format(new Date(Date.parse(report.period.toUtcExclusive) - 1))}</p>
          <p>{subjectOptions.find(option => option.value === applied.subject)?.label} · {environmentOptions.find(option => option.value === applied.environment)?.label}</p>
        </header>
        {view === "summary" ? <SummaryTable report={report} /> :
          <DetailTable rows={preview ? report.items : report.items.slice(offset, offset + pageSize)}
            offset={preview ? 0 : offset} />}
      </article>
      {view === "details" && !preview && pageCount > 1 && <div className="reports-pagination reports-no-print">
        <button type="button" className="console-row-action" disabled={page === 1} onClick={() => setPage(page - 1)}>قبلی</button>
        <span>صفحه {count.format(page)} از {count.format(pageCount)}</span>
        <button type="button" className="console-row-action" disabled={page === pageCount} onClick={() => setPage(page + 1)}>بعدی</button>
      </div>}
    </>}
  </section>;
}
