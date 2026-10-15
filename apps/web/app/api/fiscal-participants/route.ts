import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../lib/identity";

export const dynamic = "force-dynamic";
export async function GET(req: NextRequest) {
  if (!identityUiEnabled()) return NextResponse.json({ error: "not_found" }, { status: 404 });
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  const allowed = new URLSearchParams();
  for (const key of ["page", "pageSize", "type", "kind", "query"]) {
    const value = req.nextUrl.searchParams.get(key);
    if (value !== null) allowed.set(key, value.slice(0, 120));
  }
  try {
    const res = await fetch(identityApiOrigin() + "/api/dev/fiscal-participants?" + allowed, {
      headers: { Authorization: "Bearer " + token },
      method: "GET", cache: "no-store", redirect: "manual",
      signal: AbortSignal.timeout(15000)
    });
    if (!res.ok) return NextResponse.json({ error: "fiscal_directory_unavailable" },
      { status: [400, 401, 403, 404].includes(res.status) ? res.status : 502,
        headers: { "Cache-Control": "no-store" } });
    const body = await res.text();
    if (body.length > 300000) return NextResponse.json({ error: "response_too_large" }, { status: 502 });
    return new NextResponse(body, {
      status: 200, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" }
    });
  } catch {
    return NextResponse.json({ error: "backend_unavailable" }, { status: 503,
      headers: { "Cache-Control": "no-store" } });
  }
}
