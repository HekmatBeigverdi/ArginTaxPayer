import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../../../lib/identity-origin.mjs";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ taxpayerId: string; invoiceId: string }> };
type Method = "GET" | "POST" | "PUT" | "DELETE";
type JsonObject = Record<string, unknown>;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NO_STORE = { "Cache-Control": "no-store" };
const MAX_LENGTH = 300_000;

const isObject = (value: unknown): value is JsonObject =>
  value !== null && typeof value === "object" && !Array.isArray(value);
const isNumber = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value);
const isId = (value: unknown): value is string => typeof value === "string" && UUID.test(value);
const reject = (status: number, error: string) =>
  NextResponse.json({ error }, { status, headers: NO_STORE });

export async function GET(request: NextRequest, context: Context) {
  return handle(request, context, "GET");
}
export async function POST(request: NextRequest, context: Context) {
  return handle(request, context, "POST");
}
export async function PUT(request: NextRequest, context: Context) {
  return handle(request, context, "PUT");
}
export async function DELETE(request: NextRequest, context: Context) {
  return handle(request, context, "DELETE");
}

async function handle(request: NextRequest, context: Context, method: Method) {
  if (!identityUiEnabled()) return reject(404, "unavailable");
  const { taxpayerId, invoiceId } = await context.params;
  if (!isId(taxpayerId) || !isId(invoiceId)) return reject(404, "not_found");
  if ((method !== "GET" || request.headers.has("origin")) && !isSameLocalOrigin(
    request.headers.get("origin"), request.headers.get("host"), request.nextUrl.protocol,
  )) return reject(403, "origin_not_allowed");

  const token = request.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return reject(401, "unauthorized");

  let saveInput: JsonObject | null = null;
  if (method === "PUT") {
    try {
      const raw = await request.text();
      if (raw.length > MAX_LENGTH) return reject(413, "payload_too_large");
      saveInput = readSaveInput(JSON.parse(raw));
    } catch {
      return reject(400, "invalid_sales_return_input");
    }
    if (!saveInput) return reject(400, "invalid_sales_return_input");
  } else if (await hasPayload(request)) {
    return reject(400, "payload_not_supported");
  }

  try {
    const upstream = await fetch(identityApiOrigin() + "/api/dev/taxpayers/" + taxpayerId
      + "/invoices/" + invoiceId + "/dispatch/sales-return-draft", {
      method,
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(35000),
      headers: {
        Authorization: "Bearer " + token,
        ...(saveInput ? { "Content-Type": "application/json" } : {}),
      },
      ...(saveInput ? { body: JSON.stringify(saveInput) } : {}),
    });
    if ([401, 403, 404].includes(upstream.status)) {
      return reject(upstream.status, "sales_return_access_denied");
    }
    if (![200, 201, 400, 409].includes(upstream.status)) {
      return reject(503, "sales_return_api_unavailable");
    }

    const raw = await upstream.text();
    if (raw.length > MAX_LENGTH) return reject(502, "sales_return_response_invalid");
    let result: unknown;
    try { result = JSON.parse(raw); }
    catch { return reject(502, "sales_return_response_invalid"); }
    if (!isObject(result) || typeof result.code !== "string") {
      return reject(502, "sales_return_response_invalid");
    }
    if (!upstream.ok) {
      return NextResponse.json({ code: result.code }, { status: upstream.status, headers: NO_STORE });
    }

    const draft = result.draft == null ? null : readDraft(result.draft, invoiceId);
    if ((result.draft != null && !draft) || (method !== "DELETE" && !draft)) {
      return reject(502, "sales_return_response_invalid");
    }
    return NextResponse.json({ code: result.code, draft }, { status: upstream.status, headers: NO_STORE });
  } catch {
    return reject(503, "sales_return_api_unavailable");
  }
}

function readSaveInput(value: unknown): JsonObject | null {
  if (!isObject(value) || !Number.isSafeInteger(value.expectedRevision) || Number(value.expectedRevision) < 1
    || !Array.isArray(value.lines) || value.lines.length === 0 || value.lines.length > 1000
    || ![1, 2, 3].includes(Number(value.settlementMethod)) || !Number.isInteger(value.settlementMethod)
    || !isNumber(value.cashAmountRial) || !isNumber(value.creditAmountRial)
    || (value.reductionCorrectsOriginalMistake !== undefined && typeof value.reductionCorrectsOriginalMistake !== "boolean")
    || (value.reductionEvidenceReference != null && (typeof value.reductionEvidenceReference !== "string"
      || value.reductionEvidenceReference.length > 256))) return null;

  const lines = [];
  for (const line of value.lines) {
    if (!isObject(line) || !isNumber(line.quantity) || !isNumber(line.unitPriceRial)
      || !isNumber(line.discountPerUnitRial)) return null;
    lines.push({ quantity: line.quantity, unitPriceRial: line.unitPriceRial, discountPerUnitRial: line.discountPerUnitRial });
  }
  return {
    expectedRevision: value.expectedRevision, lines, settlementMethod: value.settlementMethod,
    cashAmountRial: value.cashAmountRial, creditAmountRial: value.creditAmountRial,
    reductionCorrectsOriginalMistake: value.reductionCorrectsOriginalMistake === true,
    reductionEvidenceReference: value.reductionEvidenceReference ?? null,
  };
}

function readDraft(value: unknown, predecessorId: string) {
  if (!isObject(value) || !isId(value.workspaceId) || value.predecessorInvoiceId !== predecessorId
    || !Number.isSafeInteger(value.revision) || Number(value.revision) < 1
    || !Array.isArray(value.lines) || value.lines.length > 1000
    || !Number.isInteger(value.settlementMethod) || !isNumber(value.cashAmountRial)
    || !isNumber(value.creditAmountRial)) return null;

  const lines = [];
  for (const line of value.lines) {
    if (!isObject(line) || typeof line.description !== "string" || !isNumber(line.quantity)
      || !isNumber(line.unitPriceRial) || !isNumber(line.discountPerUnitRial)
      || (line.vatRatePercent !== null && !isNumber(line.vatRatePercent))
      || (line.officialIdentifierSnapshot !== null && typeof line.officialIdentifierSnapshot !== "string")) return null;
    lines.push({
      description: line.description, quantity: line.quantity, unitPriceRial: line.unitPriceRial,
      discountPerUnitRial: line.discountPerUnitRial, vatRatePercent: line.vatRatePercent,
      officialIdentifierSnapshot: line.officialIdentifierSnapshot,
    });
  }
  return {
    workspaceId: value.workspaceId, predecessorInvoiceId: value.predecessorInvoiceId,
    revision: value.revision, lines, settlementMethod: value.settlementMethod,
    cashAmountRial: value.cashAmountRial, creditAmountRial: value.creditAmountRial,
  };
}

async function hasPayload(request: NextRequest): Promise<boolean> {
  const length = request.headers.get("content-length");
  if (length !== null && (!/^\d+$/.test(length) || Number(length) > 0)) return true;
  if (request.body === null) return false;
  const reader = request.body.getReader();
  try {
    while (true) {
      const part = await reader.read();
      if (part.done) return false;
      if (part.value.byteLength > 0) { await reader.cancel(); return true; }
    }
  } catch { return true; }
}
