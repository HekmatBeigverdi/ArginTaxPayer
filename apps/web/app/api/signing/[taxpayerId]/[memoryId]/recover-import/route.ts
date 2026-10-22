import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../../lib/identity-origin.mjs";

type Context = { params: Promise<{ taxpayerId: string; memoryId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const allowedErrors = new Set(["unauthorized", "reauthentication_or_scope_required",
  "no_pending_import", "integrity_failure", "recovery_required", "custody_unavailable",
  "recovery_timeout", "service_unavailable"]);
const allowedStages = new Set(["authorization", "fresh_login", "pending_record", "envelope_format",
  "envelope_missing", "decrypt", "pkcs8", "provider_version", "journal", "provider_disabled"]);
const result = (status: number, body: unknown) => NextResponse.json(body, {
  status, headers: { "Cache-Control": "no-store", Pragma: "no-cache" }
});
export const dynamic = "force-dynamic";
export async function POST(req: NextRequest, context: Context) {
  if (!identityUiEnabled()) return result(404, { error: "unavailable" });
  const { taxpayerId, memoryId } = await context.params;
  if (!uuid.test(taxpayerId) || !uuid.test(memoryId)) return result(404, { error: "not_found" });
  if (!isSameLocalOrigin(req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol))
    return result(403, { error: "origin_not_allowed" });
  if (req.headers.get("content-type") || Number(req.headers.get("content-length") || 0) > 0)
    return result(415, { error: "body_not_allowed" });
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return result(401, { error: "unauthorized" });
  try {
    const upstream = await fetch(identityApiOrigin() +
      `/api/dev/signing/${taxpayerId}/${memoryId}/recover-import`, {
      method: "POST", headers: { Authorization: "Bearer " + token },
      cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(30000)
    });
    const body = await upstream.json().catch(() => null);
    if (!upstream.ok) return result([401, 403, 404, 409, 415, 429, 503, 504].includes(upstream.status)
      ? upstream.status : 502, { error: allowedErrors.has(body?.error) ? body.error : "service_unavailable",
        stage: allowedStages.has(body?.stage) ? body.stage : null });
    return result(200, { status: body?.status === "recovered" ? "recovered" : "indeterminate" });
  } catch { return result(503, { error: "service_unavailable" }); }
}
