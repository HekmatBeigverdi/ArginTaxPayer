using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261019080000_AddInvoiceReviewWorkflowPersistence")]
public sealed class AddInvoiceReviewWorkflowPersistence : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE invoice_reviews (
 invoice_id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 state integer NOT NULL DEFAULT 1,
 revision bigint NOT NULL DEFAULT 1,
 updated_at_utc timestamptz NOT NULL,
 last_actor_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 CONSTRAINT fk_invoice_reviews_invoice FOREIGN KEY (customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_invoice_reviews_state CHECK(state IN (1,2,3,4)),
 CONSTRAINT ck_invoice_reviews_revision CHECK(revision > 0)
);
CREATE UNIQUE INDEX ux_invoice_reviews_scope ON invoice_reviews(customer_user_id,taxpayer_id,invoice_id);
CREATE INDEX ix_invoice_reviews_scope_state ON invoice_reviews(customer_user_id,taxpayer_id,state);

CREATE TABLE invoice_review_events (
 id uuid PRIMARY KEY,
 invoice_id uuid NOT NULL,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 revision bigint NOT NULL,
 from_state integer NOT NULL,
 to_state integer NOT NULL,
 action integer NOT NULL,
 actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 occurred_at_utc timestamptz NOT NULL,
 change_reason varchar(1000) NULL,
 CONSTRAINT fk_invoice_review_events_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_reviews(customer_user_id,taxpayer_id,invoice_id) ON DELETE RESTRICT,
 CONSTRAINT ck_invoice_review_event_revision CHECK (revision > 1),
 CONSTRAINT ck_invoice_review_event_states CHECK (from_state IN (1,2,3,4) AND to_state IN (1,2,3,4)),
 CONSTRAINT ck_invoice_review_event_transition CHECK (
   (from_state IN (1,3) AND to_state=2 AND action=1 AND change_reason IS NULL)
   OR (from_state=2 AND to_state=3 AND action=2 AND length(trim(change_reason))>0)
   OR (from_state=2 AND to_state=4 AND action=3 AND change_reason IS NULL))
);
CREATE UNIQUE INDEX ux_invoice_review_events_revision ON invoice_review_events(invoice_id,revision);
CREATE INDEX ix_invoice_review_events_scope_time
 ON invoice_review_events(customer_user_id,taxpayer_id,invoice_id,occurred_at_utc);
CREATE FUNCTION phase08_invoice_review_event_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Committed invoice review events are immutable';
END $$;
CREATE TRIGGER trg_phase08_invoice_review_event_guard
 BEFORE UPDATE OR DELETE ON invoice_review_events
 FOR EACH ROW EXECUTE FUNCTION phase08_invoice_review_event_guard();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM invoice_review_events) OR EXISTS (SELECT 1 FROM invoice_reviews) THEN
  RAISE EXCEPTION 'Review records exist: destructive rollback prohibited';
 END IF;
END $$;
DROP TRIGGER trg_phase08_invoice_review_event_guard ON invoice_review_events;
DROP FUNCTION phase08_invoice_review_event_guard();
DROP TABLE invoice_review_events;
DROP TABLE invoice_reviews;
""");
    }
}
