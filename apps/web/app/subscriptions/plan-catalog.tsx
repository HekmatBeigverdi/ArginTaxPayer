"use client";

import { useCallback, useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { createPortal } from "react-dom";
import FinancePagination from "./finance-pagination";

type Plan = {
  id: string;
  code: string;
  kind: number;
  validityDays: number;
  invoiceUnitLimit: number | null;
  isActive: boolean;
  createdAtUtc: string;
  isOwnedByActor: boolean;
  basePriceIrr: number | null;
  ownPriceIrr: number | null;
  partnerPriceCount: number;
  ownerMobile: string | null;
  partnerPrices: { partnerUserId: string; mobile: string | null; priceIrr: number | null }[];
};
type PriceHistory = {
  id: string; priceIrr: number; partnerUserId: string | null; partnerMobile: string | null;
  effectiveFromUtc: string; effectiveUntilUtc: string | null; isCurrent: boolean; isUpcoming: boolean;
};

type PageResult<T> = { items: T[]; totalCount: number; page: number; pageSize: number };

type ModalMode = "create" | "edit" | "view" | "archive";
type FinanceAction = "price" | "activation" | "history";

type PlanCatalogProps = {
  level1: boolean;
};

function formatPriceDate(value: string) {
  return new Intl.DateTimeFormat("fa-IR", {
    timeZone: "Asia/Tehran", dateStyle: "short", timeStyle: "short",
  }).format(new Date(value));
}

type PlanActionIcon = "price" | "history" | "activate" | "view" | "edit" | "archive";

function ActionIcon({ name }: { name: PlanActionIcon }) {
  const paths: Record<PlanActionIcon, ReactNode> = {
    price: <><path d="m20.6 13.4-7.2 7.2a2 2 0 0 1-2.8 0L3 13V4h9l8.6 8.6a.6.6 0 0 1 0 .8Z" /><circle cx="7.5" cy="8.5" r="1.2" /></>,
    history: <><path d="M3.5 8a6.5 6.5 0 1 0 2-4.7L3.5 5.5" /><path d="M3.5 2.5v3h3" /><path d="M10 5v4l2.7 1.6" /></>,
    activate: <><circle cx="12" cy="12" r="9" /><path d="m7.5 12.2 3 3 6-6.3" /></>,
    view: <><path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" /><circle cx="12" cy="12" r="2.5" /></>,
    edit: <><path d="m4 16.5-.8 4.3 4.3-.8L19 8.5 15.5 5 4 16.5Z" /><path d="m13.8 6.7 3.5 3.5M4 20.5h16" /></>,
    archive: <><path d="M3 7h18v13H3zM2 3h20v4H2z" /><path d="M9 11h6" /></>,
  };

  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</svg>;
}

function TooltipActionButton({ name, label, disabled, onClick }: {
  name: PlanActionIcon; label: string; disabled?: boolean; onClick: () => void;
}) {
  const tooltipId = useId();
  const [position, setPosition] = useState<{ left: number; top: number } | null>(null);
  const show = (button: HTMLButtonElement) => {
    const rect = button.getBoundingClientRect();
    setPosition({ left: rect.left + rect.width / 2, top: Math.max(8, rect.top - 8) });
  };

  return <span className="phase12-tooltip-anchor"
    onMouseEnter={event => show(event.currentTarget.querySelector("button")!)}
    onMouseLeave={() => setPosition(null)}
    onFocus={event => show(event.currentTarget.querySelector("button")!)}
    onBlur={() => setPosition(null)}>
    <button type="button" className="phase12-icon-action" aria-label={label}
      aria-describedby={position ? tooltipId : undefined} disabled={disabled} onClick={onClick}>
      <ActionIcon name={name} />
    </button>
    {position && createPortal(<span id={tooltipId} role="tooltip" className="phase12-action-tooltip"
      style={{ left: position.left, top: position.top }}>{label}</span>, document.body)}
  </span>;
}

const PLANS_API = "/api/phase12/subscriptions/plans";
const PRICES_API = "/api/phase12/subscriptions/prices";

const PLAN_KIND_LABELS: Record<number, string> = {
  1: "سالانه",
  2: "تعدادی",
  3: "ماهانه",
  4: "سه‌ماهه",
  5: "شش‌ماهه",
};

const VALIDITY_DAYS_BY_KIND: Record<number, number> = {
  1: 365,
  2: 365,
  3: 30,
  4: 90,
  5: 182,
};

const PLAN_KIND_ORDER = [3, 4, 5, 1, 2];

const MODAL_LABELS: Record<ModalMode, { title: string; ariaLabel: string }> = {
  create: { title: "افزودن پلن جدید", ariaLabel: "افزودن پلن" },
  edit: { title: "ویرایش پلن", ariaLabel: "ویرایش پلن" },
  view: { title: "مشخصات پلن", ariaLabel: "مشاهده پلن" },
  archive: { title: "بایگانی پلن", ariaLabel: "حذف پلن" },
};

async function submitRequest(
  url: string,
  method: "POST" | "PUT" | "DELETE",
  body?: object,
  conflictMessage = "وضعیت پلن تغییر کرده یا این عملیات به‌دلیل محدودیت پلن ممکن نیست. فهرست را بازخوانی کنید.",
) {
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    if (response.status === 409) {
      const result = await response.json().catch(() => null) as { error?: unknown } | null;
      if (typeof result?.error === "string") throw Error(result.error);
      throw Error(conflictMessage);
    }
    if (response.status === 404) throw Error("پلن دیگر در فهرست موجود نیست؛ جدول را بازخوانی کنید.");
    throw Error("عملیات ناموفق بود (HTTP " + response.status + ")");
  }

  return response.status === 204 ? null : response.json();
}

export default function PlanCatalog({ level1 }: PlanCatalogProps) {
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const plansRequest = useRef(0);

  const [modalMode, setModalMode] = useState<ModalMode | null>(null);
  const [selectedPlan, setSelectedPlan] = useState<Plan | null>(null);
  const [planCode, setPlanCode] = useState("");
  const [planKind, setPlanKind] = useState(1);
  const [validityDays, setValidityDays] = useState(365);
  const [invoiceUnitLimit, setInvoiceUnitLimit] = useState(100);

  const [priceIrr, setPriceIrr] = useState("");
  const [financePlan, setFinancePlan] = useState<Plan | null>(null);
  const [financeAction, setFinanceAction] = useState<FinanceAction | null>(null);
  const [createPriceIrr, setCreatePriceIrr] = useState("");
  const [historyScope, setHistoryScope] = useState("base");
  const [priceHistory, setPriceHistory] = useState<PriceHistory[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const [historyTotalCount, setHistoryTotalCount] = useState(0);

  const refreshPlans = useCallback(async () => {
    const request = ++plansRequest.current;
    setLoading(true);
    setError("");

    try {
      const params = new URLSearchParams({ page: String(page), search: query });
      const response = await fetch(PLANS_API + "?" + params, { cache: "no-store" });
      if (!response.ok) {
        throw Error(response.status === 409
          ? "برای این حساب سطح ۲، مدیر سطح ۱ یکتایی ثبت نشده است."
          : "خطای دریافت پلن‌ها: " + response.status);
      }

      const result = (await response.json()) as PageResult<Plan>;
      if (request !== plansRequest.current) return;
      setTotalCount(result.totalCount);
      const lastPage = Math.max(1, Math.ceil(result.totalCount / 10));
      if (page > lastPage) { setPage(lastPage); return; }
      setPlans(result.items);
    } catch (error) {
      if (request === plansRequest.current) {
        setPlans([]);
        setTotalCount(0);
        setError(error instanceof Error ? error.message : "خطای ناشناخته");
      }
    } finally {
      if (request === plansRequest.current) setLoading(false);
    }
  }, [page, query]);

  useEffect(() => {
    void refreshPlans();
  }, [refreshPlans]);

  useEffect(() => {
    if (financeAction !== "history" || !financePlan) return;
    const own = !level1 && historyScope === "own";
    const partnerId = level1 && historyScope !== "base" ? historyScope : "";
    const params = new URLSearchParams({ planId: financePlan.id, ownPrice: String(own), page: String(historyPage) });
    if (partnerId) params.set("partnerUserId", partnerId);
    let cancelled = false;
    setHistoryLoading(true);
    setError("");
    fetch(PRICES_API + "/history?" + params, { cache: "no-store" })
      .then(async response => {
        if (!response.ok) throw Error(response.status === 403
          ? "دسترسی به این تاریخچه مجاز نیست."
          : "خطای دریافت تاریخچه تعرفه: " + response.status);
        return await response.json() as PageResult<PriceHistory>;
      })
      .then(result => {
        if (cancelled) return;
        setHistoryTotalCount(result.totalCount);
        const lastPage = Math.max(1, Math.ceil(result.totalCount / 10));
        if (historyPage > lastPage) { setHistoryPage(lastPage); return; }
        setPriceHistory(result.items);
      })
      .catch(cause => {
        if (!cancelled) {
          setPriceHistory([]);
          setHistoryTotalCount(0);
          setError(cause instanceof Error ? cause.message : "خطای دریافت تاریخچه");
        }
      })
      .finally(() => { if (!cancelled) setHistoryLoading(false); });
    return () => { cancelled = true; };
  }, [financeAction, financePlan, historyScope, historyPage, level1]);

  const isReadOnly = modalMode === "view";

  function openModal(nextMode: ModalMode, plan?: Plan) {
    setSelectedPlan(plan ?? null);
    setModalMode(nextMode);
    setError("");
    setPlanCode(plan?.code ?? "");
    setPlanKind(plan?.kind ?? (level1 ? 1 : 2));
    setValidityDays(plan?.validityDays ?? 365);
    setInvoiceUnitLimit(plan?.invoiceUnitLimit ?? 100);
    setCreatePriceIrr("");
  }

  function openFinance(action: FinanceAction, plan: Plan) {
    setFinancePlan(plan);
    setFinanceAction(action);
    setError("");
    setPriceIrr("");
    setPriceHistory([]);
    setHistoryPage(1);
    setHistoryTotalCount(0);
    setHistoryScope(level1 && plan.partnerPrices.length
      ? plan.partnerPrices[0].partnerUserId : "base");
  }

  function changePlanKind(value: string) {
    const nextKind = Number(value);
    setPlanKind(nextKind);
    setValidityDays(VALIDITY_DAYS_BY_KIND[nextKind]);
  }

  async function savePlan(event: FormEvent) {
    event.preventDefault();
    if (saving) return;

    setSaving(true);
    setError("");

    try {
      if (!level1 && modalMode === "create" && !Number.isSafeInteger(Number(createPriceIrr))) {
        throw Error("مبلغ باید عدد صحیح و در محدودهٔ قابل ثبت باشد.");
      }
      if (modalMode === "archive" && selectedPlan) {
        await submitRequest(
          PLANS_API + "/" + selectedPlan.id,
          "DELETE",
          undefined,
          "این پلن به‌دلیل داشتن تعرفه یا اشتراک، قابل بایگانی نیست.",
        );
        setNotice("پلن بایگانی شد؛ اطلاعات مالی پیشین محفوظ است.");
      } else if (modalMode === "create" || modalMode === "edit") {
        const isCreating = modalMode === "create";
        const url = isCreating ? PLANS_API : PLANS_API + "/" + selectedPlan!.id;

        await submitRequest(url, isCreating ? "POST" : "PUT", {
          code: planCode,
          kind: planKind,
          validityDays,
          invoiceUnitLimit: planKind === 2 ? invoiceUnitLimit : null,
          ...(!level1 && isCreating ? { priceIrr: Number(createPriceIrr) } : {}),
        }, isCreating
          ? "کد پلن تکراری است یا مدیر سطح ۱ یکتایی برای این حساب ثبت نشده است."
          : "این پلن به‌دلیل داشتن تعرفه یا اشتراک، قابل ویرایش نیست.");
        setNotice(isCreating && !level1
          ? "پلن تعدادی و تعرفه اختصاصی ثبت شد؛ برای تعیین تعرفه پایه و فعال‌سازی به مدیر سطح ۱ ارسال شد."
          : isCreating ? "پلن جدید ثبت شد." : "پلن به‌روزرسانی شد.");
      }

      setModalMode(null);
      await refreshPlans();
    } catch (error) {
      setError(error instanceof Error ? error.message : "خطای ذخیره‌سازی");
    } finally {
      setSaving(false);
    }
  }

  async function saveFinance(event: FormEvent, action: FinanceAction) {
    event.preventDefault();
    if (action === "history" || !financePlan) return;
    setSaving(true);
    setError("");

    try {
      if (action === "price") {
        if (!Number.isSafeInteger(Number(priceIrr))) {
          throw Error("مبلغ باید عدد صحیح و در محدودهٔ قابل ثبت باشد.");
        }
        const saved = await submitRequest(PRICES_API, "POST", {
          planId: financePlan!.id,
          priceIrr: Number(priceIrr),
        }) as { effectiveUntilUtc?: string | null };
        setNotice(saved.effectiveUntilUtc
          ? "تعرفه ثبت شد و تا شروع تعرفهٔ زمان‌بندی‌شدهٔ بعدی معتبر است؛ آن تعرفه در زمان خود اعمال می‌شود."
          : "تعرفه از همین لحظه ثبت شد و نسخه قبلی در تاریخچه باقی ماند.");
      } else if (action === "activation") {
        await submitRequest(PLANS_API + "/activate", "POST", {
          planId: financePlan!.id,
        });
        setNotice("پلن فعال شد.");
      }
      setFinanceAction(null);
      await refreshPlans();
    } catch (error) {
      setError(error instanceof Error ? error.message : "خطا");
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="phase12-plan-catalog" aria-label="فهرست پلن‌ها">
      <div className="phase12-plan-toolbar">
        <div>
          <h3>پلن‌های اشتراک</h3>
          <small>نمایش {totalCount.toLocaleString("fa-IR")} پلن</small>
        </div>
        <input
          aria-label="جست‌وجوی پلن"
          maxLength={100}
          placeholder="جست‌وجوی نام یا نوع پلن..."
          value={query}
          onChange={event => { setQuery(event.target.value); setPage(1); }}
        />
        <button type="button" onClick={() => openModal("create")}>
          + افزودن پلن
        </button>
        <button type="button" onClick={() => void refreshPlans()} disabled={loading}>
          بازخوانی
        </button>
      </div>

      {notice && <p role="status" className="phase12-plan-success">{notice}</p>}
      {error && !modalMode && !financeAction && <p role="alert">{error}</p>}

      <div className="console-table-stage phase12-plan-stage" aria-busy={loading}>
        {loading && <div className="argin-loader argin-loader-overlay" role="status" aria-live="polite">
          <span className="argin-loader-spinner" aria-hidden="true" />
          <strong>در حال بارگذاری پلن‌ها…</strong>
          <span className="argin-loader-subtitle">لطفاً تا دریافت اطلاعات منتظر بمانید</span>
        </div>}
        <div className="console-table-scroll phase12-plan-table">
        <table className="console-table">
          <thead>
            <tr>
              <th>کد پلن</th>
              <th>نوع</th>
              <th>اعتبار</th>
              <th>سهمیه</th>
              <th>{level1 ? "تعرفه پایه" : "مبنای مدیر سطح ۱"}</th>
              <th>{level1 ? "تعرفه‌های اختصاصی" : "تعرفه من"}</th>
              <th>وضعیت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {!loading && plans.length === 0 ? (
              <tr><td colSpan={8}>پلنی یافت نشد.</td></tr>
            ) : (
              plans.map(plan => (
                <tr key={plan.id}>
                  <td>{plan.code}</td>
                  <td>{PLAN_KIND_LABELS[plan.kind] ?? "نامشخص"}</td>
                  <td>{plan.validityDays} روز</td>
                  <td>
                    {plan.kind === 2
                      ? plan.invoiceUnitLimit?.toLocaleString("fa-IR")
                      : "نامحدود"}
                  </td>
                  <td>{plan.basePriceIrr == null
                    ? "تعرفه تعریف نشده"
                    : plan.basePriceIrr.toLocaleString("fa-IR") + " ریال"}</td>
                  <td>{level1 ? (
                    <details className="phase12-partner-prices">
                      <summary>{plan.partnerPriceCount
                        ? plan.partnerPriceCount.toLocaleString("fa-IR") + " تعرفه"
                        : "بدون تعرفه"}</summary>
                      <ul>{plan.partnerPrices.map(item => <li key={item.partnerUserId}>
                        <button type="button" onClick={() => {
                          openFinance("history", plan);
                          setHistoryScope(item.partnerUserId);
                        }}>{item.mobile ?? item.partnerUserId}: {item.priceIrr == null
                          ? "تعریف نشده" : item.priceIrr.toLocaleString("fa-IR") + " ریال"}</button>
                      </li>)}</ul>
                    </details>
                  ) : plan.ownPriceIrr == null
                    ? "تعرفه تعریف نشده"
                    : plan.ownPriceIrr.toLocaleString("fa-IR") + " ریال"}</td>
                  <td>{plan.isActive ? "فعال" : level1
                    ? !plan.isOwnedByActor && plan.basePriceIrr == null
                      ? "در انتظار تعرفه پایه" : "آماده فعال‌سازی"
                    : "در انتظار تأیید مدیر سطح ۱"}</td>
                  <td className="phase12-plan-actions">
                    <TooltipActionButton name="price" onClick={() => openFinance("price", plan)}
                      label="تعرفه پلن" />
                    <TooltipActionButton name="history" onClick={() => openFinance("history", plan)}
                      label="تاریخچه تعرفه" />
                    {level1 && !plan.isActive && <TooltipActionButton name="activate"
                      onClick={() => openFinance("activation", plan)} disabled={plan.basePriceIrr == null}
                      label={plan.basePriceIrr == null ? "ابتدا تعرفه پلن را تعریف کنید" : "فعال‌سازی پلن"} />}
                    <TooltipActionButton name="view" onClick={() => openModal("view", plan)}
                      label="مشاهده پلن" />
                    {level1 && plan.isOwnedByActor && <TooltipActionButton name="edit"
                      onClick={() => openModal("edit", plan)} label="ویرایش پلن" />}
                    {plan.isOwnedByActor && <TooltipActionButton name="archive"
                      onClick={() => openModal("archive", plan)} disabled={!plan.isActive}
                      label={!plan.isActive ? "پلن غیرفعال است" : "بایگانی پلن"} />}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        </div>
      </div>

      <FinancePagination page={page} totalCount={totalCount} loading={loading} onPageChange={setPage} />

      {modalMode && (
        <div
          className="phase12-plan-overlay"
          role="presentation"
          onMouseDown={event => {
            if (event.target === event.currentTarget && !saving) {
              setModalMode(null);
            }
          }}
        >
          <div
            className="phase12-plan-modal"
            role="dialog"
            aria-modal="true"
            aria-label={MODAL_LABELS[modalMode].ariaLabel}
          >
            <div className="phase12-plan-modal-header">
              <h3>{MODAL_LABELS[modalMode].title}</h3>
              <button
                type="button"
                aria-label="بستن"
                disabled={saving}
                onClick={() => setModalMode(null)}
              >
                ×
              </button>
            </div>

            <form onSubmit={event => void savePlan(event)}>
              {modalMode === "archive" ? (
                <p>پلن «{selectedPlan?.code}» غیرفعال می‌شود؛ اطلاعات سفارش‌ها و اشتراک‌های قبلی باقی می‌مانند. ادامه می‌دهید؟</p>
              ) : (
                <>
                  <label>
                    کد پلن
                    <input
                      required
                      readOnly={isReadOnly}
                      value={planCode}
                      maxLength={50}
                      onChange={event => setPlanCode(event.target.value)}
                    />
                  </label>
                  <label>
                    نوع پلن
                    <select
                      disabled={isReadOnly}
                      value={planKind}
                      onChange={event => changePlanKind(event.target.value)}
                    >
                      {(level1 ? PLAN_KIND_ORDER : [planKind]).map(kind => (
                        <option key={kind} value={kind}>{PLAN_KIND_LABELS[kind]}</option>
                      ))}
                    </select>
                  </label>
                  <label>
                    اعتبار (روز)
                    <input
                      type="number"
                      required
                      readOnly={isReadOnly}
                      min={1}
                      max={366}
                      value={validityDays}
                      onChange={event => setValidityDays(Number(event.target.value))}
                    />
                  </label>
                  {planKind === 2 && (
                    <label>
                      سهمیه صورتحساب
                      <input
                        type="number"
                        min={1}
                        required
                        readOnly={isReadOnly}
                        value={invoiceUnitLimit}
                        onChange={event => setInvoiceUnitLimit(Number(event.target.value))}
                      />
                    </label>
                  )}
                  {!level1 && modalMode === "create" && (
                    <label>
                      تعرفه اختصاصی اولیه (ریال)
                      <input type="number" required min={0} value={createPriceIrr}
                        onChange={event => setCreatePriceIrr(event.target.value)} />
                      <small>پلن تا تعیین تعرفه پایه و فعال‌سازی مدیر سطح ۱ در انتظار می‌ماند.</small>
                    </label>
                  )}
                  {selectedPlan && <p>شناسه: <code dir="ltr">{selectedPlan.id}</code></p>}
                </>
              )}

              {error && <p role="alert">{error}</p>}
              <div className="phase12-plan-modal-footer">
                <button type="button" onClick={() => setModalMode(null)} disabled={saving}>
                  بستن
                </button>
                {!isReadOnly && (
                  <button type="submit" disabled={saving}>
                    {saving ? "در حال ثبت..." : modalMode === "archive" ? "تأیید بایگانی" : "ذخیره"}
                  </button>
                )}
              </div>
            </form>
          </div>
        </div>
      )}

      {financeAction && financePlan && (
        <div className="phase12-plan-overlay" role="presentation"
          onMouseDown={event => {
            if (event.target === event.currentTarget && !saving) setFinanceAction(null);
          }}>
          <div className="phase12-plan-modal" role="dialog" aria-modal="true"
            aria-label={financeAction === "history" ? "تاریخچه تعرفه" : financeAction === "price" ? "تعریف تعرفه" : "فعال‌سازی پلن"}>
            <div className="phase12-plan-modal-header">
              <h3>{financeAction === "history" ? "تاریخچه تعرفه" : financeAction === "price" ? "تعریف یا تغییر تعرفه" : "فعال‌سازی پلن"}</h3>
              <button type="button" aria-label="بستن" disabled={saving} onClick={() => setFinanceAction(null)}>×</button>
            </div>
            <p>پلن <strong>{financePlan.code}</strong></p>
            {financeAction === "history" ? <>
              <label>محدوده تعرفه
                <select value={historyScope} onChange={event => { setHistoryScope(event.target.value); setHistoryPage(1); }}>
                  <option value="base">تعرفه پایه مدیر سطح ۱</option>
                  {!level1 && <option value="own">تعرفه اختصاصی من</option>}
                  {level1 && financePlan.partnerPrices.map(partner => <option key={partner.partnerUserId} value={partner.partnerUserId}>
                    {partner.mobile ?? partner.partnerUserId}
                  </option>)}
                </select>
              </label>
              <div className="console-table-stage phase12-price-history" aria-busy={historyLoading}>
                {historyLoading && <div className="argin-loader argin-loader-overlay" role="status" aria-live="polite">
                  <span className="argin-loader-spinner" aria-hidden="true" />
                  <strong>در حال دریافت تاریخچه...</strong>
                </div>}
                <div className="console-table-scroll"><table className="console-table">
                  <thead><tr><th>مبلغ</th><th>شروع اعتبار</th><th>پایان اعتبار</th><th>وضعیت</th></tr></thead>
                  <tbody>{!historyLoading && priceHistory.length === 0
                    ? <tr><td colSpan={4}>برای این محدوده سابقه‌ای ثبت نشده است.</td></tr>
                    : priceHistory.map(item => <tr key={item.id}>
                    <td>{item.priceIrr.toLocaleString("fa-IR")} ریال</td>
                    <td>{formatPriceDate(item.effectiveFromUtc)}</td>
                    <td>{item.effectiveUntilUtc ? formatPriceDate(item.effectiveUntilUtc) : "بدون پایان"}</td>
                    <td>{item.isCurrent ? "در حال اجرا" : item.isUpcoming ? "آینده" : "پایان‌یافته"}</td>
                  </tr>)}</tbody>
                </table></div>
              </div>
              <FinancePagination page={historyPage} totalCount={historyTotalCount}
                loading={historyLoading} onPageChange={setHistoryPage} />
            </> : <form onSubmit={event => void saveFinance(event, financeAction)}>
              {financeAction === "price" ? <>
                <p>{level1 ? "تعرفه پایه فعلی" : "مبنای مدیر سطح ۱"}: {financePlan.basePriceIrr == null
                  ? "تعرفه تعریف نشده" : financePlan.basePriceIrr.toLocaleString("fa-IR") + " ریال"}</p>
                {!level1 && <p>تعرفه فعلی من: {financePlan.ownPriceIrr == null
                  ? "تعرفه تعریف نشده" : financePlan.ownPriceIrr.toLocaleString("fa-IR") + " ریال"}</p>}
                <label>{level1 ? "تعرفه پایه جدید (ریال)" : "تعرفه اختصاصی جدید (ریال)"}
                  <input type="number" required min={0} value={priceIrr}
                    onChange={event => setPriceIrr(event.target.value)} />
                </label>
                <p>تعرفه از لحظه ثبت مؤثر می‌شود؛ بازه قبلی بسته شده و در تاریخچه باقی می‌ماند.</p>
              </> : <p>پلن با تعرفه پایه فعلی <strong>{financePlan.basePriceIrr == null
                ? "تعرفه تعریف نشده" : financePlan.basePriceIrr.toLocaleString("fa-IR") + " ریال"}</strong> فعال می‌شود. ادامه می‌دهید؟</p>}
              {error && <p role="alert">{error}</p>}
              <div className="phase12-plan-modal-footer">
                <button type="button" onClick={() => setFinanceAction(null)} disabled={saving}>انصراف</button>
                <button type="submit" disabled={saving || (financeAction === "activation" && financePlan.basePriceIrr == null)}>
                  {saving ? "در حال ثبت..." : financeAction === "price" ? "ثبت تعرفه" : "تأیید فعال‌سازی"}
                </button>
              </div>
            </form>}
            {financeAction === "history" && error && <p role="alert">{error}</p>}
            {financeAction === "history" && <div className="phase12-plan-modal-footer">
              <button type="button" onClick={() => setFinanceAction(null)}>بستن</button>
            </div>}
          </div>
        </div>
      )}
    </section>
  );
}
