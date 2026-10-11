/**
 * Developer-only loopback probe. No browser fetch, credentials, external URLs,
 * taxpayer data or API proxying. Production always reports "disabled".
 */
export type ProbeState = "healthy" | "unavailable" | "not-configured";

export type ApiHealthState = Readonly<{
  liveness: ProbeState;
  readiness: ProbeState;
}>;

const NOT_CONFIGURED: ApiHealthState = {
  liveness: "not-configured",
  readiness: "not-configured",
};

export function parseLocalApiBaseUrl(
  raw: string | undefined,
  environment: string | undefined,
): string | null {
  if (environment !== "development" || !raw) return null;
  try {
    const url = new URL(raw);
    if (
      url.protocol !== "http:" ||
      !["127.0.0.1", "localhost", "[::1]"].includes(url.hostname) ||
      url.username ||
      url.password ||
      url.pathname !== "/" ||
      url.search ||
      url.hash
    ) return null;
    return url.origin;
  } catch {
    return null;
  }
}

async function probe(base: string, path: "/health/live" | "/health/ready"): Promise<ProbeState> {
  try {
    const response = await fetch(base + path, {
      method: "GET",
      cache: "no-store",
      signal: AbortSignal.timeout(1500),
      headers: { Accept: "application/json" },
    });
    if (!response.ok) return "unavailable";
    // A 200 response is not enough: preserve the existing ASP.NET health contract.
    const body: unknown = await response.json();
    return body !== null && typeof body === "object" &&
      "status" in body && body.status === "Healthy"
      ? "healthy"
      : "unavailable";
  } catch {
    // Intentionally discard URLs, network errors, diagnostics and DB details.
    return "unavailable";
  }
}

export async function getApiHealth(): Promise<ApiHealthState> {
  const base = parseLocalApiBaseUrl(
    process.env.ARGIN_API_INTERNAL_BASE_URL,
    process.env.NODE_ENV,
  );
  if (!base) return NOT_CONFIGURED;
  const [liveness, readiness] = await Promise.all([
    probe(base, "/health/live"),
    probe(base, "/health/ready"),
  ]);
  return { liveness, readiness };
}
