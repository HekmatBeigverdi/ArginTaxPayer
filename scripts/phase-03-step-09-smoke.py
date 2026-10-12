#!/usr/bin/env python3
"""Step 09 local HTTP negative-path smoke. Real SMS remains disabled; never asks for a real OTP."""
import json
import os
import secrets
import sys
import urllib.parse
import urllib.error
import urllib.request

ORIGIN = os.environ.get("ARGINTAXPAYER_API_BASE", "http://127.0.0.1:15089").rstrip("/")
parsed = urllib.parse.urlsplit(ORIGIN)
if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost") or parsed.path or parsed.query:
    raise SystemExit("Refusing non-loopback identity smoke.")

def call(path, data):
    req = urllib.request.Request(ORIGIN + "/api/dev/identity/" + path,
        data=json.dumps(data).encode(), headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=12) as reply:
            reply.read()
            return reply.status
    except urllib.error.HTTPError as err:
        err.read()
        return err.code

def require(value, expected, note):
    if value != expected:
        raise AssertionError(f"{note}: expected {expected}; got {value}")
    print("PASS", note)

try:
    with urllib.request.urlopen(ORIGIN + "/health/dev-identity", timeout=12) as resp:
        health = json.load(resp)
    if not health.get("identityEndpointsRegistered") or not health.get("connectionConfigured"):
        raise AssertionError("Identity API not configured in Development.")
    # Use an unowned disposable phone: never complete a real registration or send live SMS.
    mobile = "09" + str(secrets.randbelow(10**9)).zfill(9)
    require(call("phone/request", {"mobile": mobile, "purpose": "register"}), 202,
            "generic OTP request acknowledgement; live sending disabled by default")
    require(call("phone/register/complete", {"mobile": mobile, "code": "000000"}), 400,
            "unverified phone registration blocked")
    require(call("phone/login/complete", {"mobile": mobile, "code": "000000"}), 401,
            "nonexistent OTP login blocked")
    require(call("phone/reset/complete", {"mobile": mobile, "code": "000000",
        "newPassword": "Disposable-Step09-Password"}), 400,
            "unauthorized password reset blocked")
    print("STEP 09 PHONE OTP NEGATIVE SMOKE PASSED")
except Exception as error:
    print("FAIL:", error, file=sys.stderr)
    sys.exit(1)
