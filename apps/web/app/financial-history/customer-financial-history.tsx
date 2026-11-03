"use client";

import { useEffect, useMemo, useState } from "react";
import ConsoleLoader from "../components/console-loader";

type History = {
  orderId: string;
  planCode: string;
  planKind: number;
  invoiceUnitLimit: number|null;
  amountIrr: number;
  collectedIrr: number;
  status: number;
  wasRejected: boolean;
  createdAtUtc: string;
  invoiceNumber: string;
  subscriptionId: string | null;
  expiresAtUtc: string | null;
};

const number = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const amount = (value: number) => `${number(value)} ریال`;
const date = (value: string) => new Date(value).toLocaleDateString("fa-IR");
const planNames:Record<number,string>={1:"یک‌ساله",3:"یک‌ماهه",4:"سه‌ماهه",5:"شش‌ماهه"};
const planName = (row: History) => row.planKind===2 && row.invoiceUnitLimit!==null
  ? `بسته ${number(row.invoiceUnitLimit)} تایی`
  : planNames[row.planKind]??"پلن نرم‌افزار";

function statusOf(row: History) {
  if (row.wasRejected) return { label: "رد و لغو شده", style: "is-void" };
  if (row.subscriptionId) return { label: "فعال‌شده", style: "is-active" };
  if (row.status === 3) return { label: "پرداخت‌شده، در انتظار فعال‌سازی", style: "is-waiting" };
  if (row.status === 4) return { label: "باطل‌شده", style: "is-void" };
  return { label: "در انتظار پرداخت", style: "is-pending" };
}

export default function CustomerFinancialHistory() {
  const [rows, setRows] = useState<History[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/customer-finance", { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error("دریافت گزارش مالی ناموفق بود (HTTP " + response.status + ")");
        setRows(await response.json() as History[]);
      })
      .catch(reason => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "دریافت گزارش مالی ناموفق بود.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);

  const filtered = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    if (!normalizedQuery) return rows;
    return rows.filter(row => (row.planCode + " " + row.invoiceNumber)
      .toLocaleLowerCase().includes(normalizedQuery));
  }, [rows, query]);

  return <section className="customer-financial-history" dir="rtl">
    <div className="customer-financial-history__heading">
      <div>
        <h2>سوابق مالی من</h2>
        <p>سفارش‌ها، پرداخت‌ها و وضعیت اشتراک حساب شما</p>
      </div>
      {!loading && <span className="customer-financial-history__count">{number(filtered.length)} رکورد</span>}
    </div>

    <div className="customer-financial-history__toolbar">
      <label className="console-filter-search">
        جست‌وجو
        <input aria-label="جستجوی سوابق مالی" placeholder="نام پلن یا شماره صورتحساب…"
          value={query} onChange={event => setQuery(event.target.value)}/>
      </label>
    </div>

    {error && <p className="customer-financial-history__error" role="alert">{error}</p>}

    <div className="console-table-stage" aria-busy={loading}>
      {loading && <ConsoleLoader full label="در حال دریافت سوابق مالی…"/>}
      <div className="console-table-scroll customer-financial-history__table-wrap">
        <table className="console-table customer-financial-history__table">
          <thead>
            <tr>
              <th scope="col">تاریخ</th>
              <th scope="col">پلن</th>
              <th scope="col">صورتحساب</th>
              <th scope="col">مبلغ سفارش</th>
              <th scope="col">وصول‌شده</th>
              <th scope="col">وضعیت</th>
              <th scope="col">اعتبار تا</th>
            </tr>
          </thead>
          <tbody>
            {!loading && !error && filtered.length === 0 && <tr>
              <td colSpan={7} className="customer-financial-history__empty">
                {query.trim() ? "رکوردی با این جست‌وجو پیدا نشد." : "سابقه‌ای برای نمایش وجود ندارد."}
              </td>
            </tr>}
            {filtered.map(row => {
              const status = statusOf(row);
              return <tr key={row.orderId}>
                <td>{date(row.createdAtUtc)}</td>
                <td>{planName(row)}</td>
                <td dir="ltr"><bdi>{row.invoiceNumber}</bdi></td>
                <td>{amount(row.amountIrr)}</td>
                <td>{amount(row.collectedIrr)}</td>
                <td><span className={`customer-financial-history__status ${status.style}`}>{status.label}</span></td>
                <td>{row.expiresAtUtc ? date(row.expiresAtUtc) : "—"}</td>
              </tr>;
            })}
          </tbody>
        </table>
      </div>
    </div>

    <footer className="customer-financial-history__footer">
      <span>{number(filtered.length)} از {number(rows.length)} سفارش</span>
      <p>این گزارش فقط سفارش‌های ثبت‌شده برای حساب شما را نمایش می‌دهد.</p>
    </footer>
  </section>;
}
