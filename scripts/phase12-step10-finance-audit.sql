-- Read-only integration audit. Refuses any database other than argintaxpayer_test.
BEGIN READ ONLY;
DO $audit$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Test database only';
 END IF;
 IF EXISTS(
   SELECT 1 FROM commercial_order_activations a
   JOIN commercial_orders o ON o.id=a.order_id
   JOIN customer_subscriptions s ON s.id=a.subscription_id
   WHERE o.status<>3 OR o.gross_amount_irr<>o.collected_amount_irr
     OR o.gross_amount_irr<=0 OR o.customer_user_id<>s.customer_user_id
     OR o.price_version_id<>s.price_version_id
 ) THEN RAISE EXCEPTION 'Activation references unpaid or mismatched commercial order'; END IF;
 IF EXISTS(
   SELECT 1 FROM sms_wallet_accounts
   WHERE spent_units>granted_units OR spent_units<0
 ) THEN RAISE EXCEPTION 'SMS wallet imbalance'; END IF;
 IF EXISTS(
   SELECT 1 FROM subscription_credit_accounts a
   JOIN commercial_order_activations ca ON ca.subscription_id=a.subscription_id
   JOIN customer_subscriptions s ON s.id=ca.subscription_id
   WHERE s.kind=2 AND (a.granted_units<>s.invoice_unit_limit
      OR a.available_units+a.reserved_units+a.consumed_units<>a.granted_units)
 ) THEN RAISE EXCEPTION 'Quantity subscription credit inconsistency'; END IF;
 RAISE NOTICE 'PASS: Phase12 Step10 finance activation and SMS wallet integrity';
END $audit$;
ROLLBACK;
