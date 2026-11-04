/**
 * Check the browser-visible Origin against a strictly configured, exact host.
 * Do not trust arbitrary X-Forwarded-Host/Proto supplied by the browser.
 * In Production the TLS proxy may pass HTTP to Next.js, so nextUrl.protocol
 * is not an authority for the external scheme.
 * @param {string | null} origin
 * @param {string | null} host
 * @param {string} protocol
 */
export function isSameLocalOrigin(origin, host, protocol) {
  if (!origin || !host) return false;
  const production = process.env.NODE_ENV === "production";
  if (production) {
    const expected = process.env.ARGIN_PUBLIC_BASE_URL;
    if (expected !== "https://tax.argin.ir") return false;
    return host.toLowerCase() === "tax.argin.ir" && origin === expected;
  }
  if (protocol !== "http:" ||
      !/^(localhost|127\.0\.0\.1|\[::1\])(:[0-9]+)?$/i.test(host)) return false;
  try {
    return origin === new URL(`${protocol}//${host}`).origin;
  } catch {
    return false;
  }
}
