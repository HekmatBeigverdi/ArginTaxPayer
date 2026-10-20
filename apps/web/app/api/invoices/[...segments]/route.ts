import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

export const dynamic = "force-dynamic";
type Context = { params: Promise<{ segments: string[] }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function target(segments: string[], method: "GET" | "POST"): string | null {
  if (segments[0] === "workspaces") {
    if (segments.length === 1 && (method === "GET" || method === "POST"))
      return "/api/dev/invoice-workspaces";
    if (segments.length === 3 && method === "GET" && uuid.test(segments[1]) &&
        segments[2] === "validation") return "/api/dev/invoice-workspaces/"+segments[1]+"/validation";
    if (segments.length === 2 && method === "GET" && uuid.test(segments[1]))
      return "/api/dev/invoice-workspaces/" + segments[1];
    return null;
  }
  if (segments.length === 2 && segments[0] === "references" && method === "GET" && uuid.test(segments[1]))
    return "/api/dev/invoice-references/" + segments[1];
  if (segments.length === 2 && segments[0] === "header-options" &&
      method === "GET" && uuid.test(segments[1]))
    return "/api/dev/invoices/header-options/" + segments[1];
  if (segments.length === 1 && segments[0] === "form-metadata" && method === "GET")
    return "/api/dev/invoices/form-metadata";
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
      if (!body || body.length > (segments[0] === "workspaces" && segments[2] !== "review" ? 120000 : 2048)) return error(400, "invalid_payload");
      const parsed: unknown = JSON.parse(body);
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed))
        return error(400, "invalid_payload");
      const data = parsed as Record<string, unknown>;
      if (segments[0] === "workspaces") {
        const permitted = ["id","taxpayerId","expectedRevision","lastCompletedStep",
          "issueDateUtc","buyerPartyId","type","pattern","lines",
          "invoiceDescription","settlementMethod","cashAmountRial","creditAmountRial",
          "contractNumber","serviceBillId","flightType","goldConstructionFeeRial",
          "goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial","fiscalMemoryId"];
        if (Object.keys(data).some(k=>!permitted.includes(k)) ||
            typeof data.taxpayerId !== "string" || !uuid.test(data.taxpayerId) ||
            typeof data.issueDateUtc !== "string" || !Array.isArray(data.lines) ||
            data.lines.length > 150 || typeof data.expectedRevision !== "number" ||
            typeof data.lastCompletedStep !== "number" ||
            (data.id != null && (typeof data.id !== "string" || !uuid.test(data.id))))
          return error(400,"unsupported_invoice_fields");
      } else if (Object.keys(data).some(k => !["issueDateUtc", "sellerDisplayName", "buyerDisplayName", "buyerPartyId"].includes(k)) ||
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
    if (segments.length === 1 && ["overview","workspaces"].includes(segments[0])) {
      for (const key of ["customerId", "taxpayerId", "page", "pageSize"]) {
        const value = request.nextUrl.searchParams.get(key);
        if (value && (key === "page" || key === "pageSize" ? /^[1-9]\d{0,5}$/.test(value) : uuid.test(value))) filtered.set(key, value);
        else if (value) return error(400, "invalid_filter");
      }
      if (segments[0] === "overview") {
        const reviewState = request.nextUrl.searchParams.get("reviewState");
        if (reviewState !== null) {
          if (!["Draft", "InReview", "ChangesRequested", "Approved", "WorkingDraft"].includes(reviewState))
            return error(400, "invalid_filter");
          filtered.set("reviewState", reviewState);
        }
        const search = request.nextUrl.searchParams.get("search");
        if (search !== null) {
          if (search.length > 128 || /[\x00-\x1f\x7f]/.test(search))
            return error(400, "invalid_filter");
          if (search.trim()) filtered.set("search", search.trim());
        }
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
