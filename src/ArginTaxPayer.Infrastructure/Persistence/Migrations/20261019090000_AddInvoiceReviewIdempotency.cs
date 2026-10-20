using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261019090000_AddInvoiceReviewIdempotency")]
public sealed class AddInvoiceReviewIdempotency : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE invoice_review_requests (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 invoice_id uuid NOT NULL,
 actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 idempotency_key varchar(128) NOT NULL,
 fingerprint_sha256 varchar(64) NOT NULL,
 result_review_revision bigint NOT NULL,
 result_state integer NOT NULL,
 completed_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_invoice_review_requests_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_reviews(customer_user_id,taxpayer_id,invoice_id) ON DELETE RESTRICT,
 CONSTRAINT ck_invoice_review_requests_revision CHECK(result_review_revision > 1),
 CONSTRAINT ck_invoice_review_requests_state CHECK(result_state IN (1,2,3,4)),
 CONSTRAINT ck_invoice_review_requests_fingerprint CHECK(fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
 CONSTRAINT ck_invoice_review_requests_key CHECK(length(trim(idempotency_key)) > 0)
);
CREATE UNIQUE INDEX ux_invoice_review_requests_idempotency
 ON invoice_review_requests(customer_user_id,taxpayer_id,actor_user_id,idempotency_key);
CREATE FUNCTION phase08_invoice_review_request_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Committed review idempotency results are immutable';
END $$;
CREATE TRIGGER trg_phase08_invoice_review_request_guard BEFORE UPDATE OR DELETE
 ON invoice_review_requests FOR EACH ROW EXECUTE FUNCTION phase08_invoice_review_request_guard();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM invoice_review_requests) THEN
  RAISE EXCEPTION 'Review idempotency records exist; destructive rollback prohibited';
 END IF;
END $$;
DROP TRIGGER trg_phase08_invoice_review_request_guard ON invoice_review_requests;
DROP FUNCTION phase08_invoice_review_request_guard();
DROP TABLE invoice_review_requests;
""");
    }
}
