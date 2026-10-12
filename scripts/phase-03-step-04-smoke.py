#!/usr/bin/env python3
"""Phase 03 Step 04 end-to-end MFA smoke. Loopback only. Never prints secrets."""
import base64
import hashlib
import hmac
import json
import secrets
import struct
import sys
import time
import urllib.error
import urllib.request

URL = "http://127.0.0.1:15088/api/dev/identity"
EMAIL = "mfa-smoke-" + secrets.token_hex(8) + "@example.invalid"
PASSWORD = "Throwaway-MFA-" + secrets.token_urlsafe(24)

def api(path, payload=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = None if payload is None else json.dumps(payload).encode()
    request = urllib.request.Request(URL + path, data=data, headers=headers,
                                     method="POST" if payload is not None or path == "/logout" else "GET")
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            body = response.read()
            return response.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as err:
        err.read()
        return err.code, {}

def check(status, expected, name):
    if status != expected:
        raise RuntimeError(f"{name}: expected HTTP {expected}, received {status}")
    print("PASS " + name)

def totp(secret, offset=0):
    now = int(time.time()) // 30 + offset
    key = base64.b32decode(secret + "=" * ((-len(secret)) % 8))
    digest = hmac.new(key, struct.pack(">Q", now), hashlib.sha1).digest()
    i = digest[-1] & 15
    n = struct.unpack(">I", digest[i:i + 4])[0] & 0x7fffffff
    return f"{n % 1000000:06d}"

def main():
    check(api("/register", {"email": EMAIL, "password": PASSWORD})[0], 201, "customer registration")
    status, initial = api("/login", {"email": EMAIL, "password": PASSWORD})
    check(status, 200, "initial login")
    status, enrollment = api("/mfa/enroll", {}, initial["accessToken"])
    check(status, 200, "begin TOTP enrollment")
    secret = enrollment["secret"]
    check(api("/mfa/confirm", {"code": "000000"}, initial["accessToken"])[0], 401,
          "invalid enrollment code")
    status, confirmed = api("/mfa/confirm", {"code": totp(secret)}, initial["accessToken"])
    check(status, 200, "confirm MFA enrollment")
    codes = confirmed.get("codes", [])
    if len(codes) != 10:
        raise RuntimeError("expected 10 recovery codes")
    check(api("/me", token=initial["accessToken"])[0], 401, "prior session revoked")
    check(api("/login", {"email": EMAIL, "password": PASSWORD})[0], 401, "password-only login blocked")
    check(api("/mfa/challenge", {"email": EMAIL, "password": "incorrect"})[0], 401,
          "incorrect password rejected")
    status, challenge = api("/mfa/challenge", {"email": EMAIL, "password": PASSWORD})
    check(status, 200, "MFA challenge created")
    status, session = api("/mfa/complete",
                          {"challenge": challenge["challenge"], "code": codes[0],
                           "useRecoveryCode": True})
    check(status, 200, "one-use recovery succeeds")
    check(api("/me", token=session["accessToken"])[0], 200, "MFA session works")
    check(api("/mfa/complete",
              {"challenge": challenge["challenge"], "code": codes[1],
               "useRecoveryCode": True})[0], 401, "challenge cannot be replayed")
    status, another = api("/mfa/challenge", {"email": EMAIL, "password": PASSWORD})
    check(status, 200, "new challenge")
    check(api("/mfa/complete",
              {"challenge": another["challenge"], "code": codes[0],
               "useRecoveryCode": True})[0], 401, "used recovery code rejected")
    # The enrollment TOTP step was consumed, but the next time-step may be
    # accepted within the RFC6238 +1 drift window.
    status, challenge2 = api("/mfa/challenge", {"email": EMAIL, "password": PASSWORD})
    check(status, 200, "TOTP challenge")
    check(api("/mfa/complete", {"challenge": challenge2["challenge"],
                               "code": totp(secret, 1), "useRecoveryCode": False})[0],
          200, "next-step TOTP accepted")
    check(api("/logout", token=session["accessToken"])[0], 204, "logout")
    check(api("/me", token=session["accessToken"])[0], 401, "revoked token refused")
    print("STEP 04 MFA SMOKE PASSED — disposable account remains in argintaxpayer_test")

if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        print("FAIL:", ex, file=sys.stderr)
        sys.exit(1)
