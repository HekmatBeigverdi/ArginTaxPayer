import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../../lib/identity-origin.mjs";
type Ctx = { params: Promise<{ requestId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail = (status: number, error: string) => NextResponse.json({ error }, {
  status, headers: { "Cache-Control": "no-store" }
});
export async function POST(req: NextRequest, ctx: Ctx) {
  if (!identityUiEnabled()) return fail(404, "unavailable");
  const { requestId } = await ctx.params;
  if (!uuid.test(requestId)) return fail(404, "not_found");
  if (!isSameLocalOrigin(req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol))
    return fail(403, "origin_not_allowed");
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return fail(401, "unauthorized");
  let input: any;
  try {
    if (Number(req.headers.get("content-length") || 0) > 4096) return fail(413, "request_too_large");
    input = JSON.parse(await req.text());
  } catch { return fail(400, "invalid_request"); }
  if (!input || typeof input !== "object" || !Number.isSafeInteger(input.expectedRevision) ||
      input.expectedRevision < 1 || typeof input.approve !== "boolean" ||
      typeof input.decisionReason !== "string" || input.decisionReason.trim().length < 10 ||
      input.decisionReason.length > 1000 || Object.keys(input).some(x =>
        !["expectedRevision", "approve", "decisionReason"].includes(x)))
    return fail(400, "invalid_request");
  try {
    const response = await fetch(identityApiOrigin() +
      `/api/dev/admin/signing-change-requests/${requestId}/decision`, {
      method: "POST", cache: "no-store", redirect: "manual",
      headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
      body: JSON.stringify({ expectedRevision: input.expectedRevision,
        approve: input.approve, decisionReason: input.decisionReason.trim() }),
      signal: AbortSignal.timeout(20000)
    });
    if (!response.ok) return fail(response.status === 403 ? 403 :
      response.status === 409 ? 409 : response.status === 400 ? 400 : 503,
      response.status === 409 ? "conflict" : "review_failed");
    const data = await response.json();
    return NextResponse.json({ id: data.id, state: data.state,
      expiresAtUtc: data.expiresAtUtc ?? null, revision: data.revision },
      { headers: { "Cache-Control": "no-store" } });
  } catch { return fail(503, "service_unavailable"); }
}
