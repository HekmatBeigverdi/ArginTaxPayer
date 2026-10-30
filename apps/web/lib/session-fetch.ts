"use client";

let pendingRefresh: Promise<boolean> | null = null;

function refreshSession(): Promise<boolean> {
  // The backend rotates refresh credentials. Share one refresh between requests
  // so concurrent draft actions cannot replay the same credential.
  if (!pendingRefresh) {
    pendingRefresh = fetch("/api/identity/refresh", {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
    })
      .then(response => response.ok)
      .catch(() => false)
      .finally(() => { pendingRefresh = null; });
  }
  return pendingRefresh;
}

/** Retry a draft request once after an authentication refusal, preserving its JSON body. */
export async function fetchWithSessionRefresh(url: string, options: RequestInit): Promise<Response> {
  const requestOptions: RequestInit = { ...options, credentials: "same-origin" };
  const response = await fetch(url, requestOptions);
  if (response.status !== 401 || options.signal?.aborted) return response;

  if (!await refreshSession() || options.signal?.aborted) return response;
  return fetch(url, requestOptions);
}
