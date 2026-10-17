#!/usr/bin/env python3
"""Phase06 final handoff audit — read-only, cross-platform (macOS / Ubuntu).

Usage: python3 scripts/phase06-step16-audit.py
       python3 scripts/phase06-step16-audit.py --json

Does not run migrations, contact a tax authority, start services, fetch secrets,
alter git refs or mark Phase06 complete. Result is evidence from the CURRENT checkout.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PHASE = ROOT / "docs/phases/phase-06.md"
REQUIRED = [
    "src/ArginTaxPayer.Domain/Catalog/InvoiceCatalogHandoff.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/InvoiceCatalogHandoffService.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/ReferenceImportService.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogReleaseActivationService.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/TaxIdentifierMappingService.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CustomerProductService.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CustomerServiceCatalogService.cs",
    "src/ArginTaxPayer.Api/Catalog/CatalogDevelopmentEndpoints.cs",
    "apps/web/app/(console)/catalog/page.tsx",
    "apps/web/app/catalog/workspace.tsx",
    "apps/web/app/catalog/persian-date-picker.tsx",
    "apps/web/public/brand/argin-logo.png",
    "apps/web/app/icon.png",
    "tests/ArginTaxPayer.Tests/Phase06SecurityRegressionTests.cs",
    "tests/ArginTaxPayer.Tests/Phase06InvoiceHandoffTests.cs",
    "scripts/phase06-step15-auth-smoke.py",
    "scripts/phase06-step15-db-guards.sh",
]
EXPECTED_MIGRATIONS = (
    "20261016190000_AddVatTreatmentAndContext",
    "20261016200000_EnforceReferenceVersionHistory",
    "20261016210000_AddReferenceActivationAudit",
    "20261016220000_ProtectOfficialTaxMappings",
)


def git(*args: str) -> str:
    result = subprocess.run(["git", *args], cwd=ROOT, capture_output=True,
                            text=True, check=False, timeout=15)
    return result.stdout.strip() if result.returncode == 0 else ""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true", help="print machine-readable nonsecret results")
    opts = parser.parse_args()
    text = PHASE.read_text(encoding="utf-8")
    rows = re.findall(r"^\|\s*(\d{2})\s*\|.*$", text, flags=re.MULTILINE)
    missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
    migrations = {
        name: bool(list((ROOT / "src/ArginTaxPayer.Infrastructure/Persistence/Migrations").glob(name + ".cs")))
        for name in EXPECTED_MIGRATIONS
    }
    branch = git("branch", "--show-current")
    commit = git("rev-parse", "HEAD")
    dirty = git("status", "--porcelain")
    step15 = next((row for row in text.splitlines() if row.startswith("| 15 |")), "")
    step16 = next((row for row in text.splitlines() if row.startswith("| 16 |")), "")
    checks = {
        "branch_is_phase06": branch == "phase/06-goods-services",
        "clean_checkout": dirty == "",
        "exactly_16_roadmap_steps": sorted(rows) == [f"{i:02d}" for i in range(1, 17)],
        "step15_owner_approved": "Owner-approved" in step15,
        "step16_not_prematurely_closed": "Pending" in step16,
        "all_required_files_exist": not missing,
        "expected_migrations_present": all(migrations.values()),
        "final_phase_document_present": PHASE.is_file(),
    }
    report = {
        "phase": "06", "step": "16", "branch": branch, "commit": commit,
        "checks": checks, "missing_files": missing, "migrations": migrations,
        "outcome": "PASS" if all(checks.values()) else "FAIL",
        "limitations": "Static/read-only repository audit only; not .NET/Next tests, live sessions, PostgreSQL or release signoff.",
    }
    if opts.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        print("Phase06 Step16 repository preflight", branch, commit[:12])
        for key, passed in checks.items():
            print(("PASS " if passed else "FAIL ") + key)
        if missing:
            print("Missing: " + ", ".join(missing))
        print("Result:", report["outcome"])
        print(report["limitations"])
    return 0 if report["outcome"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
