# Deployment schema and hosted Sandbox implementation plan

Objective: deploy the schema from the exact application release before promotion, and admit legacy Sandbox workflows on the explicitly configured dedicated Production database without changing fiscal routing or retry semantics.

- [x] Centralize the existing hosted database policy and use it throughout Sandbox reserve, preparation, dispatch, evidence, reconciliation and legal successor operations. Keep owner, immutable environment, memory and key checks.
- [x] Export idempotent SQL and the ordered migration manifest from the compiled EF migration assembly during both CI and manual builds. Include these in the checksummed API archive.
- [x] Before promotion, check migration-history compatibility, back up pending schema, apply SQL with the dedicated migrator over TLS, and verify exact history. Fail before switching current on any failure. Keep runtime credentials free of DDL rights and keep privileged helper code root owned.
- [x] Cover policy exclusions, deployment ordering/failure, migration manifests and database-name regression with focused tests and builds. Document the one-time helper/configuration update and separate actual provider acceptance from offline verification.

No live invoice submissions, automatic resend, database rename, migration-history reset or immediate production deployment is included.
