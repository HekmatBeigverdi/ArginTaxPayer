"use client";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import ConsoleLoader from "../components/console-loader";

type Details = {
  economicCode: string | null; branchCode: string | null; postalCode: string | null;
  province: string | null; city: string | null; address: string | null;
  phone: string | null; foreignNationalCode: string | null; passportNumber: string | null;
  contractReference: string | null; revision: number
};
type Key = Exclude<keyof Details, "revision">;
const fields: { key: Key; label: string; hint?: string; max: number; inputMode?: "numeric" | "tel" }[] = [
  { key: "economicCode", label: "شماره اقتصادی", hint: "tinb برای خریدار / tins برای فروشنده", max: 32, inputMode: "numeric" },
  { key: "branchCode", label: "کد شعبه", hint: "bbc برای خریدار / sbc برای فروشنده", max: 16, inputMode: "numeric" },
  { key: "postalCode", label: "کد پستی ده‌رقمی", hint: "bpc برای خریدار؛ اطلاعات مکانی فروشنده", max: 10, inputMode: "numeric" },
  { key: "province", label: "استان", max: 120 },
  { key: "city", label: "شهر", max: 120 },
  { key: "address", label: "نشانی دقیق", max: 500 },
  { key: "phone", label: "شماره تلفن", max: 20, inputMode: "tel" },
  { key: "foreignNationalCode", label: "کد فراگیر اتباع خارجی (در صورت نیاز)", max: 32 },
  { key: "passportNumber", label: "شماره گذرنامه (الگوهای مرتبط)", max: 32 },
  { key: "contractReference", label: "شناسه قرارداد (الگوهای مرتبط)", max: 64 }
];
const initial = (): Details => ({
  economicCode: null, branchCode: null, postalCode: null,
  province: null, city: null, address: null, phone: null,
  foreignNationalCode: null, passportNumber: null, contractReference: null, revision: 0
});
const api = (customerId: string, role: "buyer" | "seller", subjectId: string) =>
  "/api/party-workspace/customers/" + customerId + "/invoice-details/" + role + "/" + subjectId;

export default function InvoiceParticipantForm({ customerId, subjectId, role }:
  { customerId: string; subjectId: string; role: "buyer" | "seller" }) {
  const [details, setDetails] = useState<Details>(initial);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [invoiceType, setInvoiceType] = useState("1");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const res = await fetch(api(customerId, role, subjectId), { credentials: "same-origin", cache: "no-store" });
      if (!res.ok) throw new Error("اطلاعات مالیاتی این پرونده قابل دریافت نیست.");
      const stored = await res.json() as Details | null;
      setDetails(stored || initial());
    } catch (ex) { setError((ex as Error).message); }
    finally { setLoading(false); }
  }, [customerId, role, subjectId]);
  useEffect(() => { void load(); }, [load]);

  function update(key: Key, value: string) {
    setDetails(current => ({ ...current, [key]: value || null }));
  }
  const needBuyer = role === "buyer" && invoiceType === "1";
  const needSeller = role === "seller";
  const missing = [
    ...(needBuyer || needSeller ? !details.economicCode ? ["شماره اقتصادی"] : [] : []),
    ...(needBuyer ? !details.postalCode ? ["کد پستی"] : [] : []),
    ...(needSeller ? !details.address ? ["نشانی"] : [] : [])
  ];
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (saving) return;
    setSaving(true); setMessage(""); setError("");
    try {
      const res = await fetch(api(customerId, role, subjectId), {
        method: "PUT", credentials: "same-origin",
        headers: { "Content-Type": "application/json" }, body: JSON.stringify({
          ...details, expectedRevision: details.revision
        })
      });
      if (!res.ok) throw new Error(res.status === 409
        ? "اطلاعات توسط شخص دیگری ویرایش شده؛ ابتدا بازخوانی کنید."
        : res.status === 403 ? "مجوز ثبت این اطلاعات را ندارید."
        : "ثبت اطلاعات مالیاتی ناموفق بود؛ مقادیر را بررسی کنید.");
      const result = await res.json() as Details;
      setDetails(result); setMessage("اطلاعات سربرگ مالیاتی در دیتابیس تست ذخیره شد.");
    } catch (ex) { setError((ex as Error).message); }
    finally { setSaving(false); }
  }
  return <section className="invoice-participant" aria-busy={loading || saving}>
    <div className="taxpayer-section-heading"><h3>مشخصات تکمیلی {role === "seller" ? "فروشنده / مؤدی" : "خریدار"} برای صورتحساب</h3>
      <button className="identity-secondary" disabled={loading || saving} onClick={() => void load()}>بازخوانی</button></div>
    <p className="field-hint">شناسه ملی و نام شخص در پرونده اصلی ثبت می‌شوند. این بخش فقط مشخصات تکمیلی سربرگ صورتحساب را ذخیره می‌کند.</p>
    <label htmlFor={"invoice-kind-"+role}>نوع صورتحساب موردنظر برای بررسی آمادگی اطلاعات</label>
    <select id={"invoice-kind-"+role} value={invoiceType} onChange={e => setInvoiceType(e.target.value)}>
      <option value="1">نوع اول — مشخصات خریدار و فروشنده</option>
      <option value="2">نوع دوم — اطلاعات خریدار اختیاری</option>
      <option value="3">نوع سوم — رسید پرداخت (نیازمند مسیر جداگانه)</option>
    </select>
    {invoiceType === "3" ? <p className="party-error">نوع سوم مبتنی بر پایانه فروش است؛ تکمیل این فرم به معنی ایجاد رسید یا اتصال به پایانه نیست.</p> :
      <p className="invoice-help">{role === "buyer" && invoiceType === "2"
        ? "برای نوع دوم معمولاً درج مشخصات خریدار اختیاری است؛ اطلاعات فروشنده همچنان اهمیت دارد."
        : "برای صورتحساب نوع اول، اطلاعات هویتی طرفین در زمان صدور باید طبق الگو کنترل شوند."}</p>}
    {missing.length > 0 && invoiceType !== "3" && <p className="invoice-missing">
      اطلاعات پیشنهادی برای تکمیل: {missing.join("، ")}. اعتبارسنجی نهایی به نوع و الگوی صورتحساب وابسته است.
    </p>}
    {loading ? <div className="console-table-stage"><ConsoleLoader full label="در حال دریافت اطلاعات هویتی مالیاتی…" /></div> :
      <form className="invoice-fields" onSubmit={save}>
        {fields.map(f => <label key={f.key}>{f.label}
          {f.hint && <small>{f.hint}</small>}
          <input type="text" dir={f.inputMode ? "ltr" : "rtl"} inputMode={f.inputMode}
            maxLength={f.max} value={details[f.key] ?? ""}
            onChange={e => update(f.key, e.target.value)}
            placeholder={f.key === "postalCode" ? "۱۲۳۴۵۶۷۸۹۰" : undefined}/>
        </label>)}
        {error && <p role="alert" className="party-error">{error}</p>}
        {message && <p role="status" className="party-success">{message}</p>}
        <button className="identity-primary" disabled={saving}>{saving ? "در حال ثبت…" : "ذخیره مشخصات مالیاتی"}</button>
      </form>}
    {loading && error && <p role="alert" className="party-error">{error}</p>}
    <p className="field-hint">این فرم صحت شناسه نزد سازمان امور مالیاتی را تأیید نمی‌کند؛ اطلاعات کد حافظه و گواهی مالیاتی فروشنده در پرونده مستقل مؤدی مدیریت می‌شوند.</p>
  </section>;
}
