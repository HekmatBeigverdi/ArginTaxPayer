import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityDashboardOnlyEnabled, identityUiEnabled } from "../../../../lib/identity";

export const dynamic = "force-dynamic";
const allowed = new Set(["customerId", "period", "from", "to", "subject", "environment"]);
const headers = { "Cache-Control": "private, no-store", "Content-Type": "application/json; charset=utf-8" };

export async function GET(request: NextRequest) {
  if (!identityUiEnabled() || identityDashboardOnlyEnabled())
    return NextResponse.json({ error: "unavailable" }, { status: 404, headers });
  const token = request.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ error: "unauthorized" }, { status: 401, headers });

  const query = request.nextUrl.searchParams;
  for (const [key, value] of query) {
    if (!allowed.has(key) || query.getAll(key).length !== 1 || !value || value.length > 36)
      return NextResponse.json({ error: "invalid_filter" }, { status: 400, headers });
  }
  try {
    const url = new URL("/api/dev/reports/invoices", identityApiOrigin());
    url.search = query.toString();
    const response = await fetch(url, {
      cache: "no-store", redirect: "manual",
      headers: { Authorization: "Bearer " + token, Accept: "application/json" },
      signal: AbortSignal.any([request.signal, AbortSignal.timeout(30000)])
    });
    if (!response.ok) {
      const status = [400, 401, 403, 404].includes(response.status) ? response.status : 503;
      return NextResponse.json({ error: "report_unavailable" }, { status, headers });
    }
    return new NextResponse(response.body, { headers });
  } catch {
    return NextResponse.json({ error: "report_unavailable" }, { status: 503, headers });
  }
}
