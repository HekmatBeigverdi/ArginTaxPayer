using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016210000_AddReferenceActivationAudit")]
public sealed class AddReferenceActivationAudit : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Append-only operator decisions. No changes to prior catalog records.
        m.Sql("""
CREATE TABLE catalog_release_audit (
 id uuid PRIMARY KEY,
 release_id uuid NOT NULL REFERENCES catalog_reference_releases(id) ON DELETE RESTRICT,
 actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 action varchar(32) NOT NULL,
 from_state integer NOT NULL,
 to_state integer NOT NULL,
 evidence_reference varchar(1000) NOT NULL,
 reason varchar(1000) NOT NULL,
 occurred_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_catalog_release_audit_action CHECK(action IN ('validate','activate','supersede')),
 CONSTRAINT ck_catalog_release_audit_states CHECK(from_state BETWEEN 1 AND 4 AND to_state BETWEEN 1 AND 4),
 CONSTRAINT ck_catalog_release_audit_evidence CHECK(length(trim(evidence_reference)) >= 12),
 CONSTRAINT ck_catalog_release_audit_reason CHECK(length(trim(reason)) >= 12)
);
CREATE INDEX ix_catalog_release_audit_release_time
 ON catalog_release_audit(release_id, occurred_at_utc);

CREATE FUNCTION phase06_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Reference activation audit is append-only';
END $$;
CREATE TRIGGER trg_phase06_audit_immutable
BEFORE UPDATE OR DELETE ON catalog_release_audit
FOR EACH ROW EXECUTE FUNCTION phase06_audit_immutable();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Historical audit cannot be removed on rollback after it contains decisions.
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM catalog_release_audit) THEN
   RAISE EXCEPTION 'Cannot remove nonempty reference activation audit';
 END IF;
END $$;
DROP TRIGGER trg_phase06_audit_immutable ON catalog_release_audit;
DROP FUNCTION phase06_audit_immutable();
DROP TABLE catalog_release_audit;
""");
    }
}
