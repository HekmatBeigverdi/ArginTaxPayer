import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";
import {
  ACCESS_COOKIE,
  currentIdentity,
  identityApiOrigin,
  identityUiEnabled,
} from "../../../../lib/identity";

export const dynamic = "force-dynamic";

type ProxyMethod = "GET" | "POST" | "PUT" | "DELETE";
type RouteContext = { params: Promise<{ parts: string[] }> };

const actions = new Map<string, ProxyMethod>([
  ["subscriptions/plans", "POST"],
  ["subscriptions/prices", "POST"],
  ["subscriptions/prices/history", "GET"],
  ["subscriptions/plans/activate", "POST"],
  ["subscriptions/orders/approve", "POST"],
  ["subscriptions/orders/reject", "POST"],
  ["subscriptions/paid-orders/activate", "POST"],
  ["subscriptions/commercial-orders", "POST"],
  ["subscriptions/collections/manual", "POST"],
  ["subscriptions/collections/history", "GET"],
  ["subscriptions/revenue-ledger", "GET"],
  ["subscriptions/cash-pending", "GET"],
  ["subscriptions/sales-report", "GET"],
  ["subscriptions/checkout-tax", "GET"],
  ["subscriptions/sms-usage", "GET"],
  ["subscriptions/sms-partners", "GET"],
  ["subscriptions/sms-sep-admins", "GET"],
  ["subscriptions/sms-topups/start", "POST"],
  ["subscriptions/settlements/balances", "GET"],
  ["subscriptions/settlements/partners", "GET"],
  ["subscriptions/settlements/terms", "PUT"],
  ["subscriptions/settlements/preview", "GET"],
  ["subscriptions/settlements/batches", "GET"],
  ["subscriptions/settlements/batch", "GET"],
  ["subscriptions/settlements/print", "GET"],
  ["subscriptions/settlements/expenses", "POST"],
  ["subscriptions/settlements/record", "POST"],
  ["subscriptions/settlements/reverse", "POST"],
]);

const LEVEL1_ONLY_ACTIONS = new Set([
  "subscriptions/settlements/record",
  "subscriptions/settlements/terms",
  "subscriptions/settlements/expenses",
  "subscriptions/settlements/reverse",
  "subscriptions/plans/activate",
  "subscriptions/checkout-tax",
  "subscriptions/sms-settings",
  "subscriptions/sms-sep-settings",
]);

const PLAN_RECORD_PATH = /^subscriptions\/plans\/[0-9a-fA-F-]{36}$/;
const SMS_TOPUP_PATH = /^subscriptions\/sms-topups\/[0-9a-fA-F-]{36}$/;
const SMS_TOPUP_RECONCILE_PATH = /^subscriptions\/sms-topups\/[0-9a-fA-F-]{36}\/reconcile$/;
const BINDING_EXCEPTION_REQUEST_PATH = /^subscriptions\/[0-9a-fA-F-]{36}\/binding-exceptions$/;
const BINDING_EXCEPTION_REVIEW_PATH = /^subscriptions\/binding-exceptions\/[0-9a-fA-F-]{36}\/review$/;

async function proxy(request: NextRequest, context: RouteContext) {
  if (!identityUiEnabled()) {
    return NextResponse.json({ error: "disabled" }, { status: 404 });
  }

  const actor = await currentIdentity();
  if (!actor) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  const { parts } = await context.params;
  const path = parts.join("/");
  const isLevel1 = actor.roles.includes("Level1");
  const isLevel2 = actor.roles.includes("Level2");
  const isCustomerBindingRead = path === "subscriptions/binding-exceptions/mine";
  const isCustomerBindingRequest = BINDING_EXCEPTION_REQUEST_PATH.test(path);
  const isAdminBindingQueue = path === "subscriptions/binding-exceptions/pending";
  const isAdminBindingReview = BINDING_EXCEPTION_REVIEW_PATH.test(path);
  const isCustomerTopupRoute = path === "subscriptions/sms-topups/start" || SMS_TOPUP_PATH.test(path);
  if (!isLevel1 && !isLevel2 && !(actor.roles.includes("Customer") &&
      (isCustomerTopupRoute || isCustomerBindingRead || isCustomerBindingRequest))) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }
  let method: ProxyMethod | undefined;

  if (path === "subscriptions/settlements/batches" && request.method === "POST") {
    method = "POST";
  } else if (path === "subscriptions/plans" && request.method === "GET") {
    method = "GET";
  } else if (path === "subscriptions/checkout-tax" && ["GET", "PUT"].includes(request.method)) {
    method = request.method as "GET" | "PUT";
  } else if (path === "subscriptions/sms-settings" && ["GET", "PUT"].includes(request.method)) {
    method = request.method as "GET" | "PUT";
  } else if (path === "subscriptions/sms-sep-settings" && ["GET", "PUT"].includes(request.method)) {
    method = request.method as "GET" | "PUT";
  } else if ((isCustomerBindingRead||isAdminBindingQueue)&&request.method==="GET") {
    method="GET";
  } else if ((isCustomerBindingRequest||isAdminBindingReview)&&request.method==="POST") {
    method="POST";
  } else if (PLAN_RECORD_PATH.test(path) && ["PUT", "DELETE"].includes(request.method)) {
    method = request.method as "PUT" | "DELETE";
  } else {
    method = actions.get(path) ?? (SMS_TOPUP_PATH.test(path) ? "GET" : SMS_TOPUP_RECONCILE_PATH.test(path) ? "POST" : undefined);
  }

  if (!method || method !== request.method) {
    return NextResponse.json({ error: "not_found" }, { status: 404 });
  }
  if ((LEVEL1_ONLY_ACTIONS.has(path) || path === "subscriptions/settlements/batches" && method === "POST" ||
      isAdminBindingQueue||isAdminBindingReview) && !isLevel1) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }
  if(isCustomerBindingRead&&request.method!=="GET"||isCustomerBindingRequest&&request.method!=="POST")
    return NextResponse.json({error:"not_found"},{status:404});

  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  let body: string | undefined;
  if (method === "POST" || method === "PUT") {
    body = await request.text();
    if (body.length > 2000) {
      return NextResponse.json({ error: "too_large" }, { status: 413 });
    }
    try {
      JSON.parse(body);
    } catch {
      return NextResponse.json({ error: "invalid_json" }, { status: 400 });
    }
  }

  const query = new URLSearchParams();
  if (method === "GET") {
    for (const key of ["page", "pageSize", "planId", "partnerUserId", "orderId", "batchId", "taxpayerId"]) {
      const value = request.nextUrl.searchParams.get(key);
      if (value !== null && (key === "planId" || key === "partnerUserId" || key === "orderId" || key === "batchId" || key === "taxpayerId"
        ? /^[0-9a-fA-F-]{36}$/.test(value)
        : /^\d{1,9}$/.test(value))) {
        query.set(key, value);
      }
    }
    const period = request.nextUrl.searchParams.get("period");
    if (path === "subscriptions/sales-report" && period !== null) {
      if (period !== "month" && period !== "year") {
        return NextResponse.json({ error: "invalid_period" }, { status: 400 });
      }
      query.set("period", period);
    }
    const search = request.nextUrl.searchParams.get("search");
    if (path === "subscriptions/plans" && search !== null) query.set("search", search.slice(0, 100));
    const ownPrice = request.nextUrl.searchParams.get("ownPrice");
    if (ownPrice === "true" || ownPrice === "false") query.set("ownPrice", ownPrice);
  }

  try {
    const queryString = query.size ? "?" + query : "";
    const url = identityApiOrigin() + "/api/dev/" + path + queryString;
    const backend = await fetch(url, {
      method,
      headers: {
        Authorization: "Bearer " + token,
        ...(body ? { "Content-Type": "application/json" } : {}),
      },
      body,
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(10000),
    });

    const data = await backend.text();
    // Response forbids a body on 204/205/304. The API uses 204 for successful
    // writes, so preserve the empty body instead of turning it into a proxy 502.
    return new NextResponse(backend.body === null ? null : data, {
      status: backend.status,
      headers: {
        "Content-Type": backend.headers.get("content-type") || "application/json",
        "Cache-Control": "no-store",
      },
    });
  } catch {
    return NextResponse.json({ error: "backend_unavailable" }, { status: 502 });
  }
}

export async function GET(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function PUT(request: NextRequest, context: RouteContext) {
  const origin = request.headers.get("origin");
  if (origin && origin !== request.nextUrl.origin) {
    return NextResponse.json({ error: "invalid_origin" }, { status: 403 });
  }

  return proxy(request, context);
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function POST(request: NextRequest, context: RouteContext) {
  const origin = request.headers.get("origin");
  if (origin && origin !== request.nextUrl.origin) {
    return NextResponse.json({ error: "invalid_origin" }, { status: 403 });
  }

  return proxy(request, context);
}
