import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const deny = (status: number, error: string) => NextResponse.json(
  { error }, { status, headers: { "Cache-Control": "no-store" } });

function validPath(parts: string[], method: "GET" | "POST" | "PATCH") {
  if (method === "GET") {
    return (parts.length === 1 && parts[0] === "list") ||
      (parts.length === 1 && uuid.test(parts[0])) ||
      (parts.length === 2 && uuid.test(parts[0]) && parts[1] === "memories") ||
      (parts.length === 4 && uuid.test(parts[0]) && parts[1] === "memories" &&
        uuid.test(parts[2]) && parts[3] === "certificates");
  }
  if (method === "POST") return (parts.length === 1 && parts[0] === "new") ||
    (parts.length === 2 && uuid.test(parts[0]) && ["memories", "archive"].includes(parts[1])) ||
    (parts.length === 4 && uuid.test(parts[0]) && parts[1] === "memories" && uuid.test(parts[2]) &&
      ["classify-authority", "verify-and-activate"].includes(parts[3]));
  return parts.length === 2 && uuid.test(parts[0]) &&
    ["display-name", "legal-identifier"].includes(parts[1]);
}

async function proxy(req: NextRequest, params: Promise<{ path: string[] }>,
  method: "GET" | "POST" | "PATCH") {
  if (!identityUiEnabled()) return deny(404, "unavailable");
  const { path } = await params;
  if (!validPath(path, method)) return deny(404, "unknown_operation");
  if (method !== "GET" &&
      !isSameLocalOrigin(req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol))
    return deny(403, "origin_not_allowed");
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return deny(401, "unauthorized");
  let payload: string | undefined;
  if (method !== "GET") {
    const type = req.headers.get("content-type") || "";
    if (!type.toLowerCase().startsWith("application/json")) return deny(415, "json_required");
    try {
      payload = await req.text();
      if (payload.length > 2048 || !payload.trim()) return deny(400, "invalid_request");
      const parsed = JSON.parse(payload);
      if (path.length === 4 && ["classify-authority", "verify-and-activate"].includes(path[3])) {
        if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) ||
            !Number.isSafeInteger(parsed.expectedRevision) || parsed.expectedRevision < 1 ||
            Object.keys(parsed).some(key => !["expectedRevision", ...(path[3] === "classify-authority" ? ["authorityEnvironment"] : [])].includes(key)) ||
            path[3] === "classify-authority" && ![1, 2].includes(parsed.authorityEnvironment))
          return deny(400, "invalid_request");
      }
    } catch { return deny(400, "invalid_request"); }
  }
  try {
    const response = await fetch(identityApiOrigin() + "/api/dev/taxpayers" +
      (path[0] === "list" || path[0] === "new" ? "" : "/" + path.join("/")), {
      method, headers: {
        Authorization: "Bearer " + token,
        ...(payload ? { "Content-Type": "application/json" } : {})
      }, ...(payload ? { body: payload } : {}),
      cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(path[3] === "verify-and-activate" ? 45000 : 10000)
    });
    if ([200, 201].includes(response.status)) {
      const body = await response.text();
      if (body.length > 1000000) return deny(502, "invalid_response");
      return new NextResponse(body, { status: response.status, headers: {
        "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store"
      } });
    }
    const status = [400, 401, 403, 404, 409, 422].includes(response.status) ? response.status : 502;
    if (path.length === 4 && ["classify-authority", "verify-and-activate"].includes(path[3])) {
      const raw = await response.text();
      if (raw.length <= 4096) {
        try {
          const value = JSON.parse(raw);
          if (typeof value?.error === "string" && /^[a-z0-9_.]{1,100}$/.test(value.error))
            return deny(status, value.error);
        } catch { /* Fall through to a bounded generic error. */ }
      }
    }
    return deny(status, status === 409 ? "conflict" : status === 404 ? "not_found" :
      status === 401 ? "unauthorized" : status === 403 ? "forbidden" : "request_rejected");
  } catch { return deny(503, "service_unavailable"); }
}

type Context = { params: Promise<{ path: string[] }> };
export const dynamic = "force-dynamic";
export async function GET(req: NextRequest, ctx: Context) { return proxy(req, ctx.params, "GET"); }
export async function POST(req: NextRequest, ctx: Context) { return proxy(req, ctx.params, "POST"); }
export async function PATCH(req: NextRequest, ctx: Context) { return proxy(req, ctx.params, "PATCH"); }
