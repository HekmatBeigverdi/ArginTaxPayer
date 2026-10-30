import { NextRequest, NextResponse } from "next/server";
import {
  ACCESS_COOKIE,
  identityApiOrigin,
  identityUiEnabled,
} from "../../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../../lib/identity-origin.mjs";

export const dynamic = "force-dynamic";

type Context = {
  params: Promise<{ taxpayerId: string; invoiceId: string }>;
};

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NO_STORE_HEADERS = { "Cache-Control": "no-store" };
const MAX_RESPONSE_LENGTH = 4096;
const SEND_TIMEOUT_MS = 115_000;

function reject(status: number, error: string) {
  return NextResponse.json({ error }, { status, headers: NO_STORE_HEADERS });
}

export async function POST(request: NextRequest, context: Context) {
  if (!identityUiEnabled()) {
    return reject(404, "unavailable");
  }

  const { taxpayerId, invoiceId } = await context.params;
  if (!UUID_PATTERN.test(taxpayerId) || !UUID_PATTERN.test(invoiceId)) {
    return reject(404, "not_found");
  }

  const sameOrigin = isSameLocalOrigin(
    request.headers.get("origin"),
    request.headers.get("host"),
    request.nextUrl.protocol,
  );
  if (!sameOrigin) {
    return reject(403, "origin_not_allowed");
  }

  if (await hasPayload(request)) {
    return reject(400, "payload_not_supported");
  }

  const token = request.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) {
    return reject(401, "unauthorized");
  }

  try {
    const endpoint = identityApiOrigin()
      + "/api/dev/taxpayers/" + taxpayerId
      + "/invoices/" + invoiceId
      + "/dispatch/correction-send-and-inquire";

    // One backend call: the server owns correction creation and submission.
    const upstream = await fetch(endpoint, {
      method: "POST",
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(SEND_TIMEOUT_MS),
      headers: { Authorization: "Bearer " + token },
    });

    if ([401, 403, 404].includes(upstream.status)) {
      return reject(upstream.status, "correction_access_denied");
    }
    if (![200, 201, 400, 409].includes(upstream.status)) {
      return reject(503, "correction_api_unavailable");
    }

    const raw = await upstream.text();
    if (raw.length > MAX_RESPONSE_LENGTH) {
      return reject(502, "correction_response_invalid");
    }

    let body: unknown;
    try {
      body = JSON.parse(raw);
    } catch {
      return reject(502, "correction_response_invalid");
    }
    if (!body || typeof body !== "object" || Array.isArray(body)) {
      return reject(502, "correction_response_invalid");
    }

    const result = body as Record<string, unknown>;
    if (typeof result.code !== "string") {
      return reject(502, "correction_response_invalid");
    }

    // Preserve the created invoice identity on failure so the UI can reconcile
    // its state instead of encouraging another submission.
    return NextResponse.json({
      code: result.code,
      invoiceId: typeof result.invoiceId === "string" ? result.invoiceId : null,
      status: typeof result.status === "string" ? result.status : null,
      referenceNumber: typeof result.referenceNumber === "string" ? result.referenceNumber : null,
      inquiryCode: typeof result.inquiryCode === "string" ? result.inquiryCode : null,
    }, {
      status: upstream.status,
      headers: NO_STORE_HEADERS,
    });
  } catch {
    return reject(503, "correction_api_unavailable");
  }
}

async function hasPayload(request: NextRequest): Promise<boolean> {
  const contentLength = request.headers.get("content-length");
  if (contentLength !== null && (!/^\d+$/.test(contentLength) || Number(contentLength) > 0)) {
    return true;
  }
  if (request.body === null) {
    return false;
  }

  const reader = request.body.getReader();
  try {
    while (true) {
      const chunk = await reader.read();
      if (chunk.done) {
        return false;
      }
      if (chunk.value.byteLength > 0) {
        await reader.cancel();
        return true;
      }
    }
  } catch {
    return true;
  }
}
