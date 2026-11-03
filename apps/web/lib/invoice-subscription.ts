"use client";

/** Only server subscription denials navigate; never trust a response-provided URL. */
export function redirectForInvoiceSubscription(value: unknown): boolean {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const result = value as Record<string, unknown>;
  const code = result.code ?? result.error ?? result.safeCode;
  if (!["subscription_required", "subscription_expired", "subscription_exhausted"].includes(String(code)))
    return false;
  window.location.assign("/customer-plans");
  return true;
}
