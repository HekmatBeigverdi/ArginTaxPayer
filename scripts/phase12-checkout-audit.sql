-- Read-only, isolated test-DB audit. Apply reviewed migrations first.
BEGIN READ ONLY;
DO $check$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Refusing to audit a non-test database';
 END IF;
 IF EXISTS (
   SELECT 1 FROM commercial_checkout_order_options x
   JOIN commercial_orders o ON o.id=x.order_id
   WHERE x.total_amount_irr<>o.gross_amount_irr OR
     (NOT x.official_invoice_requested AND (x.vat_basis_points<>0 OR x.vat_amount_irr<>0)) OR
     x.vat_amount_irr<>CASE WHEN x.official_invoice_requested
       THEN (x.base_amount_irr*x.vat_basis_points+5000)/10000 ELSE 0 END
 ) THEN RAISE EXCEPTION 'Invalid frozen checkout financial totals'; END IF;
 IF EXISTS (
    SELECT 1 FROM commercial_checkout_order_options x
    JOIN commercial_orders o ON o.id=x.order_id
    WHERE x.payment_method NOT IN (1,2) OR o.gross_amount_irr<o.collected_amount_irr
 ) THEN RAISE EXCEPTION 'Invalid payment booking'; END IF;
 RAISE NOTICE 'PASS: Phase12 checkout immutable VAT audit';
END $check$;
ROLLBACK;
