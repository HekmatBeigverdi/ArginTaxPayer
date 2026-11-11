using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261112110000_RestoreReviewedTimedMemoryChanges")]
public sealed class RestoreReviewedTimedMemoryChanges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF current_database()<>'argintaxpayer_test' AND NOT (
   current_database()='argin_phase18_acceptance' AND current_user='phase18_acceptance' AND
   EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl)) AND NOT (
   current_database()='argintaxpayer_prod' AND current_user='argin_prod_migrator' AND
   EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl)) THEN
   RAISE EXCEPTION 'Reviewed fiscal binding migration restricted to approved databases';
 END IF;
END $$;
ALTER TABLE subscription_binding_exceptions ADD COLUMN IF NOT EXISTS current_memory_id uuid NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT;
ALTER TABLE subscription_binding_exceptions ADD COLUMN IF NOT EXISTS decision_reason varchar(1000) NULL;
ALTER TABLE subscription_binding_exceptions DROP CONSTRAINT IF EXISTS ck_subscription_exception_status;
ALTER TABLE subscription_binding_exceptions ADD CONSTRAINT ck_subscription_exception_status CHECK(status IN (1,2,3,4));
DROP TRIGGER IF EXISTS trg_phase12_binding_integrity ON subscription_annual_bindings;
CREATE OR REPLACE FUNCTION phase12_binding_integrity() RETURNS trigger LANGUAGE plpgsql AS $fn$
DECLARE request_id text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Single-taxpayer binding deletion is prohibited'; END IF;
 IF TG_OP='INSERT' THEN
   IF NOT EXISTS (
     SELECT 1 FROM customer_subscriptions s JOIN taxpayer_profiles t
       ON t.id=NEW.taxpayer_id AND t.customer_user_id=s.customer_user_id
     JOIN fiscal_memories m ON m.id=NEW.fiscal_memory_id AND m.taxpayer_id=t.id
     WHERE s.id=NEW.subscription_id AND s.customer_user_id=NEW.customer_user_id
       AND s.kind IN (1,3,4,5) AND s.state=2
   ) THEN RAISE EXCEPTION 'Binding scope, plan or active subscription invalid'; END IF;
   RETURN NEW;
 END IF;
 IF NEW.subscription_id<>OLD.subscription_id OR NEW.customer_user_id<>OLD.customer_user_id OR NEW.taxpayer_id<>OLD.taxpayer_id THEN
   RAISE EXCEPTION 'Approved memory changes cannot change customer or taxpayer';
 END IF;
 IF NEW.fiscal_memory_id=OLD.fiscal_memory_id THEN RETURN NEW; END IF;
 request_id := current_setting('argin.binding_memory_change_request_id',true);
 IF request_id IS NULL OR request_id='' OR NOT EXISTS (
   SELECT 1 FROM subscription_binding_exceptions e
   JOIN fiscal_memories m ON m.id=e.proposed_memory_id AND m.taxpayer_id=NEW.taxpayer_id
   JOIN customer_subscriptions s ON s.id=e.subscription_id AND s.customer_user_id=e.customer_user_id
   WHERE e.id=request_id::uuid AND e.subscription_id=NEW.subscription_id
     AND e.customer_user_id=NEW.customer_user_id AND e.proposed_taxpayer_id=NEW.taxpayer_id
     AND e.current_memory_id=OLD.fiscal_memory_id AND e.proposed_memory_id=NEW.fiscal_memory_id
     AND e.status=2 AND e.reviewer_user_id IS NOT NULL
     AND m.authority_environment=1 AND m.status=2
     AND s.kind IN (1,3,4,5) AND s.state=2 AND s.expires_at_utc>now()
 ) THEN RAISE EXCEPTION 'Production memory rebind requires exact Level 1 approval'; END IF;
 RETURN NEW;
END
$fn$;
CREATE TRIGGER trg_phase12_binding_integrity BEFORE INSERT OR UPDATE OR DELETE ON subscription_annual_bindings
 FOR EACH ROW EXECUTE FUNCTION phase12_binding_integrity();
""");

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Reviewed memory rebinding history and trigger protections require a forward-only rollback review.");
}
