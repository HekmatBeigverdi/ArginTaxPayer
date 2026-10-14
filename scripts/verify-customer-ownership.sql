-- ArginTaxPayer Phase04 owner correction: READ ONLY.
-- Run ONLY after checking SELECT current_database() = 'argintaxpayer_test'
-- and applying 20261013233000_AddCustomerCommercialOwnership.
-- Never infer customer-owner matches from phone, taxpayer identifier or old grants.

SELECT current_database() AS database_name;

SELECT 'customer_accounts_without_owner' AS finding, COUNT(*) AS row_count
FROM identity_users u
WHERE EXISTS (SELECT 1 FROM identity_user_roles r
              WHERE r.user_id = u.id AND r.role = 'Customer')
  AND NOT EXISTS (SELECT 1 FROM customer_commercial_owners o
                  WHERE o.customer_user_id = u.id)
UNION ALL
SELECT 'historical_taxpayers_without_customer', COUNT(*)
FROM taxpayer_profiles WHERE customer_user_id IS NULL
UNION ALL
SELECT 'deprecated_taxpayer_grants', COUNT(*)
FROM taxpayer_delegate_grants
UNION ALL
SELECT 'partner_owned_customers', COUNT(*)
FROM customer_commercial_owners WHERE partner_level2_user_id IS NOT NULL
UNION ALL
SELECT 'root_direct_customers', COUNT(*)
FROM customer_commercial_owners WHERE partner_level2_user_id IS NULL;

-- Reconciliation candidate IDs (NO UPDATES):
SELECT u.id AS legacy_customer_user_id
FROM identity_users u
WHERE EXISTS (SELECT 1 FROM identity_user_roles r
              WHERE r.user_id = u.id AND r.role = 'Customer')
  AND NOT EXISTS (SELECT 1 FROM customer_commercial_owners o
                  WHERE o.customer_user_id = u.id)
ORDER BY u.created_at_utc;

SELECT id AS unlinked_taxpayer_id, kind, normalized_identifier
FROM taxpayer_profiles WHERE customer_user_id IS NULL
ORDER BY created_at_utc;
