#!/usr/bin/env python3
"""Back up and apply only Phase09 signing metadata to argintaxpayer_test.

Run from the checkout: python3 scripts/phase09_apply_migration.py
Requires the existing local PostgreSQL tunnel on 127.0.0.1:15432.
Does not start services, create a tunnel, reset history, or apply other migrations.
"""
import getpass
import os
from pathlib import Path
import shutil
import subprocess
import sys
from datetime import datetime, timezone

sys.dont_write_bytecode = True
import phase09_verify as verify

PREVIOUS = "20261020104500_AddInvoiceWorkspaceRevisionAudit"
TARGET = "20261020181500_AddPhase09SigningMetadata"
EF_OPTIONS = [
    "--project", "src/ArginTaxPayer.Infrastructure",
    "--startup-project", "src/ArginTaxPayer.Api",
]


def read_terminal(prompt):
    # File execution normally has a terminal stdin. Explicit /dev/tty also
    # supports a redirected stdin without the heredoc input()/EOF failure.
    try:
        with open("/dev/tty", "r") as terminal:
            print(prompt, end="", file=sys.stderr, flush=True)
            answer = terminal.readline()
    except OSError:
        raise verify.SafeError("An interactive terminal is required.") from None
    if not answer:
        raise verify.SafeError("Terminal input ended; no migration started.")
    return answer.strip()


def pg_query(pg_env, statement):
    result = subprocess.run(
        ["psql", "-X", "-w", "-A", "-t", "-q", "-v", "ON_ERROR_STOP=1",
         "-c", statement],
        env=pg_env, capture_output=True, text=True, timeout=20, check=False,
    )
    if result.returncode:
        raise verify.SafeError(
            "PostgreSQL inspection failed; check the tunnel and test credentials."
        )
    return result.stdout.strip()


def main():
    if len(sys.argv) != 1:
        raise verify.SafeError("Usage: python3 scripts/phase09_apply_migration.py")
    os.chdir(verify.ROOT)
    os.umask(0o077)
    for command in ("git", "dotnet", "psql", "pg_dump", "pg_restore"):
        if shutil.which(command) is None:
            raise verify.SafeError("Required command unavailable: " + command)
    initial_sha = verify.preflight()

    print("Target: argintaxpayer_test at 127.0.0.1:15432")
    print("Migration: " + TARGET)
    user = read_terminal("PostgreSQL user [argintaxpayer]: ") or "argintaxpayer"
    password = getpass.getpass("Test database password: ")
    if not password:
        raise verify.SafeError("An explicit test database password is required.")
    quote = lambda value: '"' + value.replace('"', '""') + '"'
    os.environ["ConnectionStrings__ArginTaxPayerDb"] = (
        "Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;"
        f"Username={quote(user)};Password={quote(password)};SSL Mode=Disable"
    )
    env = verify.guard_database()
    pg = env.copy()
    pg.update(
        PGHOST="127.0.0.1", PGPORT="15432", PGDATABASE="argintaxpayer_test",
        PGUSER=user, PGPASSWORD=password, PGSSLMODE="disable",
        PGCONNECT_TIMEOUT="10", PGPASSFILE=os.devnull,
    )

    history_sql = (
        'SELECT "MigrationId" FROM "__EFMigrationsHistory" '
        'ORDER BY "MigrationId" DESC LIMIT 1;'
    )
    history = pg_query(pg, history_sql)
    print("Latest applied migration: " + history)
    if history == TARGET:
        print("Phase09 migration is already applied. No changes made.")
        return
    if history != PREVIOUS:
        raise verify.SafeError(
            "Unexpected migration history; expected " + PREVIOUS +
            ". No migration applied. Send the displayed history for review."
        )
    existing = pg_query(pg, (
        "SELECT count(*) FROM pg_catalog.pg_tables "
        "WHERE schemaname = 'public' AND tablename IN "
        "('signing_operations','signing_consents','signing_key_versions',"
        "'signing_key_selections','signing_change_requests','signing_audit_events');"
    ))
    if existing != "0":
        raise verify.SafeError(
            "Signing tables already exist without the expected migration history; stop for review."
        )

    verify.run(
        ["dotnet", "ef", "migrations", "has-pending-model-changes"] + EF_OPTIONS, env
    )

    folder = Path.home() / ".argintaxpayer-test" / "backups"
    folder.mkdir(parents=True, exist_ok=True)
    folder.chmod(0o700)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
    backup = folder / f"before-phase09-{stamp}.dump"
    partial = backup.with_suffix(".dump.partial")
    sql = folder / f"phase09-signing-{stamp}.sql"

    result = subprocess.run(
        ["pg_dump", "-w", "-Fc", "-f", str(partial)],
        env=pg, capture_output=True, text=True, check=False,
    )
    if result.returncode or not partial.is_file() or partial.stat().st_size == 0:
        raise verify.SafeError("Backup failed. No migration applied.")
    checked = subprocess.run(
        ["pg_restore", "--list", str(partial)],
        capture_output=True, text=True, check=False,
    )
    if checked.returncode:
        raise verify.SafeError("Backup archive inspection failed. No migration applied.")
    partial.replace(backup)
    backup.chmod(0o600)
    print("Backup archive: " + str(backup))
    print("Archive inspection succeeded; this is not a full restore test.")

    verify.run(
        ["dotnet", "ef", "migrations", "script", PREVIOUS, TARGET,
         "--output", str(sql)] + EF_OPTIONS, env,
    )
    if not sql.is_file() or sql.stat().st_size == 0:
        raise verify.SafeError("No SQL review file produced. No migration applied.")
    sql.chmod(0o600)
    print("Review SQL: " + str(sql))
    print("You can inspect that file in another terminal before answering.")
    if read_terminal("Apply ONLY Phase09 to argintaxpayer_test? Type APPLY: ") != "APPLY":
        print("Cancelled. Backup and SQL retained; no migration applied.")
        return

    # Recheck checkout and database immediately before any migration mutation.
    if verify.preflight() != initial_sha:
        raise verify.SafeError("Checkout changed during review; no migration applied.")
    verify.guard_database()
    if pg_query(pg, history_sql) != PREVIOUS:
        raise verify.SafeError("Migration history changed during review; stop and rerun inspection.")

    verify.run(["dotnet", "ef", "database", "update", TARGET] + EF_OPTIONS, env)
    if pg_query(pg, history_sql) != TARGET:
        raise verify.SafeError("Target migration is not the latest applied entry; inspect before retry.")
    count = pg_query(pg, (
        "SELECT count(*) FROM pg_catalog.pg_tables "
        "WHERE schemaname = 'public' AND tablename IN "
        "('signing_operations','signing_consents','signing_key_versions',"
        "'signing_key_selections','signing_change_requests','signing_audit_events');"
    ))
    if count != "6":
        raise verify.SafeError("Migration history exists but expected table count is not six.")
    print("Phase09 migration applied to argintaxpayer_test; six signing tables found.")
    print("This is not acceptance of trigger behavior, authorization, or key custody.")


if __name__ == "__main__":
    try:
        main()
    except (verify.SafeError, subprocess.TimeoutExpired) as error:
        message = str(error) if isinstance(error, verify.SafeError) else "Command timed out; inspect the result before retrying."
        print("ERROR: " + message, file=sys.stderr)
        sys.exit(2)
    except (EOFError, KeyboardInterrupt):
        print("\nCancelled. Inspect migration history if cancellation occurred during application.", file=sys.stderr)
        sys.exit(130)
    except OSError:
        print("ERROR: Local file or command access failed; inspect before retrying.", file=sys.stderr)
        sys.exit(2)
