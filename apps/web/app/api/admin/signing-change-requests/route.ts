import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../lib/identity-origin.mjs";

const fail = (status: number, error: string) => NextResponse.json({ error }, {
  status, headers: { "Cache-Control": "no-store" }
});
export const dynamic = "force-dynamic";
export async function GET(req: NextRequest) {
  if (!identityUiEnabled()) return fail(404, "unavailable");
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return fail(401, "unauthorized");
  try {
    const response = await fetch(identityApiOrigin() + "/api/dev/admin/signing-change-requests", {
      headers: { Authorization: "Bearer " + token }, cache: "no-store",
      redirect: "manual", signal: AbortSignal.timeout(20000)
    });
    if (!response.ok) return fail(response.status === 403 ? 403 : 503, "access_denied_or_unavailable");
    const data = await response.json();
    if (!Array.isArray(data.requests) || data.requests.length > 100) return fail(502, "invalid_response");
    const projected = data.requests.map((x: any) => ({
      id: x.id, taxpayerName: x.taxpayerName, memoryIdentifier: x.memoryIdentifier,
      kind: x.kind, state: x.state, reasonCode: x.reasonCode, explanation: x.explanation,
      expectedKeyRevision: x.expectedKeyRevision, revision: x.revision,
      requestedAtUtc: x.requestedAtUtc, customerUserId: x.customerUserId
    }));
    return NextResponse.json({ requests: projected }, { headers: { "Cache-Control": "no-store" } });
  } catch { return fail(503, "service_unavailable"); }
}
