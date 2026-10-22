#!/usr/bin/env python3
"""Phase09 Step25 acceptance runner. No migrations, deletion, secrets printing or authority calls.

Usage:
  python3 scripts/phase09_step25_accept.py pure
  python3 scripts/phase09_step25_accept.py database
  python3 scripts/phase09_step25_accept.py audit
Database mode uses the existing guarded phase09 verifier; it accepts ONLY
argintaxpayer_test over the local loopback or a local SSH tunnel.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "artifacts" / "phase09-step25-acceptance.json"
PG_CLASSES = [
    "Phase09SigningOperationPostgresTests",
    "Phase09SigningGenerationPostgresTests",
    "Phase09SigningImportPostgresTests",
    "Phase09SigningMatchPostgresTests",
]
PURE_FILTER = "FullyQualifiedName~Phase09"
WEB_SUITES = [
    "phase09-connection-layout.test.mjs",
    "phase09-step21-key-download.test.mjs",
    "phase09-step22-import-recovery.test.mjs",
    "phase09-step23-change-request.test.mjs",
    "phase09-step24-admin-review.test.mjs",
    "phase09-step24-key-replacement.test.mjs",
    "phase09-step25-acceptance.test.mjs",
]

def call(argv, env=None):
    print("RUN:", " ".join(argv), flush=True)
    result = subprocess.run(argv, cwd=ROOT, env=env, check=False)
    if result.returncode:
        raise RuntimeError("Gate failed: " + argv[0] + " (exit " + str(result.returncode) + ")")

def record(report, label, argv, env=None):
    try:
        call(argv, env)
        report["gates"][label] = "PASS"
    except (RuntimeError, FileNotFoundError) as exc:
        report["gates"][label] = "FAIL"
        report["failures"].append(label)
        print("FAILED:", label, type(exc).__name__, file=sys.stderr)

def verified_db_env():
    # Reuse project's conservative DB gate instead of parsing credentials here.
    sys.path.insert(0, str(ROOT / "scripts"))
    from phase09_verify import guard_database
    return guard_database()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["pure", "database", "audit"])
    args = parser.parse_args()
    os.chdir(ROOT)
    report = {
        "phase": 9, "step": 25, "mode": args.mode,
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "head": None, "gates": {}, "failures": [],
        "manualAcceptance": {
            "owner_existing_private_key": "NOT_VERIFIED",
            "owner_new_key_zip_handoff": "NOT_VERIFIED",
            "same_memory_public_key_portal_registration": "NOT_VERIFIED",
            "actual_get_token_and_fiscal_information": "NOT_VERIFIED",
            "level1_mfa_approve_reject_expire": "NOT_VERIFIED",
            "level2_cross_tenant_denial": "NOT_VERIFIED",
            "second_memory_plan_capacity": "NOT_VERIFIED",
            "rotation_concurrency_one_time_consumption": "NOT_VERIFIED",
            "database_backup_restore_and_master_key_recovery": "NOT_VERIFIED",
            "ubuntu_live_smoke_and_mac_live_smoke": "NOT_VERIFIED"
        }
    }
    try:
        sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        report["head"] = sha
    except (OSError, subprocess.CalledProcessError):
        report["failures"].append("git_head")
    if args.mode == "pure":
        safe = {k:v for k,v in os.environ.items()
                if not k.startswith(("ConnectionStrings__", "PG", "SigningKeyProtection__", "PHASE09_VAULT_"))}
        safe.update({"RUN_ARGINTAXPAYER_PG_INTEGRATION": "0",
                     "RUN_ARGINTAXPAYER_VAULT_INTEGRATION": "0",
                     "Ghasedak__EnableLiveSending": "false"})
        record(report, "backend_build", ["dotnet", "build", "ArginTaxPayer.sln", "-c", "Release"], safe)
        record(report, "backend_phase09_tests", [
            "dotnet","test","tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj",
            "-c","Release","--no-restore","--filter",PURE_FILTER], safe)
        record(report, "web_phase09_regressions", [
            "node","--test",*[str(Path("apps/web/tests")/x) for x in WEB_SUITES]], safe)
        record(report, "web_full_tests", ["npm","--prefix","apps/web","test"], safe)
        record(report, "web_typecheck", ["npm","--prefix","apps/web","run","typecheck"], safe)
        record(report, "web_build", ["npm","--prefix","apps/web","run","build"], safe)
    elif args.mode == "database":
        try:
            env = verified_db_env()
            report["gates"]["database_destination"] = "PASS"
        except Exception:
            env = None
            report["gates"]["database_destination"] = "FAIL"
            report["failures"].append("database_destination")
        if env is not None:
            record(report, "postgres_schema_readonly",
                ["python3", "scripts/phase09_step25_db_check.py"], env)
            for name in PG_CLASSES:
                record(report, "postgres_"+name,
                    ["python3","scripts/phase09_verify.py","integration",
                     "--filter","FullyQualifiedName~"+name], env)
    else:
        # Print/report only; do not assert that a checklist has been completed.
        print("Manual gates are NOT_VERIFIED until operator supplies evidence.")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Report saved:", REPORT.relative_to(ROOT))
    if report["failures"]:
        print("Automated Step25 gates FAILED:", ", ".join(report["failures"]), file=sys.stderr)
        return 1
    print("Automated gates completed; manual authority and operational gates remain NOT_VERIFIED.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
