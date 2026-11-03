"use client";

import { useEffect, useMemo, useState } from "react";

type Plan = {
  planId: string;
  code: string;
  kind: number;
  validityDays: number;
  invoiceUnitLimit: number | null;
  priceIrr: number;
};

const planKinds: Record<number, string> = {
  1: "یک‌ساله",
  2: "تعدادی",
  3: "یک‌ماهه",
  4: "سه‌ماهه",
  5: "شش‌ماهه",
};

const formatNumber = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const planName = (plan: Plan) => plan.kind === 2 && plan.invoiceUnitLimit !== null
  ? `بسته ${formatNumber(plan.invoiceUnitLimit)} تایی`
  : ({1:"یک‌ساله",3:"یک‌ماهه",4:"سه‌ماهه",5:"شش‌ماهه"} as Record<number,string>)[plan.kind]
    || "پلن نرم‌افزار";

export default function CustomerPlans() {
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [selected, setSelected] = useState("");
  const [official, setOfficial] = useState(false);
  const [operation, setOperation] = useState(() => crypto.randomUUID());
  const selectedPlan = useMemo(() => plans.find(plan => plan.planId === selected) ?? null,
    [plans, selected]);

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/customer-checkout", { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error("دریافت پلن‌ها ناموفق بود (HTTP " + response.status + ")");
        setPlans(await response.json() as Plan[]);
      })
      .catch(reason => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "دریافت پلن‌ها ناموفق بود.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);

  async function buy() {
    if (!selected || busy) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const response = await fetch("/api/customer-checkout", {
        method: "POST",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          planId: selected,
          wantsOfficialInvoice: official,
          operationKey: operation,
        }),
      });
      if (!response.ok) {
        const details = await response.json().catch(() => null) as {message?:string}|null;
        throw new Error(details?.message || "ثبت درخواست ناموفق بود (HTTP " + response.status + ")");
      }
      const result = await response.json() as { orderId: string };
      setNotice("درخواست پرداخت نقدی ثبت شد. شماره سفارش: " + result.orderId +
        " — پس از پرداخت وجه و تأیید مدیر، اشتراک فعال می‌شود.");
      setOperation(crypto.randomUUID());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "ثبت درخواست ناموفق بود.");
    } finally {
      setBusy(false);
    }
  }

  return <section className="customer-plans-workspace" dir="rtl">
    <header className="customer-plans-intro">
      <span className="customer-plans-intro__icon" aria-hidden="true">✦</span>
      <div>
        <p className="customer-plans-intro__eyebrow">اشتراک نرم‌افزار آرگین</p>
        <h2>پلن متناسب با نیاز کسب‌وکار خود را انتخاب کنید</h2>
        <p>مدت اعتبار، سهمیه و مبلغ پلن‌ها را مقایسه کنید؛ سفارش پس از تأیید پرداخت توسط مدیر فعال می‌شود.</p>
      </div>
    </header>

    {error && <p className="customer-plans-message customer-plans-message--error" role="alert">{error}</p>}
    {notice && <p className="customer-plans-message customer-plans-message--success" role="status">{notice}</p>}

    <div className="customer-plans-layout">
      <section className="customer-plans-catalog" aria-labelledby="customer-plans-title">
        <div className="customer-plans-section-heading">
          <div>
            <h2 id="customer-plans-title">پلن‌های قابل انتخاب</h2>
            <p>پلن‌ها و تعرفه‌های فعال حساب شما</p>
          </div>
          {!loading && <span>{formatNumber(plans.length)} پلن</span>}
        </div>

        {loading ? <div className="customer-plans-empty" role="status">در حال دریافت پلن‌ها…</div> :
          plans.length === 0 ? <div className="customer-plans-empty">در حال حاضر پلن فعالی برای حساب شما وجود ندارد.</div> :
          <div className="customer-plan-grid" role="radiogroup" aria-label="انتخاب پلن نرم‌افزار">
            {plans.map(plan => {
              const isSelected = selected === plan.planId;
              return <label key={plan.planId}
                className={`customer-plan-card${isSelected ? " customer-plan-card--selected" : ""}`}>
                <input type="radio" name="customer-plan" value={plan.planId} checked={isSelected}
                  onChange={() => {
                    setSelected(plan.planId);
                    setOperation(crypto.randomUUID());
                    setError("");
                    setNotice("");
                  }}/>
                <span className="customer-plan-card__topline">
                  <span className="customer-plan-card__kind">{planKinds[plan.kind] || "پلن نرم‌افزار"}</span>
                  <span className="customer-plan-card__selection" aria-hidden="true">{isSelected ? "✓" : ""}</span>
                </span>
                <strong className="customer-plan-card__name">{planName(plan)}</strong>
                <span className="customer-plan-card__facts">
                  <span><small>مدت اعتبار</small><b>{formatNumber(plan.validityDays)} روز</b></span>
                  <span><small>سهمیه صورتحساب</small><b>{plan.kind === 2 && plan.invoiceUnitLimit !== null
                    ? formatNumber(plan.invoiceUnitLimit) + " صورتحساب" : "نامحدود"}</b></span>
                </span>
                <span className="customer-plan-card__price">
                  <b>{formatNumber(plan.priceIrr)}</b><small>ریال</small>
                </span>
                <span className="customer-plan-card__hint">{isSelected ? "این پلن انتخاب شده است" : "برای انتخاب، این پلن را برگزینید"}</span>
              </label>;
            })}
          </div>}
      </section>

      <aside className="customer-order-summary" aria-labelledby="customer-order-title">
        <div className="customer-order-summary__heading">
          <span aria-hidden="true">▤</span>
          <div>
            <h2 id="customer-order-title">خلاصه سفارش</h2>
            <p>پیش از ثبت، جزئیات را بررسی کنید.</p>
          </div>
        </div>

        {selectedPlan ? <div className="customer-order-summary__selected">
          <span className="customer-order-summary__kind">{planKinds[selectedPlan.kind] || "پلن نرم‌افزار"}</span>
          <strong>{planName(selectedPlan)}</strong>
          <span>{formatNumber(selectedPlan.validityDays)} روز اعتبار</span>
          <span>{selectedPlan.kind === 2 && selectedPlan.invoiceUnitLimit !== null
            ? formatNumber(selectedPlan.invoiceUnitLimit) + " صورتحساب" : "سهمیه صورتحساب نامحدود"}</span>
          <div className="customer-order-summary__price"><span>مبلغ پلن</span>
            <strong>{formatNumber(selectedPlan.priceIrr)} <small>ریال</small></strong></div>
        </div> : <p className="customer-order-summary__empty">برای دیدن خلاصه و ثبت سفارش، ابتدا یک پلن را انتخاب کنید.</p>}

        <label className="customer-invoice-option">
          <input type="checkbox" checked={official} onChange={event => setOfficial(event.target.checked)}/>
          <span><strong>صورتحساب رسمی می‌خواهم</strong>
            <small>ارزش افزوده در سرور محاسبه و به سفارش‌های جدید اضافه می‌شود.</small></span>
        </label>

        <div className="customer-payment-method">
          <span className="customer-payment-method__mark" aria-hidden="true">✓</span>
          <div><strong>پرداخت نقدی</strong><small>پس از ثبت سفارش، پرداخت توسط مدیر بررسی می‌شود.</small></div>
          <span className="customer-payment-method__badge">فعال</span>
        </div>
        <div className="customer-payment-unavailable" aria-disabled="true">
          <span aria-hidden="true">⌁</span>
          <div><strong>پرداخت آنلاین</strong><small>به‌زودی فعال می‌شود</small></div>
        </div>

        <button type="button" className="identity-primary customer-order-summary__submit"
          disabled={!selectedPlan || busy} onClick={() => void buy()}>
          {busy ? "در حال ثبت سفارش…" : "ثبت درخواست خرید نقدی"}
        </button>
        <p className="customer-order-summary__disclaimer">ثبت درخواست به‌معنی پرداخت یا فعال‌شدن اشتراک نیست؛ فعال‌سازی پس از تأیید مدیر انجام می‌شود.</p>
      </aside>
    </div>
  </section>;
}
