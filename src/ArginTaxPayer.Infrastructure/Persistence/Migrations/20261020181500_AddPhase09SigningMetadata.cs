using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>Manually authored additive metadata migration. Applying it requires separate owner approval.</summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261020181500_AddPhase09SigningMetadata")]
public sealed partial class AddPhase09SigningMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';

DO $$
BEGIN
  IF current_database() <> 'argintaxpayer_test' THEN
    RAISE EXCEPTION 'Phase09 draft requires argintaxpayer_test';
  END IF;
END $$;

CREATE UNIQUE INDEX ux_phase09_taxpayer_scope
  ON taxpayer_profiles(id, customer_user_id);
CREATE UNIQUE INDEX ux_phase09_memory_scope
  ON fiscal_memories(id, taxpayer_id);

CREATE TABLE signing_operations (
  id uuid PRIMARY KEY,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL CONSTRAINT fk_p09_op_taxpayer_id REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
  fiscal_memory_id uuid NOT NULL CONSTRAINT fk_p09_op_fiscal_memory_id REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
  operation_id uuid NOT NULL,
  actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  operation_kind smallint NOT NULL CONSTRAINT ck_p09_op_1 CHECK (operation_kind BETWEEN 1 AND 4),
  profile_id varchar(96) NOT NULL,
  metadata_digest_sha256 varchar(64) NOT NULL
    CONSTRAINT ck_p09_op_2 CHECK (metadata_digest_sha256 ~ '^[0-9a-f]{64}$'),
  state smallint NOT NULL CONSTRAINT ck_p09_op_3 CHECK (state BETWEEN 1 AND 5),
  safe_error_code varchar(64) NULL,
  revision bigint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_op_4 CHECK (revision > 0),
  created_at_utc timestamptz NOT NULL,
  updated_at_utc timestamptz NOT NULL,
  CONSTRAINT ck_p09_op_owner CHECK (operation_kind NOT IN (1,2) OR actor_user_id = customer_user_id),
  CONSTRAINT ck_signing_op_time CHECK (updated_at_utc >= created_at_utc),
  CONSTRAINT fk_signing_op_taxpayer
    FOREIGN KEY(taxpayer_id, customer_user_id)
    REFERENCES taxpayer_profiles(id, customer_user_id) ON DELETE RESTRICT,
  CONSTRAINT fk_signing_op_memory
    FOREIGN KEY(fiscal_memory_id, taxpayer_id)
    REFERENCES fiscal_memories(id, taxpayer_id) ON DELETE RESTRICT,
  CONSTRAINT ux_signing_op_idempotency
    UNIQUE(customer_user_id, taxpayer_id, fiscal_memory_id, operation_id),
  CONSTRAINT ux_signing_op_scope
    UNIQUE(id, customer_user_id, taxpayer_id, fiscal_memory_id)
);

CREATE TABLE signing_consents (
  id uuid PRIMARY KEY,
  operation_record_id uuid NOT NULL,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL,
  fiscal_memory_id uuid NOT NULL,
  actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  consent_version varchar(64) NOT NULL CONSTRAINT ck_p09_consent_1 CHECK (length(trim(consent_version)) > 0),
  reauthenticated_at_utc timestamptz NOT NULL,
  consented_at_utc timestamptz NOT NULL,
  CONSTRAINT ck_signing_consent_owner CHECK (actor_user_id = customer_user_id),
  CONSTRAINT ck_signing_consent_time CHECK (
    consented_at_utc >= reauthenticated_at_utc AND
    consented_at_utc <= reauthenticated_at_utc + interval '5 minutes'),
  CONSTRAINT ux_signing_consent_operation UNIQUE(operation_record_id),
  CONSTRAINT fk_signing_consent_operation FOREIGN KEY
    (operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_operations(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT
);

CREATE TABLE signing_key_versions (
  id uuid PRIMARY KEY,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL,
  fiscal_memory_id uuid NOT NULL,
  origin_operation_record_id uuid NOT NULL,
  origin smallint NOT NULL CONSTRAINT ck_p09_key_1 CHECK (origin IN (1,2)),
  version_number bigint NOT NULL CONSTRAINT ck_p09_key_2 CHECK (version_number > 0),
  profile_id varchar(96) NOT NULL,
  public_fingerprint_sha256 varchar(64) NULL
    CONSTRAINT ck_p09_key_3 CHECK (public_fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
  public_spki_der bytea NULL
    CONSTRAINT ck_p09_key_4 CHECK (octet_length(public_spki_der) BETWEEN 1 AND 4096),
  custody_reference varchar(256) NULL
    CONSTRAINT ck_p09_key_5 CHECK (custody_reference ~ '^vault://[A-Za-z0-9/_-]{1,248}$'),
  protection_key_version varchar(64) NULL,
  lifecycle_state smallint NOT NULL CONSTRAINT ck_p09_key_6 CHECK (lifecycle_state BETWEEN 1 AND 5),
  portal_state smallint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_key_7 CHECK (portal_state IN (1,2)),
  public_match smallint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_key_8 CHECK (public_match IN (1,2,3)),
  revision bigint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_key_9 CHECK (revision > 0),
  created_at_utc timestamptz NOT NULL,
  updated_at_utc timestamptz NOT NULL,
  CONSTRAINT ck_signing_key_time CHECK (updated_at_utc >= created_at_utc),
  CONSTRAINT ck_signing_key_sealed CHECK (
    lifecycle_state = 1 OR
    (public_fingerprint_sha256 IS NOT NULL AND public_spki_der IS NOT NULL
      AND custody_reference IS NOT NULL AND protection_key_version IS NOT NULL)),
  CONSTRAINT ck_signing_key_ready_match CHECK (
    lifecycle_state <> 2 OR public_match <> 3),
  CONSTRAINT fk_signing_key_origin FOREIGN KEY
    (origin_operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_operations(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT,
  CONSTRAINT ux_signing_key_origin UNIQUE(origin_operation_record_id),
  CONSTRAINT ux_signing_key_version UNIQUE(fiscal_memory_id, version_number),
  CONSTRAINT ux_signing_key_scope
    UNIQUE(id, customer_user_id, taxpayer_id, fiscal_memory_id)
);

CREATE UNIQUE INDEX ux_signing_key_custody
  ON signing_key_versions(custody_reference)
  WHERE custody_reference IS NOT NULL;
CREATE INDEX ix_signing_key_customer_history
  ON signing_key_versions(customer_user_id, taxpayer_id, created_at_utc, id);
CREATE INDEX ix_signing_op_recovery
  ON signing_operations(state, updated_at_utc, id)
  WHERE state IN (1,2,5);

CREATE TABLE signing_key_selections (
  fiscal_memory_id uuid PRIMARY KEY,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL,
  key_version_id uuid NULL,
  revision bigint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_selection_1 CHECK (revision > 0),
  selected_at_utc timestamptz NOT NULL,
  selected_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  CONSTRAINT fk_signing_selection_key FOREIGN KEY
    (key_version_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_key_versions(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT
);

CREATE TABLE signing_change_requests (
  id uuid PRIMARY KEY,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL,
  fiscal_memory_id uuid NOT NULL,
  current_key_version_id uuid NOT NULL,
  expected_key_revision bigint NOT NULL CONSTRAINT ck_p09_review_1 CHECK (expected_key_revision > 0),
  kind smallint NOT NULL CONSTRAINT ck_p09_review_2 CHECK (kind BETWEEN 1 AND 3),
  state smallint NOT NULL CONSTRAINT ck_p09_review_3 CHECK (state BETWEEN 1 AND 5),
  reason_code varchar(64) NOT NULL,
  explanation varchar(1000) NOT NULL CONSTRAINT ck_p09_review_4 CHECK (length(trim(explanation)) > 0),
  requested_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  reviewed_by_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  decision_reason varchar(1000) NULL,
  requested_at_utc timestamptz NOT NULL,
  reviewed_at_utc timestamptz NULL,
  expires_at_utc timestamptz NULL,
  consumed_at_utc timestamptz NULL,
  consumed_by_operation_record_id uuid NULL,
  revision bigint NOT NULL DEFAULT 1 CONSTRAINT ck_p09_review_5 CHECK (revision > 0),
  CONSTRAINT ck_signing_review_distinct CHECK (
    reviewed_by_user_id IS NULL OR reviewed_by_user_id <> requested_by_user_id),
  CONSTRAINT ck_signing_review_decision CHECK (
    (state = 1 AND reviewed_by_user_id IS NULL AND reviewed_at_utc IS NULL
       AND expires_at_utc IS NULL) OR
    (state IN (2,3,4,5) AND reviewed_by_user_id IS NOT NULL
       AND reviewed_at_utc IS NOT NULL AND reviewed_at_utc >= requested_at_utc
       AND decision_reason IS NOT NULL AND length(trim(decision_reason)) > 0)),
  CONSTRAINT ck_signing_review_expiry CHECK (
    (state = 3 AND expires_at_utc IS NULL) OR state = 1 OR
    (state IN (2,4,5) AND expires_at_utc IS NOT NULL
      AND expires_at_utc = reviewed_at_utc + interval '24 hours')),
  CONSTRAINT ck_signing_review_consumption CHECK (
    (state = 4 AND consumed_at_utc IS NOT NULL
      AND consumed_by_operation_record_id IS NOT NULL
      AND consumed_at_utc >= reviewed_at_utc AND consumed_at_utc <= expires_at_utc) OR
    (state <> 4 AND consumed_at_utc IS NULL AND consumed_by_operation_record_id IS NULL)),
  CONSTRAINT fk_signing_review_key FOREIGN KEY
    (current_key_version_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_key_versions(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT,
  CONSTRAINT fk_signing_review_consumption FOREIGN KEY
    (consumed_by_operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_operations(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT
);
CREATE INDEX ix_signing_review_scope_state
  ON signing_change_requests(customer_user_id, taxpayer_id, state, requested_at_utc);

CREATE TABLE signing_audit_events (
  id uuid PRIMARY KEY,
  customer_user_id uuid NOT NULL,
  taxpayer_id uuid NOT NULL,
  fiscal_memory_id uuid NOT NULL,
  operation_record_id uuid NOT NULL,
  actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
  subject_kind smallint NOT NULL CONSTRAINT ck_p09_audit_1 CHECK (subject_kind BETWEEN 1 AND 5),
  subject_id uuid NOT NULL,
  action_code varchar(64) NOT NULL,
  outcome_code varchar(64) NOT NULL,
  revision bigint NOT NULL CONSTRAINT ck_p09_audit_2 CHECK (revision > 0),
  public_fingerprint_sha256 varchar(64) NULL
    CONSTRAINT ck_p09_audit_3 CHECK (public_fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
  occurred_at_utc timestamptz NOT NULL,
  CONSTRAINT fk_signing_audit_operation FOREIGN KEY
    (operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id)
    REFERENCES signing_operations(id, customer_user_id, taxpayer_id, fiscal_memory_id)
    ON DELETE RESTRICT
);
CREATE INDEX ix_signing_audit_scope_time
  ON signing_audit_events(customer_user_id, taxpayer_id, occurred_at_utc, id);

CREATE UNIQUE INDEX ux_p09_review_consumption ON signing_change_requests(consumed_by_operation_record_id) WHERE consumed_by_operation_record_id IS NOT NULL;
CREATE UNIQUE INDEX ux_signing_audit_subject_revision ON signing_audit_events(subject_kind, subject_id, revision);

CREATE INDEX ix_p09_op_fk1 ON signing_operations(taxpayer_id);

CREATE INDEX ix_p09_op_fk2 ON signing_operations(fiscal_memory_id);

CREATE INDEX ix_p09_op_fk3 ON signing_operations(actor_user_id);

CREATE INDEX ix_p09_consent_fk1 ON signing_consents(actor_user_id);

CREATE INDEX ix_p09_consent_fk2 ON signing_consents(operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id);

CREATE INDEX ix_p09_key_fk1 ON signing_key_versions(origin_operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id);

CREATE INDEX ix_p09_selection_fk1 ON signing_key_selections(selected_by_user_id);

CREATE INDEX ix_p09_selection_fk2 ON signing_key_selections(key_version_id, customer_user_id, taxpayer_id, fiscal_memory_id);

CREATE INDEX ix_p09_review_fk1 ON signing_change_requests(requested_by_user_id);

CREATE INDEX ix_p09_review_fk2 ON signing_change_requests(reviewed_by_user_id);

CREATE INDEX ix_p09_review_fk3 ON signing_change_requests(current_key_version_id, customer_user_id, taxpayer_id, fiscal_memory_id);

CREATE INDEX ix_p09_review_fk4 ON signing_change_requests(consumed_by_operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id);

CREATE INDEX ix_p09_audit_fk1 ON signing_audit_events(actor_user_id);

CREATE INDEX ix_p09_audit_fk2 ON signing_audit_events(operation_record_id, customer_user_id, taxpayer_id, fiscal_memory_id);
-- Immutable evidence and retained revision history.
CREATE FUNCTION phase09_no_delete() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'Phase09 history cannot be deleted or truncated'
    USING ERRCODE = '23514';
END $$;

CREATE FUNCTION phase09_evidence_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'Phase09 evidence is append-only'
    USING ERRCODE = '23514';
END $$;

CREATE FUNCTION phase09_mutation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE allowed text[];
BEGIN
  IF NEW.revision <> OLD.revision + 1 THEN
    RAISE EXCEPTION 'Phase09 revision must advance by one' USING ERRCODE = '23514';
  END IF;
  CASE TG_TABLE_NAME
  WHEN 'signing_operations' THEN
    allowed := ARRAY['state','safe_error_code','revision','updated_at_utc'];
    IF NEW.updated_at_utc < OLD.updated_at_utc THEN
      RAISE EXCEPTION 'Phase09 timestamp regression' USING ERRCODE = '23514';
    END IF;
    IF NEW.state <> OLD.state AND NOT (
      (OLD.state = 1 AND NEW.state IN (2,3,4,5)) OR
      (OLD.state = 2 AND NEW.state IN (3,4,5)) OR
      (OLD.state = 5 AND NEW.state IN (2,3,4))
    ) THEN
      RAISE EXCEPTION 'Invalid Phase09 operation transition' USING ERRCODE = '23514';
    END IF;
    IF OLD.state IN (3,4) THEN
      RAISE EXCEPTION 'Terminal Phase09 operation' USING ERRCODE = '23514';
    END IF;
  WHEN 'signing_key_versions' THEN
    allowed := ARRAY['public_fingerprint_sha256','public_spki_der','custody_reference',
      'protection_key_version','lifecycle_state','portal_state','public_match','revision','updated_at_utc'];
    IF NEW.updated_at_utc < OLD.updated_at_utc THEN
      RAISE EXCEPTION 'Phase09 timestamp regression' USING ERRCODE = '23514';
    END IF;
    IF (OLD.lifecycle_state <> 1 OR OLD.public_fingerprint_sha256 IS NOT NULL)
      AND NEW.public_fingerprint_sha256 IS DISTINCT FROM OLD.public_fingerprint_sha256 THEN
      RAISE EXCEPTION 'Immutable public fingerprint' USING ERRCODE = '23514';
    END IF;
    IF (OLD.lifecycle_state <> 1 OR OLD.public_spki_der IS NOT NULL)
      AND NEW.public_spki_der IS DISTINCT FROM OLD.public_spki_der THEN
      RAISE EXCEPTION 'Immutable public key' USING ERRCODE = '23514';
    END IF;
    IF (OLD.lifecycle_state <> 1 OR OLD.custody_reference IS NOT NULL)
      AND NEW.custody_reference IS DISTINCT FROM OLD.custody_reference THEN
      RAISE EXCEPTION 'Immutable custody reference' USING ERRCODE = '23514';
    END IF;
    IF NEW.lifecycle_state <> OLD.lifecycle_state AND NOT (
      (OLD.lifecycle_state = 1 AND NEW.lifecycle_state = 2) OR
      (OLD.lifecycle_state = 2 AND NEW.lifecycle_state IN (3,4,5)) OR
      (OLD.lifecycle_state = 3 AND NEW.lifecycle_state IN (2,4,5))
    ) THEN
      RAISE EXCEPTION 'Invalid Phase09 key transition' USING ERRCODE = '23514';
    END IF;
  WHEN 'signing_key_selections' THEN
    allowed := ARRAY['key_version_id','revision','selected_at_utc','selected_by_user_id'];
  WHEN 'signing_change_requests' THEN
    allowed := ARRAY['state','reviewed_by_user_id','decision_reason','reviewed_at_utc',
      'expires_at_utc','consumed_at_utc','consumed_by_operation_record_id','revision'];
    IF NEW.state <> OLD.state AND NOT (
      (OLD.state = 1 AND NEW.state IN (2,3)) OR
      (OLD.state = 2 AND NEW.state IN (4,5))
    ) THEN
      RAISE EXCEPTION 'Invalid Phase09 review transition' USING ERRCODE = '23514';
    END IF;
    IF OLD.state = 2 AND NEW.state = 4 AND clock_timestamp() > OLD.expires_at_utc THEN
      RAISE EXCEPTION 'Expired Phase09 approval cannot be consumed' USING ERRCODE = '23514';
    END IF;
    IF OLD.state = 2 AND NEW.state = 5 AND clock_timestamp() < OLD.expires_at_utc THEN
      RAISE EXCEPTION 'Phase09 approval has not expired' USING ERRCODE = '23514';
    END IF;
    IF OLD.state IN (3,4,5) THEN
      RAISE EXCEPTION 'Terminal Phase09 review' USING ERRCODE = '23514';
    END IF;
    IF OLD.state = 2 AND ROW(NEW.reviewed_by_user_id,NEW.decision_reason,
       NEW.reviewed_at_utc,NEW.expires_at_utc) IS DISTINCT FROM
       ROW(OLD.reviewed_by_user_id,OLD.decision_reason,OLD.reviewed_at_utc,OLD.expires_at_utc) THEN
      RAISE EXCEPTION 'Immutable Phase09 approval' USING ERRCODE = '23514';
    END IF;
  ELSE
    RAISE EXCEPTION 'Unexpected Phase09 guard target';
  END CASE;
  IF (to_jsonb(NEW) - allowed) IS DISTINCT FROM (to_jsonb(OLD) - allowed) THEN
    RAISE EXCEPTION 'Immutable Phase09 metadata' USING ERRCODE = '23514';
  END IF;
  RETURN NEW;
END $$;

-- Lock keys in UUID order before a selection mutation; key updates lock their own row.
CREATE FUNCTION phase09_selection_lock() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE prior_id uuid;
BEGIN
  IF TG_OP = 'UPDATE' THEN prior_id := OLD.key_version_id; END IF;
  PERFORM id FROM signing_key_versions
    WHERE id = NEW.key_version_id OR id = prior_id ORDER BY id FOR UPDATE;
  RETURN NEW;
END $$;

CREATE FUNCTION phase09_selection_ready() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM signing_key_selections s
    JOIN signing_key_versions k ON k.id = s.key_version_id
    WHERE s.fiscal_memory_id = NEW.fiscal_memory_id AND k.lifecycle_state <> 2
  ) THEN
    RAISE EXCEPTION 'Selected Phase09 key is not locally ready' USING ERRCODE = '23514';
  END IF;
  RETURN NULL;
END $$;

CREATE FUNCTION phase09_require_consent() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.lifecycle_state <> 1 AND NOT EXISTS (
    SELECT 1 FROM signing_consents c
    JOIN signing_operations o ON o.id = c.operation_record_id
    WHERE c.operation_record_id = NEW.origin_operation_record_id
      AND c.customer_user_id = NEW.customer_user_id AND c.taxpayer_id = NEW.taxpayer_id
      AND c.fiscal_memory_id = NEW.fiscal_memory_id AND c.actor_user_id = NEW.customer_user_id
      AND o.operation_kind = NEW.origin
      AND o.actor_user_id = NEW.customer_user_id
  ) THEN
    RAISE EXCEPTION 'Sealed Phase09 key requires matching owner consent' USING ERRCODE = '23514';
  END IF;
  RETURN NULL;
END $$;

-- Every subject revision requires an immutable, same-scope audit event at commit.
CREATE FUNCTION phase09_require_audit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE subject uuid; subject_revision bigint; kind smallint;
BEGIN
  kind := TG_ARGV[0]::smallint;
  IF TG_TABLE_NAME = 'signing_key_selections' THEN
    subject := NEW.fiscal_memory_id;
  ELSE
    subject := NEW.id;
  END IF;
  IF TG_TABLE_NAME = 'signing_consents' THEN
    subject_revision := 1;
  ELSE
    subject_revision := NEW.revision;
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM signing_audit_events e
    WHERE e.subject_kind = kind AND e.subject_id = subject AND e.revision = subject_revision
      AND e.customer_user_id = NEW.customer_user_id
      AND e.taxpayer_id = NEW.taxpayer_id AND e.fiscal_memory_id = NEW.fiscal_memory_id
  ) THEN
    RAISE EXCEPTION 'Phase09 mutation requires atomic audit' USING ERRCODE = '23514';
  END IF;
  RETURN NULL;
END $$;

CREATE TRIGGER trg_p09_signing_operations_retain BEFORE DELETE ON signing_operations FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_operations_truncate BEFORE TRUNCATE ON signing_operations FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_operations_revision BEFORE UPDATE ON signing_operations FOR EACH ROW EXECUTE FUNCTION phase09_mutation_guard();
CREATE CONSTRAINT TRIGGER ct_p09_signing_operations_audit AFTER INSERT OR UPDATE ON signing_operations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_audit('1');

CREATE TRIGGER trg_p09_signing_consents_retain BEFORE DELETE ON signing_consents FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_consents_truncate BEFORE TRUNCATE ON signing_consents FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_consents_immutable BEFORE UPDATE ON signing_consents FOR EACH ROW EXECUTE FUNCTION phase09_evidence_immutable();
CREATE CONSTRAINT TRIGGER ct_p09_signing_consents_audit AFTER INSERT OR UPDATE ON signing_consents DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_audit('5');

CREATE TRIGGER trg_p09_signing_key_versions_retain BEFORE DELETE ON signing_key_versions FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_key_versions_truncate BEFORE TRUNCATE ON signing_key_versions FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_key_versions_revision BEFORE UPDATE ON signing_key_versions FOR EACH ROW EXECUTE FUNCTION phase09_mutation_guard();
CREATE CONSTRAINT TRIGGER ct_p09_signing_key_versions_audit AFTER INSERT OR UPDATE ON signing_key_versions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_audit('2');

CREATE TRIGGER trg_p09_signing_key_selections_retain BEFORE DELETE ON signing_key_selections FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_key_selections_truncate BEFORE TRUNCATE ON signing_key_selections FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_key_selections_revision BEFORE UPDATE ON signing_key_selections FOR EACH ROW EXECUTE FUNCTION phase09_mutation_guard();
CREATE CONSTRAINT TRIGGER ct_p09_signing_key_selections_audit AFTER INSERT OR UPDATE ON signing_key_selections DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_audit('3');

CREATE TRIGGER trg_p09_signing_change_requests_retain BEFORE DELETE ON signing_change_requests FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_change_requests_truncate BEFORE TRUNCATE ON signing_change_requests FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_change_requests_revision BEFORE UPDATE ON signing_change_requests FOR EACH ROW EXECUTE FUNCTION phase09_mutation_guard();
CREATE CONSTRAINT TRIGGER ct_p09_signing_change_requests_audit AFTER INSERT OR UPDATE ON signing_change_requests DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_audit('4');

CREATE TRIGGER trg_p09_signing_audit_events_retain BEFORE DELETE ON signing_audit_events FOR EACH ROW EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_audit_events_truncate BEFORE TRUNCATE ON signing_audit_events FOR EACH STATEMENT EXECUTE FUNCTION phase09_no_delete();
CREATE TRIGGER trg_p09_signing_audit_events_immutable BEFORE UPDATE ON signing_audit_events FOR EACH ROW EXECUTE FUNCTION phase09_evidence_immutable();

CREATE TRIGGER trg_p09_selection_lock BEFORE INSERT OR UPDATE ON signing_key_selections
 FOR EACH ROW EXECUTE FUNCTION phase09_selection_lock();
CREATE CONSTRAINT TRIGGER ct_p09_selection_ready AFTER INSERT OR UPDATE ON signing_key_selections
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_selection_ready();
CREATE CONSTRAINT TRIGGER ct_p09_key_selection_ready AFTER INSERT OR UPDATE ON signing_key_versions
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_selection_ready();
CREATE CONSTRAINT TRIGGER ct_p09_key_consent AFTER INSERT OR UPDATE ON signing_key_versions
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION phase09_require_consent();

""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Phase09 downgrade requires argintaxpayer_test';
 END IF;
 IF EXISTS(SELECT 1 FROM signing_audit_events)
 OR EXISTS(SELECT 1 FROM signing_change_requests)
 OR EXISTS(SELECT 1 FROM signing_key_selections)
 OR EXISTS(SELECT 1 FROM signing_key_versions)
 OR EXISTS(SELECT 1 FROM signing_consents)
 OR EXISTS(SELECT 1 FROM signing_operations) THEN
   RAISE EXCEPTION 'Nonempty Phase09 history; use reviewed forward repair or restore';
 END IF;
END $$;
DROP TABLE signing_audit_events;
DROP TABLE signing_change_requests;
DROP TABLE signing_key_selections;
DROP TABLE signing_key_versions;
DROP TABLE signing_consents;
DROP TABLE signing_operations;
DROP FUNCTION phase09_require_audit();
DROP FUNCTION phase09_require_consent();
DROP FUNCTION phase09_selection_ready();
DROP FUNCTION phase09_selection_lock();
DROP FUNCTION phase09_mutation_guard();
DROP FUNCTION phase09_evidence_immutable();
DROP FUNCTION phase09_no_delete();
DROP INDEX ux_phase09_memory_scope;
DROP INDEX ux_phase09_taxpayer_scope;

""");
    }
}
