# Release schema and hosted Sandbox — 2026-11-12

## Release contract

Both GitHub release packaging and the Ubuntu manual build export `api/schema/migrations.sql` and `migrations.json` from the compiled EF migration assembly. They travel inside the checksummed API archive. No database is contacted while generating them.

The root-owned release helper, under the deployment lock, verifies that the database migration history is an exact prefix of the release manifest. Unknown migrations, holes and a database newer than the requested release block promotion. If migrations are pending it stops both services, takes and checks a custom PostgreSQL backup of the complete application `public` schema (including its data), encrypts it, applies the idempotent SQL through the restricted `argin_prod_migrator` role over verified TLS, and checks exact migration history before switching `current`. On failure it restarts the current release and rejects deployment. On application health failure only the application is rolled back; the schema is retained.

Future release migrations must remain compatible with the previous application during rollback. Destructive schema changes require a separately planned rollout. An EF migration history check cannot detect arbitrary manual schema drift; compare the physical schema when drift is suspected.

The runtime application keeps `argin_prod_app`, without DDL permissions. The pipeline does not grant its permissions or run uploaded shell scripts as root. SQL executes with database migration privileges, not OS privileges. The client runs as UID/GID 65534 and migration input is prefixed with a random-key restricted-mode command to block psql shell/file meta-commands.

## One-time Ubuntu setup before deploying this change

The current installed helper predates schema migration support. The updated CI and manual command refuse deployment until its capability is `schema-before-promote-v1`; an old helper can no longer silently publish an application without its schema.

1. Commit/review the complete release, including all migration source files. Use that exact clean checkout for installation.
2. Provision `/etc/argintaxpayer/migrations.env`, root-owned mode 0600. Raw `KEY=value` format, without shell quotes or expansion. Keep the dedicated migrator password in this private file, never Git or workflow output:

   ```text
   CONTAINER=argintaxpayer-production-postgres
   PGHOST=127.0.0.1
   PGPORT=5432
   PGDATABASE=argintaxpayer_prod
   PGUSER=argin_prod_migrator
   PGPASSWORD=<existing dedicated migration password>
   PGSSLMODE=verify-full
   PGSSLROOTCERT=<absolute path to the dedicated CA inside the container>
   ```

   The port is 5432 **inside** the container. The helper separately verifies that API configuration targets the dedicated host loopback port 15434. The CA must be a readable **public** certificate outside the private PostgreSQL data directory: the client runs as UID/GID 65534. The psql client must support [restricted mode](https://www.postgresql.org/docs/17/app-psql.html); unsupported clients stop before running migration SQL. Container name and CA path must match the actual deployment, which can differ from older Phase18 examples. Verify migration-role connectivity and privileges before upgrading.
3. Record the existing installed helper SHA256, then run from the clean reviewed checkout:

   ```sh
   sudo sha256sum /usr/local/sbin/argin-taxpayer-release
   sudo bash scripts/phase18-install-deploy-helper.sh --install <full-source-SHA> \
     --replace-helper-sha256 <reviewed-current-helper-SHA256>
   /usr/local/sbin/argin-taxpayer-release --capabilities
   ```

   The installer preserves the previous helper and creates the root-only backup encryption key only if absent. Back up `/etc/argintaxpayer/migration-backup.key` securely: encrypted database backups cannot be recovered without it.
4. Run the normal approved deployment. With the current database baseline, the three pending migrations are applied before the new API/Web become active. Nothing runs against Production during local tests.

The release helper backs up the application `public` schema; unrelated schemas or database-wide role definitions require their own administrative backup. The public CA at `/usr/local/share/argintaxpayer-migration/ca.pem` inside the production container must be provisioned again if that container is recreated.

Encrypted backups reside under `/var/backups/argintaxpayer` (0700). To restore, the operator decrypts a selected backup with `openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -pass file:/etc/argintaxpayer/migration-backup.key`, then follows the approved isolated restore process. Never restore over the live database as a routine deploy rollback.

## Sandbox and real Production on one database

`SandboxDatabasePolicy` retains the isolated test database and additionally admits the dedicated prod connection only through the existing `HostedSandboxDeployment` policy: explicit hosted opt-in, approved HTTPS public origin, Sandbox authority origin, dual-authority enablement, exact DB/user/host/port, VerifyFull and dedicated CA. Unknown deployments remain blocked.

The policy is used through legacy Sandbox serial reservation, preparation, arming, send attempts, receipts/failures, inquiries and legal successors. The worker also retains its scoped operation and immutable Sandbox payload/schema checks. Database deployment admission never selects the fiscal environment. Production and Sandbox routing still follows the persisted memory/invoice binding, with their separate key namespaces, serial reservations and authority adapters. No automatic retry or live invoice submission is part of deployment or verification.

## Verification

- A disposable local PostgreSQL instance with database `argintaxpayer_prod`, role `argin_prod_migrator` and TLS applied the prior 83-migration baseline, upgraded to all 86 compiled migrations and successfully replayed the complete idempotent SQL without adding migrations again. Neither live database was changed.
- Focused deployment tests exercise wrong database identity, unknown/holey histories, migration ordering, backup/SQL/verification failures, repeat deployment, manifest validation and versioned build metadata.
- Dual-authority tests cover hosted policy exclusions and persisted operation routing. Provider acceptance still requires an explicitly authorized invoice and actual official inquiry; offline checks do not prove authority acceptance.

Final checks: 1,615 .NET tests passed (55 optional integration/service tests skipped), 746 web tests passed, and focused Python deployment tests passed. TypeScript typecheck passed. Independent read-only code review found two deployment boundary issues; both were corrected and re-reviewed.
