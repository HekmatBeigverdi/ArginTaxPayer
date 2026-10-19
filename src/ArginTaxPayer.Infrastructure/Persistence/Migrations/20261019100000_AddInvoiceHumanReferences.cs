using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261019100000_AddInvoiceHumanReferences")]
public sealed class AddInvoiceHumanReferences : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE SEQUENCE invoice_admin_code_ordinal_seq AS integer MINVALUE 1 MAXVALUE 17576 NO CYCLE;
CREATE TABLE invoice_admin_codes (
 admin_user_id uuid PRIMARY KEY REFERENCES identity_users(id) ON DELETE RESTRICT,
 ordinal integer NOT NULL UNIQUE DEFAULT nextval('invoice_admin_code_ordinal_seq'),
 code varchar(3) GENERATED ALWAYS AS (
     chr(65 + ((ordinal - 1) / 676)) ||
     chr(65 + (((ordinal - 1) / 26) % 26)) ||
     chr(65 + ((ordinal - 1) % 26))
 ) STORED,
 CONSTRAINT ck_invoice_admin_ordinal CHECK (ordinal BETWEEN 1 AND 17576),
 CONSTRAINT uq_invoice_admin_code UNIQUE (code)
);
CREATE TABLE invoice_support_counters (
 admin_user_id uuid NOT NULL REFERENCES invoice_admin_codes(admin_user_id) ON DELETE RESTRICT,
 persian_year smallint NOT NULL,
 last_serial bigint NOT NULL,
 PRIMARY KEY(admin_user_id,persian_year),
 CONSTRAINT ck_invoice_support_serial CHECK(last_serial >= 1)
);
CREATE TABLE invoice_support_references (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 source_kind varchar(12) NOT NULL CHECK (source_kind IN ('document','workspace')),
 source_id uuid NOT NULL,
 admin_user_id uuid NOT NULL REFERENCES invoice_admin_codes(admin_user_id) ON DELETE RESTRICT,
 persian_year smallint NOT NULL,
 serial bigint NOT NULL CHECK (serial >= 1),
 system_code varchar(40) NOT NULL,
 assigned_at_utc timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT uq_invoice_support_source UNIQUE(source_kind,source_id),
 CONSTRAINT uq_invoice_support_system_code UNIQUE(system_code),
 CONSTRAINT uq_invoice_support_serial UNIQUE(admin_user_id,persian_year,serial),
 CONSTRAINT ck_invoice_support_format CHECK(system_code ~ '^[0-9]{5,}-[0-9]{2}-[A-Z]{3}
);

CREATE FUNCTION phase08_persian_year_for_issue(date_value timestamptz)
RETURNS integer LANGUAGE plpgsql STABLE AS $$
DECLARE
 local_date date := (date_value AT TIME ZONE 'Asia/Tehran')::date;
 year_number integer := EXTRACT(YEAR FROM local_date)::integer;
 -- Iranian year boundaries: supported fixed calendar dates, no approximate rollover.
 boundary date;
BEGIN
 boundary := CASE year_number
   WHEN 2024 THEN DATE '2024-03-20'
   WHEN 2025 THEN DATE '2025-03-21'
   WHEN 2026 THEN DATE '2026-03-21'
   WHEN 2027 THEN DATE '2027-03-21'
   WHEN 2028 THEN DATE '2028-03-20'
   WHEN 2029 THEN DATE '2029-03-21'
   WHEN 2030 THEN DATE '2030-03-21'
   WHEN 2031 THEN DATE '2031-03-21'
   WHEN 2032 THEN DATE '2032-03-20'
   WHEN 2033 THEN DATE '2033-03-20'
   WHEN 2034 THEN DATE '2034-03-21'
   WHEN 2035 THEN DATE '2035-03-21'
   WHEN 2036 THEN DATE '2036-03-20'
   WHEN 2037 THEN DATE '2037-03-21'
   WHEN 2038 THEN DATE '2038-03-21'
   WHEN 2039 THEN DATE '2039-03-21'
   WHEN 2040 THEN DATE '2040-03-20'
   ELSE NULL
 END;
 IF boundary IS NULL THEN
   RAISE EXCEPTION 'Unsupported Persian calendar year: review calendar schedule before extension';
 END IF;
 RETURN year_number - 621 - CASE WHEN local_date < boundary THEN 1 ELSE 0 END;
END $$;

CREATE FUNCTION phase08_allocate_invoice_support(kind varchar, item_id uuid, customer_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
 owner_id uuid;
 admin_code varchar(3);
 ordinal_value integer;
 year_value integer;
 serial_value bigint;
 full_code text;
BEGIN
 IF EXISTS (SELECT 1 FROM invoice_support_references
     WHERE source_kind = kind AND source_id = item_id) THEN RETURN; END IF;
 -- Lock the owner row: partner transfer and invoice allocation must serialize.
 SELECT coalesce(partner_level2_user_id,root_level1_user_id)
   INTO owner_id FROM customer_commercial_owners
   WHERE customer_user_id = customer_id FOR SHARE;
 IF owner_id IS NULL THEN
   RAISE EXCEPTION 'Missing customer ownership for system code allocation';
 END IF;
 INSERT INTO invoice_admin_codes(admin_user_id) VALUES(owner_id)
   ON CONFLICT(admin_user_id) DO NOTHING;
 SELECT code INTO admin_code FROM invoice_admin_codes WHERE admin_user_id = owner_id;
 year_value := phase08_persian_year_for_issue(now());
 INSERT INTO invoice_support_counters(admin_user_id,persian_year,last_serial)
   VALUES(owner_id,year_value,1)
   ON CONFLICT(admin_user_id,persian_year)
   DO UPDATE SET last_serial = invoice_support_counters.last_serial + 1
     WHERE invoice_support_counters.last_serial < 9223372036854775807
   RETURNING last_serial INTO serial_value;
 IF serial_value IS NULL THEN
   RAISE EXCEPTION 'Invoice support sequence exhausted';
 END IF;
 full_code := lpad(serial_value::text,5,'0') || '-' ||
              lpad((year_value % 100)::text,2,'0') || '-' || admin_code;
 INSERT INTO invoice_support_references(
   source_kind,source_id,admin_user_id,persian_year,serial,system_code)
 VALUES(kind,item_id,owner_id,year_value,serial_value,full_code);
END $$;

CREATE FUNCTION phase08_invoice_support_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM phase08_allocate_invoice_support(TG_ARGV[0]::varchar,NEW.id,NEW.customer_user_id);
 RETURN NEW;
END $$;
CREATE TRIGGER trg_phase08_invoice_workspace_system_code
 AFTER INSERT ON invoice_entry_workspaces FOR EACH ROW
 EXECUTE FUNCTION phase08_invoice_support_insert('workspace');
CREATE TRIGGER trg_phase08_invoice_document_system_code
 AFTER INSERT ON invoice_documents FOR EACH ROW
 EXECUTE FUNCTION phase08_invoice_support_insert('document');

-- Existing records receive codes at migration time according to CURRENT commercial owner.
-- No historical owner-at-creation assumption is fabricated.
DO $$
DECLARE item record;
BEGIN
 FOR item IN
   SELECT 'workspace'::varchar AS kind,id,customer_user_id,updated_at_utc AS at_utc
   FROM invoice_entry_workspaces
   UNION ALL
   SELECT 'document'::varchar,id,customer_user_id,updated_at_utc
   FROM invoice_documents
   ORDER BY at_utc,id
 LOOP
   PERFORM phase08_allocate_invoice_support(item.kind,item.id,item.customer_user_id);
 END LOOP;
END $$;

CREATE FUNCTION phase08_immutable_support_ref() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Assigned support references cannot be updated or deleted';
END $$;
CREATE TRIGGER trg_phase08_immutable_support_ref BEFORE UPDATE OR DELETE
 ON invoice_support_references FOR EACH ROW EXECUTE FUNCTION phase08_immutable_support_ref();
CREATE TRIGGER trg_phase08_immutable_admin_code BEFORE UPDATE OR DELETE
 ON invoice_admin_codes FOR EACH ROW EXECUTE FUNCTION phase08_immutable_support_ref();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_support_references) THEN
   RAISE EXCEPTION 'Assigned system codes exist; destructive downgrade refused';
 END IF;
END $$;
DROP TRIGGER trg_phase08_immutable_admin_code ON invoice_admin_codes;
DROP TRIGGER trg_phase08_immutable_support_ref ON invoice_support_references;
DROP FUNCTION phase08_immutable_support_ref();
DROP TRIGGER trg_phase08_invoice_document_system_code ON invoice_documents;
DROP TRIGGER trg_phase08_invoice_workspace_system_code ON invoice_entry_workspaces;
DROP FUNCTION phase08_invoice_support_insert();
DROP FUNCTION phase08_allocate_invoice_support(varchar,uuid,uuid);
DROP FUNCTION phase08_persian_year_for_issue(timestamptz);
DROP TABLE invoice_support_references;
DROP TABLE invoice_support_counters;
DROP TABLE invoice_admin_codes;
DROP SEQUENCE invoice_admin_code_ordinal_seq;
""");
    }
}
)
);

CREATE FUNCTION phase08_persian_year_for_issue(date_value timestamptz)
RETURNS integer LANGUAGE plpgsql STABLE AS $$
DECLARE
 local_date date := (date_value AT TIME ZONE 'Asia/Tehran')::date;
 year_number integer := EXTRACT(YEAR FROM local_date)::integer;
 -- Iranian year boundaries: supported fixed calendar dates, no approximate rollover.
 boundary date;
BEGIN
 boundary := CASE year_number
   WHEN 2024 THEN DATE '2024-03-20'
   WHEN 2025 THEN DATE '2025-03-21'
   WHEN 2026 THEN DATE '2026-03-21'
   WHEN 2027 THEN DATE '2027-03-21'
   WHEN 2028 THEN DATE '2028-03-20'
   WHEN 2029 THEN DATE '2029-03-21'
   WHEN 2030 THEN DATE '2030-03-21'
   WHEN 2031 THEN DATE '2031-03-21'
   WHEN 2032 THEN DATE '2032-03-20'
   WHEN 2033 THEN DATE '2033-03-20'
   WHEN 2034 THEN DATE '2034-03-21'
   WHEN 2035 THEN DATE '2035-03-21'
   WHEN 2036 THEN DATE '2036-03-20'
   WHEN 2037 THEN DATE '2037-03-21'
   WHEN 2038 THEN DATE '2038-03-21'
   WHEN 2039 THEN DATE '2039-03-21'
   WHEN 2040 THEN DATE '2040-03-20'
   ELSE NULL
 END;
 IF boundary IS NULL THEN
   RAISE EXCEPTION 'Unsupported Persian calendar year: review calendar schedule before extension';
 END IF;
 RETURN year_number - 621 - CASE WHEN local_date < boundary THEN 1 ELSE 0 END;
END $$;

CREATE FUNCTION phase08_allocate_invoice_support(kind varchar, item_id uuid, customer_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
 owner_id uuid;
 admin_code varchar(3);
 ordinal_value integer;
 year_value integer;
 serial_value bigint;
 full_code text;
BEGIN
 IF EXISTS (SELECT 1 FROM invoice_support_references
     WHERE source_kind = kind AND source_id = item_id) THEN RETURN; END IF;
 -- Lock the owner row: partner transfer and invoice allocation must serialize.
 SELECT coalesce(partner_level2_user_id,root_level1_user_id)
   INTO owner_id FROM customer_commercial_owners
   WHERE customer_user_id = customer_id FOR SHARE;
 IF owner_id IS NULL THEN
   RAISE EXCEPTION 'Missing customer ownership for system code allocation';
 END IF;
 INSERT INTO invoice_admin_codes(admin_user_id) VALUES(owner_id)
   ON CONFLICT(admin_user_id) DO NOTHING;
 SELECT code INTO admin_code FROM invoice_admin_codes WHERE admin_user_id = owner_id;
 year_value := phase08_persian_year_for_issue(now());
 INSERT INTO invoice_support_counters(admin_user_id,persian_year,last_serial)
   VALUES(owner_id,year_value,1)
   ON CONFLICT(admin_user_id,persian_year)
   DO UPDATE SET last_serial = invoice_support_counters.last_serial + 1
     WHERE invoice_support_counters.last_serial < 9223372036854775807
   RETURNING last_serial INTO serial_value;
 IF serial_value IS NULL THEN
   RAISE EXCEPTION 'Invoice support sequence exhausted';
 END IF;
 full_code := lpad(serial_value::text,5,'0') || '-' ||
              lpad((year_value % 100)::text,2,'0') || '-' || admin_code;
 INSERT INTO invoice_support_references(
   source_kind,source_id,admin_user_id,persian_year,serial,system_code)
 VALUES(kind,item_id,owner_id,year_value,serial_value,full_code);
END $$;

CREATE FUNCTION phase08_invoice_support_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM phase08_allocate_invoice_support(TG_ARGV[0]::varchar,NEW.id,NEW.customer_user_id);
 RETURN NEW;
END $$;
CREATE TRIGGER trg_phase08_invoice_workspace_system_code
 AFTER INSERT ON invoice_entry_workspaces FOR EACH ROW
 EXECUTE FUNCTION phase08_invoice_support_insert('workspace');
CREATE TRIGGER trg_phase08_invoice_document_system_code
 AFTER INSERT ON invoice_documents FOR EACH ROW
 EXECUTE FUNCTION phase08_invoice_support_insert('document');

-- Existing records receive codes at migration time according to CURRENT commercial owner.
-- No historical owner-at-creation assumption is fabricated.
DO $$
DECLARE item record;
BEGIN
 FOR item IN
   SELECT 'workspace'::varchar AS kind,id,customer_user_id,updated_at_utc AS at_utc
   FROM invoice_entry_workspaces
   UNION ALL
   SELECT 'document'::varchar,id,customer_user_id,updated_at_utc
   FROM invoice_documents
   ORDER BY at_utc,id
 LOOP
   PERFORM phase08_allocate_invoice_support(item.kind,item.id,item.customer_user_id);
 END LOOP;
END $$;

CREATE FUNCTION phase08_immutable_support_ref() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Assigned support references cannot be updated or deleted';
END $$;
CREATE TRIGGER trg_phase08_immutable_support_ref BEFORE UPDATE OR DELETE
 ON invoice_support_references FOR EACH ROW EXECUTE FUNCTION phase08_immutable_support_ref();
CREATE TRIGGER trg_phase08_immutable_admin_code BEFORE UPDATE OR DELETE
 ON invoice_admin_codes FOR EACH ROW EXECUTE FUNCTION phase08_immutable_support_ref();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_support_references) THEN
   RAISE EXCEPTION 'Assigned system codes exist; destructive downgrade refused';
 END IF;
END $$;
DROP TRIGGER trg_phase08_immutable_admin_code ON invoice_admin_codes;
DROP TRIGGER trg_phase08_immutable_support_ref ON invoice_support_references;
DROP FUNCTION phase08_immutable_support_ref();
DROP TRIGGER trg_phase08_invoice_document_system_code ON invoice_documents;
DROP TRIGGER trg_phase08_invoice_workspace_system_code ON invoice_entry_workspaces;
DROP FUNCTION phase08_invoice_support_insert();
DROP FUNCTION phase08_allocate_invoice_support(varchar,uuid,uuid);
DROP FUNCTION phase08_persian_year_for_issue(timestamptz);
DROP TABLE invoice_support_references;
DROP TABLE invoice_support_counters;
DROP TABLE invoice_admin_codes;
DROP SEQUENCE invoice_admin_code_ordinal_seq;
""");
    }
}
