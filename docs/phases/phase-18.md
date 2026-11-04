# ArginTaxPayer — Phase 18: Controlled Production Deployment & Dual Tax-Authority Environments

**Status:** STARTED — Step 01 planning/baseline recorded; runtime implementation and acceptance NOT STARTED.  
**Date:** 2026-11-04  
**Branch:** `phase/18-production-deployment`  
**Starting main SHA:** `6ad5246a39cace77e76901a20d149c301094f7c3` (Phase 15 merge; release `phase-15-v1.0.0` reported in merge metadata).  
**Target:** `https://tax.argin.ir`, Ubuntu 24 VPS; ASP.NET Core API + Next.js production build + PostgreSQL 17.  
**Target release:** `phase-18-v1.0.0` **only after owner-approved evidence-based gates**.  
**Roadmap:** `docs/roadmap/18-phases.md` — Phase 18 is moved ahead of uncompleted Phases 16 and 17 by owner request; numbering and deferred obligations stay unchanged.

## 0. Owner-approved non-negotiable product contract

1. Host `argin.ir` remains on company A's cPanel hosting; DNS **A** record `tax.argin.ir` resolves to company B's Ubuntu VPS public IP, at the authoritative DNS provider. No need to relocate the apex or mail DNS. Validate A/AAAA, proxy/CDN, DNS propagation and certificate challenge before enabling traffic.
2. The **application deployment environment** is Production, but it must support BOTH **tax authority Sandbox and tax authority Production concurrently**. Do not disable existing Sandbox users or force all dispatches to the real authority.
3. **Fiscal memories for Sandbox and real authority are already separately defined.** Audit and reuse the existing schema, UI, services and connectors before proposing changes. Do not recreate fiscal memory flows, add an unnecessary global switch or migrate memory IDs just to implement hosting.
4. The authoritative dispatch/inquiry target must be bound to the **persisted authority environment of the invoice's fiscal memory** and validated server-side; never infer from fiscal memory ID text, UI toggle, ASPNETCORE_ENVIRONMENT, domain, global application config or current selected account. Invoice, fiscal memory, credential, signing, request id, receipt, inquiry and legal lineage must agree on environment.
5. Sandbox memory -> Sandbox authority API; Production memory -> authority's officially documented **live** API at/under `https://tp.tax.gov.ir` after reviewing the official versioned SDK/specifications and real service paths. **Do not assume or hard-code endpoint paths** from recollection. Read-only authenticated checks precede actual dispatch.
6. Maintain original, correction, cancellation and sales return behaviors and Phase 10/11 idempotency, Indeterminate safeguards, Phase 12 subscription/financial invariants and Phase 15 role-scoped dashboards. Do not blindly resend unknown-outcome operations. No automatic inquiry, restart queue recovery or multi-worker support may be claimed if still deferred.
7. Every production tax submission has real legal consequences. Only an explicitly authorized real taxpayer/memory, approved operator, and controlled invoice data may be used for live acceptance. Never use a fabricated taxpayer or production invoice for a smoke test.
8. No production secrets, private keys, access tokens, sensitive invoice dumps, credentials or database snapshots in GitHub, logs, CI artifacts or committed configuration. A public repo demands stricter checks.
9. Prior to any real-user traffic, deliver minimum Phase 17 security/reliability controls as **blocking Phase 18 gates**: tenant isolation, secure secret custody, least privilege, TLS, CSRF/session/cookie settings, backup+restore, encryption key persistence, log redaction, firewall, rate limiting where relevant, secure migrations, rollback and operator access. Broader Phase 16 admin enhancements and Phase 17 extended penetration/load testing remain tracked deferred work; neither must be falsely described as completed.

## 1. Existing evidence and audit baseline

- Roadmap states Phase 18 deployment, operator runbooks, user guidance, release/tag, verification and rollback.
- Phase 15 merged to main at baseline SHA above; this DOES NOT prove Phase 15/earlier production safety without new checks.
- `src/ArginTaxPayer.Api/appsettings.Production.json` presently contains only `ArginTaxPayer.DeploymentLabel = production`; presence of this file is **not proof** of secured production deployment or tax API routing.
- `docs/architecture/fiscal-memory-connection-onboarding.md` is an older **design proposal** and explicitly NOT IMPLEMENTED as of its date; inspect current actual implementations to establish truth, prioritizing owner's confirmation that Sandbox and real fiscal memories are already separate.
- No VPS access, runtime command, migration, DNS modification, live API verification or billing action has yet been performed by this document. Connection between macOS and the VPS remains the owner-mandated AutoSSH tunnel for test database only.

## 2. Fixed step plan and approval gates

| Step | Deliverable | Evidence / pass criteria |
|---|---|---|
| **01** | Baseline & scope freeze | Inventory current main SHA, installed runtimes, environment-specific memory & dispatch paths, config/secrets references, migration status, release evidence; document risks and absence of live tests. No runtime writes. |
| **02** | Production architecture & deployment contract | Document architecture, ports, DNS/TLS, separate app/database/service identities, secret custody, tax authority environment matrix, outbound network and rollout/rollback; owner approval before provisioning. |
| **03** | Ubuntu VPS readiness & hardening | Snapshot/backup of existing VPS; disk/RAM/time sync, OS patches, ssh access, nonroot service users, UFW and ingress 80/443, outbound tax endpoint access; existing development/database services remain functional. |
| **04** | DNS & HTTPS | Authoritative DNS A record for `tax.argin.ir` to VPS, check conflicts with AAAA/CDN, reverse proxy and valid renewable TLS certificate; apex `argin.ir` and mail unchanged, enforce HTTPS. |
| **05** | Build/release packaging | Deterministic .NET Release publish and Next.js production build, lockfiles, pinned toolchains, server-side env injection; reproducible same-commit artifacts, no dev servers/debug auth/public source maps. |
| **06** | Production process supervision | systemd or explicitly chosen container service, unprivileged users, startup order, health/readiness probes, bounded restarts, controlled migration/release switches; API and database not publicly exposed. |
| **07** | Production PostgreSQL & migrations | Separate `argintaxpayer_prod` (name subject to owner approval), distinct restricted credentials, backups and tested restore. Dry-run SQL/migration diff, production preflight to assert `current_database()` and no destructive surprises; transactional migration where possible. Never copy test data/secrets. |
| **08** | Configuration & key custody | Secure config for production deployment, separate credentials per Sandbox/live memory, RSA/secret encryption persistence across restart, rotation, JWT/cookie/session settings, SMS/payment provider secrets; no secrets in Git or output. |
| **09** | Fiscal-memory routing audit | Verify existing explicit Sandbox/live memory distinction in DB + UI + API; verify send/sign/inquiry/correction/cancellation/return use authoritative per-memory environment, no global environment leakage; fix only reproducible defects with tests. |
| **10** | Tax-authority connector & version gate | Cross-check archived SDK and official current documented API endpoints, auth, signing and reference-ID inquiry against implemented adapter. Read-only connectivity checks to Sandbox and real service separately; record edition, sanitized request IDs and precise outcomes. |
| **11** | Tenant/security minimum gate | Verify tenant and role isolation, authorization, Level1 approvals, CORS/CSRF, secure cookies, rate limits, redacted telemetry, secrets accessibility and audit retention; real-user go-live blocked on failures. |
| **12** | Observability, backup & rollback | Redacted structured app/proxy logs, correlation IDs, metrics/alerts/disk monitoring, daily backup plan, restore drill, encryption-key recovery, release rollback and schema backward compatibility. |
| **13** | Production web/API feature smoke | `https://tax.argin.ir` works over TLS, identity/MFA, taxpayer/memory management, draft, permission checks, plans, billing and dashboards, health, restart resilience; no accidental provider charge or tax submission. |
| **14** | Sandbox regression on deployed Production app | With an authorized Sandbox memory, run submit, reference-ID inquiry, correction/cancel/return where applicable, negative cross-environment tests. Capture official sandbox results; preserve no-duplicate protections. |
| **15** | Controlled real authority acceptance | Authenticated read-only live connection first; with express owner authorization and genuine valid taxpayer/memory, send a real invoice and verify true authority outcome via official inquiry, with optional legal follow-up only when appropriate. Distinguish HTTP receipt vs final authority acceptance; never auto-send test/fake invoices. |
| **16** | macOS + Ubuntu verification | Same Git commit and reviewed migration set; dotnet restore/build/tests and Next.js tests/typecheck/build on macOS and Ubuntu, API/UI/manual tenant negative tests, install/upgrade/rollback drill, documented PASS/FAIL/SKIP with logs. |
| **17** | Operator/user documentation & handoff | DNS/TLS, deployment, backup/restore, credentials rotation, dual-authority routing, incident response, known Phase 16/17 deferrals and customer guidance, support escalation. |
| **18** | Final signoff, release & controlled rollout | Explicit owner approval after all mandatory gates and both Sandbox/live real evidence. Clean branch/PR, reviewed merge/tag/release, operator readiness and verified rollback path. No release claim before acceptance. |

## 3. Environment invariant matrix

| App runtime | Fiscal memory type | Destination | Allowed? |
|---|---|---|---|
| Production VPS | Sandbox | Official Sandbox endpoint | YES |
| Production VPS | Real/Production | Official Production endpoint | YES after eligibility/approval |
| Production VPS | Sandbox | Official Production endpoint | NEVER |
| Production VPS | Real/Production | Official Sandbox endpoint | NEVER |
| Local/test | Either | Live submission by default | NEVER; explicit separately gated authorization required |

Resolve target from immutable per-operation environment snapshot derived from the persisted memory. Protect against memory reassignment and time-of-check/time-of-use races. Inquiries and legal child invoices inherit the original operation's environment regardless of future app config. The endpoint allowlist is server-managed per environment and versioned; reject unknown values closed.

## 4. Production readiness and explicit blockers

- DNS and TLS ownership; VPS public IP, ports and any NAT/CDN.
- Authenticated prod DB secrets and connection pool; DB isolation from dev and test.
- A real registered fiscal memory/key under taxpayer authorization and production authority connectivity; no assumption that passing Sandbox implies live success.
- Verified official SDK edition, certificate/signature, invoice data rules and reference inquiry contract.
- Ability to recover both DB and encryption/wrapping keys; verified restore into a **separate** recovery database before go-live.
- Least-privileged systemd/container identities, log access, reverse proxy security and server-side tenancy.
- Production action authorization for potentially real SMS gateway charges and SEP/ZarinPal payment activation; do not auto-enable based on tax deployment.
- Unfinished Phase 16/17 requirements registered for future work. If any security-critical item is missing, block live rollout rather than waiving it.

## 5. Command/runbook policy (commands to verify/parameterize in Steps 02–03, NOT executed now)

On macOS, repo and source checks:
```bash
git fetch origin
git switch phase/18-production-deployment
git status --short
git rev-parse HEAD
dotnet --info
node --version
```

Owner-mandated macOS PostgreSQL test tunnel (use dedicated terminal; **not** a production database migration channel):
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

Ubuntu shell:
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git status --short
```

Never assume the development VPS IP is the final public IP for `tax.argin.ir` until verified. Never run `dotnet ef database update` on any DB without database-name assertion, reviewed migration and backup/restore gate. Running as `root` or altering firewall/SSH/DNS is not authorized by these example commands.

## 6. Mandatory step-by-step log contract

Update **this same file** for each step with: start/end date, branch and commit SHA, exact touched files/DB migrations, source/spec evidence, security review, macOS automated tests with outputs, Ubuntu independent tests with outputs, external Sandbox/live evidence (only when actually run), unresolved risks and owner explicit acceptance. Never manufacture PASS. Each step is owner-approved before continuing to the next. All failed/indeterminate submissions must be handled without double-send.

## 7. Step 01 record (current)

**Completed:** Phase 18 scope/step plan authored; base SHA and Phase 15 merge metadata inspected; roadmap and existing production appsettings reviewed; owner-confirmed already-separated fiscal memory definitions preserved as existing behavior rather than falsely billed as a new feature.  
**Not completed:** Detailed routing code audit, remote Ubuntu inspection, actual database inspection, official API validation, DNS or HTTPS change, automated tests, real authority connection.  
**Gate:** Await local/VPS baseline, detailed source audit, and owner review before marking Step 01 accepted.  
