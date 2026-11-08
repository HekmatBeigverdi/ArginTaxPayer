import { NextRequest, NextResponse } from "next/server";

// A second, public-BFF-side boundary for the initial Production rollout.
// The API also maps ONLY login/MFA and read-only dashboard routes in this mode.
// Do not use this as a substitute for API authorization or tenant isolation.
export function middleware(request: NextRequest) {
  if (process.env.NODE_ENV !== "production" ||
      process.env.ARGIN_IDENTITY_DASHBOARD_ONLY !== "true")
    return NextResponse.next();

  const path = request.nextUrl.pathname;
  const method = request.method;
  const publicPage = path === "/" || path === "/identity" || path === "/dashboard";
  if (publicPage && (method === "GET" || method === "HEAD"))
    return NextResponse.next();

  const identityRead = new Set(["me", "mfa/status"]);
  const identityWrite = new Set([
    "login", "logout", "refresh", "mfa/challenge", "mfa/complete"
  ]);
  if (path.startsWith("/api/identity/")) {
    const operation = path.slice("/api/identity/".length);
    if ((method === "GET" && identityRead.has(operation)) ||
        (method === "POST" && identityWrite.has(operation)))
      return NextResponse.next();
    return new NextResponse(null, { status: 404 });
  }
  if (path.startsWith("/api/dashboard/") && method === "GET")
    return NextResponse.next();
  if (path.startsWith("/api/"))
    return new NextResponse(null, { status: 404 });

  // No deep links to mutable workspace, finance, signing or taxpayer pages.
  // Static Next assets and brand imagery are excluded from the matcher.
  return NextResponse.redirect(new URL("/dashboard", request.url), 307);
}

export const config = {
  matcher: ["/((?!_next/|brand/|favicon.ico|robots.txt|sitemap.xml).*)"]
};
