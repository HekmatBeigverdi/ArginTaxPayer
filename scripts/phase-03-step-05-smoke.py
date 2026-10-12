#!/usr/bin/env python3
"""Step 05 RBAC HTTP negative-path smoke (Development + loopback only).

Does not print tokens/passwords. Full privileged grant path is covered by
IdentityRolePostgresTests using independently authenticated Level 1 and Level 2.
"""
import json
import secrets
import sys
import urllib.error
import urllib.request

BASE = "http://127.0.0.1:15088/api/dev/identity"
EMAIL = "rbac-" + secrets.token_hex(8) + "@example.invalid"
PASSWORD = "Disposable-RBAC-" + secrets.token_urlsafe(18)
TARGET = "00000000-0000-0000-0000-000000000001"


def call(path, payload=None, bearer=None):
    headers = {"Content-Type": "application/json"}
    if bearer:
        headers["Authorization"] = "Bearer " + bearer
    request = urllib.request.Request(
        BASE + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        headers=headers,
        method="POST" if payload is not None else "GET")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            body = response.read()
            return response.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as err:
        err.read()
        return err.code, {}


def expected(status, required, label):
    if status != required:
        raise AssertionError(f"{label}: expected HTTP {required}, received {status}")
    print("PASS " + label)


def main():
    expected(call("/authorization/admin")[0], 401, "unauthenticated admin denied")
    expected(call("/authorization/customer")[0], 401, "unauthenticated customer denied")
    expected(call("/roles/level2/grant", {"targetUserId": TARGET})[0],
             401, "anonymous role grant denied")
    expected(call("/register", {"email": EMAIL, "password": PASSWORD})[0],
             201, "customer registered")
    status, login = call("/login", {"email": EMAIL, "password": PASSWORD})
    expected(status, 200, "customer logged in")
    token = login["accessToken"]
    expected(call("/authorization/customer", bearer=token)[0],
             200, "customer permission accepted")
    expected(call("/authorization/admin", bearer=token)[0],
             403, "customer admin permission denied")
    status, identity = call("/authorization/me", bearer=token)
    expected(status, 200, "server-resolved roles returned")
    if identity.get("roles") != ["Customer"]:
        raise AssertionError("Expected Customer-only roles")
    expected(call("/roles/level2/grant", {"targetUserId": TARGET}, token)[0],
             403, "customer cannot grant Level2")
    expected(call("/roles/level2/grant", {"targetUserId": identity["userId"]}, token)[0],
             403, "customer cannot self-promote")
    print("STEP 05 RBAC SMOKE PASSED — disposable test customer remains in test DB")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("FAIL:", exc, file=sys.stderr)
        sys.exit(1)
