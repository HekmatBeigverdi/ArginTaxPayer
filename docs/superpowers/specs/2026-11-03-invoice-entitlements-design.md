# Invoice subscription enforcement

User requirements: block external invoice submission when a quantity package is exhausted or a subscription has expired. Cover original, correction, cancellation and sales-return submissions. Keep draft creation, editing and discarding unrestricted. Redirect blocked customers to `/customer-plans`.

Architecture decision: use one authoritative gate in the transaction that claims a dispatch attempt, with a lightweight service precheck before finalization or creation of a legal successor. An endpoint-only check would miss direct worker calls and races; separate checks in each sender would duplicate accounting. Lock the customer row to serialize subscription activation with dispatches, then lock the active subscription and quantity balance. Quantity reservations and the attempt commit together; the initial durable receipt consumes the reservation in the same transaction. Unknown outcomes retain reservations. Use operation IDs as stable accounting keys. Timed plans have no invoice count.

Existing trial policy: preserve the first two dispatches only for customers who have never held an activated subscription; expired paid subscriptions must never fall back to trial. Each started trial dispatch (including unresolved results) occupies one slot. This is an implementation assumption pending user clarification.

Do not back-charge historical sends. No schema change is needed: use existing credit journal and dispatch history. Acknowledged replay and inquiry remain accessible after expiry. Subscription errors use stable `subscription_*` codes with HTTP 409; browser code maps only these codes to the fixed purchase URL.

Validation: time boundaries for every plan, missing/exhausted balances, trial eligibility, quantity conservation, simultaneous last-credit claims, rollback, receipt idempotency, API error forwarding and browser navigation. No live authority calls.
