import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const error = (status: number) => NextResponse.json({ error: "request_rejected" },
  { status, headers: { "Cache-Control": "no-store" } });
type Context = { params: Promise<{ path: string[] }> };
async function handle(req: NextRequest, ctx: Context, method: "GET" | "POST") {
  if (!identityUiEnabled()) return error(404);
  const { path } = await ctx.params;
  const valid = method === "GET" && path.length === 1 && path[0] === "list" ||
    method === "GET" && path.length === 1 && uuid.test(path[0]) ||
    method === "POST" && path.length === 2 && uuid.test(path[0]) && path[1] === "transfer";
  if (!valid) return error(404);
  if (method === "POST" && !isSameLocalOrigin(req.headers.get("origin"),
      req.headers.get("host"), req.nextUrl.protocol)) return error(403);
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return error(401);
  let payload: string | undefined;
  if (method === "POST") {
    if (!(req.headers.get("content-type") || "").startsWith("application/json")) return error(415);
    try {
      payload = await req.text();
      if (!payload || payload.length > 2048) return error(400);
      JSON.parse(payload);
    } catch { return error(400); }
  }
  const target = path[0] === "list" ? "" : "/" + path.join("/");
  try {
    const response = await fetch(identityApiOrigin() + "/api/dev/customer-owners" + target, {
      method, cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(10000),
      headers: { Authorization: "Bearer " + token, ...(payload ? { "Content-Type": "application/json" } : {}) },
      ...(payload ? { body: payload } : {})
    });
    if (!response.ok) return error([400, 401, 403, 404, 409].includes(response.status) ? response.status : 502);
    const body = await response.text();
    if (body.length > 500000) return error(502);
    return new NextResponse(body, { status: response.status, headers: {
      "Cache-Control": "no-store", "Content-Type": "application/json" } });
  } catch { return error(503); }
}
export const dynamic = "force-dynamic";
export async function GET(req: NextRequest, ctx: Context) { return handle(req, ctx, "GET"); }
export async function POST(req: NextRequest, ctx: Context) { return handle(req, ctx, "POST"); }
