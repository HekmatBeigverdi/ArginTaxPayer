"use client";

import Link from "next/link";
import PartnerDashboardDetails from "./partner-dashboard-details";
import RootDashboardDetails from "./root-dashboard-details";
import DashboardFinancialCharts from "./dashboard-financial-charts";
import DashboardSubscriptionWatch from "./dashboard-subscription-watch";
import DashboardActionCenter from "./dashboard-action-center";

import DashboardCalendar from "./dashboard-calendar";
import DashboardIcon from "../../components/dashboard-icon";
import { useEffect, useId, useMemo, useState, type ReactNode, type FormEvent } from "react";

type Role = "Level1" | "Level2" | "Customer";
type Period = "7d" | "30d" | "90d" | "year" | "custom";
type CustomRange = {from:string;to:string};
type RemoteState = "loading" | "ready" | "error";

type InvoiceStatus = {
  logicalInvoiceCount: number;
  accepted: number;
  acceptedCancellation: number;
  rejected: number;
  processing: number;
  awaitingInquiry: number;
  unknown: number;
  prepared: number;
  attention: number;
  unclassified: number;
  successRatePercent: number | null;
};

type Subscription = {
  planName: string;
  hasActivePlan: boolean;
  remainingInvoices: number | null;
  remainingDays: number;
  expiresAtUtc: string | null;
};

type Overview = {
  schemaVersion: number;
  role: Role;
  asOfUtc: string;
  invoiceStatus: InvoiceStatus;
  invoiceDrafts: number;
  subscription: Subscription | null;
};

type Trends = {
  schemaVersion: number;
  buckets: {
    date: string;
    issued: number;
    acceptedByIssueDate: number;
  }[];
};

type Finance = {
  schemaVersion: number;
  role: "Level1" | "Level2";
  currency: "IRR";
  salesGrossIrr: number;
  salesCollectedForCohortIrr: number;
  salesOutstandingIrr: number;
  collectionsInPeriodIrr: number;
  partnerEarnedShareIrr: number;
  rootEarnedShareIrr?: number;
  partnerSettlementNetIrr: number;
};

type AttentionItem = {
  invoiceId: string;
  taxpayerId: string;
  status: string;
  issuedAtUtc: string;
  link: string;
};

type AttentionResponse = { items: AttentionItem[] };

type KpiCardProps = {
  label: string;
  value: string;
  note: string;
  tone?: "success" | "warning" | "danger";
};

const periodNames: Record<Period, string> = {
  "7d": "۷ روز",
  "30d": "۳۰ روز",
  "90d": "۹۰ روز",
  year: "سال جاری",
  custom: "بازه دلخواه",
};

const dashboardTitles: Record<Role, string> = {
  Level1: "نمای مدیریتی کل سامانه",
  Level2: "عملکرد مشتریان زیرمجموعه",
  Customer: "وضعیت صورتحساب‌های شما",
};

const statusLabels: Record<string, string> = {
  unknown: "نتیجه نامشخص",
  rejected: "ردشده",
  attention: "نیازمند بررسی",
  accepted: "تأییدشده",
  prepared: "آماده ارسال",
  awaitingInquiry: "در انتظار استعلام",
  processing: "در حال ارسال",
  unclassified: "تعیین‌نشده",
};

const stateMessages = {
  loading: "در حال دریافت آمار واقعی...",
  error: "دریافت آمار ممکن نشد؛ مقادیر قدیمی یا ساختگی نمایش داده نمی‌شود.",
  empty: "برای این بازه زمانی اطلاعاتی ثبت نشده است.",
};

function formatNumber(value: number) {
  return new Intl.NumberFormat("fa-IR").format(value);
}

function formatMoney(value: number) {
  return formatNumber(value) + " ریال";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("fa-IR-u-ca-persian", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "Asia/Tehran",
  }).format(new Date(value + "T12:00:00Z"));
}

function formatLastUpdated(value: string) {
  return new Intl.DateTimeFormat("fa-IR", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "Asia/Tehran",
  }).format(new Date(value));
}

async function query(section: string, period: Period, signal: AbortSignal, range?: CustomRange | null): Promise<unknown> {
  const suffix=period==="custom"&&range ? "&from="+range.from+"&to="+range.to : "";
  const response = await fetch("/api/dashboard/" + section + "?period=" + period + suffix, {
    cache: "no-store",
    signal,
  });

  if (!response.ok) throw new Error("dashboard_unavailable");
  return response.json();
}

function buildKpiCards(role: Role, overview: Overview | null, finance: Finance | null): KpiCardProps[] {
  if (role !== "Customer" && finance) {
    const earnedShare = role === "Level1"
      ? finance.rootEarnedShareIrr
      : finance.partnerEarnedShareIrr;

    return [
      {
        label: "فروش اشتراک",
        value: formatMoney(finance.salesGrossIrr),
        note: "مبلغ سفارش‌های صادرشده در بازه",
      },
      {
        label: "دریافتی تأییدشده",
        value: formatMoney(finance.collectionsInPeriodIrr),
        note: "دریافت‌های ثبت‌شده در بازه",
        tone: "success",
      },
      {
        label: "مطالبات فروش",
        value: formatMoney(finance.salesOutstandingIrr),
        note: "مانده سفارش‌های همین بازه",
      },
      {
        label: role === "Level1" ? "سهم مدیر سطح ۱" : "سهم همکاری شما",
        value: role === "Level1" && earnedShare === undefined ? "نامشخص" : formatMoney(earnedShare!),
        note: "سهم ثبت‌شده در بازه",
      },
    ];
  }

  if (role !== "Customer" || !overview) return [];

  const status = overview.invoiceStatus;
  const acceptedCount = status.accepted + status.acceptedCancellation;
  const followUpCount = status.unknown + status.attention + status.rejected;

  return [
    {
      label: "صورتحساب‌ها",
      value: formatNumber(status.logicalInvoiceCount),
      note: "زنجیره‌های یکتا در بازه",
    },
    {
      label: "تأییدشده توسط سازمان",
      value: formatNumber(acceptedCount),
      note: "فقط وضعیت قطعی",
      tone: "success",
    },
    {
      label: "نامشخص / نیازمند بررسی",
      value: formatNumber(followUpCount),
      note: "بدون ارسال مجدد خودکار",
      tone: "warning",
    },
    {
      label: "پیش‌نویس‌های فعال",
      value: formatNumber(overview.invoiceDrafts),
      note: "پیش‌نویس‌های تبدیل‌نشده در بازه",
    },
  ];
}

function DashboardKpiCard({ label, value, note, tone, selected, onSelect }: KpiCardProps & {
  selected?: boolean; onSelect?: () => void;
}) {
  return (
    <article className="argin-dashboard__card argin-dashboard__kpi-interactive"
      aria-label={label} data-selected={selected}>
      <button type="button" className="argin-dashboard__kpi-button" aria-expanded={Boolean(selected)}
        aria-label={label+"، نمایش جزئیات"} onClick={onSelect}>
        <span className="argin-dashboard__kpi-label">{label}</span>
        <strong className="argin-dashboard__kpi-value" data-tone={tone}>{value}</strong>
        <span className="argin-dashboard__kpi-note">{note}</span>
        <span className="argin-dashboard__kpi-hint">مشاهده جزئیات ←</span>
      </button>
    </article>
  );
}

function DashboardKpiDetails({role,index,overview,finance,onClose}:{
  role:Role;index:number;overview:Overview|null;finance:Finance|null;onClose:()=>void;
}){
  // All displayed amounts come from the authenticated read-only API, not chart samples.
  const admin=role!=="Customer";
  const sections=admin&&finance?[
    {heading:"فروش اشتراک",note:"مبلغ سفارش‌های صادرشده در بازه؛ مبلغ دریافتی ممکن است متفاوت باشد.",
      rows:[["ارزش سفارش‌های دوره",formatMoney(finance.salesGrossIrr)],["وصول سفارش‌های همین دوره تا امروز",formatMoney(finance.salesCollectedForCohortIrr)],["مانده فعلی سفارش‌ها",formatMoney(finance.salesOutstandingIrr)]]},
    {heading:"دریافتی تأییدشده",note:"مبلغ وجه تأییدشده در بازه، شامل پرداخت سفارش‌های قدیمی‌تر.",
      rows:[["دریافتی دوره",formatMoney(finance.collectionsInPeriodIrr)],["تسویه خالص همکاران",formatMoney(finance.partnerSettlementNetIrr)]]},
    {heading:"مطالبات فروش",note:"مانده سفارش‌های صادرشده در بازه انتخاب‌شده، نه تمام مطالبات تاریخی.",
      rows:[["فروش دوره",formatMoney(finance.salesGrossIrr)],["مانده سفارش‌ها",formatMoney(finance.salesOutstandingIrr)]]},
    {heading:role==="Level1"?"سهم مدیر اصلی":"سهم همکاری",note:"سهم تخصیص‌یافته در بازه؛ معادل سود حسابداری نیست.",
      rows:[["سهم دوره",role==="Level1"&&finance.rootEarnedShareIrr==null?"نامشخص":formatMoney(role==="Level1"?finance.rootEarnedShareIrr!:finance.partnerEarnedShareIrr)],
        ["تسویه خالص دوره",formatMoney(finance.partnerSettlementNetIrr)]]}
  ]:overview?[
    {heading:"صورتحساب‌ها",note:"هر زنجیره قانونی فقط یک بار در آمار حساب می‌شود.",
      rows:[["زنجیره‌های یکتا",formatNumber(overview.invoiceStatus.logicalInvoiceCount)],["پیش‌نویس‌ها",formatNumber(overview.invoiceDrafts)]]},
    {heading:"تأییدهای سازمان",note:"تنها نتیجه قطعی رسمی، نه رسید اولیه ارسال.",
      rows:[["تأییدشده",formatNumber(overview.invoiceStatus.accepted)],["ابطال تأییدشده",formatNumber(overview.invoiceStatus.acceptedCancellation)]]},
    {heading:"نیازمند پیگیری",note:"نتیجه نامشخص به معنی مجوز ارسال مجدد نیست.",
      rows:[["نامشخص",formatNumber(overview.invoiceStatus.unknown)],["ردشده",formatNumber(overview.invoiceStatus.rejected)],["نیازمند بررسی",formatNumber(overview.invoiceStatus.attention)]]},
    {heading:"پیش‌نویس‌ها",note:"اسناد پیش‌نویس داخلی، بدون ارسال خودکار.",
      rows:[["پیش‌نویس‌های فعال",formatNumber(overview.invoiceDrafts)]]}
  ]:[];
  const selected=sections[index];
  if(!selected)return null;
  return <section className="argin-dashboard__kpi-detail" aria-label={"جزئیات "+selected.heading}>
    <div className="argin-dashboard__kpi-detail-head">
      <div><h2>{selected.heading}</h2><p>{selected.note}</p></div>
      <button type="button" aria-label="بستن جزئیات شاخص" onClick={onClose}>بستن ×</button>
    </div>
    <dl>{selected.rows.map(([label,value])=><div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
    <Link className="argin-dashboard__more" href={admin?"/subscriptions":"/invoices"}>
      {admin?"ورود به مدیریت اشتراک‌ها و مالی":"ورود به مدیریت صورتحساب‌ها"}
    </Link>
  </section>;
}

function DashboardPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="argin-dashboard__card" aria-label={title}>
      <h2 className="argin-dashboard__section-title">{title}</h2>
      {children}
    </section>
  );
}

function DashboardState({ kind, onRetry }: {
  kind: "loading" | "error" | "empty";
  onRetry?: () => void;
}) {
  return (
    <div
      className={"argin-dashboard__" + kind}
      role={kind === "error" ? "alert" : "status"}
      aria-live="polite"
    >
      {stateMessages[kind]}{" "}
      {kind === "error" && onRetry && (
        <button type="button" onClick={onRetry} className="argin-dashboard__retry">
          تلاش مجدد
        </button>
      )}
    </div>
  );
}

/** Keep each region's geometry while its own request is loading. */
function DashboardSkeleton({ variant = "lines", rows = 3 }: {
  variant?: "lines" | "chart" | "list";
  rows?: number;
}) {
  const gradientId = useId().replace(/:/g, "");
  const height = variant === "chart" ? 176 : variant === "list" ? Math.max(80, rows * 37) : 74;

  return (
    <div
      className="argin-dashboard__skeleton"
      role="status"
      aria-label="در حال دریافت اطلاعات از سرور"
      aria-busy="true"
    >
      <svg viewBox={`0 0 400 ${height}`} width="100%" aria-hidden="true" focusable="false" preserveAspectRatio="none">
        <defs>
          <linearGradient id={gradientId} x1="0" x2="1">
            <stop offset="0%" stopColor="#e9f0f2" />
            <stop offset="48%" stopColor="#d6e9e7" />
            <stop offset="100%" stopColor="#e9f0f2" />
          </linearGradient>
        </defs>
        {variant === "chart" ? (
          <>
            {[40, 88, 136].map(y => (
              <line key={y} x1="8" x2="392" y1={y} y2={y} stroke="#e8eef0" strokeDasharray="5 6" />
            ))}
            <path
              className="argin-dashboard__skeleton-chart-line"
              d="M 12 138 L 60 114 L 110 126 L 155 68 L 205 90 L 255 42 L 310 68 L 388 22"
              fill="none" stroke={`url(#${gradientId})`} strokeWidth="9"
              strokeLinecap="round" strokeLinejoin="round"
            />
          </>
        ) : Array.from({ length: rows }, (_, index) => {
          const y = variant === "list" ? 5 + index * 37 : 9 + index * 24;
          const width = [236, 342, 175][index % 3];

          return (
            <g key={index}>
              <rect
                className="argin-dashboard__skeleton-bar"
                x="8" y={y} rx="7" width={width} height="13" fill={`url(#${gradientId})`}
              />
              {variant === "list" && (
                <rect
                  className="argin-dashboard__skeleton-bar"
                  x="330" y={y} rx="7" width="61" height="13" fill={`url(#${gradientId})`}
                />
              )}
            </g>
          );
        })}
      </svg>
      <span className="argin-dashboard__sr-only">در حال دریافت اطلاعات از سرور</span>
    </div>
  );
}

function DashboardKpiSkeleton() {
  return (
    <section aria-label="در حال دریافت شاخص‌ها" className="argin-dashboard__kpis" aria-busy="true">
      {Array.from({ length: 4 }, (_, index) => (
        <article key={index} className="argin-dashboard__card">
          <DashboardSkeleton rows={3} />
        </article>
      ))}
    </section>
  );
}

function DashboardPeriodFilter({ period, setPeriod, onCustom }: {
  period: Period;
  setPeriod: (period: Period) => void;
  onCustom: (from:string,to:string) => void;
}) {
  const [from,setFrom]=useState("");
  const [to,setTo]=useState("");
  const [message,setMessage]=useState("");
  // API custom date inputs use ISO Gregorian YYYY-MM-DD; summaries remain Jalali.
  const today=useMemo(()=>{
    const parts=new Intl.DateTimeFormat("en-GB",{timeZone:"Asia/Tehran",year:"numeric",
      month:"2-digit",day:"2-digit"}).formatToParts(new Date());
    const get=(key:string)=>parts.find(p=>p.type===key)?.value??"";
    return get("year")+"-"+get("month")+"-"+get("day");
  },[]);
  const apply=(event:FormEvent<HTMLFormElement>)=>{
    event.preventDefault();
    const iso=/^\d{4}-\d{2}-\d{2}$/;
    const start=Date.parse(from+"T00:00:00Z"),end=Date.parse(to+"T00:00:00Z");
    if(!iso.test(from)||!iso.test(to)||!Number.isFinite(start)||!Number.isFinite(end)||
       new Date(start).toISOString().slice(0,10)!==from||
       new Date(end).toISOString().slice(0,10)!==to||start>end||to>today||
       (end-start)/86400000>365){
      setMessage("بازه معتبر انتخاب کنید: حداکثر ۳۶۶ روز، بدون تاریخ آینده.");
      return;
    }
    setMessage("");
    onCustom(from,to);
  };
  return <div className="argin-dashboard__period-box">
    <div className="argin-dashboard__filters" role="group" aria-label="بازه زمانی آمار">
      {(["7d","30d","90d","year"] as Period[]).map(key=>(
        <button type="button" key={key} aria-pressed={period===key}
          onClick={()=>setPeriod(key)}>{periodNames[key]}</button>
      ))}
    </div>
    <form onSubmit={apply} className="argin-dashboard__date-range" aria-label="بازه تاریخی سفارشی">
      <label>از (میلادی)
        <input type="date" value={from} max={today} onChange={event=>setFrom(event.target.value)} required/>
      </label>
      <label>تا (میلادی)
        <input type="date" value={to} max={today} onChange={event=>setTo(event.target.value)} required/>
      </label>
      <button type="submit" aria-pressed={period==="custom"}>اعمال بازه</button>
      {message&&<span className="argin-dashboard__date-error" role="alert">{message}</span>}
    </form>
  </div>;
}

function DashboardTrendChart({ data }: { data: Trends["buckets"] }) {
  const points = useMemo(() => {
    if (!data.length) return { issued: "", accepted: "", labels: [] as string[] };

    const maxCount = Math.max(
      1,
      ...data.map(item => item.issued),
      ...data.map(item => item.acceptedByIssueDate),
    );

    const coordinates = (field: "issued" | "acceptedByIssueDate") => (
      data.map((item, index) => {
        const x = data.length === 1 ? 160 : 12 + index * (296 / (data.length - 1));
        const y = 122 - item[field] / maxCount * 104;
        return x.toFixed(2) + "," + y.toFixed(2);
      }).join(" ")
    );

    const middleIndex = Math.floor((data.length - 1) / 2);
    return {
      issued: coordinates("issued"),
      accepted: coordinates("acceptedByIssueDate"),
      labels: [data[0].date, data[middleIndex].date, data[data.length - 1].date],
    };
  }, [data]);

  if (!data.length) return <DashboardState kind="empty" />;

  return (
    <>
      <div className="argin-dashboard__chart">
        <svg
          viewBox="0 0 320 140"
          width="100%"
          role="img"
          aria-label="نمودار تعداد صدور صورتحساب و وضعیت تأیید به تفکیک تاریخ صدور"
          preserveAspectRatio="none"
        >
          {[18, 70, 122].map(y => (
            <line key={y} x1="12" x2="308" y1={y} y2={y} stroke="#dce7e9" strokeDasharray="4 5" />
          ))}
          <polyline
            fill="none" stroke="#177f81" strokeWidth="2.8"
            strokeLinejoin="round" strokeLinecap="round" points={points.issued}
          />
          <polyline
            fill="none" stroke="#e2a54b" strokeWidth="2.4"
            strokeLinejoin="round" strokeLinecap="round" points={points.accepted}
          />
        </svg>
        <div className="argin-dashboard__chart-dates">
          {points.labels.map((date, index) => <span key={index}>{formatDate(date)}</span>)}
        </div>
      </div>
      <div className="argin-dashboard__legend">
        <span><i className="argin-dashboard__dot" data-tone="primary" />صدورشده</span>
        <span><i className="argin-dashboard__dot" data-tone="amber" />دارای تأیید قطعی (براساس تاریخ صدور)</span>
      </div>
      <p className="argin-dashboard__kpi-note">
        تأییدها بر مبنای تاریخ صدور صورتحساب محاسبه شده‌اند، نه تاریخ پاسخ سازمان.
      </p>
    </>
  );
}

function DashboardStatusChart({ status }: { status: InvoiceStatus }) {
  const groups = [
    { label: "تأیید قطعی", value: status.accepted + status.acceptedCancellation, tone: "success" },
    { label: "ردشده", value: status.rejected, tone: "danger" },
    { label: "نامشخص", value: status.unknown, tone: "warning" },
    { label: "در انتظار استعلام", value: status.awaitingInquiry, tone: "neutral" },
    { label: "آماده / در جریان", value: status.prepared + status.processing, tone: "neutral" },
    { label: "نیازمند بررسی", value: status.attention, tone: "warning" },
    { label: "تعیین‌نشده", value: status.unclassified, tone: "neutral" },
  ] as const;

  if (!status.logicalInvoiceCount) return <DashboardState kind="empty" />;

  return (
    <div className="argin-dashboard__composition">
      {groups.filter(group => group.value > 0).map(group => (
        <div className="argin-dashboard__composition-row" key={group.label}>
          <div><span>{group.label}</span><strong>{formatNumber(group.value)}</strong></div>
          <div className="argin-dashboard__track">
            <span
              data-tone={group.tone}
              style={{ width: Math.min(100, group.value / status.logicalInvoiceCount * 100) + "%" }}
            />
          </div>
        </div>
      ))}
      <p className="argin-dashboard__kpi-note">
        هر زنجیره صورتحساب یک‌بار شمرده می‌شود. رسید ارسال به‌معنای تأیید سازمان نیست.
      </p>
    </div>
  );
}

function DashboardFold({id,title,note,children}:{id:string;title:string;note:string;children:ReactNode}){
  const [open,setOpen]=useState(false);
  return <section className="argin-dashboard__fold" data-open={open}>
    <button type="button" className="argin-dashboard__fold-trigger" aria-expanded={open}
      aria-controls={id} onClick={()=>setOpen(x=>!x)}>
      <span><strong>{title}</strong><small>{note}</small></span>
      <span aria-hidden="true" className="argin-dashboard__fold-chevron">⌄</span>
    </button>
    <div id={id} className="argin-dashboard__fold-body" hidden={!open}>
      {open ? children : null}
    </div>
  </section>;
}

type Loadable<T> = {
  status: RemoteState;
  data: T | null;
};

function waiting<T>(): Loadable<T> {
  return { status: "loading", data: null };
}

export default function DashboardAnalytics({ role }: { role: Role }) {
  const [period, setPeriod] = useState<Period>("30d");
  const [version, setVersion] = useState(0);
  const [overviewState, setOverviewState] = useState<Loadable<Overview>>(waiting);
  const [trendState, setTrendState] = useState<Loadable<Trends>>(waiting);
  const [attentionState, setAttentionState] = useState<Loadable<AttentionItem[]>>(waiting);
  const [financeState, setFinanceState] = useState<Loadable<Finance>>(waiting);
  const [selectedKpi,setSelectedKpi] = useState<number|null>(null);
  const [customRange,setCustomRange] = useState<CustomRange|null>(null);
  const admin = role !== "Customer";

  useEffect(() => {
    const controller = new AbortController();

    // Each section settles independently so slower feeds do not block the others.
    setOverviewState(waiting());
    setTrendState(waiting());
    setAttentionState(waiting());
    setFinanceState(waiting());

    query("overview", period, controller.signal, customRange)
      .then(data => {
        const next = data as Overview;
        if (next.schemaVersion !== 1 || next.role !== role || !next.invoiceStatus) {
          throw new Error("invalid_dashboard_response");
        }
        if (!controller.signal.aborted) setOverviewState({ status: "ready", data: next });
      })
      .catch(() => {
        if (!controller.signal.aborted) setOverviewState({ status: "error", data: null });
      });

    query("trends", period, controller.signal, customRange)
      .then(data => {
        const next = data as Trends;
        if (next.schemaVersion !== 1 || !Array.isArray(next.buckets)) {
          throw new Error("invalid_dashboard_response");
        }
        if (!controller.signal.aborted) setTrendState({ status: "ready", data: next });
      })
      .catch(() => {
        if (!controller.signal.aborted) setTrendState({ status: "error", data: null });
      });

    query("attention", period, controller.signal, customRange)
      .then(data => {
        const next = data as AttentionResponse;
        if (!Array.isArray(next.items)) throw new Error("invalid_dashboard_response");
        if (!controller.signal.aborted) setAttentionState({ status: "ready", data: next.items });
      })
      .catch(() => {
        if (!controller.signal.aborted) setAttentionState({ status: "error", data: null });
      });

    if (admin) {
      query("finance", period, controller.signal, customRange)
        .then(data => {
          const next = data as Finance;
          if (next.schemaVersion !== 1 || next.role !== role || next.currency !== "IRR") {
            throw new Error("invalid_dashboard_response");
          }
          if (!controller.signal.aborted) setFinanceState({ status: "ready", data: next });
        })
        .catch(() => {
          if (!controller.signal.aborted) setFinanceState({ status: "error", data: null });
        });
    }

    return () => controller.abort();
  }, [role, admin, period, customRange, version]);

  function changePeriod(next: Period) {
    if (next === period) return;

    setOverviewState(waiting());
    setTrendState(waiting());
    setAttentionState(waiting());
    setFinanceState(waiting());
    setSelectedKpi(null);
    setPeriod(next);
  }

  function applyCustom(from:string,to:string){
    setOverviewState(waiting());setTrendState(waiting());setAttentionState(waiting());setFinanceState(waiting());
    setSelectedKpi(null);setCustomRange({from,to});setPeriod("custom");
  }
  const dateQuery=period==="custom"&&customRange
    ? "custom&from="+customRange.from+"&to="+customRange.to : period;
  const currentPeriodLabel=period==="custom"&&customRange
    ? formatDate(customRange.from)+" تا "+formatDate(customRange.to) : periodNames[period];

  const overview = overviewState.data;
  const finance = financeState.data;
  const kpiState = admin ? financeState : overviewState;
  const cards = kpiState.status === "ready" ? buildKpiCards(role, overview, finance) : [];
  const visibleAttention = attentionState.data?.slice(0, 5) ?? [];
  const lastUpdated = overviewState.status === "ready" && overview
    ? "آخرین دریافت: " + formatLastUpdated(overview.asOfUtc)
    : "اطلاعات هر بخش به‌صورت مستقل دریافت می‌شود";
  const retryRead = () => setVersion(current => current + 1);
  const actionCenter = (
    <DashboardActionCenter role={role} attentionStatus={attentionState.status}
      attention={attentionState.data ?? []} onRetry={retryRead} />
  );

  return (
    <div className="argin-dashboard" dir="rtl">
      <header className="argin-dashboard__header">
        <div>
          <span className="argin-dashboard__eyebrow">پیشخوان تحلیلی آرگین</span>
          <h1 className="argin-dashboard__title">{dashboardTitles[role]}</h1>
          <p className="argin-dashboard__subtitle">
            گزارش‌های واقعی متناسب با دسترسی شما؛ {lastUpdated} · بازه: {currentPeriodLabel}
          </p>
        </div>
        <DashboardPeriodFilter period={period} setPeriod={changePeriod} onCustom={applyCustom} />
      </header>

      {kpiState.status === "loading" ? <DashboardKpiSkeleton /> : (
        kpiState.status === "error" ? (
          <section className="argin-dashboard__card argin-dashboard__kpi-error" aria-label="شاخص‌های کلیدی">
            <DashboardState kind="error" onRetry={retryRead} />
          </section>
        ) : (
          <section aria-label="شاخص‌های کلیدی" className="argin-dashboard__kpis">
            {cards.map((card,index) => <DashboardKpiCard key={card.label} {...card}
              selected={selectedKpi===index} onSelect={()=>setSelectedKpi(selectedKpi===index?null:index)} />)}
          </section>
        )
      )}

      {selectedKpi!==null && kpiState.status==="ready" &&
        <DashboardKpiDetails role={role} index={selectedKpi} overview={overview}
          finance={finance} onClose={()=>setSelectedKpi(null)} />}
      {!admin && actionCenter}
      <div className="argin-dashboard__charts">
        <DashboardPanel title="روند صورتحساب‌ها">
          {trendState.status === "loading" ? <DashboardSkeleton variant="chart" /> : (
            trendState.status === "error"
              ? <DashboardState kind="error" onRetry={retryRead} />
              : <DashboardTrendChart data={trendState.data?.buckets ?? []} />
          )}
        </DashboardPanel>
        <div className="argin-dashboard__side">
          <DashboardPanel title="دسترسی سریع">
            <nav className="argin-dashboard__quick" aria-label="اقدامات پرکاربرد">
              <Link href="/invoices">
                <span className="argin-dashboard__quick-icon"><DashboardIcon name="invoice" /></span>
                <span>صورتحساب‌ها</span>
              </Link>
              <Link href="/parties">
                <span className="argin-dashboard__quick-icon"><DashboardIcon name="parties" /></span>
                <span>خریداران و مؤدیان</span>
              </Link>
              <Link href={admin ? "/subscriptions" : "/account"}>
                <span className="argin-dashboard__quick-icon"><DashboardIcon name="subscription" /></span>
                <span>{admin ? "اشتراک‌ها و فروش" : "اشتراک من"}</span>
              </Link>
            </nav>
            {role === "Customer" && overviewState.status === "loading" && (
              <div className="argin-dashboard__subscription-waiting" aria-label="در حال دریافت اشتراک">
                <DashboardSkeleton rows={1} />
              </div>
            )}
            {role === "Customer" && overviewState.status === "error" && (
              <p className="argin-dashboard__kpi-note">
                وضعیت اشتراک در دسترس نیست. <Link href="/account">مشاهده حساب</Link>
              </p>
            )}
            {role === "Customer" && overview?.subscription && (
              <section className="argin-dashboard__subscription" aria-label="وضعیت اشتراک فعلی">
                <div className="argin-dashboard__subscription-name">
                  <span>اشتراک فعلی شما</span>
                  <strong>{overview.subscription.planName}</strong>
                  <span className="argin-dashboard__subscription-badge" data-active={overview.subscription.hasActivePlan}>
                    {overview.subscription.hasActivePlan ? "فعال" : "غیرفعال"}
                  </span>
                </div>
                <div className="argin-dashboard__subscription-remaining">
                  <strong>{formatNumber(Math.max(0, overview.subscription.remainingDays))}</strong>
                  <span>روز اعتبار باقی‌مانده</span>
                </div>
                {overview.subscription.remainingInvoices !== null && (
                  <p className="argin-dashboard__subscription-quota">
                    <strong>{formatNumber(overview.subscription.remainingInvoices)}</strong> صورتحساب باقی‌مانده
                  </p>
                )}
              </section>
            )}
            <DashboardCalendar />
          </DashboardPanel>
        </div>
      </div>

      {admin && <DashboardFold id="finance-details" title="گزارش‌های تحلیلی مالی"
        note="روند فروش، وصول وجه و تسویه همکاران — برای مشاهده باز کنید">
        <DashboardFinancialCharts role={role === "Level1" ? "Level1" : "Level2"} period={dateQuery} />
      </DashboardFold>}
      {admin && <DashboardFold id="subscription-details" title="جزئیات اشتراک‌ها و سهمیه‌ها"
        note="تمدیدهای نزدیک و بسته‌های نیازمند رسیدگی — برای مشاهده باز کنید">
        <DashboardSubscriptionWatch role={role === "Level1" ? "Level1" : "Level2"} />
      </DashboardFold>}
      {role === "Level1" && <DashboardFold id="root-details" title="گزارش تکمیلی مدیر اصلی"
        note="آمار کل سامانه و عملکرد همکاران">
        <RootDashboardDetails period={period} />
      </DashboardFold>}
      {role === "Level2" && (
        <DashboardFold id="partner-details" title="گزارش تکمیلی مدیر سطح ۲"
          note="مشتریان و وضعیت سهم همکاری">
          <PartnerDashboardDetails
            finance={financeState.data}
            financeState={financeState.status}
            periodLabel={currentPeriodLabel}
            onRetry={retryRead}
          />
        </DashboardFold>
      )}
      <DashboardFold id="invoice-details" title="جزئیات وضعیت صورتحساب‌ها"
        note="فهرست پیگیری و ترکیب آخرین وضعیت — گزارش تکمیلی">
      <div className="argin-dashboard__footer-grid">
        <DashboardPanel title="موارد نیازمند پیگیری">
          {attentionState.status === "loading" ? <DashboardSkeleton variant="list" rows={4} /> : (
            attentionState.status === "error" ? <DashboardState kind="error" onRetry={retryRead} /> : (
              !visibleAttention.length ? <DashboardState kind="empty" /> : (
                <>
                  <ul className="argin-dashboard__attention">
                    {visibleAttention.map(item => (
                      <li key={item.invoiceId}>
                        <span className="argin-dashboard__status" data-tone="warning">
                          {statusLabels[item.status] ?? "نیازمند بررسی"}
                        </span>
                        <span>{formatDate(item.issuedAtUtc.slice(0, 10))}</span>
                        <Link href="/invoices">مشاهده صورتحساب</Link>
                      </li>
                    ))}
                  </ul>
                  <Link className="argin-dashboard__more" href="/invoices">مشاهده همه صورتحساب‌ها</Link>
                </>
              )
            )
          )}
        </DashboardPanel>
        <DashboardPanel title="ترکیب آخرین وضعیت صورتحساب‌ها">
          {overviewState.status === "loading" ? <DashboardSkeleton variant="chart" /> : (
            overviewState.status === "error" ? <DashboardState kind="error" onRetry={retryRead} /> : (
              overview ? <DashboardStatusChart status={overview.invoiceStatus} /> : <DashboardState kind="empty" />
            )
          )}
        </DashboardPanel>
      </div>
      </DashboardFold>
      {admin && (
        <DashboardFold id="operational-priorities" title="اولویت‌های عملیاتی"
          note="اقدام‌های نیازمند پیگیری — برای مشاهده باز کنید">
          {actionCenter}
        </DashboardFold>
      )}
    </div>
  );
}
