using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261018090000_ScopeFiscalParticipantsPerCustomer")]
public sealed class ScopeFiscalParticipantsPerCustomer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing party IDs, links, invoices and details are never rewritten.
        // A legacy party with one active owner can be assigned safely;
        // shared and unlinked legacy parties remain unassigned and keep their links.
        migrationBuilder.Sql("""
ALTER TABLE parties ADD COLUMN owner_customer_user_id uuid NULL;
ALTER TABLE parties ADD CONSTRAINT fk_parties_owner_customer
  FOREIGN KEY(owner_customer_user_id) REFERENCES identity_users(id) ON DELETE RESTRICT;

UPDATE parties p SET owner_customer_user_id = owners.customer_user_id
FROM (
  SELECT party_id, min(customer_user_id::text)::uuid AS customer_user_id
  FROM customer_party_links WHERE unlinked_at_utc IS NULL
  GROUP BY party_id HAVING count(DISTINCT customer_user_id) = 1
) owners
WHERE p.id = owners.party_id;

DROP INDEX ux_parties_kind_identifier;
CREATE UNIQUE INDEX ux_parties_owner_kind_identifier
  ON parties(owner_customer_user_id, kind, normalized_identifier)
  WHERE owner_customer_user_id IS NOT NULL;

DROP INDEX ux_taxpayer_profiles_kind_identifier;
CREATE UNIQUE INDEX ux_taxpayer_profiles_customer_kind_identifier
  ON taxpayer_profiles(customer_user_id, kind, normalized_identifier)
  WHERE customer_user_id IS NOT NULL;
CREATE UNIQUE INDEX ux_taxpayer_profiles_legacy_identifier
  ON taxpayer_profiles(kind, normalized_identifier)
  WHERE customer_user_id IS NULL;

-- New explicitly owned parties must only be linked to their owning customer.
-- Historical shared-party links (owner null) remain untouched, read-only
-- across customers in existing application update services.
CREATE FUNCTION phase07_party_owner_link_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE party_owner uuid;
BEGIN
  SELECT owner_customer_user_id INTO party_owner FROM parties WHERE id = NEW.party_id;
  IF party_owner IS NOT NULL AND NEW.customer_user_id <> party_owner THEN
    RAISE EXCEPTION 'Cross-customer party reference denied'
      USING ERRCODE = '23503';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_phase07_party_owner_link_guard
  BEFORE INSERT OR UPDATE OF party_id, customer_user_id ON customer_party_links
  FOR EACH ROW EXECUTE FUNCTION phase07_party_owner_link_guard();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reinstating global indexes is impossible once separate customers
        // legitimately own the same identifier; abort instead of data loss.
        migrationBuilder.Sql("""
DO $$
BEGIN
 IF EXISTS(
   SELECT 1 FROM parties
   GROUP BY kind, normalized_identifier HAVING count(*) > 1
 ) OR EXISTS(
   SELECT 1 FROM taxpayer_profiles
   GROUP BY kind, normalized_identifier HAVING count(*) > 1
 ) THEN
   RAISE EXCEPTION 'Cross-customer fiscal identities exist; unsafe downgrade refused';
 END IF;
END $$;
DROP TRIGGER trg_phase07_party_owner_link_guard ON customer_party_links;
DROP FUNCTION phase07_party_owner_link_guard();
DROP INDEX ux_parties_owner_kind_identifier;
CREATE UNIQUE INDEX ux_parties_kind_identifier ON parties(kind, normalized_identifier);
DROP INDEX ux_taxpayer_profiles_customer_kind_identifier;
DROP INDEX ux_taxpayer_profiles_legacy_identifier;
CREATE UNIQUE INDEX ux_taxpayer_profiles_kind_identifier ON taxpayer_profiles(kind, normalized_identifier);
ALTER TABLE parties DROP CONSTRAINT fk_parties_owner_customer;
ALTER TABLE parties DROP COLUMN owner_customer_user_id;
""");
    }
}
