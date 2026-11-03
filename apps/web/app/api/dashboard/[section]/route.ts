import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";
import { ACCESS_COOKIE, currentIdentity, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";

export const dynamic = "force-dynamic";
const sections = new Set(["overview", "trends", "finance", "attention", "recent", "partner", "root"]);
const allowedKeys = new Set(["period", "from", "to", "taxpayerId", "partnerUserId"]);
type Context = { params: Promise<{ section: string }> };

export async function GET(request: NextRequest, context: Context) {
  const unavailable = (status: number, error: string) =>
    NextResponse.json({ error }, { status, headers: { "Cache-Control": "private, no-store" } });
  if (!identityUiEnabled()) return unavailable(404, "unavailable");
  const { section } = await context.params;
  if (!sections.has(section)) return unavailable(404, "unknown_operation");
  const actor = await currentIdentity();
  if (!actor) return unavailable(401, "unauthorized");
  const query = request.nextUrl.searchParams;
  if ([...query.keys()].some(key => !allowedKeys.has(key) || query.getAll(key).length !== 1))
    return unavailable(400, "invalid_filter");
  const administrator = actor.roles.includes("Level1") || actor.roles.includes("Level2");
  if (section === "finance" && !administrator) return unavailable(403, "forbidden");
  if (section === "partner" && (actor.roles.includes("Level1") || !actor.roles.includes("Level2")))
    return unavailable(403, "forbidden");
  if (section === "root" && !actor.roles.includes("Level1")) return unavailable(403, "forbidden");
  if (section === "root" && (query.has("taxpayerId") || query.has("partnerUserId")))
    return unavailable(400, "invalid_filter");
  if (section === "partner" && query.size > 0) return unavailable(400, "invalid_filter");
  if (query.has("partnerUserId") && (section !== "finance" || !actor.roles.includes("Level1")))
    return unavailable(403, "forbidden");
  if (query.has("taxpayerId") && (section === "finance" || section === "partner")) return unavailable(400, "invalid_filter");
  const bearer = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!bearer) return unavailable(401, "unauthorized");
  try {
    const endpoint = new URL("/api/dev/dashboard/" + section, identityApiOrigin());
    endpoint.search = query.toString();
    const response = await fetch(endpoint, {
      method: "GET",
      headers: { Authorization: "Bearer " + bearer, Accept: "application/json" },
      cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(15000)
    });
    if (!response.ok) return unavailable(response.status >= 500 ? 503 : response.status, "dashboard_unavailable");
    return new NextResponse(await response.text(), {
      status: 200,
      headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "private, no-store" }
    });
  } catch {
    return unavailable(503, "dashboard_unavailable");
  }
}
