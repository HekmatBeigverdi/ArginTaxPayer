# Phase 18 Step 09 — Fiscal-memory routing audit

Status: source audit underway; deployment of dual-authority routing is NOT accepted.

The existing code stores regular fiscal memories in FiscalMemory and Sandbox memories in sandbox_fiscal_memories. The Sandbox service validates ownership and has encrypted key custody. Phase10 readiness requires a Sandbox invoice snapshot and checks the related memory and outbox. Reference-ID and UID inquiry join against sandbox_fiscal_memories. Cancellation, correction, and sales return are Sandbox-only workflows.

Critical gap: FiscalMemory does not expose an authoritative persisted Sandbox/Production environment. Several Sandbox operations require the database to be named argintaxpayer_test. These routes cannot operate on the isolated argintaxpayer_prod deployment database without a safe refactor. Existing Sandbox-only connectors are not verified real-authority connectors.

Before accepting Step09, enforce a durable server-owned environment binding across invoice, memory, signing, dispatch, receipts, inquiry, and legal successors. Test mismatch rejection before HTTP. Keep the existing test database guards until equivalent stronger controls are implemented and verified. Do not infer destination from app mode, domain, fiscal memory ID text, or user-provided JSON. Official real API endpoint verification belongs to Step10.

No production endpoints, authority operations, or public runtime activation are authorized by this audit.

Source-only preflight:
```bash
export PHASE18_EXPECTED_SHA="$(git rev-parse HEAD)"
bash scripts/phase18-step09-dual-authority-preflight.sh
```
A source contract PASS does not mean dual authority routing PASS. 
