import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
type Context = { params: Promise<{ path: string[] }> };
type Method = "GET" | "POST" | "PUT" | "DELETE";
const reject = (status: number) => NextResponse.json({ error: "request_rejected" },
  { status, headers: { "Cache-Control": "no-store" } });

function target(path: string[], method: Method): string | null {
  if (path.length === 1 && path[0] === "mine" && method === "GET")
    return "/api/dev/customer/self-service";
  if (path.length < 2 || path[0] !== "customers" || !uuid.test(path[1])) return null;
  const rest = path.slice(2);
  if (rest.length === 1 && rest[0] === "profile" && (method === "GET" || method === "PUT"))
    return "/api/dev/customers/" + path[1] + "/profile";
  if (rest[0] === "taxpayer-memberships") {
    if (rest.length === 1 && method === "GET" ||
        rest.length === 2 && uuid.test(rest[1]) && (method === "POST" || method === "DELETE"))
      return "/api/dev/customers/" + path[1] + "/" + rest.join("/");
    return null;
  }
  if (rest[0] !== "parties") return null;
  const suffix = rest.slice(1);
  const ok =
    suffix.length === 0 && method === "GET" ||
    suffix.length === 1 && ["natural", "legal"].includes(suffix[0]) && method === "POST" ||
    suffix.length === 2 && uuid.test(suffix[0]) &&
      (["natural", "legal"].includes(suffix[1]) && ["GET", "PUT"].includes(method) ||
       ["details", "duplicates"].includes(suffix[1]) && method === "GET") ||
    suffix.length === 3 && suffix[0] === "duplicates" && suffix[1] === "reviews" && false ||
    suffix.length === 2 && suffix[0] === "duplicates" && suffix[1] === "reviews" && method === "POST" ||
    suffix.length === 2 && uuid.test(suffix[0]) &&
      ["contacts", "addresses"].includes(suffix[1]) && method === "POST" ||
    suffix.length === 3 && uuid.test(suffix[0]) &&
      ["contacts", "addresses"].includes(suffix[1]) &&
      uuid.test(suffix[2]) && ["PUT", "DELETE"].includes(method);
  return ok ? "/api/dev/customers/" + path[1] + "/parties" +
    (suffix.length ? "/" + suffix.join("/") : "") : null;
}

async function handle(req: NextRequest, ctx: Context, method: Method) {
  if (!identityUiEnabled()) return reject(404);
  const { path } = await ctx.params;
  const url = target(path, method);
  if (!url) return reject(404);
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return reject(401);
  if (method !== "GET" && !isSameLocalOrigin(
      req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol))
    return reject(403);
  let payload: string | undefined;
  if (method === "POST" || method === "PUT") {
    if (!(req.headers.get("content-type") || "").startsWith("application/json")) return reject(415);
    try {
      payload = await req.text();
      if (!payload || payload.length > 8192) return reject(400);
      JSON.parse(payload);
    } catch { return reject(400); }
  }
  const allowed = new URLSearchParams();
  for (const key of ["page", "pageSize", "query", "kind", "includeHistory", "expectedRevision"]) {
    const value = req.nextUrl.searchParams.get(key);
    if (value !== null) allowed.set(key, value.slice(0, 120));
  }
  try {
    const response = await fetch(identityApiOrigin() + url + (allowed.size ? "?" + allowed : ""), {
      method, cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(10000),
      headers: { Authorization: "Bearer " + token,
        ...(payload ? { "Content-Type": "application/json" } : {}) },
      ...(payload ? { body: payload } : {})
    });
    if (![200, 201].includes(response.status))
      return reject([400, 401, 403, 404, 409].includes(response.status) ? response.status : 502);
    const body = await response.text();
    if (body.length > 500000) return reject(502);
    return new NextResponse(body, {
      status: response.status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" }
    });
  } catch { return reject(503); }
}

export const dynamic = "force-dynamic";
export async function GET(req: NextRequest, ctx: Context) { return handle(req, ctx, "GET"); }
export async function POST(req: NextRequest, ctx: Context) { return handle(req, ctx, "POST"); }
export async function PUT(req: NextRequest, ctx: Context) { return handle(req, ctx, "PUT"); }
export async function DELETE(req: NextRequest, ctx: Context) { return handle(req, ctx, "DELETE"); }
