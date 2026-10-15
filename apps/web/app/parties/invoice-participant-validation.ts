/** Draft metadata validation, not the authority's invoice issuance rule engine.
 * All fields are independently optional; requiredness belongs to Phase07 invoice type/pattern.
 */
export type FiscalField = "economicCode" | "branchCode" | "postalCode" |
  "province" | "city" | "address" | "phone" | "foreignNationalCode" |
  "passportNumber" | "contractReference";

export const fieldSpecs: {
  key: FiscalField; label: string; max: number; inputMode?: "numeric" | "tel";
  hint?: string;
}[] = [
  { key: "economicCode", label: "شماره اقتصادی", max: 32,
    inputMode: "numeric", hint: "tinb (خریدار) / tins (فروشنده)" },
  { key: "branchCode", label: "کد شعبه", max: 16,
    inputMode: "numeric", hint: "bbc (خریدار) / sbc (فروشنده)" },
  { key: "postalCode", label: "کد پستی", max: 10,
    inputMode: "numeric", hint: "در صورت ورود، دقیقاً ۱۰ رقم" },
  { key: "province", label: "استان", max: 120 },
  { key: "city", label: "شهر", max: 120 },
  { key: "address", label: "نشانی", max: 500 },
  { key: "phone", label: "شماره تماس", max: 20, inputMode: "tel" },
  { key: "foreignNationalCode", label: "کد فراگیر اتباع خارجی", max: 32 },
  { key: "passportNumber", label: "شماره گذرنامه", max: 32 },
  { key: "contractReference", label: "شناسه قرارداد", max: 64 }
];

export const digitNormal = (s: string) => s.replace(/[۰-۹]/g, c =>
  String(c.charCodeAt(0) - 0x06F0)).replace(/[٠-٩]/g, c =>
  String(c.charCodeAt(0) - 0x0660));
export function normalizeFiscalField(key: FiscalField, input: string): string {
  const value = input.trim().normalize("NFC");
  return ["economicCode", "branchCode", "postalCode", "phone",
    "foreignNationalCode", "passportNumber"].includes(key) ? digitNormal(value) : value;
}
export function validateFiscalField(key: FiscalField, raw: string): string | null {
  const value = normalizeFiscalField(key, raw);
  // Empty/null is permitted even in one-field updates. Invoice issuance validates completeness.
  if (!value) return null;
  const spec = fieldSpecs.find(s => s.key === key)!;
  if (value.length > spec.max) return `حداکثر ${spec.max} نویسه مجاز است.`;
  if (/[\u0000-\u001f\u007f]/u.test(value)) return "نویسه کنترلی مجاز نیست.";
  if (key === "postalCode" && !/^\d{10}$/.test(value))
    return "کد پستی در صورت ورود باید دقیقاً ۱۰ رقم باشد.";
  if (["economicCode", "branchCode"].includes(key) && !/^\d+$/.test(value))
    return "فقط رقم‌های فارسی یا انگلیسی مجاز هستند.";
  if (key === "phone" && !/^[+\d -]+$/.test(value))
    return "شماره تماس فقط می‌تواند شامل رقم، +، فاصله و خط تیره باشد.";
  // Other identifiers may legitimately be alphanumeric under different invoice patterns.
  return null;
}
