#!/usr/bin/env python3
"""Phase 03 / Step 10: read-only and negative-path loopback HTTP integration smoke.

Never sends a real SMS, creates an account, or logs credentials.
Requires a running Development API, live SMS disabled, dedicated test database.
"""
import json
import os
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("ARGINTAXPAYER_API_BASE", "http://127.0.0.1:15089").rstrip("/")
parsed = urllib.parse.urlsplit(BASE)
if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost") or parsed.port is None:
    raise SystemExit("REFUSED: API must be on loopback with an explicit port")
if parsed.path or parsed.query or parsed.fragment or parsed.username or parsed.password:
    raise SystemExit("REFUSED: invalid API base URL")

def request(path, method="GET", body=None, token=None):
    headers = {"Accept": "application/json"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if token is not None:
        headers["Authorization"] = "Bearer " + token
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(BASE + "/api/dev/identity/" + path, data=data,
                                 method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=12) as res:
            return res.status, res.read(8192)
    except urllib.error.HTTPError as err:
        return err.code, err.read(8192)

def expect(path, status, method="GET", body=None, token=None):
    got, _ = request(path, method, body, token)
    if got != status:
        raise AssertionError(f"{method} {path}: expected {status}, got {got}")
    print(f"PASS {method} {path}: {status}")

try:
    with urllib.request.urlopen(BASE + "/health/dev-identity", timeout=12) as res:
        health = json.load(res)
    if not (health.get("development") and health.get("connectionConfigured")
            and health.get("identityEndpointsRegistered") and health.get("otpPepperConfigured")):
        raise AssertionError("Development identity preflight failed")
    if health.get("smsSendingEnabled") is not False:
        raise AssertionError("REFUSED: disable real Ghasedak sending before negative-path smoke")
    with urllib.request.urlopen(BASE + "/health/ready", timeout=12) as res:
        if res.status != 200:
            raise AssertionError("PostgreSQL readiness failed")

    expect("authorization/me", 401)
    expect("authorization/admin", 401)
    expect("authorization/customer", 401)
    expect("resources/sessions", 401)
    expect("resources/users/" + str(secrets.token_hex(16)[:8]) +
           "-0000-0000-0000-000000000000", 401)
    expect("resources/sessions/" + "00000000-0000-4000-8000-000000000001", 401)
    expect("resources/sessions/" + "00000000-0000-4000-8000-000000000001" + "/revoke",
           401, method="POST")
    expect("roles/level2/grant", 401, "POST",
           {"targetUserId": "00000000-0000-4000-8000-000000000001"})
    expect("admin/customers", 401, "POST",
           {"mobile": "09123456789", "email": None, "password": "NeverUsed-Test-Password-2026!"})
    expect("me", 401)
    expect("refresh", 401, "POST", {"refreshToken": "not-a-token"})
    expect("logout", 401, "POST")
    expect("login", 401, "POST", {"email": "invalid@example.invalid", "password": "invalid"})
    expect("phone/login/complete", 401, "POST", {"mobile": "09123456789", "code": "000000"})
    expect("phone/register/complete", 400, "POST",
           {"mobile": "invalid", "code": "000000", "password": None})
    expect("phone/reset/complete", 400, "POST",
           {"mobile": "invalid", "code": "000000", "newPassword": "NeverUsed-2026-Test!"})
    expect("phone/request", 400, "POST", {"mobile": "invalid", "purpose": "register"})

    print("PHASE 03 STEP 10 HTTP NEGATIVE SMOKE PASSED")
except Exception as exc:
    print("FAIL: " + str(exc), file=sys.stderr)
    sys.exit(1)
