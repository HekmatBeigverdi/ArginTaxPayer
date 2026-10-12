#!/usr/bin/env python3
"""Step 06 local HTTP scope isolation test. Requires Development registration on loopback."""
import json
import secrets
import sys
import urllib.error
import urllib.request

BASE = "http://127.0.0.1:15088/api/dev/identity"
PASS = "Disposable-Scope-2026-" + secrets.token_hex(10)


def call(path, payload=None, bearer=None):
    headers = {"Content-Type": "application/json"}
    if bearer:
        headers["Authorization"] = "Bearer " + bearer
    request = urllib.request.Request(BASE + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        headers=headers, method="POST" if payload is not None else "GET")
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as error:
        error.read()
        return error.code, {}


def require(status, expected, description):
    if status != expected:
        raise AssertionError(f"{description}: HTTP {status} (expected {expected})")
    print("PASS " + description)


def create_customer():
    email = "scope-smoke-" + secrets.token_hex(10) + "@example.invalid"
    require(call("/register", {"email": email, "password": PASS})[0], 201, "register disposable customer")
    status, result = call("/login", {"email": email, "password": PASS})
    require(status, 200, "login disposable customer")
    return result["accessToken"], result["refreshToken"]


def main():
    with urllib.request.urlopen("http://127.0.0.1:15088/health/dev-identity", timeout=10) as response:
        diagnostics = json.load(response)
    if not diagnostics.get("identityEndpointsRegistered") or not diagnostics.get("registrationEnabled"):
        raise AssertionError("API configuration does not enable test identity routes: " + str(diagnostics))
    print("PASS API identity route and registration preflight")
    first, first_refresh = create_customer()
    second, second_refresh = create_customer()
    status, me1 = call("/authorization/me", bearer=first)
    require(status, 200, "first identity resolved by bearer")
    status, me2 = call("/authorization/me", bearer=second)
    require(status, 200, "second identity resolved by bearer")

    user1, user2 = me1["userId"], me2["userId"]
    require(call("/resources/users/" + user1, bearer=first)[0], 200, "own user allowed")
    require(call("/resources/users/" + user2, bearer=first)[0], 404, "foreign user hidden")
    require(call("/resources/users/" + user2)[0], 401, "anonymous request denied")
    require(call("/resources/users/" + "00000000-0000-0000-0000-000000000005", bearer=first)[0],
            404, "missing user hidden")

    status, first_list = call("/resources/sessions", bearer=first)
    require(status, 200, "list first own sessions")
    status, second_list = call("/resources/sessions", bearer=second)
    require(status, 200, "list second own sessions")
    sid1 = first_list["sessionIds"][0]
    sid2 = second_list["sessionIds"][0]
    if sid2 in first_list["sessionIds"] or sid1 in second_list["sessionIds"]:
        raise AssertionError("Cross-user session ID leaked")
    require(call("/resources/sessions/" + sid1, bearer=first)[0], 200, "own session allowed")
    require(call("/resources/sessions/" + sid2, bearer=first)[0], 404, "foreign session hidden")
    require(call("/resources/sessions/" + sid2 + "/revoke", {}, first)[0],
            404, "foreign session revoke denied")
    require(call("/authorization/me", bearer=second)[0], 200, "foreign session unaffected")
    require(call("/resources/sessions/" + sid1 + "/revoke", {}, first)[0],
            204, "own session revoked")
    require(call("/authorization/me", bearer=first)[0], 401, "revoked access token denied")
    require(call("/refresh", {"refreshToken": first_refresh})[0], 401, "revoked refresh denied")
    require(call("/authorization/me", bearer=second)[0], 200, "second account still active")
    print("STEP 06 RESOURCE SCOPE SMOKE PASSED")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("FAIL:", exc, file=sys.stderr)
        sys.exit(1)
