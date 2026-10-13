/**
 * Compare against the browser-facing Host, since Next.js may reconstruct
 * nextUrl with a different loopback hostname. Only local HTTP is supported.
 * @param {string | null} origin
 * @param {string | null} host
 * @param {string} protocol
 */
export function isSameLocalOrigin(origin, host, protocol) {
  if (!origin || !host || protocol !== "http:" ||
      !/^(localhost|127\.0\.0\.1|\[::1\])(:[0-9]+)?$/i.test(host)) return false;
  try {
    return origin === new URL(`${protocol}//${host}`).origin;
  } catch {
    return false;
  }
}
