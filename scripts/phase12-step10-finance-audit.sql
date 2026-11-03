-- Phase 12 Step 10: read-only audit of current approval-on-credit and quantity add-on model.
-- Check historical account balances against receipts; do not require payment in full
-- before an approved subscription can exist.
BEGIN READ ONLY;
DO $audit$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Run only against argintaxpayer_test';
 END IF;
 -- Each activation must point to the same customer and commercial owner as the
 -- associated subscription. Quantity add-ons can share a subscription and can
 -- change its displayed plan/price, so do not equate historical price versions.
 IF EXISTS (
   SELECT 1 FROM commercial_order_activations a
   JOIN commercial_orders o ON o.id=a.order_id
   JOIN customer_subscriptions s ON s.id=a.subscription_id
   WHERE o.status=4 OR o.gross_amount_irr<=0
     OR o.customer_user_id IS DISTINCT FROM s.customer_user_id
     OR o.root_admin_user_id IS DISTINCT FROM s.root_admin_user_id
     OR o.partner_user_id IS DISTINCT FROM s.partner_user_id
 ) THEN RAISE EXCEPTION 'Activation references voided order or mismatched owner'; END IF;

 -- The sum of independently recorded collections must match the order balance;
 -- an approved-but-unpaid or partially paid order is allowed.
 IF EXISTS (
   SELECT 1 FROM commercial_orders o
   LEFT JOIN LATERAL (
     SELECT COALESCE(SUM(c.amount_irr),0) AS verified_irr
     FROM commercial_collections c WHERE c.order_id=o.id
   ) c ON TRUE
   WHERE o.collected_amount_irr<>c.verified_irr
      OR o.collected_amount_irr<0
      OR o.collected_amount_irr>o.gross_amount_irr
      OR (o.status=3 AND o.collected_amount_irr<>o.gross_amount_irr)
 ) THEN RAISE EXCEPTION 'Order balance differs from verified collections'; END IF;

 -- Every activation must have an immutable expiry snapshot and cannot also be
 -- rejected. A reused quantity subscription is permitted.
 IF EXISTS (
   SELECT 1 FROM commercial_order_activations a
   LEFT JOIN commercial_order_activation_expiries e ON e.order_id=a.order_id
   LEFT JOIN commercial_order_rejections r ON r.order_id=a.order_id
   WHERE e.order_id IS NULL OR e.expires_at_utc<a.verified_at_utc
      OR r.order_id IS NOT NULL
 ) THEN RAISE EXCEPTION 'Activation has invalid expiry snapshot or rejection'; END IF;

 IF EXISTS (SELECT 1 FROM sms_wallet_accounts
            WHERE spent_units>granted_units OR spent_units<0)
 THEN RAISE EXCEPTION 'SMS wallet imbalance'; END IF;

 -- Account totals are cumulative across top-ups, not just the latest plan's
 -- invoice_unit_limit. Validate the double-entry conservation invariant.
 IF EXISTS (
   SELECT 1 FROM subscription_credit_accounts a
   JOIN customer_subscriptions s ON s.id=a.subscription_id
   WHERE s.kind<>2 OR a.customer_user_id<>s.customer_user_id
      OR a.available_units+a.reserved_units+a.consumed_units<>a.granted_units
      OR a.granted_units<0 OR a.available_units<0
      OR a.reserved_units<0 OR a.consumed_units<0
 ) THEN RAISE EXCEPTION 'Quantity credit accounting invariant failed'; END IF;

 RAISE NOTICE 'PASS: Phase12 Step10 finance approval, collection and credit audit';
END $audit$;
ROLLBACK;
