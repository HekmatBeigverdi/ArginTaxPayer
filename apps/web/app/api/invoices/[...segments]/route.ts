import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

export const dynamic = "force-dynamic";
type Context = { params: Promise<{ segments: string[] }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function target(segments: string[], method: "GET" | "POST"): string | null {
  if (segments.length === 1 && segments[0] === "overview" && method === "GET")
    return "/api/dev/invoices";
  if (segments.length < 1 || !uuid.test(segments[0])) return null;
  if (segments.length === 1 && (method === "GET" || method === "POST"))
    return "/api/dev/taxpayers/" + segments[0] + "/invoices";
  if (segments.length === 2 && uuid.test(segments[1]) && method === "GET")
    return "/api/dev/taxpayers/" + segments[0] + "/invoices/" + segments[1];
  return null;
}
function error(status: number, code: string) {
  return NextResponse.json({ error: code }, {
    status, headers: { "Cache-Control": "no-store" }
  });
}
async function proxy(request: NextRequest, context: Context, method: "GET" | "POST") {
  if (!identityUiEnabled()) return error(404, "unavailable");
  const { segments } = await context.params;
  const url = target(segments, method);
  if (!url) return error(404, "unknown_operation");
  if (method === "POST" && !isSameLocalOrigin(
    request.headers.get("origin"), request.headers.get("host"), request.nextUrl.protocol))
    return error(403, "origin_not_allowed");
  const token = request.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return error(401, "unauthorized");
  let body: string | undefined;
  if (method === "POST") {
    if (!(request.headers.get("content-type") ?? "").toLowerCase().startsWith("application/json"))
      return error(415, "json_required");
    try {
      body = await request.text();
      if (!body || body.length > 2048) return error(400, "invalid_payload");
      const parsed: unknown = JSON.parse(body);
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed))
        return error(400, "invalid_payload");
      const data = parsed as Record<string, unknown>;
      if (Object.keys(data).some(k => !["issueDateUtc", "sellerDisplayName", "buyerDisplayName", "buyerPartyId"].includes(k)) ||
          typeof data.issueDateUtc !== "string" ||
          typeof data.sellerDisplayName !== "string" ||
          (data.buyerDisplayName !== undefined && data.buyerDisplayName !== null &&
           typeof data.buyerDisplayName !== "string") ||
          (data.buyerPartyId !== undefined && data.buyerPartyId !== null &&
           (typeof data.buyerPartyId !== "string" || !uuid.test(data.buyerPartyId))))
        return error(400, "unsupported_invoice_fields");
    } catch { return error(400, "invalid_payload"); }
  }
  try {
    const filtered = new URLSearchParams();
    if (segments.length === 1 && segments[0] === "overview") {
      for (const key of ["customerId", "taxpayerId"]) {
        const value = request.nextUrl.searchParams.get(key);
        if (value && uuid.test(value)) filtered.set(key, value);
        else if (value) return error(400, "invalid_filter");
      }
    }
    const response = await fetch(identityApiOrigin() + url + (filtered.size ? "?" + filtered : ""), {
      method, cache: "no-store", redirect: "manual",
      signal: AbortSignal.timeout(10000),
      headers: { Authorization: "Bearer " + token,
        ...(body ? { "Content-Type": "application/json" } : {}) },
      ...(body ? { body } : {})
    });
    if (response.status >= 300 && response.status < 400) return error(502, "unexpected_redirect");
    if (![200, 201].includes(response.status))
      return error([400, 401, 403, 404, 409].includes(response.status) ?
        response.status : 502, "request_rejected");
    const payload = await response.text();
    if (payload.length > 500000) return error(502, "response_too_large");
    return new NextResponse(payload, {
      status: response.status, headers: {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff"
      }
    });
  } catch { return error(503, "api_unavailable"); }
}
export async function GET(request: NextRequest, context: Context) {
  return proxy(request, context, "GET");
}
export async function POST(request: NextRequest, context: Context) {
  return proxy(request, context, "POST");
}
