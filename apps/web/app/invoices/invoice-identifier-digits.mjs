/** Preserve identifier length/leading zeros while accepting Persian and Arabic digits. */
export function normalizeInvoiceIdentifier(value) {
  return value.trim().replace(/[۰-۹]/g, c => String(c.charCodeAt(0) - 0x06f0))
    .replace(/[٠-٩]/g, c => String(c.charCodeAt(0) - 0x0660));
}
