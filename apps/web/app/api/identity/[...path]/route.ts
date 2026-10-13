import { NextRequest, NextResponse } from "next/server";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";
import { ACCESS_COOKIE, REFRESH_COOKIE, identityBackend, identityUiEnabled } from "../../../../lib/identity";

type TokenResponse = { accessToken: string; refreshToken: string; accessExpiresAtUtc?: string; refreshExpiresAtUtc?: string };
const operations = new Set(["register", "login", "logout", "mfa/challenge", "mfa/complete", "mfa/enroll", "mfa/confirm", "refresh", "me", "sessions", "sessions/revoke", "phone/request", "phone/register/complete", "phone/login/complete", "phone/reset/complete", "admin/customers"]);
const cookieOptions = { httpOnly: true, sameSite: "strict" as const, secure: false, path: "/" };
const errorResult = (code: number, message: string) =>
  NextResponse.json({ error: message }, { status: code, headers: { "Cache-Control": "no-store" } });

function sameOrigin(req: NextRequest): boolean {
  return isSameLocalOrigin(req.headers.get("origin"), req.headers.get("host"), req.nextUrl.protocol);
}

function saveTokens(response: NextResponse, tokens: TokenResponse) {
  response.cookies.set(ACCESS_COOKIE, tokens.accessToken, { ...cookieOptions, maxAge: 600 });
  response.cookies.set(REFRESH_COOKIE, tokens.refreshToken, { ...cookieOptions, maxAge: 7 * 86400 });
}

function clearTokens(response: NextResponse) {
  response.cookies.delete(ACCESS_COOKIE);
  response.cookies.delete(REFRESH_COOKIE);
}

async function callIdentity(operation: string, req: NextRequest, method: "GET" | "POST") {
  if (!identityUiEnabled()) return errorResult(404, "unavailable");
  if (!operations.has(operation)) return errorResult(404, "unavailable");
  if (method === "POST" && !sameOrigin(req)) return errorResult(403, "origin_not_allowed");
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = req.cookies.get(REFRESH_COOKIE)?.value;
  if (operation === "logout") {
    if (token) {
      try {
        await identityBackend("logout", { method: "POST", headers: { Authorization: "Bearer " + token } });
      } catch { /* Always clear local credentials, even if backend is unavailable. */ }
    }
    const response = NextResponse.json({ status: "signed_out" });
    clearTokens(response);
    return response;
  }

  const backendPath: Record<string, string> = {
    register: "register", login: "login", refresh: "refresh", me: "authorization/me",
    "mfa/challenge": "mfa/challenge", "mfa/complete": "mfa/complete",
    "mfa/enroll": "mfa/enroll", "mfa/confirm": "mfa/confirm",
    sessions: "resources/sessions",
    "phone/request": "phone/request",
    "phone/register/complete": "phone/register/complete",
    "phone/login/complete": "phone/login/complete",
    "phone/reset/complete": "phone/reset/complete",
    "admin/customers": "admin/customers"
  };
  if (operation === "sessions/revoke") {
    let data: { sessionId?: string };
    try { data = await req.json(); } catch { return errorResult(400, "invalid_request"); }
    if (!data.sessionId || !/^[0-9a-f-]{36}$/i.test(data.sessionId)) return errorResult(400, "invalid_request");
    backendPath[operation] = "resources/sessions/" + data.sessionId + "/revoke";
  }
  const path = backendPath[operation];
  if (!path) return errorResult(404, "unavailable");
  if (["me", "sessions", "mfa/enroll", "mfa/confirm", "sessions/revoke", "admin/customers"].includes(operation) && !token)
    return errorResult(401, "unauthorized");

  let payload: unknown = undefined;
  if (method === "POST" && operation !== "sessions/revoke") {
    try { payload = operation === "refresh" ? { refreshToken: refresh } : await req.json(); }
    catch { return errorResult(400, "invalid_request"); }
  }

  try {
    const response = await identityBackend(path, {
      method,
      headers: {
        ...(method === "POST" && operation !== "sessions/revoke" ? { "Content-Type": "application/json" } : {}),
        ...(token ? { Authorization: "Bearer " + token } : {})
      },
      ...(method === "POST" && operation !== "sessions/revoke" ? { body: JSON.stringify(payload) } : {})
    });
    if (response.status === 204) return new NextResponse(null, { status: 204 });
    if (["login", "refresh", "mfa/complete", "phone/login/complete"].includes(operation) && response.ok) {
      const tokens = (await response.json()) as TokenResponse;
      if (!tokens.accessToken || !tokens.refreshToken) return errorResult(502, "invalid_response");
      const result = NextResponse.json({ status: "authenticated" });
      saveTokens(result, tokens);
      return result;
    }
    if (operation === "phone/request" && response.status === 429) {
      const body = await response.json().catch(() => ({})) as { retryAfterSeconds?: number };
      const remaining = Math.max(1, Math.min(900,
        Number.isFinite(body.retryAfterSeconds) ? Math.ceil(body.retryAfterSeconds!) : 60));
      return NextResponse.json({ error: "otp_cooldown", retryAfterSeconds: remaining },
        { status: 429, headers: { "Cache-Control": "no-store" } });
    }
    if (operation === "phone/request" && response.status === 409)
      return errorResult(409, "mobile_already_registered");
    if (operation === "phone/request" && response.status === 503)
      return errorResult(503, "otp_unavailable");
    const result = await response.text();
    // Never expose raw upstream diagnostics; for enrollment only, explicitly return
    // the one-time provisioning secret to an authenticated local development user.
    if (response.ok) {
      if (operation === "mfa/enroll" || operation === "mfa/confirm" || operation === "sessions" || operation === "me" || operation === "mfa/challenge") {
        return new NextResponse(result, { status: response.status, headers: {
          "Content-Type": "application/json", "Cache-Control": "no-store"
        } });
      }
      return NextResponse.json({ status: "ok" }, { status: response.status });
    }
    return errorResult([400, 401, 403, 404, 409, 429].includes(response.status) ? response.status : 502,
      response.status === 429 ? "too_many_attempts" : "request_rejected");
  } catch {
    return errorResult(503, "identity_service_unavailable");
  }
}

export async function POST(req: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  return callIdentity(path.join("/"), req, "POST");
}
export async function GET(req: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const operation = path.join("/");
  if (!["me", "sessions"].includes(operation)) return errorResult(404, "unavailable");
  return callIdentity(operation, req, "GET");
}
