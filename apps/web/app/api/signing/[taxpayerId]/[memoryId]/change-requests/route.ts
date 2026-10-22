import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../../lib/identity-origin.mjs";

type Context = { params: Promise<{ taxpayerId: string; memoryId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const reasons = /^[a-z][a-z0-9_]{2,63}$/;
const allowErrors = new Set(["invalid_request", "forbidden", "not_found", "conflict",
  "second_memory_requires_plan_review", "key_not_ready"]);
const reply = (status: number, value: unknown) => NextResponse.json(value,
  { status, headers: { "Cache-Control": "no-store" } });
const validItem = (item: any) => item && uuid.test(item.id) &&
  uuid.test(item.fiscalMemoryId) && Number.isInteger(item.kind) &&
  item.kind >= 1 && item.kind <= 3 && Number.isInteger(item.state) &&
  item.state >= 1 && item.state <= 5 &&
  Number.isSafeInteger(item.expectedKeyRevision) && item.expectedKeyRevision > 0 &&
  Number.isSafeInteger(item.revision) && typeof item.reasonCode === "string" &&
  reasons.test(item.reasonCode) && typeof item.explanation === "string" &&
  item.explanation.length <= 1000 && typeof item.requestedAtUtc === "string";
const project = (x: any) => ({
  id: x.id, fiscalMemoryId: x.fiscalMemoryId, kind: x.kind, state: x.state,
  reasonCode: x.reasonCode, explanation: x.explanation,
  expectedKeyRevision: x.expectedKeyRevision, revision: x.revision,
  requestedAtUtc: x.requestedAtUtc
});

export const dynamic = "force-dynamic";
async function proxy(req: NextRequest, ctx: Context, method: "GET" | "POST") {
  if (!identityUiEnabled()) return reply(404, { error: "unavailable" });
  const { taxpayerId, memoryId } = await ctx.params;
  if (!uuid.test(taxpayerId) || !uuid.test(memoryId)) return reply(404, { error: "not_found" });
  if (method === "POST" && !isSameLocalOrigin(req.headers.get("origin"),
      req.headers.get("host"), req.nextUrl.protocol)) return reply(403, { error: "origin_not_allowed" });
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return reply(401, { error: "unauthorized" });
  try {
    let body: string | undefined;
    if (method === "POST") {
      if (req.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json")
        return reply(415, { error: "json_required" });
      if (Number(req.headers.get("content-length") || 0) > 4096)
        return reply(413, { error: "request_too_large" });
      const raw = await req.text();
      if (raw.length > 2048) return reply(413, { error: "request_too_large" });
      let parsed: any;
      try { parsed = JSON.parse(raw); }
      catch { return reply(400, { error: "invalid_request" }); }
      if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed) ||
          !uuid.test(parsed.requestId) || !Number.isSafeInteger(parsed.expectedKeyRevision) ||
          parsed.expectedKeyRevision < 1 || ![1, 2, 3].includes(parsed.kind) ||
          typeof parsed.reasonCode !== "string" || !reasons.test(parsed.reasonCode) ||
          typeof parsed.explanation !== "string" || parsed.explanation.length < 10 ||
          parsed.explanation.length > 1000 ||
          Object.keys(parsed).some(k => !["requestId","expectedKeyRevision","kind","reasonCode","explanation"].includes(k)))
        return reply(400, { error: "invalid_request" });
      body = JSON.stringify(parsed);
    }
    const upstream = await fetch(identityApiOrigin() +
      `/api/dev/signing/${taxpayerId}/${memoryId}/change-requests`, {
      method, headers: { Authorization: "Bearer " + token,
        ...(body ? { "Content-Type": "application/json" } : {}) },
      ...(body ? { body } : {}), cache: "no-store", redirect: "manual",
      signal: AbortSignal.timeout(20000)
    });
    if (!upstream.ok) {
      const data = await upstream.json().catch(() => null);
      return reply([400, 401, 403, 404, 409, 413, 415, 429, 503].includes(upstream.status) ?
        upstream.status : 502, { error: allowErrors.has(data?.error) ? data.error : "service_unavailable" });
    }
    const data = await upstream.json();
    if (method === "GET") {
      if (!Array.isArray(data.requests) || data.requests.length > 30 ||
          data.requests.some((x: unknown) => !validItem(x)))
        return reply(502, { error: "invalid_response" });
      return reply(200, { currentKeyRevision: Number.isSafeInteger(data.currentKeyRevision) ?
        data.currentKeyRevision : null, requests: data.requests.map(project) });
    }
    return validItem(data) ? reply(upstream.status, project(data)) :
      reply(502, { error: "invalid_response" });
  } catch { return reply(503, { error: "service_unavailable" }); }
}
export function GET(req: NextRequest, ctx: Context) { return proxy(req, ctx, "GET"); }
export function POST(req: NextRequest, ctx: Context) { return proxy(req, ctx, "POST"); }
