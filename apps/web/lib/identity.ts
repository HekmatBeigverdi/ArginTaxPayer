import { cookies } from "next/headers";

export const ACCESS_COOKIE = "argin_identity_access";
export const REFRESH_COOKIE = "argin_identity_refresh";
const defaultOrigin = "http://127.0.0.1:15089";

export function identityApiOrigin(): string {
  const value = process.env.ARGIN_IDENTITY_API_INTERNAL_BASE_URL || defaultOrigin;
  const url = new URL(value);
  if (url.protocol !== "http:" || !["127.0.0.1", "localhost", "::1"].includes(url.hostname) ||
      url.username || url.password || url.pathname !== "/" || url.search || url.hash) {
    throw new Error("Identity API must use a dedicated local development HTTP origin.");
  }
  return url.origin;
}

export function identityUiEnabled(): boolean {
  return process.env.NODE_ENV === "development" && process.env.ARGIN_IDENTITY_UI_ENABLED === "true";
}

export async function identityBackend(path: string, options: RequestInit = {}) {
  if (!identityUiEnabled()) throw new Error("Identity UI is disabled.");
  return fetch(identityApiOrigin() + "/api/dev/identity/" + path, {
    ...options,
    cache: "no-store",
    redirect: "manual",
    signal: AbortSignal.timeout(10000)
  });
}

export async function currentIdentity(): Promise<{ userId: string; roles: string[] } | null> {
  if (!identityUiEnabled()) return null;
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return null;
  try {
    const result = await identityBackend("authorization/me", {
      headers: { Authorization: "Bearer " + token }
    });
    if (!result.ok) return null;
    const data: unknown = await result.json();
    if (!data || typeof data !== "object" || !("userId" in data) || !("roles" in data)) return null;
    const record = data as { userId: string; roles: string[] };
    return typeof record.userId === "string" && Array.isArray(record.roles)
      ? record : null;
  } catch {
    return null;
  }
}
