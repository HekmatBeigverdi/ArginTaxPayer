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
  ["subscriptions/sms-wallet/grant", "POST"],
  ["subscriptions/plans/activate", "POST"],
  ["subscriptions/paid-orders/activate", "POST"],
  ["subscriptions/commercial-orders", "POST"],
  ["subscriptions/collections/manual", "POST"],
  ["subscriptions/revenue-ledger", "GET"],
  ["subscriptions/cash-pending", "GET"],
  ["subscriptions/checkout-tax", "GET"],
  ["subscriptions/settlements/balances", "GET"],
  ["subscriptions/settlements/record", "POST"],
  ["subscriptions/settlements/reverse", "POST"],
]);

const LEVEL1_ONLY_ACTIONS = new Set([
  "subscriptions/settlements/record",
  "subscriptions/settlements/reverse",
  "subscriptions/plans/activate",
  "subscriptions/sms-wallet/grant",
  "subscriptions/paid-orders/activate",
]);

const PLAN_RECORD_PATH = /^subscriptions\/plans\/[0-9a-fA-F-]{36}$/;
const SMS_WALLET_PATH = /^subscriptions\/sms-wallet\/[0-9a-fA-F-]{36}$/;

async function proxy(request: NextRequest, context: RouteContext) {
  if (!identityUiEnabled()) {
    return NextResponse.json({ error: "disabled" }, { status: 404 });
  }

  const actor = await currentIdentity();
  if (!actor) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  const isLevel1 = actor.roles.includes("Level1");
  const isLevel2 = actor.roles.includes("Level2");
  if (!isLevel1 && !isLevel2) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }

  const { parts } = await context.params;
  const path = parts.join("/");
  let method: ProxyMethod | undefined;

  if (path === "subscriptions/plans" && request.method === "GET") {
    method = "GET";
  } else if (PLAN_RECORD_PATH.test(path) && ["PUT", "DELETE"].includes(request.method)) {
    method = request.method as "PUT" | "DELETE";
  } else {
    method = actions.get(path) ?? (SMS_WALLET_PATH.test(path) ? "GET" : undefined);
  }

  if (!method || method !== request.method) {
    return NextResponse.json({ error: "not_found" }, { status: 404 });
  }
  if (LEVEL1_ONLY_ACTIONS.has(path) && !isLevel1) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }

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
    for (const key of ["page", "pageSize", "planId", "partnerUserId"]) {
      const value = request.nextUrl.searchParams.get(key);
      if (value !== null && (key === "planId" || key === "partnerUserId"
        ? /^[0-9a-fA-F-]{36}$/.test(value)
        : /^\d{1,3}$/.test(value))) {
        query.set(key, value);
      }
    }
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
    return new NextResponse(data, {
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
