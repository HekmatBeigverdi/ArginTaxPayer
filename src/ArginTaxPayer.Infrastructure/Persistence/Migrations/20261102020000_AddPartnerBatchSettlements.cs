using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261102020000_AddPartnerBatchSettlements")]
public sealed class AddPartnerBatchSettlements : Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
 CREATE TABLE partner_settlement_batches (
 id uuid PRIMARY KEY, root_user_id uuid NOT NULL REFERENCES identity_users(id),
 partner_user_id uuid NOT NULL REFERENCES identity_users(id), operation_key uuid NOT NULL,
 request_hash text NOT NULL, created_at_utc timestamptz NOT NULL, snapshot jsonb NOT NULL,
 UNIQUE(root_user_id,operation_key), CHECK(root_user_id<>partner_user_id));
 CREATE INDEX ix_partner_batch_scope ON partner_settlement_batches(root_user_id,partner_user_id,created_at_utc DESC,id);
 CREATE TABLE partner_settlement_batch_lines (
 id uuid PRIMARY KEY, batch_id uuid NOT NULL REFERENCES partner_settlement_batches(id),
 collection_id uuid NOT NULL REFERENCES commercial_collections(id),
 root_user_id uuid NOT NULL REFERENCES identity_users(id),partner_user_id uuid NOT NULL REFERENCES identity_users(id),
 partner_basis_points integer NOT NULL CHECK(partner_basis_points BETWEEN 0 AND 10000),
 gross_irr bigint NOT NULL CHECK(gross_irr>0),cost_irr bigint NOT NULL CHECK(cost_irr>=0 AND cost_irr<=gross_irr),
 root_paid_cost_irr bigint NOT NULL CHECK(root_paid_cost_irr>=0 AND root_paid_cost_irr<=cost_irr),
 partner_share_irr bigint NOT NULL CHECK(partner_share_irr>=0), root_share_irr bigint NOT NULL CHECK(root_share_irr>=0),
 previous_gross_irr bigint NOT NULL,previous_cost_irr bigint NOT NULL,previous_root_paid_cost_irr bigint NOT NULL,
 previous_partner_share_irr bigint NOT NULL,previous_root_share_irr bigint NOT NULL,previous_net_to_root_irr bigint NOT NULL,
 net_to_root_irr bigint NOT NULL, created_at_utc timestamptz NOT NULL,snapshot jsonb NOT NULL,
 UNIQUE(collection_id,cost_irr,root_paid_cost_irr),UNIQUE(batch_id,collection_id),
 CHECK(gross_irr=cost_irr+partner_share_irr+root_share_irr),CHECK(net_to_root_irr=root_share_irr+root_paid_cost_irr));
 CREATE INDEX ix_partner_batch_lines_receipt ON partner_settlement_batch_lines(collection_id,created_at_utc DESC,id);
 CREATE INDEX ix_partner_batch_lines_batch ON partner_settlement_batch_lines(batch_id,collection_id);
 CREATE TABLE partner_settlement_expenses (
 id uuid PRIMARY KEY, collection_id uuid NOT NULL REFERENCES commercial_collections(id),
 root_user_id uuid NOT NULL REFERENCES identity_users(id),partner_user_id uuid NOT NULL REFERENCES identity_users(id),
 amount_irr bigint NOT NULL CHECK(amount_irr>0), paid_by_level1 boolean NOT NULL,
 reference varchar(160) NOT NULL CHECK(length(trim(reference))>0),note varchar(1000) NOT NULL,
 operation_key uuid NOT NULL,request_hash text NOT NULL,approved_at_utc timestamptz NOT NULL,
 UNIQUE(root_user_id,operation_key));
 CREATE INDEX ix_partner_expense_receipt ON partner_settlement_expenses(collection_id);
 CREATE FUNCTION partner_batch_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
 BEGIN RAISE EXCEPTION 'Partner batch settlement evidence is immutable'; END $fn$;
 CREATE TRIGGER trg_partner_batches_immutable BEFORE UPDATE OR DELETE ON partner_settlement_batches FOR EACH ROW EXECUTE FUNCTION partner_batch_immutable();
 CREATE TRIGGER trg_partner_batch_lines_immutable BEFORE UPDATE OR DELETE ON partner_settlement_batch_lines FOR EACH ROW EXECUTE FUNCTION partner_batch_immutable();
 CREATE TRIGGER trg_partner_expenses_immutable BEFORE UPDATE OR DELETE ON partner_settlement_expenses FOR EACH ROW EXECUTE FUNCTION partner_batch_immutable();
 """);
 protected override void Down(MigrationBuilder m)=>m.Sql("""
 DROP TABLE partner_settlement_expenses;
 DROP TABLE partner_settlement_batch_lines;
 DROP TABLE partner_settlement_batches;
 DROP FUNCTION partner_batch_immutable();
 """);
}
