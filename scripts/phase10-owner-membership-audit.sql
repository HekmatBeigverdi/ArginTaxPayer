-- Read only: distinguishes never-linked taxpayers from intentionally unlinked ones.
-- Run only in the isolated argintaxpayer_test database.
BEGIN READ ONLY;
SELECT current_database() AS database_name;
SELECT
 p.id AS taxpayer_id,
 p.customer_user_id AS owner_id,
 COUNT(m.id) AS membership_history_rows,
 COUNT(m.id) FILTER (WHERE m.unlinked_at_utc IS NULL) AS active_memberships,
 MAX(m.linked_at_utc) AS most_recent_linked_at,
 MAX(m.unlinked_at_utc) AS most_recent_unlinked_at,
 CASE
   WHEN COUNT(m.id) FILTER (WHERE m.unlinked_at_utc IS NULL) > 0 THEN 'ACTIVE'
   WHEN COUNT(m.id) = 0 THEN 'NEVER_LINKED'
   ELSE 'EXPLICITLY_UNLINKED'
 END AS diagnosis
FROM taxpayer_profiles p
LEFT JOIN customer_taxpayer_memberships m
 ON m.taxpayer_id = p.id AND m.customer_user_id = p.customer_user_id
WHERE p.customer_user_id = '425a8f26-7633-41ca-a3cc-431c9e2f1719'::uuid
GROUP BY p.id, p.customer_user_id
ORDER BY p.id;
COMMIT;
