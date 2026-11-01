"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";

type Plan = {
  id: string;
  code: string;
  kind: number;
  validityDays: number;
  invoiceUnitLimit: number | null;
  isActive: boolean;
  createdAtUtc: string;
};

type ModalMode = "create" | "edit" | "view" | "archive";
type FinanceAction = "price" | "activation";

type PlanCatalogProps = {
  level1: boolean;
};

const PLANS_API = "/api/phase12/subscriptions/plans";

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
) {
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
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

  const [modalMode, setModalMode] = useState<ModalMode | null>(null);
  const [selectedPlan, setSelectedPlan] = useState<Plan | null>(null);
  const [planCode, setPlanCode] = useState("");
  const [planKind, setPlanKind] = useState(1);
  const [validityDays, setValidityDays] = useState(365);
  const [invoiceUnitLimit, setInvoiceUnitLimit] = useState(100);

  const [priceIrr, setPriceIrr] = useState("");
  const [pricePlanId, setPricePlanId] = useState("");
  const [activationPlanId, setActivationPlanId] = useState("");

  const refreshPlans = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const response = await fetch(PLANS_API, { cache: "no-store" });
      if (!response.ok) {
        throw Error("خطای دریافت پلن‌ها: " + response.status);
      }

      const result = (await response.json()) as Plan[];
      setPlans(result);
    } catch (error) {
      setError(error instanceof Error ? error.message : "خطای ناشناخته");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshPlans();
  }, [refreshPlans]);

  const filteredPlans = useMemo(
    () =>
      plans.filter(plan =>
        (plan.code + " " + PLAN_KIND_LABELS[plan.kind])
          .toLocaleLowerCase()
          .includes(query.toLocaleLowerCase()),
      ),
    [plans, query],
  );

  const isReadOnly = modalMode === "view";

  function openModal(nextMode: ModalMode, plan?: Plan) {
    setSelectedPlan(plan ?? null);
    setModalMode(nextMode);
    setError("");
    setPlanCode(plan?.code ?? "");
    setPlanKind(plan?.kind ?? 1);
    setValidityDays(plan?.validityDays ?? 365);
    setInvoiceUnitLimit(plan?.invoiceUnitLimit ?? 100);
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
      if (modalMode === "archive" && selectedPlan) {
        await submitRequest(PLANS_API + "/" + selectedPlan.id, "DELETE");
        setNotice("پلن بایگانی شد؛ اطلاعات مالی پیشین محفوظ است.");
      } else if (modalMode === "create" || modalMode === "edit") {
        const isCreating = modalMode === "create";
        const url = isCreating ? PLANS_API : PLANS_API + "/" + selectedPlan!.id;

        await submitRequest(url, isCreating ? "POST" : "PUT", {
          code: planCode,
          kind: planKind,
          validityDays,
          invoiceUnitLimit: planKind === 2 ? invoiceUnitLimit : null,
        });
        setNotice(isCreating ? "پلن جدید ثبت شد." : "پلن به‌روزرسانی شد.");
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
    setSaving(true);
    setError("");

    try {
      if (action === "price") {
        await submitRequest("/api/phase12/subscriptions/prices", "POST", {
          planId: pricePlanId,
          priceIrr: Number(priceIrr),
          effectiveFromUtc: new Date().toISOString(),
          effectiveUntilUtc: null,
        });
        setNotice("تعرفه اختصاصی ثبت شد.");
      } else {
        await submitRequest(PLANS_API + "/activate", "POST", {
          planId: activationPlanId,
        });
        setNotice("پلن فعال شد.");
        await refreshPlans();
      }
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
          <small>نمایش {filteredPlans.length} پلن</small>
        </div>
        <input
          aria-label="جست‌وجوی پلن"
          placeholder="جست‌وجوی نام یا نوع پلن..."
          value={query}
          onChange={event => setQuery(event.target.value)}
        />
        <button type="button" onClick={() => openModal("create")}>
          + افزودن پلن
        </button>
        <button type="button" onClick={() => void refreshPlans()} disabled={loading}>
          بازخوانی
        </button>
      </div>

      {notice && <p role="status" className="phase12-plan-success">{notice}</p>}
      {error && !modalMode && <p role="alert">{error}</p>}

      <div className="phase12-plan-table">
        <table>
          <thead>
            <tr>
              <th>کد پلن</th>
              <th>نوع</th>
              <th>اعتبار</th>
              <th>سهمیه</th>
              <th>وضعیت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={6}>در حال بارگذاری...</td></tr>
            ) : filteredPlans.length === 0 ? (
              <tr><td colSpan={6}>پلنی یافت نشد.</td></tr>
            ) : (
              filteredPlans.map(plan => (
                <tr key={plan.id}>
                  <td>{plan.code}</td>
                  <td>{PLAN_KIND_LABELS[plan.kind] ?? "نامشخص"}</td>
                  <td>{plan.validityDays} روز</td>
                  <td>
                    {plan.kind === 2
                      ? plan.invoiceUnitLimit?.toLocaleString("fa-IR")
                      : "نامحدود"}
                  </td>
                  <td>{plan.isActive ? "فعال" : "غیرفعال"}</td>
                  <td className="phase12-plan-actions">
                    <button type="button" onClick={() => openModal("view", plan)}>
                      مشاهده
                    </button>
                    <button type="button" onClick={() => openModal("edit", plan)}>
                      ویرایش
                    </button>
                    <button
                      type="button"
                      onClick={() => openModal("archive", plan)}
                      disabled={!plan.isActive}
                    >
                      حذف
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <details className="phase12-plan-extra">
        <summary>تعرفه و فعال‌سازی پلن</summary>
        <form onSubmit={event => void saveFinance(event, "price")}>
          <label>
            پلن
            <select
              required
              value={pricePlanId}
              onChange={event => setPricePlanId(event.target.value)}
            >
              <option value="">انتخاب پلن</option>
              {plans.map(plan => <option key={plan.id} value={plan.id}>{plan.code}</option>)}
            </select>
          </label>
          <label>
            قیمت (ریال)
            <input
              type="number"
              required
              min={0}
              value={priceIrr}
              onChange={event => setPriceIrr(event.target.value)}
            />
          </label>
          <button disabled={saving}>ثبت تعرفه مستقل</button>
        </form>

        {level1 && (
          <form onSubmit={event => void saveFinance(event, "activation")}>
            <label>
              پلن
              <select
                required
                value={activationPlanId}
                onChange={event => setActivationPlanId(event.target.value)}
              >
                <option value="">انتخاب پلن</option>
                {plans.filter(plan => !plan.isActive).map(plan => (
                  <option key={plan.id} value={plan.id}>{plan.code}</option>
                ))}
              </select>
            </label>
            <button disabled={saving}>فعال‌سازی پلن</button>
          </form>
        )}
      </details>

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
                      {PLAN_KIND_ORDER.map(kind => (
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
                    {saving ? "در حال ثبت..." : modalMode === "archive" ? "تأیید حذف" : "ذخیره"}
                  </button>
                )}
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  );
}
