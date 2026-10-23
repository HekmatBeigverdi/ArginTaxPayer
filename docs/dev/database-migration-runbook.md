# ArginTaxPayer — canonical macOS PostgreSQL migration runbook

**Scope:** All ArginTaxPayer future development-phase migration steps. Use this runbook instead of reconstructing ad-hoc SSH/password/psql commands. No Ubuntu shell/test before Phase10 Step23. **Never** change production DB with development instructions.

## Standard order

1. From macOS repository root: `git status --short`, `git fetch origin --prune`, `git switch phase/10-invoice-submission`, `git pull --ff-only origin phase/10-invoice-submission`.
2. Launch **existing project helper**: `zsh scripts/dev-status.zsh`; if stopped run **`zsh scripts/dev-up.zsh`**, then `zsh scripts/dev-status.zsh`. `dev-up.zsh` loads gitignored `.env.dev.local`, starts AutoSSH `127.0.0.1:15432`, API and Next.js, and preflights `argintaxpayer_test`; do not suggest a second manual AutoSSH tunnel. If API/Web already running, do not restart needlessly.
3. **The launcher's exported environment does not propagate to the invoking shell.** For subsequent `dotnet ef` or `psql` in that terminal run:
   ```zsh
   set -a
   source .env.dev.local
   set +a
   ```
   **Correct file name is `.env.dev.local` (not `.end.dev.local`).** Never print/export credentials to chat/logs. `chmod 600 .env.dev.local`.
4. Verify `ConnectionStrings__ArginTaxPayerDb` is present and targets **exactly** `Host=127.0.0.1;Port=15432;Database=argintaxpayer_test`; exit on any other target. Existing `ArginTaxPayerDesignTimeDbContextFactory` reads exactly this variable. Missing/incorrect name causes dangerous confusion; never fall back to prod.
5. Use `psql` with `"MigrationId"` / `"__EFMigrationsHistory"` (case-sensitive, quoted) — **not `migration_id`**. Expected Phase09 latest: `20261022150000_AllowDatabaseSigningCustodyReference`. Check tables absent/present and history before applying any schema change.
6. Run macOS `dotnet restore`, `dotnet build`, focused tests and full suite. Generate the precise idempotent EF migration SQL for review and **confirm source migration and expected previous entry match actual DB**.
7. Back up isolated `argintaxpayer_test` with `pg_dump -Fc` to `~/.argintaxpayer-test/backups/`; check `pg_restore --list`. Neither a valid archive list nor successful backup means it has been restore-tested.
8. **Explicit owner approval required before applying each migration**, then use pinned `dotnet ef database update <target>` on the same local shell with `.env.dev.local` sourced. Never use `database drop`, `update 0`, blind `DROP`/destructive rollback or reset migrations.
9. Requery `"__EFMigrationsHistory"`, `to_regclass` for expected tables, constraints/indices, then execute scoped DB concurrency/integration smoke only on test data. Do not make Tax Authority invoice sends as part of DB tests.
10. Record final SHA, backup location (not secret), migration result, test counts and DB identity in the phase document. For Phase10, **Ubuntu VPS acceptance is Step23 only**; Mac-side DB through existing tunnel is not an Ubuntu OS test.

## Phase10 Step11: reproducible operator entrypoint

Run `zsh scripts/phase10-step11-migration.zsh` to do **read-only preflight**, take a backup, and generate a review SQL. It asks for explicit `APPLY` before any database schema mutation. The script only works on `argintaxpayer_test` and stops if the prior migration is not `20261022150000_AllowDatabaseSigningCustodyReference`.

Execute in the same Terminal after:

```zsh
cd ~/projects/ArginTaxPayer
zsh scripts/dev-status.zsh
# Only if helper reports stopped and no conflicting services:
zsh scripts/dev-up.zsh
set -a
source .env.dev.local
set +a
zsh scripts/phase10-step11-migration.zsh
```

**Owner must inspect generated SQL first**. At the prompt, press Enter (no changes) until explicitly authorized. On approval of reviewed SQL, run the same script and type `APPLY`.

All DB safety controls should be embedded in reusable helpers; instructions in chat should reference this runbook and the helper, not reproduce manual AutoSSH, ad-hoc connection parsing or inline passwords.
