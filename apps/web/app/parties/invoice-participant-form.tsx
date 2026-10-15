"use client";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import ConsoleLoader from "../components/console-loader";
import { fieldSpecs, normalizeFiscalField, validateFiscalField, type FiscalField } from "./invoice-participant-validation";

type Details = Record<FiscalField, string | null> & { revision: number };
const blank = (): Details => ({
  economicCode: null, branchCode: null, postalCode: null, province: null,
  city: null, address: null, phone: null, foreignNationalCode: null,
  passportNumber: null, contractReference: null, revision: 0
});
const urlFor = (customer: string, role: "buyer" | "seller", id: string) =>
  "/api/party-workspace/customers/" + customer + "/invoice-details/" + role + "/" + id;
async function bodyOrNull(res: Response): Promise<Details | null> {
  const body = await res.text();
  if (!body.trim()) return null;
  try { return JSON.parse(body) as Details | null; }
  catch { throw new Error("پاسخ سرویس قابل خواندن نیست؛ لطفاً بازخوانی کنید."); }
}
function responseError(status: number, body: string): string {
  let code = "";
  try { code = (JSON.parse(body) as {error?: string}).error ?? ""; } catch {}
  if (status === 401) return "نشست ورود منقضی شده است.";
  if (status === 403) return "اجازه ویرایش اطلاعات این پرونده را ندارید.";
  if (status === 404) return "پرونده یافت نشد یا دیگر به این حساب تعلق ندارد.";
  if (status === 409) return "اطلاعات از زمان باز شدن فرم تغییر کرده است؛ بازخوانی کنید و دوباره ویرایش کنید.";
  if (status === 502 || status === 503)
    return "سرویس یا دیتابیس پاسخ معتبر نداد. وضعیت API و Migration اطلاعات مالیاتی را بررسی کنید.";
  if (status === 400 || code.includes("invalid"))
    return "یک مقدار با قواعد اعتبارسنجی سرور سازگار نیست. فقط فیلد تغییرکرده را بررسی کنید.";
  return "ذخیره ناموفق بود (کد " + status + ").";
}

export default function InvoiceParticipantForm({ customerId, subjectId, role, personKind }:
  { customerId: string; subjectId: string; role: "buyer" | "seller"; personKind?: number }) {
  const [details, setDetails] = useState<Details>(blank);
  const [changed, setChanged] = useState<FiscalField[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [invoiceType, setInvoiceType] = useState("1");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const endpoint = urlFor(customerId, role, subjectId);
  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const res = await fetch(endpoint, { cache: "no-store", credentials: "same-origin" });
      if (!res.ok) throw new Error(responseError(res.status, await res.text()));
      const stored = await bodyOrNull(res);
      setDetails(stored ? { ...blank(), ...stored } : blank());
      setChanged([]);
    } catch (ex) { setError((ex as Error).message); }
    finally { setLoading(false); }
  }, [endpoint]);
  useEffect(() => { void load(); }, [load]);

  function update(key: FiscalField, value: string) {
    setDetails(current => ({ ...current, [key]: value }));
    setChanged(current => current.includes(key) ? current : [...current, key]);
    setError(""); setMessage("");
  }
  const problems = changed.map(key => ({ key, error: validateFiscalField(key, details[key] ?? "") }))
    .filter(x => x.error !== null);
  // Reference: RC_IITP.IS V7.9.2, buyer-information rules (type 1).
  // These are informational at dossier-draft stage; final requirements are checked
  // in the invoice rule engine with both invoice type and pattern.
  const readiness = role === "buyer" && invoiceType === "1" ?
    personKind === 1
      ? "برای خریدار حقیقی در صورتحساب نوع اول، هنگام صدور معمولاً شماره اقتصادی یا ترکیب شماره ملی و کد پستی لازم است. ثبت اطلاعات این پرونده مستقل از تکمیل شرایط صدور است."
      : personKind === 2
        ? "برای خریدار حقوقی در صورتحساب نوع اول، درج شماره اقتصادی هنگام صدور الزامی است؛ ولی ذخیره جزئی پرونده اکنون مجاز است."
        : "در نوع اول، اطلاعات موردنیاز خریدار به نوع شخصیت و الگوی رسمی وابسته است. در این فرم می‌توان هر فیلد را جداگانه ذخیره کرد."
    : role === "seller"
      ? "شناسه اقتصادی و کد شعبه فروشنده هنگام صدور طبق نوع و الگوی صورتحساب بررسی می‌شوند؛ ثبت این پرونده به تکمیل همه فیلدها نیاز ندارد."
      : "برای نوع دوم و سوم، مشخصات خریدار عموماً الزامی نیستند؛ قواعد الگوی رسمی در مرحله صدور اعمال خواهند شد.";

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || loading) return;
    if (!changed.length) { setMessage("تغییری برای ذخیره وجود ندارد."); return; }
    if (problems.length) { setError("مقادیر مشخص‌شده را اصلاح کنید."); return; }
    setSaving(true); setError(""); setMessage("");
    try {
      const modifications: Record<string, string | number | null> = { expectedRevision: details.revision };
      for (const key of changed) {
        const value = normalizeFiscalField(key, details[key] ?? "");
        modifications[key] = value || null;
      }
      const res = await fetch(endpoint, {
        method: "PATCH", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(modifications)
      });
      if (!res.ok) throw new Error(responseError(res.status, await res.text()));
      const result = await bodyOrNull(res);
      if (!result) throw new Error("سرور نتیجه ذخیره را برنگرداند؛ پیش از تلاش مجدد بازخوانی کنید.");
      setDetails({ ...blank(), ...result }); setChanged([]);
      setMessage("فقط فیلدهای تغییرکرده با موفقیت ذخیره شدند.");
    } catch (ex) { setError((ex as Error).message); }
    finally { setSaving(false); }
  }

  return <section className="invoice-participant" aria-busy={loading || saving}>
    <div className="taxpayer-section-heading">
      <h3>اطلاعات تکمیلی {role === "seller" ? "مؤدی" : "خریدار"}</h3>
      <button className="identity-secondary" type="button" disabled={loading || saving}
        onClick={() => void load()}>بازخوانی</button>
    </div>
    <p className="field-hint">هر کدام از فیلدها مستقل و اختیاری است. می‌توانید تنها یک مورد مانند استان یا شهر را ثبت یا ویرایش کنید. اطلاعات هویتی اصلی در پرونده شخص نگهداری می‌شوند.</p>
    <label htmlFor={"invoice-type-" + role}>نوع صورتحساب برای راهنمای تکمیل اطلاعات (نه اعتبارسنجی ذخیره)</label>
    <select id={"invoice-type-" + role} value={invoiceType} onChange={e => setInvoiceType(e.target.value)}>
      <option value="1">نوع اول</option><option value="2">نوع دوم</option><option value="3">نوع سوم</option>
    </select>
    <p className="invoice-help">{readiness}</p>
    {loading ? <ConsoleLoader label="در حال دریافت اطلاعات مالیاتی…" /> :
      <form className="invoice-fields" onSubmit={save} noValidate>
        {fieldSpecs.map(field => {
          const value = details[field.key] ?? "";
          const problem = changed.includes(field.key) ? validateFiscalField(field.key, value) : null;
          return <label key={field.key}>{field.label} <small>(اختیاری)</small>
            {field.hint && <small>{field.hint}</small>}
            <input type="text" dir={field.inputMode ? "ltr" : "rtl"} inputMode={field.inputMode}
              maxLength={field.max} value={value} aria-invalid={Boolean(problem)}
              onChange={e => update(field.key, e.target.value)}/>
            {problem && <small role="alert" className="party-error">{problem}</small>}
          </label>;
        })}
        {error && <p role="alert" className="party-error">{error}</p>}
        {message && <p role="status" className="party-success">{message}</p>}
        <button type="submit" className="identity-primary" disabled={saving || Boolean(problems.length) || !changed.length}>
          {saving ? "در حال ذخیره…" : "ذخیره تغییرات (" + changed.length.toLocaleString("fa-IR") + " فیلد)"}
        </button>
      </form>}
    {loading && error && <p role="alert" className="party-error">{error}</p>}
    <p className="field-hint">قواعد اجباری بودن اطلاعات، اعتبار شماره اقتصادی نزد سازمان و ترکیب فیلدهای صورتحساب در مرحله صدور و بر اساس نوع و الگو بررسی می‌شوند؛ ثبت این فرم به معنی تأیید سازمان امور مالیاتی نیست.</p>
  </section>;
}
