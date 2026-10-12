#!/usr/bin/env python3
"""Step 08 Next.js identity UI HTTP smoke; no credentials or test users required."""
import sys
import urllib.error
import urllib.request
import urllib.parse
import os

ORIGIN = os.environ.get("ARGIN_WEB_BASE", "http://127.0.0.1:3000").rstrip("/")
parsed = urllib.parse.urlsplit(ORIGIN)
if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost") or parsed.path:
    raise SystemExit("Refusing non-local Next.js origin.")

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        return None

client = urllib.request.build_opener(NoRedirect(), urllib.request.ProxyHandler({}))

def call(path, method="GET", payload=None):
    req = urllib.request.Request(ORIGIN + path, data=payload,
        method=method, headers={"Content-Type":"application/json"})
    try:
        with client.open(req, timeout=12) as result:
            # Keep HTTPMessage's case-insensitive HTTP header lookup.
            return result.status, result.headers, result.read().decode("utf-8")
    except urllib.error.HTTPError as err:
        return err.code, err.headers, err.read().decode("utf-8")

def require(cond, label):
    if not cond: raise AssertionError(label)
    print("PASS " + label)

try:
    status, headers, html = call("/identity")
    require(status == 200, "Persian identity page HTTP 200")
    require('lang="fa"' in html and 'dir="rtl"' in html, "RTL Persian root layout")
    require("ورود" in html and "ثبت‌نام" in html, "login and register controls")
    status, headers, _ = call("/account")
    require(status in (307, 308) and "/identity" in headers.get("Location", ""),
            "unauthenticated account route is server guarded")
    status, _, _ = call("/api/identity/login", "POST", b'{"email":"x@example.invalid","password":"bad"}')
    require(status == 403, "cross-site or missing Origin rejected before credentials")
    status, _, _ = call("/api/identity/not-allowed", "POST", b"{}")
    require(status in (403, 404), "unallowlisted identity operations inaccessible")
    print("STEP 08 PERSIAN IDENTITY UI SMOKE PASSED")
except Exception as exc:
    print("FAIL:", exc, file=sys.stderr)
    sys.exit(1)
