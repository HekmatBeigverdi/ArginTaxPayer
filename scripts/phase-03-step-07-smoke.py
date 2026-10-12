#!/usr/bin/env python3
"""Step 07: loopback-only disposable-account throttling smoke."""
import json, os, secrets, sys, urllib.error, urllib.request, urllib.parse

ORIGIN = os.environ.get("ARGINTAXPAYER_API_BASE", "http://127.0.0.1:15089").rstrip("/")
parsed = urllib.parse.urlsplit(ORIGIN)
if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost") or parsed.path or parsed.query or parsed.fragment:
    raise SystemExit("Only local HTTP origins are supported.")

def call(path, email, password):
    req = urllib.request.Request(ORIGIN + "/api/dev/identity/" + path,
        data=json.dumps({"email":email,"password":password}).encode(),
        headers={"Content-Type":"application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            return response.status
    except urllib.error.HTTPError as error:
        error.read()
        return error.code

def require(got, want, description):
    if got != want:
        raise AssertionError(f"{description}: HTTP {got}; expected {want}")
    print("PASS", description)

try:
    with urllib.request.urlopen(ORIGIN + "/health/dev-identity", timeout=10) as response:
        diag = json.load(response)
    if not diag["identityEndpointsRegistered"] or not diag["registrationEnabled"]:
        raise AssertionError("Development registration not configured")
    email = "security-smoke-" + secrets.token_hex(12) + "@example.invalid"
    password = "Security-Demo-" + secrets.token_hex(12)
    require(call("register", email, password), 201, "register isolated customer")
    for i in range(5):
        require(call("login", email, "wrong-" + secrets.token_hex(9)), 401, f"wrong password {i+1}")
    require(call("login", email, password), 429, "correct password temporarily rate limited")
    require(call("verify-password", email, password), 429, "verification cannot bypass throttle")
    print("STEP 07 ACCOUNT PROTECTION SMOKE PASSED")
except Exception as ex:
    print("FAIL:", str(ex), file=sys.stderr)
    sys.exit(1)
