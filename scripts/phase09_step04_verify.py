#!/usr/bin/env python3
"""Run Phase09 focused checks and the six opt-in test-DB journal tests.
No migration is generated/applied and no key provider is called.
Requires the existing dev-up tunnel at 127.0.0.1:15432.
"""
import getpass
import os
import subprocess
import sys

sys.dont_write_bytecode = True
import phase09_verify as verify
from phase09_apply_migration import read_terminal


def main():
    if len(sys.argv) != 1:
        raise verify.SafeError("Usage: python3 scripts/phase09_step04_verify.py")
    os.chdir(verify.ROOT)
    verify.preflight()
    verify.run(
        ["bash", "scripts/phase09-verify.sh", "focused", "--filter",
         "FullyQualifiedName~Phase09Signing"], verify.pure_environment())
    print("The next run writes synthetic fixtures only to argintaxpayer_test.")
    print("Append-only test journal/audit records are retained. No migration is applied.")
    user = read_terminal("PostgreSQL user [argintaxpayer]: ") or "argintaxpayer"
    password = getpass.getpass("Test database password: ")
    if not password:
        raise verify.SafeError("An explicit test password is required.")
    quote = lambda value: '"' + value.replace('"', '""') + '"'
    os.environ["ConnectionStrings__ArginTaxPayerDb"] = (
        "Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;"
        f"Username={quote(user)};Password={quote(password)};SSL Mode=Disable")
    env = verify.guard_database()
    verify.run(
        ["bash", "scripts/phase09-verify.sh", "integration", "--filter",
         "FullyQualifiedName~Phase09SigningOperationPostgresTests"], env)
    print("Step04 verifier completed. Owner review and later cryptographic/provider checks remain separate.")


if __name__ == "__main__":
    try:
        main()
    except verify.SafeError as error:
        print("ERROR: " + str(error), file=sys.stderr)
        sys.exit(2)
    except (EOFError, KeyboardInterrupt):
        print("\nCancelled.", file=sys.stderr)
        sys.exit(130)
    except (OSError, subprocess.TimeoutExpired):
        print("ERROR: Required command or local environment unavailable.", file=sys.stderr)
        sys.exit(2)
