-- Phase10 Step29: READ-ONLY TEST DATABASE quality gate.
-- Execute through psql -X -v ON_ERROR_STOP=1 -At -f ... against argintaxpayer_test.
BEGIN READ ONLY;
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'REFUSED: not the test database';
 END IF;
END $$;
DO $$
DECLARE bad bigint;
BEGIN
 SELECT count(*) INTO bad
 FROM sandbox_invoice_legal_relations rel
 LEFT JOIN invoice_documents predecessor
  ON predecessor.id=rel.predecessor_invoice_id
  AND predecessor.customer_user_id=rel.customer_user_id
  AND predecessor.taxpayer_id=rel.taxpayer_id
 LEFT JOIN invoice_documents successor
  ON successor.id=rel.successor_invoice_id
  AND successor.customer_user_id=rel.customer_user_id
  AND successor.taxpayer_id=rel.taxpayer_id
 WHERE predecessor.id IS NULL OR successor.id IS NULL;
 IF bad > 0 THEN RAISE EXCEPTION '% invalid owner/tenant relations',bad; END IF;

 SELECT count(*) INTO bad FROM (
  SELECT customer_user_id,taxpayer_id,predecessor_invoice_id
  FROM sandbox_invoice_legal_relations
  WHERE subject=3 AND state IN (1,2,4)
  GROUP BY 1,2,3 HAVING count(*)>1
 ) x;
 IF bad > 0 THEN RAISE EXCEPTION '% duplicate active cancellation references',bad; END IF;

 SELECT count(*) INTO bad FROM sandbox_invoice_legal_relations rel
 JOIN invoice_documents successor ON successor.id=rel.successor_invoice_id
  AND successor.customer_user_id=rel.customer_user_id
  AND successor.taxpayer_id=rel.taxpayer_id
 WHERE rel.subject<>successor.subject;
 IF bad>0 THEN RAISE EXCEPTION '% mismatched legal subject values',bad; END IF;

 -- Pending and indeterminate returns reserve quantities; terminal FAILED is excluded.
 WITH originals AS (
   SELECT rel.customer_user_id,rel.taxpayer_id,rel.predecessor_invoice_id,
    entries.ordinality AS item_no,
    (entries.value->>'Quantity')::numeric AS available_quantity
   FROM sandbox_invoice_legal_relations rel
   JOIN invoice_documents source ON source.id=rel.predecessor_invoice_id
     AND source.taxpayer_id=rel.taxpayer_id
     AND source.customer_user_id=rel.customer_user_id
   CROSS JOIN LATERAL jsonb_array_elements(source.snapshot_json::jsonb->'lines')
     WITH ORDINALITY entries(value,ordinality)
   WHERE rel.subject=4 AND rel.state IN (1,2,4)
   GROUP BY rel.customer_user_id,rel.taxpayer_id,rel.predecessor_invoice_id,
     entries.ordinality,entries.value
 ), returned AS (
   SELECT rel.customer_user_id,rel.taxpayer_id,rel.predecessor_invoice_id,
    entries.ordinality AS item_no,
    SUM((entries.value->>'Quantity')::numeric) AS returned_quantity
   FROM sandbox_invoice_legal_relations rel
   JOIN invoice_documents successor ON successor.id=rel.successor_invoice_id
     AND successor.taxpayer_id=rel.taxpayer_id
     AND successor.customer_user_id=rel.customer_user_id
   CROSS JOIN LATERAL jsonb_array_elements(successor.snapshot_json::jsonb->'lines')
     WITH ORDINALITY entries(value,ordinality)
   WHERE rel.subject=4 AND rel.state IN (1,2,4)
   GROUP BY rel.customer_user_id,rel.taxpayer_id,rel.predecessor_invoice_id,entries.ordinality
 )
 SELECT count(*) INTO bad FROM originals o JOIN returned r
 ON r.customer_user_id=o.customer_user_id AND r.taxpayer_id=o.taxpayer_id
 AND r.predecessor_invoice_id=o.predecessor_invoice_id AND r.item_no=o.item_no
 WHERE r.returned_quantity > o.available_quantity;
 IF bad>0 THEN RAISE EXCEPTION '% cumulative return quantities exceed original',bad; END IF;
END $$;
SELECT 'step29_readonly_db_audit_PASS', current_database();
ROLLBACK;
