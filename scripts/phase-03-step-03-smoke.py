#!/usr/bin/env python3
"""Loopback-only end-to-end smoke for Phase 03 Step 03. Never prints tokens."""
import json
import secrets
import sys
import urllib.error
import urllib.request

BASE = "http://127.0.0.1:15088/api/dev/identity"
EMAIL = "phase03-step03-" + secrets.token_hex(8) + "@example.invalid"
PASSWORD = "Disposable-Test-" + secrets.token_urlsafe(22)


def call(path, payload=None, bearer=None):
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if bearer:
        headers["Authorization"] = "Bearer " + bearer
    request = urllib.request.Request(BASE + path, data=data, headers=headers,
                                     method="POST" if data is not None or path == "/logout" else "GET")
    try:
        with urllib.request.urlopen(request, timeout=8) as response:
            body = response.read()
            return response.status, json.loads(body) if body else None
    except urllib.error.HTTPError as error:
        error.read()
        return error.code, None


def expect(actual, expected, label):
    if actual != expected:
        raise AssertionError(f"{label}: expected HTTP {expected}, got {actual}")
    print("PASS:", label)


def main():
    expect(call("/register", {"email": EMAIL, "password": PASSWORD})[0], 201, "register")
    expect(call("/register", {"email": EMAIL.upper(), "password": PASSWORD})[0], 409, "duplicate rejected")
    expect(call("/login", {"email": EMAIL, "password": "wrong"})[0], 401, "wrong password rejected")
    status, first = call("/login", {"email": EMAIL, "password": PASSWORD})
    expect(status, 200, "customer login")
    if not first or not first.get("accessToken") or not first.get("refreshToken"):
        raise AssertionError("Login must return both tokens")
    expect(call("/me", bearer=first["accessToken"])[0], 200, "access accepted")
    expect(call("/me", bearer=first["refreshToken"])[0], 401, "refresh is not access")
    status, second = call("/refresh", {"refreshToken": first["refreshToken"]})
    expect(status, 200, "refresh rotation")
    if not second or first["accessToken"] == second["accessToken"] or first["refreshToken"] == second["refreshToken"]:
        raise AssertionError("Rotation must replace both tokens")
    expect(call("/me", bearer=first["accessToken"])[0], 401, "old access rejected")
    expect(call("/me", bearer=second["accessToken"])[0], 200, "new access accepted")
    expect(call("/refresh", {"refreshToken": first["refreshToken"]})[0], 401, "refresh replay rejected")
    expect(call("/me", bearer=second["accessToken"])[0], 401, "replay revoked session")
    status, third = call("/login", {"email": EMAIL, "password": PASSWORD})
    expect(status, 200, "new session")
    expect(call("/logout", bearer=third["accessToken"])[0], 204, "logout")
    expect(call("/me", bearer=third["accessToken"])[0], 401, "revoked access rejected")
    expect(call("/refresh", {"refreshToken": third["refreshToken"]})[0], 401, "revoked refresh rejected")
    print("STEP 03 SESSION SMOKE PASSED — disposable test account remains in argintaxpayer_test")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("FAIL:", exc, file=sys.stderr)
        sys.exit(1)
