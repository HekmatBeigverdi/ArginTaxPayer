import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const failure = (status: number, error: string) =>
  NextResponse.json({ error }, { status, headers: { "Cache-Control": "no-store" } });

async function execute(req: NextRequest, params: Promise<{ path: string[] }>, method: "GET" | "POST") {
  if (!identityUiEnabled()) return failure(404, "unavailable");
  const { path } = await params;
  const valid = path.length === 2 && uuid.test(path[0]) && path[1] === "history" && method === "GET" ||
    path.length === 2 && uuid.test(path[0]) && path[1] === "grant" && method === "POST" ||
    path.length === 3 && uuid.test(path[0]) && uuid.test(path[1]) && path[2] === "revoke" && method === "POST";
  if (!valid) return failure(404, "not_found");
  if (method === "POST" &&
      !isSameLocalOrigin(req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol))
    return failure(403, "origin_not_allowed");
  const bearer = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!bearer) return failure(401, "unauthorized");
  const tail = path[1] === "history" ? "/delegates/history" :
    path[1] === "grant" ? "/delegates" : "/delegates/" + path[1] + "/revoke";
  let body: string | undefined;
  if (method === "POST") {
    if (!(req.headers.get("content-type") || "").startsWith("application/json"))
      return failure(415, "json_required");
    try {
      body = await req.text();
      if (body.length > 2048 || !body.trim()) return failure(400, "invalid_request");
      JSON.parse(body);
    } catch { return failure(400, "invalid_request"); }
  }
  try {
    const result = await fetch(identityApiOrigin() + "/api/dev/taxpayers/" + path[0] + tail, {
      method, cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(10000),
      headers: { Authorization: "Bearer " + bearer,
        ...(body ? { "Content-Type": "application/json" } : {}) },
      ...(body ? { body } : {})
    });
    if (result.ok) {
      const data = await result.text();
      if (data.length > 250000) return failure(502, "invalid_response");
      return new NextResponse(data, { status: result.status,
        headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
    }
    const status = [400, 401, 403, 404, 409].includes(result.status) ? result.status : 502;
    return failure(status, status === 409 ? "conflict" : "request_rejected");
  } catch { return failure(503, "service_unavailable"); }
}

type Context = { params: Promise<{ path: string[] }> };
export const dynamic = "force-dynamic";
export async function GET(req: NextRequest, ctx: Context) {
  return execute(req, ctx.params, "GET");
}
export async function POST(req: NextRequest, ctx: Context) {
  return execute(req, ctx.params, "POST");
}
