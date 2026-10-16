import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";

export const dynamic = "force-dynamic";
const allowed = [
  /^official$/,
  /^units$/,
  /^customers\/[0-9a-f-]{36}\/(products|services)$/,
  /^customers\/[0-9a-f-]{36}\/(products|services)\/[0-9a-f-]{36}$/,
  /^customers\/[0-9a-f-]{36}\/items\/[0-9a-f-]{36}\/tax-mappings$/,
  /^customers\/[0-9a-f-]{36}\/tax-mappings\/[0-9a-f-]{36}\/close$/,
];
function verifyRoute(path: string, method: string): boolean {
  if (!allowed.some(rule => rule.test(path))) return false;
  if (method === "GET") return !path.endsWith("/close");
  if (method === "POST") return path !== "official" && path !== "units" && !path.endsWith("/close");
  if (method === "PUT") return /\/(products|services)\/[0-9a-f-]{36}$/.test(path) || path.endsWith("/close");
  return false;
}
async function handle(request: NextRequest, segments: string[], method: string) {
  if (!identityUiEnabled()) return NextResponse.json({ error: "not_found" }, { status: 404 });
  const path = segments.join("/");
  if (!verifyRoute(path, method)) return NextResponse.json({ error: "not_found" }, { status: 404 });
  if (method !== "GET" && request.headers.get("origin") !== request.nextUrl.origin)
    return NextResponse.json({ error: "origin_not_allowed" }, { status: 403 });
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  if (method !== "GET" && Number(request.headers.get("content-length") ?? 0) > 8192)
    return NextResponse.json({ error: "payload_too_large" }, { status: 413 });
  try {
    const body = method === "GET" ? undefined : await request.text();
    if (body && body.length > 8192)
      return NextResponse.json({ error: "payload_too_large" }, { status: 413 });
    const url = identityApiOrigin() + "/api/dev/catalog/" + path +
      (request.nextUrl.search ? request.nextUrl.search : "");
    const response = await fetch(url, {
      method,
      headers: {
        Authorization: "Bearer " + token,
        Accept: "application/json",
        ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      },
      body,
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(10000),
    });
    const payload = await response.text();
    if (payload.length > 1_000_000)
      return NextResponse.json({ error: "response_too_large" }, { status: 502 });
    const status = response.status >= 300 && response.status < 400 ? 502 : response.status;
    return new NextResponse(payload, {
      status,
      headers: {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch {
    return NextResponse.json({ error: "api_unavailable" }, { status: 503 });
  }
}
type Params = { params: Promise<{ segments: string[] }> };
export async function GET(request: NextRequest, { params }: Params) {
  return handle(request, (await params).segments, "GET");
}
export async function POST(request: NextRequest, { params }: Params) {
  return handle(request, (await params).segments, "POST");
}
export async function PUT(request: NextRequest, { params }: Params) {
  return handle(request, (await params).segments, "PUT");
}
