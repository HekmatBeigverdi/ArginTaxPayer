#!/usr/bin/env python3
"""Phase06 Step15: read-only real-session authorization smoke.

Never writes or changes a customer, reference release, or fiscal record.
The server must be locally bound in Development and reachable at 127.0.0.1:15089.
Token values come only from environment; no token is printed or stored.
"""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

HOST = "http://127.0.0.1:15089"
ENV = (
    "PHASE06_ROOT_TOKEN",
    "PHASE06_PARTNER_TOKEN",
    "PHASE06_CUSTOMER_TOKEN",
    "PHASE06_OTHER_TOKEN",
    "PHASE06_CUSTOMER_ID",
    "PHASE06_OTHER_CUSTOMER_ID",
)


def get(token: str | None, path: str) -> tuple[int, object]:
    request = urllib.request.Request(
        HOST + path,
        headers={"Authorization": f"Bearer {token}"} if token else {},
        method="GET",
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read(2_000_000)
            return response.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as error:
        return error.code, {}
    except urllib.error.URLError as error:
        raise RuntimeError("Local API is not reachable. Run zsh scripts/dev-status.zsh.") from error


def check(name: str, token: str | None, path: str, expected: int,
          requested_customer: str | None = None) -> None:
    status, body = get(token, path)
    if status != expected:
        raise AssertionError(f"{name}: expected HTTP {expected}, got HTTP {status}")
    if status == 200 and requested_customer:
        if not isinstance(body, dict) or not isinstance(body.get("items"), list):
            raise AssertionError(f"{name}: missing paginated items payload")
        for item in body["items"]:
            if not isinstance(item, dict) or str(item.get("customerUserId", "")).lower() != requested_customer:
                raise AssertionError(f"{name}: cross-tenant row was returned")
    print(f"PASS {name}: HTTP {status}")


def main() -> int:
    missing = [name for name in ENV if not os.getenv(name)]
    if missing:
        print("Required variables missing: " + ", ".join(missing), file=sys.stderr)
        print("Supply four DISTINCT active Level1/Level2/Customer/Other sessions. "
              "Tokens are never logged. Test fixture must have root and partner assigned "
              "to PHASE06_CUSTOMER_ID, with other unassigned.", file=sys.stderr)
        return 2

    customer = str(uuid.UUID(os.environ["PHASE06_CUSTOMER_ID"])).lower()
    other = str(uuid.UUID(os.environ["PHASE06_OTHER_CUSTOMER_ID"])).lower()
    if customer == other:
        raise ValueError("Customer IDs must be different")
    tokens = [os.environ[n] for n in ENV[:4]]
    if len(set(tokens)) != len(tokens):
        raise ValueError("All four test access tokens must be distinct")
    root, partner, self_token, outsider = tokens

    path = f"/api/dev/catalog/customers/{customer}"
    other_path = f"/api/dev/catalog/customers/{other}"
    for resource in ("products", "services"):
        query = f"/{resource}?page=1&pageSize=20"
        check(f"anonymous {resource}", None, path + query, 401)
        check(f"customer owns {resource}", self_token, path + query, 200, customer)
        check(f"Level1 owns {resource}", root, path + query, 200, customer)
        check(f"Level2 owns {resource}", partner, path + query, 200, customer)
        check(f"unrelated customer denied {resource}", outsider, path + query, 403)
        check(f"customer cannot read another {resource}", self_token, other_path + query, 403)

    check("anonymous official search", None,
          "/api/dev/catalog/official?page=1&pageSize=20", 401)
    check("no unauthenticated unit search", None,
          "/api/dev/catalog/units?page=1&pageSize=20", 401)

    check("invalid pagination", self_token, path + "/products?page=0&pageSize=20", 400)
    check("invalid max page size", self_token, path + "/services?page=1&pageSize=101", 400)
    check("negative official kind", self_token,
          "/api/dev/catalog/official?page=1&pageSize=20&kind=9", 400)

    print("All read-only Phase06 authorization smoke checks passed.")
    print("NOTE: former-partner transfers, disabled MFA, revoked sessions, concurrent "
          "writes and database trigger semantics require separate disposable integration fixtures.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, ValueError, RuntimeError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        sys.exit(1)
