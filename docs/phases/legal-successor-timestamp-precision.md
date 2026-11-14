# Legal successor timestamp precision and safe continuation

## Observed failure

Production cancellation and sales-return preparation returned `invoice_calculation_audit_failed`, while local testing passed. Read-only diagnostics of the two affected frozen Type 2 documents found only `0200201 / header.indatim`: JSON issue timestamps differed from the persisted PostgreSQL timestamps by three and two .NET ticks respectively (0.3 and 0.2 microseconds). Financial findings were empty. Neither document had a dispatch operation or local fiscal serial reservation.

The snapshot JSON retains .NET's 100 ns ticks; PostgreSQL/Npgsql timestamps preserve microseconds. An exact comparison was rejecting the expected loss of sub-microsecond precision. This is an internal storage comparison fix, not a relaxation of tax date rules.

## Changes

- New legal documents use UTC issue timestamps truncated to microseconds before both JSON serialization and persistence, in Production and legacy Sandbox paths.
- The immutable-snapshot audit compares at PostgreSQL microsecond precision. UTC, future-date, malformed-date and differences of at least one microsecond remain rejected. Existing immutable JSON, hashes and amounts are not rewritten.
- An explicit `resume-unprepared` operation continues the exact existing Production legal document. It requires enabled Production legal operations, scoped live memory/ownership, a finalized correction/cancellation/return, an active relation, and no fiscal reservation or dispatch operation. The established submission service rechecks predecessor acceptance, revision, subscription and durable admission.
- The lineage view offers **ادامه ارسال همین سند** for eligible pending cancellation and sales-return documents. It passes the successor ID, not the original invoice ID, and creates no replacement document. The existing correction continuation path remains available.

## Recovery after deployment

Deploy backend and web together. No migration or environment change is required. Open **زنجیره** on the original invoice and continue the existing pending document using **ادامه ارسال همین سند**. Do not create a new cancellation/return to replace these documents. Documents with an existing reservation or dispatch are excluded from this continuation path and require normal reconciliation.

No live legal operation was submitted during diagnosis or verification. No Production invoice data was modified. The patched audit accepted both original captured snapshots without changing their contents.

## Verification

- Regression first reproduced rejection of 100 ns remainders; the patched audit accepts those and rejects >=1 microsecond differences.
- A real TEST PostgreSQL parameter/timestamp roundtrip passed.
- Recovery policy tests reject original invoices, inactive relations, Sandbox, nonfinalized documents, reservations and existing dispatches.
- UI tests verify continuation targets the same successor; proxy tests cover authentication, origin and output sanitization.
- Backend suite: 1665 passed, 55 skipped. Web suite: 760 passed. TypeScript checking passed. Independent review found no important/critical issues; full live submission is not part of these checks.
