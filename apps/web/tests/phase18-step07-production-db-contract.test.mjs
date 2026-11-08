import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = path => readFileSync(new URL("../../../" + path, import.meta.url), "utf8");
const compose = read("ops/phase18-production-db/compose.yaml");
const init = read("ops/phase18-production-db/init/01-init-production.sh");
const provisioning = read("scripts/phase18-step07-provision-production-db.sh");
const audit = read("scripts/phase18-step07-production-db-audit.sh");
const review = read("scripts/phase18-step07-migration-sql-review.sh");
const apply = read("scripts/phase18-step07-apply-reviewed-migration.sh");
const backup = read("scripts/phase18-step07-backup-restore-drill.sh");

test("isolated PostgreSQL 17 uses a separate Docker project, volume and only loopback", () => {
  assert.match(compose, /name: argintaxpayer-phase18-production/);
  assert.match(compose, /image: \$\{PHASE18_PROD_POSTGRES_IMAGE:\?/);
  assert.match(compose, /container_name: argintaxpayer-phase18-production-postgres/);
  assert.match(compose, /127\.0\.0\.1:15434:5432/);
  assert.match(compose, /prod_pgdata:|prod_pgdata\//);
  assert.match(compose, /ssl=on/);
  assert.match(compose, /scram-sha-256/);
  assert.match(compose, /restart: "no"/);
  assert.doesNotMatch(compose, /postgres-postgres-1|argintaxpayer-phase18-postgres|15433:5432/);
});

test("bootstrap creates non-superuser app + migration identities and denies plaintext", () => {
  assert.match(init, /CREATE ROLE argin_prod_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION/);
  assert.match(init, /CREATE ROLE argin_prod_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION/);
  assert.match(init, /CREATE DATABASE argintaxpayer_prod OWNER argin_prod_migrator/);
  assert.match(init, /REVOKE ALL ON DATABASE argintaxpayer_prod FROM PUBLIC/);
  assert.match(init, /REVOKE ALL ON SCHEMA public FROM PUBLIC/);
  assert.match(init, /ALTER DEFAULT PRIVILEGES FOR ROLE argin_prod_migrator/);
  assert.match(init, /hostnossl all all 0\.0\.0\.0\/0 reject/);
  assert.match(init, /hostnossl all all ::\/0 reject/);
});

test("operator scripts never reference shared DB writes or public app services", () => {
  for (const src of [provisioning, audit, review, apply, backup]) {
    assert.doesNotMatch(src, /docker\s+compose\s+down|docker\s+(rm|volume\s+rm)|dropdb/i);
    assert.doesNotMatch(src, /systemctl\s+(start|enable|restart)|nginx\s+-s|ufw\s+(allow|deny)/);
    assert.doesNotMatch(src, /http[s]?:\/\/tp\.tax\.gov\.ir|send-and-inquire/);
  }
  assert.match(provisioning, /--provision-isolated-production-db/);
  assert.match(provisioning, /15434/);
  assert.match(provisioning, /image_digest/);
  assert.match(provisioning, /RepoDigests/);
  assert.match(audit, /PGSSLMODE=verify-full/);
  assert.match(audit, /PGSSLMODE=disable/);
  assert.match(review, /migrations script --idempotent/);
  assert.match(apply, /PHASE18_REVIEWED_SQL_SHA256/);
  assert.match(apply, /PHASE18_OWNER_APPROVED_INITIAL_MIGRATION/);
  assert.match(backup, /argin_p18_restore_/);
  assert.match(backup, /identity_users/);
});

const guarded = [
  "20261020181500_AddPhase09SigningMetadata.cs",
  "20261022140000_AddEncryptedDatabaseSigningCustody.cs",
  "20261022150000_AllowDatabaseSigningCustodyReference.cs",
  "20261023190000_AddPhase10InvoiceDispatchOutbox.cs",
  "20261024103000_AddSandboxFiscalMemoryMetadata.cs",
  "20261024170000_AddSandboxEncryptedCustodyColumns.cs",
  "20261025183000_AddSandboxInvoiceFiscalReservations.cs",
  "20261025230000_AddSandboxInvoiceLifecycleEvents.cs",
  "20261025235900_AlignSandboxDispatchMemoryForeignKey.cs",
  "20261026110000_AddLegacyAuthorityInquiryReports.cs"
];

test("all 10 legacy signing and sandbox migrations admit only dedicated TLS migrator", () => {
  const dir = "src/ArginTaxPayer.Infrastructure/Persistence/Migrations/";
  for (const name of guarded) {
    const text = read(dir + name);
    assert.match(text, /current_database\(\) = 'argintaxpayer_prod'/);
    assert.match(text, /current_user = 'argin_prod_migrator'/);
    assert.match(text, /pg_stat_ssl/);
    assert.match(text, /current_database\(\) = 'argin_phase18_acceptance'/);
    assert.match(text, /current_user = 'phase18_acceptance'/);
    assert.doesNotMatch(text, /current_user = 'argin_prod_app'/);
  }
});
