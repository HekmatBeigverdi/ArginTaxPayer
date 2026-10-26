import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../lib/identity-origin.mjs";

export const dynamic = "force-dynamic";
type Context = { params: Promise<{ taxpayerId: string; invoiceId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function reject(status: number, error: string) {
  return NextResponse.json({ error, submissionEnabled: false }, {
    status, headers: { "Cache-Control": "no-store" }
  });
}

async function dispatch(request: NextRequest, context: Context, method: "GET" | "POST") {
  if (!identityUiEnabled()) return reject(404, "unavailable");
  const { taxpayerId, invoiceId } = await context.params;
  if (!uuid.test(taxpayerId) || !uuid.test(invoiceId)) return reject(404, "not_found");
  if (method === "POST" && !isSameLocalOrigin(
    request.headers.get("origin"), request.headers.get("host"),
    request.nextUrl.protocol)) return reject(403, "origin_not_allowed");
  if (method === "POST" && (Number(request.headers.get("content-length") ?? "0") > 0))
    return reject(400, "send_payload_not_supported");

  const bearer = request.cookies.get(ACCESS_COOKIE)?.value;
  if (!bearer) return reject(401, "unauthorized");
  const action = method === "GET" ? "status" : "send";
  const target = "/api/dev/taxpayers/" + taxpayerId + "/invoices/" +
    invoiceId + "/dispatch/" + action;
  try {
    const response = await fetch(identityApiOrigin() + target, {
      method,
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(method === "POST" ? 45000 : 10000),
      headers: { Authorization: "Bearer " + bearer }
    });
    if (response.status >= 300 && response.status < 400)
      return reject(502, "unexpected_redirect");
    if (![200, 400, 401, 403, 404, 409, 503].includes(response.status))
      return reject(502, "unexpected_api_status");
    const raw = await response.text();
    if (raw.length > 4096) return reject(502, "response_too_large");
    let payload: unknown;
    try { payload = JSON.parse(raw); } catch { return reject(502, "invalid_api_response"); }
    if (!payload || typeof payload !== "object" || Array.isArray(payload))
      return reject(502, "invalid_api_response");
    const value = payload as Record<string, unknown>;
    // Only a small bounded and explicitly allowlisted status reaches the browser.
    return NextResponse.json({
      environment: value.environment === "Sandbox" ? "Sandbox" : "Mock",
      submissionEnabled: value.submissionEnabled === true,
      inquiryEnabled: false,
      fiscalAccepted: false,
      status: typeof value.status === "string" &&
        /^[A-Za-z]{1,30}$/.test(value.status) ? value.status : "Unavailable",
      safeCode: typeof value.safeCode === "string" && /^[a-z0-9_.-]{1,100}$/.test(value.safeCode)
        ? value.safeCode : undefined,
      referenceNumber: typeof value.referenceNumber === "string" && /^[A-Za-z0-9_-]{1,128}$/.test(value.referenceNumber)
        ? value.referenceNumber : undefined,
      error: typeof value.error === "string" &&
        /^[a-z_]{1,80}$/.test(value.error) ? value.error : undefined
    }, { status: response.status, headers: { "Cache-Control": "no-store" } });
  } catch {
    return reject(503, "api_unavailable");
  }
}

export async function GET(request: NextRequest, context: Context) {
  return dispatch(request, context, "GET");
}
export async function POST(request: NextRequest, context: Context) {
  return dispatch(request, context, "POST");
}
