"use client";
import { getConnectionPresentation, type ConnectionHistory } from '../../lib/fiscal-connection-status';

export default function ConnectionSummary({ identifier, environment, hasKey, history, activationLabel, legacyFallbackAvailable = false }:
  { identifier: string; environment: 'Production' | 'Sandbox'; hasKey: boolean;
    history?: ConnectionHistory | null; activationLabel?: string; legacyFallbackAvailable?: boolean }) {
  const view = getConnectionPresentation(hasKey, history);
  const date = (value: string) => new Date(value).toLocaleString('fa-IR');
  return <div className="connection-summary" data-state={view.state}>
    <header><div><small>{environment === 'Sandbox' ? 'محیط آزمایشی — Sandbox' : 'محیط واقعی — Production'}</small>
      <strong dir="ltr">{identifier}</strong></div><span className="connection-summary__badge">{view.label}</span></header>
    <dl><div><dt>کلید خصوصی</dt><dd>{hasKey ? 'به‌صورت امن ذخیره شده' : 'هنوز ثبت نشده'}</dd></div>
      {activationLabel && <div><dt>وضعیت حافظه در برنامه</dt><dd>{activationLabel}</dd></div>}
      <div><dt>آخرین آزمون اتصال</dt><dd>{history?.lastAttempt ? date(history.lastAttempt.testedAtUtc) : 'آزمون ثبت‌شده‌ای موجود نیست'}</dd></div>
      {view.lastSuccess && view.state !== 'verified' && <div><dt>آخرین اتصال موفق</dt><dd>{date(view.lastSuccess.testedAtUtc)}</dd></div>}
    </dl>
    {history && !history.available && !legacyFallbackAvailable && <p role="status" className="field-hint">ثبت سوابق تفصیلی آزمون در پایگاه داده هنوز آماده نیست.</p>}
    {view.state === 'service-error' && <p role="status" className="field-hint">برای تکمیل بررسی دوباره آزمون کنید؛ این نتیجه به‌تنهایی نشانهٔ خرابی کلید نیست.</p>}
    {view.state === 'authentication-error' && <p role="status" className="field-hint">شناسهٔ حافظه و ثبت کلید عمومی در کارپوشهٔ همین محیط را بررسی کنید.</p>}
  </div>;
}
