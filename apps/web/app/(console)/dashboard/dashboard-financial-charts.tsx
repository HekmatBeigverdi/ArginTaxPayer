"use client";

import { useEffect, useId, useMemo, useState } from "react";

type Period = string;
type Role = "Level1" | "Level2";
type Status = "loading" | "ready" | "error";

type Bucket = {
  date: string;
  salesGrossIrr: number;
  salesOutstandingForCohortIrr: number;
  collectionsInPeriodIrr: number;
  partnerEarnedShareIrr: number;
  rootEarnedShareIrr?: number;
  partnerSettlementNetIrr: number;
};

type Trends = {
  schemaVersion: 1;
  role: Role;
  currency: "IRR";
  buckets: Bucket[];
};

type Metric =
  | "salesGrossIrr"
  | "collectionsInPeriodIrr"
  | "salesOutstandingForCohortIrr"
  | "partnerEarnedShareIrr"
  | "partnerSettlementNetIrr"
  | "rootEarnedShareIrr";

type FinancialChartsProps = {
  role: Role;
  period: Period;
};

const metricLabels: Record<Metric, string> = {
  salesGrossIrr: "فروش اشتراک",
  collectionsInPeriodIrr: "وصول وجه",
  salesOutstandingForCohortIrr: "مانده فروش‌های دوره",
  partnerEarnedShareIrr: "سهم تخصیص‌یافته همکار",
  partnerSettlementNetIrr: "تسویه خالص همکاران",
  rootEarnedShareIrr: "سهم مدیر اصلی",
};

const sharedMetrics: Metric[] = [
  "collectionsInPeriodIrr",
  "salesGrossIrr",
  "salesOutstandingForCohortIrr",
  "partnerEarnedShareIrr",
  "partnerSettlementNetIrr",
];

function formatMoney(value: number) {
  return new Intl.NumberFormat("fa-IR").format(value) + " ریال";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("fa-IR-u-ca-persian", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "Asia/Tehran",
  }).format(new Date(value + "T12:00:00Z"));
}

function buildChartPoints(items: Bucket[], metric: Metric) {
  if (!items.length) return "";

  // Include zero and negative settlement values caused by reversals.
  const values = items.map(item => item[metric] ?? 0);
  const minimum = Math.min(0, ...values);
  const maximum = Math.max(0, ...values);
  const range = Math.max(1, maximum - minimum);

  return items.map((item, index) => {
    const x = items.length === 1 ? 180 : 12 + (336 * index / (items.length - 1));
    const value = item[metric] ?? 0;
    const y = 154 - (value - minimum) / range * 130;
    const boundedY = Math.max(12, Math.min(154, y));
    return x.toFixed(1) + "," + boundedY.toFixed(1);
  }).join(" ");
}

function ChartSkeleton() {
  return (
    <div
      className="argin-dashboard__finance-skeleton"
      role="status"
      aria-label="در حال دریافت نمودار مالی"
      aria-busy="true"
    >
      <svg viewBox="0 0 360 160" width="100%" aria-hidden="true" focusable="false">
        {[30, 70, 110, 150].map(y => (
          <line key={y} x1="10" x2="350" y1={y} y2={y} stroke="#e6eff0" strokeDasharray="5 6" />
        ))}
        <path
          className="argin-dashboard__skeleton-chart-line"
          d="M 10 138 L 64 116 L 118 125 L 172 68 L 226 84 L 280 34 L 350 54"
          fill="none"
          stroke="#c9e1de"
          strokeWidth="8"
          strokeLinecap="round"
        />
      </svg>
    </div>
  );
}

function FinanceLineChart({ items, metric }: { items: Bucket[]; metric: Metric }) {
  const titleId = useId().replace(/:/g, "");
  const points = useMemo(() => buildChartPoints(items, metric), [items, metric]);

  if (!items.length) {
    return <p className="argin-dashboard__empty">در این بازه هیچ رویداد مالی ثبت نشده است.</p>;
  }

  const firstBucket = items[0];
  const lastBucket = items[items.length - 1];

  return (
    <>
      <div className="argin-dashboard__finance-graph">
        <svg
          viewBox="0 0 360 170"
          width="100%"
          role="img"
          aria-labelledby={titleId}
          preserveAspectRatio="none"
        >
          <title id={titleId}>نمودار {metricLabels[metric]} بر اساس تاریخ رویداد در تهران</title>
          {[24, 88, 154].map(y => (
            <line key={y} x1="12" x2="348" y1={y} y2={y} stroke="#dbe9ec" strokeDasharray="4 6" />
          ))}
          <polyline
            points={points}
            fill="none"
            stroke="#177f81"
            strokeWidth="3"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        <div className="argin-dashboard__finance-axis">
          <span>{formatDate(firstBucket.date)}</span>
          <span>{formatDate(lastBucket.date)}</span>
        </div>
      </div>
      <div className="argin-dashboard__table-wrap">
        <table className="argin-dashboard__table">
          <caption>جدول جایگزین نمودار {metricLabels[metric]}، آخرین هفت تاریخ دارای رویداد</caption>
          <thead>
            <tr>
              <th scope="col">تاریخ</th>
              <th scope="col">مبلغ (ریال)</th>
            </tr>
          </thead>
          <tbody>
            {items.slice(-7).map(item => (
              <tr key={item.date}>
                <td>{formatDate(item.date)}</td>
                <td>{formatMoney(item[metric] ?? 0)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export default function DashboardFinancialCharts({ role, period }: FinancialChartsProps) {
  const [status, setStatus] = useState<Status>("loading");
  const [data, setData] = useState<Trends | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [metric, setMetric] = useState<Metric>("collectionsInPeriodIrr");

  useEffect(() => {
    const controller = new AbortController();
    setStatus("loading");
    setData(null);

    fetch("/api/dashboard/financial-trends?period=" + period, {
      cache: "no-store",
      signal: controller.signal,
    })
      .then(async response => {
        if (!response.ok) throw new Error("financial_trends_unavailable");

        const result = await response.json() as Trends;
        const isValidResponse = (
          result.schemaVersion === 1 &&
          result.role === role &&
          result.currency === "IRR" &&
          Array.isArray(result.buckets)
        );
        if (!isValidResponse) throw new Error("invalid_financial_trends");

        if (!controller.signal.aborted) {
          setData(result);
          setStatus("ready");
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setData(null);
          setStatus("error");
        }
      });

    return () => controller.abort();
  }, [role, period, attempt]);

  const metrics: Metric[] = role === "Level1"
    ? [...sharedMetrics, "rootEarnedShareIrr"]
    : sharedMetrics;

  function renderChart() {
    if (status === "loading") return <ChartSkeleton />;
    if (status === "error") {
      return (
        <div className="argin-dashboard__error" role="alert">
          دریافت نمودار مالی ممکن نشد.
          <button
            type="button"
            className="argin-dashboard__retry"
            onClick={() => setAttempt(current => current + 1)}
          >
            تلاش مجدد
          </button>
        </div>
      );
    }
    return data && <FinanceLineChart items={data.buckets} metric={metric} />;
  }

  return (
    <section
      className="argin-dashboard__card argin-dashboard__finance-section"
      aria-label="روند مالی اشتراک‌ها"
    >
      <header className="argin-dashboard__finance-heading">
        <div>
          <h2 className="argin-dashboard__section-title">روند مالی اشتراک‌ها و تسویه</h2>
          <p>مبالغ واقعی به ریال و بر اساس تاریخ رویداد به وقت تهران</p>
        </div>
        <div className="argin-dashboard__finance-tabs" role="group" aria-label="انتخاب شاخص نمودار">
          {metrics.map(availableMetric => (
            <button
              key={availableMetric}
              type="button"
              aria-pressed={metric === availableMetric}
              onClick={() => setMetric(availableMetric)}
            >
              {metricLabels[availableMetric]}
            </button>
          ))}
        </div>
      </header>

      {renderChart()}

      <p className="argin-dashboard__kpi-note">
        فروش: تاریخ صدور سفارش؛ وصول: تاریخ دریافت تأییدشده؛ سهم: تاریخ تخصیص؛ تسویه: تاریخ ثبت پرداخت یا برگشت.
        مانده فروش، وضعیت فعلی سفارش‌های صادرشده در هر روز است و جریان نقدی روزانه نیست.
        تاریخ‌هایی که رویدادی نداشته‌اند در نمودار نقطه ندارند.
      </p>
    </section>
  );
}
