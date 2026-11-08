# ArginTaxPayer — Phase 18: Controlled Production Deployment & Dual Tax-Authority Environments

**Status:** Steps 01–06 OWNER-APPROVED; Step 07 OWNER-APPROVED (2026-11-04) with isolated PostgreSQL 17, TLS VerifyFull, 72 EF history rows and pre-migration restore confirmed; post-migration audit/restore outputs have not been independently provided in this conversation. Step 08 source-level configuration and custody controls IMPLEMENTED and macOS/Ubuntu preflight reported PASS by owner; private config staged with read-only security audit PASS and services disabled. Step 09 source audit in progress: Sandbox-on-Production and real authority routing NOT YET IMPLEMENTED. Public Production application remains disabled.
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
  argindev@109.70.78.95
```

Ubuntu shell:
```bash
ssh argindev@109.70.78.95
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
**Gate:** Owner explicitly approved Step 01 on 2026-11-04. Source-level gaps discovered in Step 02 remain mandatory Step 03–11 blockers; no undocumented PASS or runtime deployment is implied.  

## 8. Step 02 — Production architecture and deployment contract (2026-11-04)

**Status:** Design/documentation completed on branch, awaiting explicit owner acceptance. No DNS/VPS/database changes made; no tests executed in this design-only step.

### 8.1 Source-level findings and critical prerequisite

Audited `src/ArginTaxPayer.Api/Program.cs`, `appsettings.json`, `appsettings.Production.json`, `apps/web/next.config.ts`, `apps/web/package.json`, and `apps/web/app/api/invoices/[...segments]/route.ts` at the Phase 18 branch. **Critical:** `Program.cs` currently guards the principal DI setup and most functional API endpoint maps behind `identityDevelopmentEnabled = builder.Environment.IsDevelopment() && connectionStringPresent`. In Production this evaluates false, disabling identity, taxpayers, invoices, dispatch, dashboard, signing and billing routes; health may still respond. This is a hard blocker. Many Next.js BFF proxies call backend paths under `/api/dev/*`. A production deployment MUST first refactor endpoint/service registration to an explicit secure production-capable structure, with real authentication/authorization and no development diagnostics, fixture paths or bypasses inadvertently exposed. Do NOT set `ASPNETCORE_ENVIRONMENT=Development` in public hosting to work around the blocker. Keep `/health/dev-identity` and development OpenAPI unreachable publicly. Phase 18 Step 05/06/11 require tests for route availability and role/tenant protections in Production mode.

The existing `appsettings.Production.json` only supplies deployment label, not a deployable secure configuration. Next.js `npm start` already binds to 127.0.0.1 (port must be supplied); application package uses Next 15.5.28. Version choice is recorded, not an upgrade mandate.

### 8.2 Network and trust zones

```text
User browser -- HTTPS:443 --> tax.argin.ir (Ubuntu VPS, Company B)
                                    |
                           Nginx TLS termination
                                    |
                   +----------------+------------------+
                   |                                   |
          127.0.0.1:3000                        (No public API)
           Next.js server                                |
                   |  server-to-server BFF               |
                   +------> 127.0.0.1:15089 ASP.NET Core |
                                        |                |
                             127.0.0.1:5432 PostgreSQL <-+
                                        |
                            outbound HTTPS 443
                       +------------------------+
                       | Official Sandbox API   |
                       | Official Production API|
                       +------------------------+
```

Architecture **decision**: use existing Ubuntu VPS as one deploy host (single application instance and one controlled worker only if already implemented). Nginx is sole public HTTP server; Next.js API/BFF accesses loopback ASP.NET Core; the .NET API and Postgres must not bind public interfaces. The architecture is an explicit operational choice, **not** proof that nginx/systemd are installed. Prefer same-origin browser access only (`https://tax.argin.ir`); avoid permissive CORS. Preserve client's actual HTTPS origin safely through a narrowly trusted proxy setup and validate forwarded headers; never blindly trust public `X-Forwarded-*` headers. Use separate deployment directories, application identities and environment files; package service restart policy, shutdown, and health checks in later steps. No Docker requirement is introduced without verifying existing setup.

Port contract (verify collisions during Step 03):
| Component | Listen | Public | Allowed clients |
|---|---|---|---|
| Nginx | 0.0.0.0:80, :443 (+ IPv6 only if configured) | YES | internet; port 80 ACME/redirect |
| Next.js | 127.0.0.1:3000 | NO | Nginx |
| ASP.NET Core | 127.0.0.1:15089 | NO | local Next.js BFF / diagnostics |
| PostgreSQL 17 | 127.0.0.1:5432 or isolated local network | NO | local API and authorized admin sessions |
| Adminer | existing local-only :8081 if retained | NO | authenticated SSH tunnel only |

Existing development services should not be overwritten or stopped without a separate maintenance decision; allocate alternative loopback ports if collision exists.

### 8.3 DNS, TLS and origin contract

- Retain authoritative nameserver delegation and cPanel site for apex `argin.ir` (company A), email MX/SPF/DKIM/DMARC and existing www records unchanged.
- Create or update ONLY the `tax` A record at the currently authoritative DNS operator pointing to the **verified** public IPv4 of Ubuntu VPS company B. Check inherited wildcards, existing tax CNAME, AAAA records, Cloudflare proxy and CAA before applying; do not hard-code a guessed VPS IP.
- Nginx `server_name tax.argin.ir`; ACME certificate via Let's Encrypt Certbot (or equivalently managed ACME), auto-renewal validated, HTTP 80 redirects to 443 after issuance. Establish HSTS after HTTPS works and all subdomain implications reviewed; do not enable `includeSubDomains` globally.
- Browser public base URL = `https://tax.argin.ir`; backend base origin is loopback, never a tax-authority host. Ensure session cookie `Secure`, `HttpOnly`, suitable `SameSite` and trusted public host/origin checks. No open redirect, unvalidated host or mixed-content assets.
- Maintenance preflight: `dig +short tax.argin.ir A`, `dig +short tax.argin.ir AAAA`, `curl -I https://tax.argin.ir`, `openssl s_client -connect tax.argin.ir:443 -servername tax.argin.ir </dev/null`; these are checks, not evidence yet.

### 8.4 Database and migration isolation

- One production database (proposed `argintaxpayer_prod`), application role with minimal DML privileges, migration role with narrowly controlled DDL permissions; neither may reuse `argintaxpayer_test` nor its credentials.
- Lock down loopback DB; no public 5432/8081 and no uncontrolled macOS forwarded production access. The existing owner-mandated AutoSSH port 15432 points **only to the test Postgres transport** as previously approved, and must not be used for production schema updates without independently verifying the target database name and explicit approval.
- Review migration history and EF model changes and generate an auditable migration SQL script before applying. Check database name, owner, expected schema, schema diff, backup freshness and restoring a separate copy. Rehearse on disposable copy, then run once under controlled maintenance; rollback preference is application/schema compatibility + database restore plan, not unreviewed reverse SQL.
- Production must have its own key-encryption root and backup of wrapping keys, with tested decryptability after restart and restore, separated from sanitized developer/test keys.

### 8.5 Tax authority environment invariants (existing feature preserved)

Deployment mode = `Production` regardless of fiscal memory destination. Preserve **existing** separated Sandbox and real memory definitions. A server-side transaction resolves stored fiscal-memory environment and ownership, binds it to immutable invoice dispatch/inquiry metadata, signs with the correct key and selects only the matching officially verified origin/version. Never route by UI selection, environment variable alone, raw memory code prefix, or generic fallback. Deny unknown/mismatched mappings before network I/O. Amendment, cancellation and sales return inherit the original invoice's authority environment; inquiry uses original operation target and official reference identifier semantics.

Official endpoint exact paths, auth/token formats, SDK edition and allowed egress addresses are to be established from the project archived SDK plus current official specification in Step 10. Preserve all existing Sandbox behavior; never silently enable live transmission on boot or as a smoke test. Live submission requires valid live identity, legitimate invoice and separately recorded operator authorization.

### 8.6 Secret, egress and service operations

- No encryption key, PEM, API token, DB password, SMS/API gateway credential or TLS private key in Git, `appsettings.*.json`, shell history, process arguments, Next.js `NEXT_PUBLIC_*`, browser response, or journald. Use root-controlled/unprivileged service-readable files (systemd `LoadCredential` or equivalent), managed KMS/Vault where genuinely available; file mode and ownership verified in Step 08.
- Service identity must only access its own binaries/secrets and required runtime files. State/config shared across restarts securely; startup does not reinitialize memory key material. Secrets and database backups are encrypted, rotation and recovery documented.
- Permit outgoing HTTPS to specific authoritative Sandbox/live FQDNs and configured OTP/payment provider hosts with TLS validation; do not claim actual network reachability before testing from VPS. Never disable TLS verification or follow cross-origin redirects with signed payloads/tokens.
- Health checks distinguish process `/health/live` from database `/health/ready`; exposing them publicly requires review/minimal response. Audit actor, customer, memory, environment, operation ID, redacted response and correlation ID; do not log signed raw invoice/private data. Production operations must not depend on existing development-only observer/worker semantics.
- Start with controlled release deployment (no immediate open public onboarding); backup and rollback to previous known-good version, do not auto-resend Indeterminate operations after service restart.

### 8.7 Release layout and ownership

Proposed file layout to implement in later steps (not yet created):
```text
/opt/argintaxpayer/releases/<sha>/{api,web}
/opt/argintaxpayer/current -> /opt/argintaxpayer/releases/<sha>
/etc/argintaxpayer/{api.env,web.env}     # restricted, never committed
/etc/nginx/sites-available/tax.argin.ir
/var/lib/argintaxpayer/                # app state, if required
/var/backups/argintaxpayer/            # encrypted, protected, externally replicated
```
Operator: `argindev` via SSH/sudo on explicitly allowed commands; application runs as dedicated non-login, non-root identities. `root` interactive use not mandatory. Production deploy performed only from an accepted and pinned commit, not a mutable unverified local worktree.

### 8.8 Threat model and fail-closed acceptance

| Risk / failure | Mandatory control |
|---|---|
| Public Production app lacks functional API | Refactor development-only DI/route registration; Production mode integration smoke for every essential route |
| Exposing dev bypass endpoints to internet | Explicit route authorization classification and negative Production tests; deny diagnostic/bootstrap secrets |
| Sandbox invoice accidentally sent live | Persisted authority-environment binding + strict endpoint allowlist + mismatch rejection/negative tests |
| Public Postgres/API/Adminer | Loopback bind + firewall/port scan acceptance |
| Cross-tenant invoice or signing access | Backend policy + SQL scopes + cross-tenant negative tests |
| Failed release/migration, lost encryption keys | Versioned build, SQL migration rehearsal, tested encrypted backup and restore with keys |
| Duplicate official invoice after ambiguous result | Durable outbox guard, original-reference inquiry, never auto-resend Indeterminate |
| DNS/certificate outage | Authoritative DNS preflight, ACME renew check, redirect and TLS validation |
| Live tax/OTP/payment side effects during smoke | Safe read-only checks; real actions require explicit controlled approval |

### 8.9 Decisions, approvals and transition

**Fixed now:** public FQDN, company-A apex unchanged, company-B Ubuntu host, Nginx/TLS front, loopback Next/API/DB, separate production data/credentials, both tax authority environments available inside a production deployment, current memory definitions reused, fail-closed routing, single-host deployment and controlled rollback.

**Not yet proven / blocked until later steps:** exact public VPS IP, authoritative DNS account, Nginx/systemd presence, production process registration refactor, Next.js BFF env names and all proxy policy, complete fiscal-memory schema/dispatch source audit, official live endpoint edition, VPS outbound connectivity, current production user onboarding, real fiscal credentials, operational tests, server capacity, certificate issuance, backup restore. No claim of deployed/live readiness.

**Step 02 gate:** architecture contract completed and explicitly APPROVED by owner on 2026-11-04. No infrastructure or runtime changes were performed. Step 03 may proceed; route-registration refactor remains a hard Step 05/06/11 blocker.

## 9. Step 03 — Ubuntu VPS readiness and non-disruptive hardening (2026-11-04)

**Owner approval:** Step 02 explicitly approved 2026-11-04.  
**Implementation state:** Read-only inspection script added at `scripts/phase18-step03-ubuntu-preflight.sh` (commit `f53738408dc7e0a4f5fce1763c84b160110c595f`). Execution on the actual VPS and any changes to firewall/SSH/packages were **not** performed. Do NOT mark Step 03 infrastructure acceptance PASS until actual logs are provided and reviewed.

### 9.1 Existing Ubuntu environment and non-regression contract

Source evidence: `scripts/ubuntu-dev.sh` uses `$HOME/.argintaxpayer-test/ubuntu.env`, local Postgres test database `argintaxpayer_test` at `127.0.0.1:5432`, test API at `127.0.0.1:15089`, test Next.js at `127.0.0.1:3000`, and local-only Adminer at `127.0.0.1:8081` when configured; `scripts/dev-up.zsh` uses AutoSSH 15432 for macOS development. These ports are **currently reserved for development when its processes are active**, not guaranteed to be free for production. Do not stop or replace `ubuntu-dev.sh`, its test database, the SSH tunnel or Adminer to make space. If active listeners conflict, allocate dedicated production loopback ports after gathering `ss` evidence, and record actual final ports in Step 06. Do not assume `postgresql.service` reflects containerized PostgreSQL status; inspect actual listeners and Docker separately.

### 9.2 Safe operator workflow: read-only inspection

Run from Ubuntu **as `argindev`, without sudo**:
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
bash scripts/phase18-step03-ubuntu-preflight.sh
```
The script rejects non-Ubuntu-24, root execution, dirty tracked worktrees and wrong branch. It only reads OS, time sync, capacity, runtime presence, TCP listeners, firewall state, SSH configuration validity, limited service status and file ownership. Reports are written in `artifacts/phase18-step03/` with private file permissions. **Do not upload raw report logs** to a public GitHub repository; redact IPs/hostnames and return summarized PASS/WARN values. If `artifacts/` is unexpectedly tracked, refuse to commit reports. The script does not restart services, access a production database, change routing, touch UFW, issue certificates or call the tax authority.

Read-only comparison before/after any future infrastructure change:
```bash
bash scripts/ubuntu-dev.sh status
ss -ltn
sudo ufw status verbose
sudo systemctl status ssh --no-pager --lines=0
```
The last two commands require authorized sudo privileges, are inspection-only, and can expose private network metadata; redact in shared output. Verify `ssh` access from a **second independent terminal** before future firewall/SSH changes. SSH daemon name is usually `ssh` on Ubuntu; confirm on this host, not assumed.

### 9.3 Hardening changes permitted only after preflight approval

1. Verify authenticated console/provider recovery path and obtain a provider VPS snapshot or consistent off-host backup with documented restoration. Snapshot completion is a **manual pending gate**, not claimed. Confirm spare disk/inodes/memory and time synchronization.
2. Inventory real ingress listeners: TCP 22 (or actual SSH port) for admin with SSH keys, 80/443 public for Nginx only, 5432 DB/8081 Adminer/3000 web/15089 API and any other dev ports strictly loopback. Check provider edge firewall/NAT separately from UFW.
3. Stage firewall rules **without enabling/reloading them in this step**. Preserve the verified SSH entry path (including the existing AutoSSH service); add only the actual SSH port/address and HTTP(S) rules in later maintenance with a proven console fallback. Evaluate IPv6 and VPN routes before enabling UFW. Avoid a blanket `ufw enable` command. Cloud firewall needs matching review.
4. Validate `sshd -t` and SSH key login using an independent second session before modifying authentication policy. Do not disable password login or SSH port blindly; root login changes require proof that sudo and recovery both work.
5. Ensure `unattended-upgrades` maintenance policy, security patches, fail2ban/rate-limiting and Nginx package installation are reviewed for service restart and source compatibility before applying. No automatic system-wide `apt upgrade` during an active dev/test session.
6. Define non-login production application principal(s), reserved release/config/state directories and least privileges during Steps 05–08 only after ownership/permissions review. No `chmod -R 777`, no world-readable secrets.
7. Record actual ports, DNS IP, resource sizing, backup policy and operational owner in the operator runbook. Defer changes requiring maintenance approval, not their risk tracking.

### 9.4 Step 03 acceptance matrix

| Gate | Evidence | Current state |
|---|---|---|
| Ubuntu release + runtime inventory | Local script platform/runtime logs | NOT RUN ON VPS |
| CPU/memory/disk/inodes/time sync | Local script resources/platform logs | NOT RUN ON VPS |
| Listening sockets/port conflicts | Local script sockets log + operator review | NOT RUN ON VPS |
| SSH key access + recovery second session | Independently confirmed operator test | PENDING |
| Firewall/public exposures reviewed | UFW + provider firewall + external ingress scan | PENDING |
| Provider snapshot / encrypted off-host backup | Provider reference + tested restore plan | PENDING |
| Existing test DB, AutoSSH and dev UI/API unaffected | `ubuntu-dev.sh status`, read-only DB health and external tunnel | PENDING |
| Unauthorized public API and Postgres blocked | External network validation; no sensitive endpoint leak | PENDING |
| DNS/TLS issued | Specifically Step 04, not counted in Step 03 | FUTURE |
| Secure operational production API registration | Step 05/06/11 code and tests | BLOCKED UNTIL IMPLEMENTED |

**Exit criteria:** This is an implementation artifact and precise operator procedure, **not** an assertion of a hardened VPS. Step 03 may only be approved as a server-readiness gate after the operator runs preflight and returns reviewed results, provider backup evidence, SSH fallback and non-disruption/ingress checks. No DNS/SSL, firewall, endpoint or tax authority changes are performed automatically. Never merge or release based on an unrun checklist.

### 9.5 Owner-provided Ubuntu evidence (2026-11-04; partial)

Operator ran `bash scripts/ubuntu-dev.sh status`: neither managed test API nor test Next.js process running; API readiness and Next.js probes unavailable. This is a **reported stopped state**, not evidence of production readiness or unexpected loss. Do not restart or assume collision-free production ports without separate listener inspection.

Operator ran `sudo ufw status verbose`: active; incoming deny, outgoing allow, routed deny, low logging; IPv4/IPv6 rules allow 22/tcp, 8080/tcp, 8081/tcp from anywhere. **Potential exposure:** 8080/8081 must be mapped to actual listeners and business dependencies before safely restricting; no rule changes authorized. 80/443 not currently allowed, intended for Step 04 after preflight and safe change approval.

Operator ran `sudo systemctl status ssh --no-pager --lines=0`: `ssh.service` active (running), triggered by `ssh.socket`, `sshd -t` startup check returned status 0. Disabling `ssh.service` in listing does not mean SSH unavailable; socket activation present. Existing administrator remote access must not be disrupted.

**Remaining Step 03 blockers:** `phase18-step03-ubuntu-preflight.sh` output and capacity/listeners/time synchronization; provider snapshot/recovery evidence; second-session AutoSSH access; internet-side exposure check; SSH/DB non-regression. Do not mark Step 03 PASS on partial evidence. Operator explicitly prefers AutoSSH, including interactive VPS shell.

### 9.6 Verified SSH diagnostic and preflight correction (2026-11-04)

Owner provided a real Ubuntu preflight result from commit `26e1a1d11ab7831dc8a08506b99288fba7999514` at `artifacts/phase18-step03/ubuntu-20261104T071014Z`: platform, resources, runtime, sockets, firewall, services and disk_ownership = PASS (7/8); sshd_validation = WARN. The exact log reported `sshd: no hostkeys available -- exiting.` when run as unprivileged `argindev`. Owner then executed `sudo /usr/sbin/sshd -t` with **exit code 0**. Therefore **SSH daemon configuration validation is PASS based on the privileged test**, and the earlier warning was a script permission false positive, not evidence of missing SSH host keys. Do not generate or replace server host keys, restart sshd or alter ssh configuration on account of this warning.

Script patched at `scripts/phase18-step03-ubuntu-preflight.sh` to attempt non-interactive privileged validation (`sudo -n`) or mark MANUAL_PENDING rather than raise a false security alarm. Script changed only; no host changes were performed remotely.

Owner clarified that public UFW allowances 8080 and 8081 serve unrelated applications. They are **out of Phase 18 modification scope**, and no Phase 18 work may remove, block, rebind or take over these ports. Existing UFW policy remains unchanged. Test API/web being down is acceptable at this stage; do not assert they must be running for Phase 18 readiness.

**Confirmed checks:** platform, resource script completion, runtime inventory script completion, socket inventory script completion, UFW presence/status, service inventory, disk ownership and privileged `sshd -t`. Note: a script PASS means a diagnostic command completed, not automatically that all infrastructure was hardened or correctly exposed.

**Outstanding operational acceptance:** provider snapshot/off-host backup and recovery option not yet evidenced; second independent AutoSSH connection not separately evidenced; external exposure/loopback review and actual inventory contents not supplied. Step 03 remains **CONDITIONALLY READY / NOT YET FINAL PASS** until these conditions are verified or explicitly dispositioned by owner with appropriate risk control. Further checks must not interrupt unrelated applications.

### 9.7 Owner clarification — provider backup unavailable (2026-11-04)

Owner confirms the VPS provider offers **no usable Backup/Snapshot capability**. Provider snapshot is not an achievable prerequisite. Replace it with an approved independent, **off-VPS encrypted recovery strategy**; do not label provider backup PASS. This does not authorize wiping/reprovisioning a host or running risky patches.

Alternative mandatory before actual production financial/tax operations:
1. Verify a separate trusted off-host destination (not the same VPS) and scheduled encrypted PostgreSQL logical backup (consistent `pg_dump` of actual production DB with an appropriate minimally privileged backup account, plus required PostgreSQL role/schema metadata); protect keys and access controls. Record schedule, retention, capacity and backup-failure monitoring.
2. Securely escrow **encryption key material / credential recovery instructions** independently of DB backups, with least privilege. Never store raw keys/secrets/backup dumps in the public GitHub repository or plaintext in remotely synced development folders.
3. Save reproducible deployment artifacts and version-pinned OS package/service/Nginx configs, DNS/TLS issuance steps, and secure secret restoration procedures for rebuilding on a fresh Ubuntu 24 host. An in-place OS rollback is **not** promised.
4. Demonstrate successful restore into an isolated disposable PostgreSQL environment with the correct DB identity and decryption/read checks; record recovery time and recovery point objectives and any lost intervals. Only after verified restore and owner signoff may the no-provider-snapshot alternative satisfy the production disaster recovery gate.
5. For **Step 03**, operator's lack of provider snapshot is a documented and accepted *constraint*, not proof of full backup compliance. It is safe to continue read-only discovery and non-destructive planning; actual public go-live remains BLOCKED until Step 07/12 restores and access/network preflight evidence succeed.

Explicitly avoid any unreviewed SSH or firewall hardening, destructive migration, upgrade, or `ufw enable` while this recovery path is not established.

### 9.8 Owner correction — provider snapshot exists (2026-11-04)

Owner **corrected** Section 9.7: the hosting provider DOES provide a reported **24-hour snapshot capability**, and the owner has **initiated/taken one snapshot**. Therefore Section 9.7's assertion that provider snapshots are unavailable is **superseded** and must not be used as current provider capability. At this stage, this is owner-reported evidence, not an independently validated recovery test.

Step 03 backup gate status updated to **SNAPSHOT TAKEN (owner-reported); restore readiness unverified**. Before marking complete, check the provider panel reports the snapshot as completed/successful; record its reference/time, retention/expiry, coverage of attached volumes/PostgreSQL and the precise restore procedure, without publishing credentials or private material. Also verify independent AutoSSH access and ingress/loopback exposure. Avoid reboot/recovery tests on the active VPS; rehearse on a disposable recovery target if available.

A single provider snapshot **does not replace** scheduled encrypted off-host PostgreSQL backups and restoration drills for real production operations, which remain Step 07/12 mandatory gates. Production go-live remains gated on tested recovery and additional Step 04–18 acceptance.

### 9.9 Provider snapshot status confirmed by owner (2026-11-04)

Owner explicitly confirms the just-created VPS snapshot is shown as **Successful** in the provider console. Record Step 03 pre-change provider snapshot as **PASS (owner-confirmed provider status)**. This does not independently establish a successful restore, recovery scope or retention. Keep database encrypted off-site backup and restore drill gates for Steps 07/12 before production go-live.

Outstanding Step 03 server acceptance evidence: second independent AutoSSH session and verification that ArginTaxPayer API/DB ports are not publicly accessible (without changing 8080/8081, which belong to unrelated software); optionally confirm the snapshot expiry and recovery instructions. The successfully completed preflight checks and privileged sshd -t test are already recorded. Do not mark the entire Step 03 PASS until these remaining items have evidence.

### 9.10 Owner-provided loopback listener evidence (2026-11-04)

Operator ran `sudo ss -lntp | grep -E ':(3000|5432|15089)\\b' || true`; the sole reported listener was `127.0.0.1:5432` via `docker-proxy`. This demonstrates PostgreSQL port 5432 is published on IPv4 loopback only in the observed host socket inventory; app test ports 3000 and 15089 were not listening. **PASS for host listener bind review**, not a substitute for independent external port scan, provider firewall inspection or inspection of Docker network forwarding/IPv6. Ports 8080 and 8081 are explicitly out of ArginTaxPayer scope per owner.

Other Step 03 owner-confirmed results: privileged `sudo /usr/sbin/sshd -t` exit 0 and successful provider snapshot. Remaining acceptance evidence: independent secondary AutoSSH login (and external exposure check as part of Step 03 security gate). Do not claim Step 03 fully accepted without this evidence.

## 10. Step 03 final signoff / Step 04 initiation (2026-11-04)

**Owner acceptance:** The owner explicitly confirmed successful second independent interactive **AutoSSH** connection to `argindev@109.70.78.95` and instructed finalizing Step 03. Step 03 is **ACCEPTED as Ubuntu pre-deployment readiness** at reported audit SHA `26e1a1d11ab7831dc8a08506b99288fba7999514`, with final PASS for `sudo /usr/sbin/sshd -t`, owner-confirmed completed provider snapshot, Ubuntu preflight results, loopback-only docker-proxy PostgreSQL `127.0.0.1:5432`, and independent AutoSSH login. This does NOT establish tested full disaster recovery, an internet-side port scan, or production application readiness. Carry these as mandatory Step 04/07/11/12 release gates; no risk is silently waived. Ports 8080 and 8081 belong to another application and must remain unchanged. Owner prefers AutoSSH for every future interactive VPS command, not plain SSH.

### Step 04 — DNS and HTTPS rollout plan (STARTED; not yet deployed)

**Goal:** Public `https://tax.argin.ir` terminating TLS at Nginx on the company-B VPS, while apex `argin.ir` continues on company-A cPanel with email and other services unaffected.

**Read-only preflight deliverable:** `scripts/phase18-step04-dns-tls-preflight.sh` created on branch; execute as ordinary argindev on Ubuntu 24. Captures `argin.ir` authoritative NS, `tax.argin.ir` A/AAAA/CNAME/CAA, current loopback/public listener inventory, nginx state and presence of certbot/openssl; writes private reports under `artifacts/phase18-step04/`. No provider DNS write, UFW write, package install, certificate issuance, service restart or API calls. Report command PASS means inspection completed, not DNS correct.

**Sequence and owner gates:**
1. Verify DNS is managed by the authoritative provider. Identify existing `tax` A/CNAME/AAAA wildcard and any Cloudflare proxy; record actual provider-confirmed VPS public IP; never assume prior SSH address is necessarily the HTTP endpoint IP. Check compatibility with IPv6 and CAA.
2. Determine who currently owns TCP 80/443 on VPS and whether a reverse proxy exists; ensure neither existing app (including unrelated 8080/8081) nor SSH will be disrupted. Nginx must have one explicit `server_name tax.argin.ir` block with valid config and loopback reverse proxy to Next.js **only once a working production build is available**. Backend BFF routes remain loopback/private.
3. Change ONLY the authoritative `tax` DNS A record to the verified VPS public IPv4 when approved. Do not change apex A, NS, MX, TXT, SPF/DKIM/DMARC or `www`. Only add AAAA if valid IPv6 HTTPS routing/TLS actually exists. Record propagation from multiple resolvers.
4. Stage explicit UFW TCP 80 and 443 rules only with SSH continuity and provider firewall context validated. UFW is already active; avoid broad reload or changing existing 22, 8080, 8081 rules. Nginx HTTP port 80 serves ACME challenge, then redirects to HTTPS.
5. Validate Nginx config `nginx -t` before and after staging. Issue a certificate for `tax.argin.ir` with certbot/ACME only after authoritative DNS matches VPS and port 80 is reachable; confirm cert SAN and chain, certbot renew dry-run and a monitored timer. Never issue a wildcard cert unnecessarily or expose internal ports.
6. Confirm `curl -I https://tax.argin.ir`, TLS hostname verification, HTTPS redirect, no mixed content, appropriate cookie security (functional in later Steps 05/06/11). If the application is not yet deployable in Production mode, use a harmless temporary maintenance response or keep access gated rather than exposing development-mode APIs.
7. Rollback: preserve old DNS TTL/values and previous nginx config (secrets excluded), avoid destructive changes; if new config fails, revert only tax-specific vhost and DNS to verified former values. Do not rollback unrelated sites.

**Step 04 acceptance evidence:** owner-confirmed DNS target and provider, public ingress and nginx port ownership checks, `nginx -t` PASS, authoritative DNS resolving to intended VPS, verified TLS cert chain and hostname, HTTP redirect, renewal drill, zero disturbance to company-A apex/mail and unrelated 8080/8081 applications. No official tax invoices will be submitted as part of Step 04.

**Current state:** Script authored only. DNS, port access, Nginx installation/configuration, SSL issuance and public endpoint all **NOT YET VERIFIED / NOT YET DEPLOYED**. Step 04 remains OPEN until Ubuntu operator run and DNS operator action with evidence.

### Step 04 operator evidence — 2026-11-04 07:21 UTC

Owner executed `bash scripts/phase18-step04-dns-tls-preflight.sh` on Ubuntu at Git SHA `7bd0a6434dcf06d4c6c1a7dd7e22c5ddc30863dc`; reported: `dig`, `curl`, `openssl` PRESENT, `nginx` and `certbot` MISSING. DNS interrogation command succeeded but its data (`artifacts/phase18-step04/20261104T072152Z/dns.log`) has not been provided, so the record/target is **NOT VERIFIED**. Owner also ran `sudo ss -lntp | grep -E ':(80|443)\\b' || true` with empty output, indicating no TCP listeners on those ports in the observed host socket table. `nginx -v` and `systemctl status nginx` reported Nginx not installed. This is a safe pre-install baseline, not HTTPS/DNS acceptance. Ports 8080 and 8081 remain outside scope and untouched.

**Next safe sequence:** inspect DNS A/AAAA/CNAME and authoritative NS, verify actual VPS public IP with provider, confirm cloud firewall; install Nginx and Certbot through approved Ubuntu packages after capturing package/disk/backup status; retain loopback-only API and PostgreSQL. Opening public 80/443 and issuing a certificate require correct DNS and second-session SSH continuity. Never switch application to Development to serve a public host, nor mark Step 04 accepted before verified HTTPS, redirects and renewal. 

### Step 04 operator evidence — DNS authority and UFW opening (2026-11-04)

Owner-provided `dns.log` reports apex authoritative NS: `omid.shetabanhost.com`, `arta.shetabanhost.com`, `yekta.shetabanhost.com`. `dig +short tax.argin.ir A`, `AAAA`, `CNAME`, and `CAA` showed **no answer** on the VPS resolver as observed, so no tax subdomain target is configured yet. This suggests DNS zone changes should be made at the actual authoritative ShetabanHost-operated DNS control plane; **cPanel zone edit must be confirmed authoritative** (not assumed from brand labels).

Owner already executed `sudo ufw allow 80/tcp` and `sudo ufw allow 443/tcp`; UFW reported rules added for IPv4 and IPv6. SSH 22 and unrelated 8080/8081 must remain unchanged. This confirms rules were inserted, **not** that external reachability, Nginx installation, active listeners or provider-level firewall access have been tested. Avoid claiming HTTPS is available.

Next operator actions: confirm VPS public IPv4 from the provider and compare with its observed management SSH target (historically `109.70.78.95`), then create only the `tax` A record with TTL 300 at the authoritative DNS zone, leaving apex, NS, mail and unrelated records untouched. No AAAA unless IPv6 is intentionally configured. Validate authoritative response via `dig @omid.shetabanhost.com tax.argin.ir A +short` and public resolvers; note DNS propagation. Install and verify Nginx + Certbot if not yet installed, ensuring Nginx default site has no conflicting effect on other software. Stage a dedicated `server_name tax.argin.ir` maintenance-only vhost before ACME; issue cert only when domain resolves to confirmed VPS IP and port 80 responds from public internet; test TLS and renewal. Do not start development-mode API publicly or submit any tax invoice in Step 04.

### Step 04 — DNS A registration owner confirmation (2026-11-04)

Owner confirms `tax.argin.ir` A record was created successfully at the DNS management provider. The target IPv4 and propagation were not independently observed in the conversation; authoritative and public `dig` checks, plus comparison to provider-verified VPS public IP, remain required before invoking ACME. UFW 80/443 openings are already reported. The next runbook is: install Nginx/Certbot if absent, configure a tax-only maintenance virtual host on port 80 (do not change unrelated 8080/8081), validate `nginx -t`, confirm external HTTP 200, then request `certbot --nginx -d tax.argin.ir --redirect`. Verify HTTPS hostname, HTTP redirect and certbot renew dry run. Do not expose Development-mode API. Step 04 is still open pending observed tests.

## 11. Step 04 owner signoff and Step 05 reproducible release package (2026-11-04)

**Step 04 accepted:** Owner reports successful DNS A registration and completed Nginx installation, Nginx configuration check, HTTP-to-HTTPS redirect, HTTPS response, certificate issuance and `certbot renew --dry-run`. This is **owner-reported PASS** (not independent API/browser check); no raw outputs, certificate fingerprint, issuance dates or DNS A answers retained in chat. The existing temporary maintenance host may remain until a secured Production app is available. DNS/certificate renew checks should be repeated in final Ubuntu acceptance; no tax authority connection/send was tested in Step 04.

**Step 05 scope:** reproducible release compilation, test and non-deployed packaging. Files added: `scripts/phase18-step05-build-release.sh`; `.gitignore` ignores `artifacts/phase18-step03/`, `phase18-step04/`, `phase18-step05/` (private logs and packages must never be committed). The script (1) confirms correct Phase18 branch and clean tracked tree, (2) restores/builds/tests .NET Release then publishes API, (3) runs locked `npm ci`, all web tests, typecheck, production build, (4) stages API and Next.js build plus package manifests, (5) produces SHA256 hashes with macOS/Linux compatibility, and (6) reports source-backed Production deployment blockers. No database connection string, secret, TLS credential, official API request or web service is passed to it by the script. It writes artifacts only to `artifacts/phase18-step05/<git-sha>/<UTC-stamp>/` locally; the host remains untouched.

**Verified source blockers, MUST be resolved before ANY public web application release:**
- `src/ArginTaxPayer.Api/Program.cs`: `identityDevelopmentEnabled = builder.Environment.IsDevelopment() && connectionStringPresent`; most DI services and real API routes (including invoices/signing/identity/dashboards) are wrapped by `if(identityDevelopmentEnabled)`. Simply setting `ASPNETCORE_ENVIRONMENT=Production` prevents required APIs.
- `apps/web/lib/identity.ts`: `identityUiEnabled()` requires `NODE_ENV === "development"`. Normal Next.js production builds cannot authenticate. Existing Next.js proxy routes call `/api/dev/*`; these paths require a security review/refactor rather than simply turning on development endpoints.
- Keep `ASPNETCORE_ENVIRONMENT=Production`, `DOTNET_ENVIRONMENT=Production`, and Next.js `NODE_ENV=production`. Never work around these blockers with publicly exposed Development mode.
- `appsettings.Production.json` currently contains only `DeploymentLabel=production`; actual secure config, credentials, host validation and database environment controls are Steps 06–11.
- Build scripts only scan obvious secret file **names**. Do not consider this a comprehensive secret scanner; output could incorporate build-time environment values. Audit built contents and storage permissions before deployment and never upload artifacts to the public repo.

**Step 05 execution (local macOS first; then Ubuntu at EXACT same commit):**

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
bash scripts/phase18-step05-build-release.sh
```

For Ubuntu access user-mandated interactive AutoSSH:
```bash
AUTOSSH_GATETIME=0 autossh -M 0 \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  argindev@109.70.78.95
```

Then on Ubuntu:
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git rev-parse HEAD
bash scripts/phase18-step05-build-release.sh
```

The same Git commit must be used, but macOS and Ubuntu build artifact SHA256 values are **not expected to be bit-identical** across OS: compare source SHA, tests, manifest, version, build outputs and runtime behavior; reproducible byte-for-byte build claims require a separate pinned identical build environment.

**Acceptance:** macOS and Ubuntu automated tests plus Release builds PASS; package manifests and hashes recorded; no tracked modifications or leaked secret files; staging files remain off public Internet. The source-backed Production DI/identity restrictions remain **hard go-live blockers**, and require separate code changes, security review and Production-mode integration tests (start in Step 05 follow-up and complete before Steps 06/11). No false assertion that a successful build alone means the application is deployable.

**Current evidence:** Implementation of script and documentation committed only. macOS/Ubuntu build/test command outputs **not yet supplied**; Step05 runtime verification remains OPEN. No files installed on VPS production paths and Nginx continues as configured in Step04.

### Step 05 — owner-provided macOS acceptance manifest (2026-11-04)

Owner ran `scripts/phase18-step05-build-release.sh` on macOS. Provided `manifest.tsv`:
- PASS: `dotnet-restore`, `dotnet-release-build`, `dotnet-release-tests`, `dotnet-publish`, `web-install`, `web-tests`, `web-typecheck`, `web-production-build`.
- `packaging = BUILT_NOT_DEPLOYED` (expected).
- `api-prod-routes = BLOCKED_DEV_GATE`; `web-prod-auth = BLOCKED_DEV_GATE` (expected safety blockers).

**Interpretation:** macOS compilation/packaging completed; **Production functional acceptance FAIL/BLOCKED**, Ubuntu same-SHA acceptance still unreported. Do not assign an accepted macOS SHA solely from this manifest if its SHA is not included; read `build-info.txt` first. Do not start public app, enable real tax authority credentials, or change `NODE_ENV`/`ASPNETCORE_ENVIRONMENT` to Development as a workaround.

**Source audit expansion:** `src/ArginTaxPayer.Api/Identity/IdentityDevelopmentEndpoints.cs` explicitly defines `/api/dev/identity` and uses `IsLoopback(http)` checks, while `Phase10InvoiceDispatchDevelopmentEndpoints.cs` similarly maps `/api/dev/taxpayers/.../dispatch` with `Local(http)` restrictions. Next.js proxies call these via server-to-server loopback. Therefore a secure production refactor must classify **individual routes** by capabilities and authorization, not simply remove `if(identityDevelopmentEnabled)`, change `identityUiEnabled()`, or expose existing `/api/dev/*` paths to the public Internet. The Next.js BFF may communicate with the loopback API but must enforce own origin/session/auth policies, with the API binding loopback and independently validating bearer tokens and taxpayer ownership.

**Required remediation before go-live:** establish production-safe DI registration, classify/rename API routes (identity, billing, signing and tax actions), remove/restrict development registration, diagnostics and fixture/bypass routes, review MFA/cookie/CSRF/session boundaries, create negative tests for public network endpoints and cross-tenant actions, and then re-enable necessary Next.js BFF functionality with a fail-closed production config. Versioned Sandbox/live fiscal-memory routing must be validated separately in Step09/10. Any changes must preserve local Development behavior and Phase10–15 regression. No official API call, DB migration, or deployment is authorized merely by this manifest.

### Step 05 remediation implementation — guarded Production API + web (2026-11-04)

**Owner instruction:** Continue and complete the two macOS `BLOCKED_DEV_GATE` remediation items. Source changes committed on the Phase18 branch; **runtime tests have NOT been executed here** (GitHub source inspection only; automated QA on macOS/Ubuntu still required).

**Backend** `src/ArginTaxPayer.Api/Program.cs`:
- Added explicit, default-off `ArginTaxPayer:EnableOperationalEndpoints` requirement when `IHostEnvironment.IsProduction()`; no automatic enabling from merely choosing Production or having a DB connection.
- Production opt-in requires DB connection, rejects the test DB name, enforces the dedicated bind URL `http://127.0.0.1:15090`, and rejects Kestrel endpoint override configuration. Port 15090 differs from development 15089. Do not expose this port in Nginx or UFW.
- Rejects `Identity:AllowDevelopmentRegistration=true` and `Identity:OtpDeliveryMode=LocalFile` for this Production mode.
- Registers existing operational DI and API endpoints only for Development-with-DB or explicitly opted-in Production. Development OpenAPI and `/health/dev-identity` diagnostics stay inside the Development-only branch.
- Adds additional server-side non-loopback denial for internal `/api/dev` routes, independently of any reverse proxy headers, while preserving existing per-endpoint bearer/tenant checks. The old internal route names are a compatibility bridge for BFF only, **not public API contracts**; audit and migration to dedicated non-dev names remain Step 06/11 work. A route becoming registered in Production does **not** certify its production readiness or actual authority routing.

**Frontend** `apps/web/lib/identity.ts`, `apps/web/lib/identity-origin.mjs`, `apps/web/app/api/identity/[...path]/route.ts`:
- Existing Development mode preserved.
- Production requires `ARGIN_PRODUCTION_UI_ENABLED=true`, `ARGIN_IDENTITY_UI_ENABLED=true`, and `ARGIN_PUBLIC_BASE_URL=https://tax.argin.ir` to opt in. The internal API origin in Production is locked to `http://127.0.0.1:15090` (set `ARGIN_IDENTITY_API_INTERNAL_BASE_URL` accordingly at deployment).
- Browser POST Origin must match exact `https://tax.argin.ir` with Host `tax.argin.ir`, not an arbitrary header, HTTPS downgrade, or loopback browser origin. This accounts for internal HTTP Next.js behind HTTPS Nginx.
- Authentication cookies are HttpOnly, SameSite=Strict and now **Secure in Production**.
- `apps/web/tests/identity-ui.test.mjs` assertions updated; new `apps/web/tests/phase18-production-boundaries.test.mjs` covers production flags, registration prevention, loopback/port constraints, strict HTTPS origin and development origin regression.

**Release script:** `scripts/phase18-step05-build-release.sh` now distinguishes source-gate implementation (`SOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING`) from the old `BLOCKED_DEV_GATE` markers. It **always** retains `production-deployment=BLOCKED_PENDING_LIVE_RUNTIME_SECURITY_ACCEPTANCE`; packaging must not start or deploy services.

**Production deployment (for future configuration in Step06, not permission to run now):**
```text
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:15090
ArginTaxPayer__EnableOperationalEndpoints=true
Identity__AllowDevelopmentRegistration=false
# Backend secrets and dedicated real DB configuration supplied through protected env/credentials.

NODE_ENV=production
ARGIN_IDENTITY_UI_ENABLED=true
ARGIN_PRODUCTION_UI_ENABLED=true
ARGIN_PUBLIC_BASE_URL=https://tax.argin.ir
ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15090
# Next.js must bind only 127.0.0.1:3000 or separately assigned internal production port.
```
**IMPORTANT:** Do not place actual production DB strings, secret key material, provider credentials or private keys in this document/GitHub; do not start this configuration until security and runtime tests, DB isolation and secrets recovery are accepted. The final decision on port 3000 vs a different dedicated web port belongs to Step06 and must not disturb the existing dev runner.

**Remaining acceptance:** Pull the new HEAD on macOS and Ubuntu and run `bash scripts/phase18-step05-build-release.sh` on both at the exact same SHA. Require all tests, typecheck, publish and production build PASS, inspect `phase18-production-boundaries.test.mjs` results, verify `/api/dev/*` remote 404, `/health/dev-identity` and OpenAPI 404 in Production, test login/MFA/tenant isolation via HTTPS BFF with valid authorized test accounts and *without issuing an actual tax invoice*. Only then can Step05 source and runtime gates be accepted. **Live Production tax API and per-memory routing still require Steps09/10/15**. The user's prior macOS PASS manifest predates these code changes and cannot be reused as post-fix acceptance evidence.

### Step 05 — macOS post-remediation manifest reported by owner (2026-11-04 08:24 UTC)

Owner supplied the complete macOS manifest located at `artifacts/phase18-step05/$(git rev-parse HEAD)/20261104T082420Z/manifest.tsv`. PASS: `dotnet-restore`, `dotnet-release-build`, `dotnet-release-tests`, `dotnet-publish`, `web-install`, `web-tests`, `web-typecheck`, `web-production-build`. `packaging=BUILT_NOT_DEPLOYED`; `api-prod-routes=SOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING`; `web-prod-auth=SOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING`; `production-deployment=BLOCKED_PENDING_LIVE_RUNTIME_SECURITY_ACCEPTANCE`. Accept macOS **source/build/test/package stage only**; no runtime production auth, security, environment isolation, official tax authority send, or Ubuntu same-SHA PASS has yet been provided. The actual git SHA was not printed as a literal in this output (the path contains shell command substitution), so obtain `git rev-parse HEAD` on both hosts before claiming same SHA. No public deployment authorized.

### Step 05 — Ubuntu post-remediation manifest reported by owner (2026-11-04)

Owner provided Ubuntu `manifest.tsv` containing PASS for `dotnet-restore`, `dotnet-release-build`, `dotnet-release-tests`, `dotnet-publish`, `web-install`, `web-tests`, `web-typecheck`, and `web-production-build`. Packaging `BUILT_NOT_DEPLOYED`; `api-prod-routes=SOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING`; `web-prod-auth=SOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING`; `production-deployment=BLOCKED_PENDING_LIVE_RUNTIME_SECURITY_ACCEPTANCE`. **Ubuntu build/test/package accepted** on owner-reported manifest, matching macOS check outcome. SHA was not included in either owner-provided manifest, so strict same-SHA comparison remains unverified; must compare `build-info.txt` `sha=` fields for respective runs, not current `git rev-parse HEAD` after documentation-only commits.

**Next gate (not yet executed):** production-mode runtime health and refusal checks, confirming loopback-only API binding, no OpenAPI/dev diagnostics, non-loopback denial, exact HTTPS Origin, Secure/HttpOnly/SameSite cookies, MFA and tenant authorization, no sensitive logs or official invoice submission. Start only with dedicated isolated credentials/database and a controlled local-only staging configuration; never redirect live tax domain to dev services. Active production Nginx remains separate. Step 05 publication gate stays BLOCKED pending security acceptance; Phase18 Steps06–15 remain OPEN.

### Step 05 — same-SHA cross-platform build accepted; isolated runtime negative QA prepared (2026-11-04)

Owner confirmed build directories exist for both macOS `artifacts/phase18-step05/36821909abcbcc29ce262229e25a117666314863/20261104T085329Z` and Ubuntu `artifacts/phase18-step05/36821909abcbcc29ce262229e25a117666314863/20261104T085335Z`. Owner subsequently approved progressing to the next task and reported both manifests OK. Earlier explicitly provided macOS and Ubuntu manifests contained eight PASS entries each. **Cross-platform compilation/package gate accepted (owner-reported) for Git SHA `36821909abcbcc29ce262229e25a117666314863`**. Node versions differ between hosts (macOS v24.18.0, Ubuntu v22.23.3 in earlier runs); runtime parity should be pinned before final release. Production functional/security gate is still BLOCKED.

**Next independent stage:** added `scripts/phase18-step05-production-negative-smoke.sh` and ignored `artifacts/phase18-step05-runtime/`. It performs a new local Release publish, launches API with `ASPNETCORE_ENVIRONMENT=Production` and `DOTNET_ENVIRONMENT=Production` on reserved high port `127.0.0.1:15991`, `ArginTaxPayer__EnableOperationalEndpoints=false` (never enables invoice workflows), and overrides the DB connection with dummy configuration targeting an empty private Unix socket directory. Production requires a configured connection even with operational routes disabled; this socket has no PostgreSQL server, so no real database or TCP listener is contacted. Checks HTTP200 `/health/live`, HTTP503 `/health/ready` with the database unavailable, HTTP404 for development identity diagnostic and OpenAPI, HTTP404 for internal identity/authorization/invoice routes with operational endpoints disabled. Cleans up only the process it launches; does not modify Nginx, UFW, DNS, systemd, databases, production credentials, fiscal memories, or authority endpoints. Private logs and manifest are under `artifacts/phase18-step05-runtime/<sha>/<timestamp>/`.

**Run on macOS FIRST, then Ubuntu after fetching the *same* new SHA:**
```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git rev-parse HEAD
bash scripts/phase18-step05-production-negative-smoke.sh
```
Before running, verify that port `15991` is not assigned to another service and current tracked worktree is clean. The script fails closed if busy. Do not run with sudo; if there is a failure, inspect local private logs, do not publish secrets.

**Caveat:** Static source and negative smoke do not prove operational Production enabled, legitimate login, MFA, authorization, BFF CSRF/cookies, production database isolation, official authority Sandbox/Production per-fiscal-memory routing, idempotency or rollback. These need controlled isolated DB fixtures, real browser & integration tests, and subsequent Ph18 steps. The public Nginx maintenance host stays unchanged, and no real tax invoices are sent. New commit for runtime smoke invalidates prior same-SHA build parity by Git HEAD but does NOT invalidate accepted historical gate for the previous tested SHA; final release must be retested at its own HEAD.

### Step 05 — Production-mode API integration test harness (2026-11-04)

Following owner approval to implement integration acceptance, added:
- `tests/ArginTaxPayer.Tests/Phase18ProductionIsolationTests.cs`: WebApplicationFactory tests starting the actual API composition with `Production` environment, operational endpoints **explicitly enabled**, configured HTTPS public URL and dedicated loopback URL `127.0.0.1:15090`; replaces the database connection in host configuration with intentionally unreachable `127.0.0.1:15992`, database `phase18_unreachable`, SSL Require, and disables real SMS and legacy Sandbox invoice dispatch configuration. It does **not** create or migrate any database or send invoices. Checks external (non-loopback) caller receives 404 for internal `/api/dev` routes, no production OpenAPI/dev health route, liveness 200; for loopback caller ensures development registration is disabled (404) even though other operational routes were registered.
- `scripts/phase18-step05-production-integration.sh`: guarded runner requiring a clean tracked tree on `phase/18-production-deployment`, executes xUnit Production isolation test and Next.js production origin/cookie and identity regression tests; writes local-only logs under `artifacts/phase18-step05-integration/<sha>/<utc>`. This directory is gitignored.
- This is a **source implementation only**; no tool in this conversation executed the macOS or Ubuntu xUnit/Next.js tests. Operator acceptance evidence is still required at the same SHA.

**Run macOS first, Ubuntu second on identical SHA:**
```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git rev-parse HEAD
bash scripts/phase18-step05-production-integration.sh
```
If failed, inspect only the specific private log under `artifacts/phase18-step05-integration/`, never post secrets. `dotnet test` exercises an ephemeral TestServer in-process, not public ports, Docker, or live databases. A production-mode startup failure means remediate and rerun before acceptance.

**Hard remaining boundary:** This is *not* a real database-backed Production MFA/tenancy test. Such a test needs a newly provisioned, uniquely named, disposable PostgreSQL test database with a verified SSL connection configuration and safe isolated fixtures; never reuse the shared `argintaxpayer_test`, customers' actual DB, or fiscal memory credentials. It also needs legitimate route enrollment, tenant-isolation checks, browser BFF login and cookie/CSRF tests, a verified cleanup strategy, and absolutely no actual tax authority send. Do not claim Step05 final acceptance or production go-live from these tests alone. Public Nginx continues maintenance-only. Final release gate remains BLOCKED.

### Step 05 — owner confirms macOS and Ubuntu integration parity (2026-11-04)

Owner explicitly reports the **same** acceptance manifest on macOS and Ubuntu:
- `production-api-isolation PASS`
- `web-production-boundaries PASS`
- `web-identity-regression PASS`
- `production_mfa_tenancy PENDING_DEDICATED_DB`
- `production_go_live BLOCKED`

Accept the three isolated Production integration tests as **PASS (owner-reported on both platforms)**. The manifests as quoted do not contain a literal SHA; do not claim same-SHA parity for this new integration test execution until the SHA metadata is checked. The remaining gate is an independent and disposable PostgreSQL environment, TLS-verified connection, no real users/taxpayer data, controlled fixture setup/teardown, Production-authentication/MFA and tenant isolation, cookie/CSRF browser checks, without contacting tax authority. Existing `argintaxpayer_test` must not be reclassified or reused as Production database. Keep operational publishing disabled and Nginx maintenance page unchanged. Step05 is NOT yet finally accepted.

### Step 05 — dedicated disposable PostgreSQL MFA/role acceptance harness (2026-11-04)

Owner approved continuing the remaining Step05 database-backed security QA. Added **source only**, not yet validated by execution:
- `tests/ArginTaxPayer.Tests/Phase18DisposableDatabaseAcceptanceTests.cs` — explicit opt-in `RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1`. The test **refuses** any database other than the exact `argin_phase18_acceptance` name with exact `phase18_acceptance` PostgreSQL role on loopback (`localhost`/`127.0.0.1`) and `SslMode=VerifyFull`. Operator must provide a validated trusted Postgres certificate suitable for the chosen hostname. It checks no pending EF migrations and **zero identity users** before inserting synthetic, disposable identities. Tests: password-only login blocked for Level1/Level2, customer login and forbidden admin actions, MFA enrollment and recovery-code challenge, Level1-only ManageLevel2 capability, Level2 ReadAdministration without ManageLevel2, no arbitrary privilege creation, token logout and MFA challenge reuse. No tax authority calls, external SMS, invoice send, Nginx changes, or shared PostgreSQL test DB accesses are performed by this test.
- `scripts/phase18-step05-disposable-db-acceptance.sh` — refuses without explicit opt-in and separately supplied private connection string; executes only targeted xUnit fixture. It never creates, migrates or drops DBs. Reports private logs to `artifacts/phase18-step05-db/<sha>/<utc>`, ignored by Git. Preserve log confidentiality, since integration errors could include infrastructure details.

**Operator DB prerequisite and risk gates (MUST NOT skip):**
1. Use a **new disposable database** created manually by a privileged DB operator. Database and role exactly as specified, dedicated local Postgres listener with proper TLS hostname identity and CA validation (not just `SslMode=Require`), no shared customer, dev, staging or `argintaxpayer_test` database. Verify role has permissions only in this new DB, no superuser or access to existing databases. Do not use root role to run tests.
2. On the new empty database only, apply the complete EF schema/migrations using documented migration tooling with an **explicit connection** pointing to `argin_phase18_acceptance`; verify backup/snapshot and confirm the exact target before any migration. Never execute migration commands against any existing project database as part of this acceptance.
3. Secrets belong in an owner-controlled, mode-600 environment file/session and must never enter command history, a public PR, logs, assistant chat, or `appsettings.*.json`. An example *without secrets*:
```bash
# Run in a private shell where PHASE18_DISPOSABLE_DB_CONNECTION has been securely set:
export RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
bash scripts/phase18-step05-disposable-db-acceptance.sh
```
4. The test deliberately retains synthetic fixtures in the disposable DB for audit. After acceptance, explicitly destroy that **dedicated** DB and role, only after verifying exact name and no other dependent resources. There is no automated database drop.
5. Run macOS through its existing protected localhost:15432 tunnel only if the TLS certificate's hostname validation can be satisfied; otherwise run on Ubuntu loopback on the dedicated Postgres instance and report macOS as NOT RUN for the database gate. Do not weaken TLS or reconfigure the shared test DB to work around certificates.

**Important limitations:** Test does not yet prove real browser BFF cookie/session/CSRF behavior, cross-tenant ownership of fiscal entities, live Production tax authority routing, or the TLS certificate configuration of a future production database. Those remain separate **BLOCKED** gates. Step05 is **NOT FINALLY ACCEPTED**. No fabricated database or credential setup is claimed. Existing Nginx tax domain continues to serve its separate HTTPS maintenance site.

### Step 05 — implementation: fully separate TLS PostgreSQL 17 QA instance on Ubuntu (2026-11-04)

Owner authorized creating a **new disposable Docker database** on the existing Ubuntu VPS after confirmed existing `postgres-postgres-1` is PostgreSQL 17, bound to `127.0.0.1:5432`, and has `SHOW ssl = off`. Existing `argintaxpayer-adminer` is on `127.0.0.1:8081`. Existing DB and Adminer must never be modified to satisfy acceptance TLS.

Source implementation:
- `ops/phase18-disposable-db/compose.yaml`: dedicated Docker Compose project `argintaxpayer-phase18-qa`, separate PostgreSQL 17 container `argintaxpayer-phase18-postgres`, independent named volume `phase18_pg_data`, host binding `127.0.0.1:15433:5432`, no restart policy and TLS enabled.
- `ops/phase18-disposable-db/init/01-phase18-init.sh`: fresh-volume-only PostgreSQL initialization. Private bootstrap role and a separate `phase18_acceptance` login with `NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION`; creates `argin_phase18_acceptance` owned by that role. Does not alter existing PostgreSQL container.
- `scripts/phase18-step05-isolated-postgres-up.sh`: Ubuntu-only explicit provisioning, requires `sudo docker compose`, rejects already-running container, occupied 15433 or prior private directory. Generates locally random secrets and a short-lived self-signed **testing CA**; signs localhost/127.0.0.1 server certificate; CA private key is not mounted into container. Saves secrets in gitignored mode-600 `artifacts/phase18-step05-db/isolated/compose.env`; only dedicated TLS files are chowned to container postgres UID 999. The key and local CA are **not suitable for Production**. Leaves prior Docker resources untouched.
- `scripts/phase18-step05-isolated-postgres-migrate-test.sh`: checks PostgreSQL `VerifyFull` using generated CA, exact role, exact DB, authenticated SSL and absence of privileged role flags. Refuses non-empty foreign schemas and requires `dotnet ef`. Applies complete EF migrations *only* to isolated acceptance database using explicit connection string. Then executes the guarded disposable DB MFA/RBAC acceptance test. All logs stay local in ignored artifacts. No data is automatically deleted.
- Existing xUnit disposable DB admission has strengthened guard requiring port `15433` and explicit trust certificate in addition to exact DB, role, local hostname and `SslMode=VerifyFull`.

**Execution only on Ubuntu, AFTER pulling same phase branch commit and verifying build:**
```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git rev-parse HEAD
bash scripts/phase18-step05-isolated-postgres-up.sh
sudo docker ps --filter name=argintaxpayer-phase18-postgres --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
# Only after the independent container is healthy:
bash scripts/phase18-step05-isolated-postgres-migrate-test.sh
```
Do **not** run these on macOS; the macOS UI/build tests already run independently. Do not add `argindev` to the Docker group merely for acceptance. The scripts expect `sudo` for Docker. If the TLS setup, tools, or migrations fail, stop and inspect only private logs, do not weaken VerifyFull or switch to existing DB.

**Cleanup is intentionally operator-controlled:** `docker compose down --volumes` against this specific Compose project would irreversibly remove *only this QA project's* named volume, but should be executed only after explicit validation of container/project names, user approval and acceptance evidence. Generated private material under ignored artifacts needs separate manual cleanup; never run a broad `docker volume prune` or `docker system prune`. There is no automatic cleanup in scripts.

**Acceptance status:** SOURCE_IMPLEMENTED_NOT_EXECUTED. Migration and MFA database tests on actual VPS still need owner execution and PASS outputs, then cross-tenant fiscal access and browser session/CSRF still pending. All Production Go-Live remains BLOCKED, no Tax Authority sends, no public DNS/Nginx changes.

### Step 05 — fresh PostgreSQL Migration failure 42P01 / catalog order remediation (2026-11-04)

Owner executed `scripts/phase18-step05-isolated-postgres-migrate-test.sh` on Ubuntu. The dedicated `argin_phase18_acceptance` database passed the script's VerifyFull TLS, exact database/role and least-privilege preflight, but `dotnet ef database update` failed: PostgreSQL error `42P01: relation "official_goods_service_entries" does not exist`. Investigation confirmed `20261016082706_AddOfficialGoodsCodeFormat` attempted to add a CHECK to the table *before* `20261016120000_AddPhase06CatalogFoundations` creates that table.

Code correction:
- Preserve the **historical migration ID** `20261016082706` but turn its Up/Down into deliberately inert ordering placeholders for fresh installs (existing DBs with this migration already applied are unaffected).
- Create `ck_official_goods_code_format` immediately *after* the `official_goods_service_entries` table is created in `20261016120000_AddPhase06CatalogFoundations`. This preserves exact constraint name and 13-digit expression verified by `20261016150000_ValidateOfficialGoodsServiceIdentifiers`. Existing DB migration histories do not rerun the edited migrations.
- Add `Phase18FreshCatalogMigrationOrderTests` to assert that early migration is inert, table creation precedes the constraint, and later validation remains in place.

**Status: code committed; no test execution available via the GitHub editing connection.** Re-run on Ubuntu **only**, with the existing dedicated QA container left intact, after `git pull --ff-only`; no need to recreate container, reset/drop any DB, or weaken TLS. First:
```bash
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --filter FullyQualifiedName~Phase18FreshCatalogMigrationOrderTests
```
Then, only if PASS:
```bash
bash scripts/phase18-step05-isolated-postgres-migrate-test.sh
```
The script rechecks the exact TLS DB/role and accepts a nonempty QA schema only if EF migration history exists. Each PostgreSQL EF migration is transactionally committed, so the failing migration should remain pending; if the retry fails, stop and review its *new* private migration log rather than dropping any database. Do not apply these historical migration changes to a customer DB without a separate compatibility review.

Production deployment still BLOCKED. This is a fix for initial schema creation, not approval for public data or tax dispatch.

### Step 05 — Phase09/10 historical test-only migration admission gate (2026-11-04)

Owner's isolated, TLS-verified PostgreSQL test passed database identity and least-privilege checks but failed when applying `20261020181500_AddPhase09SigningMetadata`, with PostgreSQL `P0001: Phase09 draft requires argintaxpayer_test`. Source inspection found **ten** legacy migrations in Phase09/10 that restrict the target to `argintaxpayer_test`. Single-migration fixes would merely fail later.

For these ten migrations only, the prior SQL fail-closed guard is preserved as an explicitly bounded disjunction:
- Existing, historically allowed `current_database()='argintaxpayer_test'` unchanged.
- A **new narrow alternative** requiring **all** of `current_database()='argin_phase18_acceptance'`, `current_user='phase18_acceptance'` and the PostgreSQL backend's `pg_stat_ssl.ssl = true` for the current `pg_backend_pid()`.
- Every other database and role is still refused. The guarded, Ubuntu-only Phase18 script verifies exact role/database, least-privilege, loopback port 15433, CA-validated `VerifyFull` TLS before invoking the EF migrator. SSL flag alone in SQL **does not prove VerifyFull**; the external runner's verification is required and must not be bypassed.
- Historical migration IDs remain unchanged; do not apply these code edits to existing production/customer databases without independent migration-history and deployment compatibility review. Existing applied migrations are not automatically rerun.
- Added xUnit `Phase18LegacyMigrationAdmissionTests` checking all ten migration UpOperations preserve narrow database, role and SSL conditions.

**Source is committed, but neither the new tests nor the resumed migration have been executed by the assistant.** Owner's next step on Ubuntu:
```bash
git fetch origin
git pull --ff-only origin phase/18-production-deployment
dotnet build ArginTaxPayer.sln -c Release
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --filter 'FullyQualifiedName~Phase18LegacyMigrationAdmissionTests|FullyQualifiedName~Phase18FreshCatalogMigrationOrderTests'
# Only if both commands PASS:
bash scripts/phase18-step05-isolated-postgres-migrate-test.sh
```
Do not reset/drop or migrate the shared `argintaxpayer_test`, normal VPS `postgres-postgres-1` database, or any future Production database. Production deployment remains **BLOCKED**; MFA/tenant/browser acceptance not yet established.

### Step 05 — Ubuntu dedicated TLS DB migration and MFA/RBAC acceptance PASS (2026-11-04)

Operator supplied command output for Git SHA `4a6958638c701a7523d8243555e454457214388e`. `bash scripts/phase18-step05-isolated-postgres-migrate-test.sh` performed the bounded exact-database/role/SSL/TLS verification and returned:
- `[PASS] Dedicated QA database migrations completed.`
- `[PASS] disposable DB MFA, sessions, roles`
- `[PASS] Dedicated QA MFA and role acceptance`
- `[DONE] Existing PostgreSQL and public site unchanged; test DB retained for explicit cleanup.`

Private manifest (Ubuntu, operator-provided): `artifacts/phase18-step05-db/4a6958638c701a7523d8243555e454457214388e/20261104T104455Z/manifest.tsv`. This indicates **Ubuntu isolated PostgreSQL schema migration + service-layer MFA/RBAC fixture acceptance PASS**. The DB remains for controlled cleanup after all acceptance evidence. This does **not** establish that production BFF login cookies, browser CSRF defenses, HTTP-level authorization or actual cross-tenant taxpayer/invoice isolation have passed. The previous integration manifest likewise explicitly left `production_tenant_data_isolation` and `production_bff_browser_cookies` pending.

**Decision:** Advance to Step05 scoped-tenant and browser BFF tests without enabling live tax dispatch, modifying Nginx, using existing customer DB, or deleting the disposable QA database. Production go-live remains BLOCKED until these controls and subsequent Phase18 steps are accepted.

### Step 05 — final security acceptance implementation: tenant scope + real Next.js BFF runtime (2026-11-04)

After successful Ubuntu isolated PostgreSQL VerifyFull migrations and MFA/RBAC acceptance at `4a695863`, owner requested completing Step05.

**Implemented but not yet executed on operator machines:**
1. `tests/ArginTaxPayer.Tests/Phase18TenantIsolationDatabaseTests.cs`: opt-in against *only* `argin_phase18_acceptance` at loopback port 15433 with role `phase18_acceptance` and CA-validated VerifyFull. Creates synthetic Level1, two Level2 and two customer identities, two owned taxpayer records and commercial owner rows. Confirms each customer sees only its own taxpayer; partner A/B see only owned taxpayer; MFA-authenticated Level1 sees both; cross-tenant reads and unknown IDs fail with NotFound; an audited Level1-approved commercial owner transfer via CustomerCommercialOwnershipService.TransferAsync immediately revokes the former partner's read access while the partner token remains unchanged. No genuine taxpayer data or invoice dispatch. Fixture is retained only in the dedicated disposable DB for operator-authorized cleanup.
2. `scripts/phase18-step05-production-bff-runtime.mjs`: real local `next start` on 127.0.0.1:15994 and synthetic HTTP identity backend on 127.0.0.1:15090. **Refuses** if either port is taken; never contacts external services. Verifies Production BFF refuses missing, forged, insecure and foreign Origin/Host; no Set-Cookie on CSRF denial; accepts synthetic same-origin login and sets exactly two Secure/HttpOnly/SameSite=Strict cookies without exposing raw tokens; resolves synthetic cookie identity and checks logout cookie clearing. This is HTTP-level BFF integration with synthetic backend, NOT real human-browser acceptance or real API login.
3. `scripts/phase18-step05-final-security-acceptance.sh`: clean-tree guarded macOS/Ubuntu runner, runs the real Next.js BFF runtime and existing production boundary tests, builds xUnit test project; Ubuntu additionally derives private credentials from preexisting ignored disposable DB environment and runs just the tenant-isolation database integration test. It intentionally does **not** rerun `Phase18DisposableDatabaseAcceptanceTests`, whose seed-on-empty requirement has already passed and would refuse the now-populated disposable database. Generated report: `artifacts/phase18-step05-final/<SHA>/<platform>-<UTC>/manifest.tsv`, gitignored.

**Operator commands (run on both at the same new Git SHA, macOS first):**
```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git rev-parse HEAD
bash scripts/phase18-step05-build-release.sh
bash scripts/phase18-step05-final-security-acceptance.sh
```
On Ubuntu, the dedicated QA PostgreSQL container on 127.0.0.1:15433 and existing CA/private environment must remain available. Do not start/restart services on 15090/15994; the test owns them temporarily and refuses to run if they are already in use. No Nginx, firewall, DNS, authority endpoints, or customer DB changes. A successful manifest should contain `browser-bff-runtime PASS`, `web-boundary-tests PASS`, `dotnet-tenant-compile PASS`, and `tenant-isolation-db PASS` on Ubuntu (`UBUNTU_ONLY` on macOS). The last lines deliberately keep `public-http-auth NOT_TESTED_NO_LIVE_SITE` and `production-go-live BLOCKED_PENDING_SUBSEQUENT_PHASE18_STEPS`.

**Acceptance nuance:** The test asserts live persisted service-level taxpayer read scope, but it does not yet exercise actual invoice-domain cross-tenant HTTP request authorization or authenticated user flows in a real browser with the actual backend. These require distinct later end-to-end acceptance, not invented PASS claims. Since neither operator run has been supplied for the new final runner, mark `STEP05_FINAL_ACCEPTANCE_PENDING`; production publication is prohibited. Upon two-platform PASS and accepted residuals, close Step05 packaging/security test scope and proceed to Step06 isolated Systemd preparation while retaining the maintenance page.

### Step 05 — operator acceptance evidence, close scoped security gate (2026-11-04)

Operator supplied execution results from both macOS and Ubuntu for the final security runner. Ubuntu private manifest at `artifacts/phase18-step05-final/99716b7c2f4875d90002baf4470961f075be9eb3/Linux-20261104T110536Z/manifest.tsv` reports `browser-bff-runtime PASS`, `web-boundary-tests PASS`, `dotnet-tenant-compile PASS`, `tenant-isolation-db PASS`. macOS pasted manifest reports the same first three `PASS` values and `tenant-isolation-db UBUNTU_ONLY`. The actual SHA embedded in the macOS manifest path was not provided; same-SHA macOS/Ubuntu parity remains **unverified** and should be verified before release.

Earlier Ubuntu disposable DB migration + MFA/RBAC on the VerifyFull TLS isolated PostgreSQL also PASS. **Step05 scoped code/build/integration security acceptance: OPERATOR_PASS with explicit residuals.** Persisted tenant read queries and Level1-authorized audited partner transfer passed on disposable DB. Synthetic Next.js production BFF runtime tested cookies, origin/CSRF and logout; this was not a full user-controlled browser with real API.

**Unresolved pre-Go-Live gates, do not mislabel as passing:** `public-http-auth NOT_TESTED_NO_LIVE_SITE`; authenticated real browser-to-backend login, post-login permissions and cookie/CSRF over actual HTTPS reverse proxy; invoice-domain HTTP cross-tenant authorization; separate final deployment checks. `production-go-live BLOCKED_PENDING_SUBSEQUENT_PHASE18_STEPS` remains mandatory. No change to Nginx, DNS, UFW, real production DB, SMS or tax authority dispatch. Proceed next to Phase18 Step06 isolated systemd unit preparation, keeping public maintenance response and fiscal dispatch disabled. Keep disposable QA database for remaining acceptance until explicit operator-approved disposal.

### Step 05 — retained real HTTP + actual API/BFF loopback closure (2026-11-04)

The owner authorized implementing the remaining **non-public** Step05 security QA, while deferring real HTTPS browser/reverse-proxy checks until services are staged in later Phase18 steps. The code now includes:

- `tests/ArginTaxPayer.Tests/Phase18ProductionHttpTenantAcceptanceTests.cs`: opt-in ASP.NET `WebApplicationFactory<Program>` in **Production mode**, with actual routing/middleware and the previously accepted synthetic customer A/B fixture from the **sole** `argin_phase18_acceptance` database on loopback `15433`, with exact `phase18_acceptance` role and TLS `VerifyFull`. It checks customer A/B can read their own taxpayer, are denied other customers' taxpayer resources, unauthenticated/invalid-token denials, admin permission denial, internal-only API denial for simulated nonloopback callers, absence of Production diagnostics/OpenAPI, and **HTTP invoice list** admission (owner 200 vs foreign 404; unauthenticated 401). Synthetic QA customer logins may write only disposable session rows. It does **not** submit/send/mutate invoice data or validate cross-tenant reads of populated invoice documents.
- `scripts/phase18-step05-real-api-bff.mjs`: locally starts the actual **Production .NET API** on `127.0.0.1:15090` and actual **Production Next.js** on `127.0.0.1:15994`, only if both ports are free. Actual customer login from the controlled synthetic fixture via Next.js BFF and backend is checked, including sessions and cookies (Secure/HttpOnly/SameSite=Strict), Origin CSRF rejection and logout cookie deletion. Disallows dev registration and requires DB readiness. The process environment is explicitly constrained so no live inherited application credentials or external signing configuration are supplied. No actual outside browser/proxy is involved, and no tax authority operations are called.
- `scripts/phase18-step05-real-api-acceptance.sh`: guarded **Ubuntu-only** runner, refuses wrong branch or dirty tracked tree, requires existing private QA DB TLS files (without recreating or resetting DB), explicitly checks the exact TLS database/user and reads an existing synthetic customer fixture. Requires an **exact macOS full 40-character SHA** passed as `PHASE18_EXPECTED_SHA` before running anything. Builds a dedicated gitignored API publish directory, runs both real middleware + actual loopback BFF tests, generates private manifest under `artifacts/phase18-step05-e2e/reports/<SHA>/<UTC>/manifest.tsv`. Never touches existing PostgreSQL, Nginx, DNS or other web services; rejects occupied 15090/15994. 
- `.gitignore` excludes all new local QA reports and binaries.

To run macOS SHA/build first:
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
dotnet build tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release
```

Then on Ubuntu **the same SHA**, with existing QA PostgreSQL container and frontend Production build present:
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
# Replace the placeholder with the full hash printed on macOS.
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' bash scripts/phase18-step05-real-api-acceptance.sh
```

Expected private manifest checks: `real-production-http-tenant PASS`, `qa-api-publish PASS`, `real-production-api-bff PASS`. The final manifest explicitly leaves `production-https-browser DEFERRED_TO_SYSTEMD_NGINX`, `invoice-http-tenant-authorization PENDING_FIXTURE_QA` (populated invoice IDOR not yet covered), and `production-go-live BLOCKED`.

**Status: SOURCE_IMPLEMENTED; NO EXECUTION PASS YET** for these new tests. The owner must provide macOS SHA/build and Ubuntu acceptance logs. A prior Step05 test PASS for mocked BFF + service-layer tenant scope remains valid, but cannot be upgraded to real HTTP/BFF PASS before this runner succeeds. Real external HTTPS reverse-proxy browser validation, actual MFA user journey, and populated invoice cross-tenant IDOR remain mandatory pre-Go-Live. Do not publish `tax.argin.ir` or enable tax dispatch to satisfy this QA.

### Step 05 — real Production HTTP / BFF Ubuntu QA PASS (operator evidence, 2026-11-04)

The operator executed `PHASE18_EXPECTED_SHA=fa757a90860f364a99d3f3a207b5c7151ba63c9c bash scripts/phase18-step05-real-api-acceptance.sh` on Ubuntu. The displayed report for exactly that SHA at `artifacts/phase18-step05-e2e/reports/fa757a90860f364a99d3f3a207b5c7151ba63c9c/20261104T112144Z/manifest.tsv` states:

```text
real-production-http-tenant  PASS
qa-api-publish               PASS
real-production-api-bff      PASS
production-https-browser     DEFERRED_TO_SYSTEMD_NGINX
invoice-http-tenant-authorization PENDING_FIXTURE_QA
production-go-live           BLOCKED
```

**Accepted scope:** ASP.NET Core Production middleware with genuine isolated QA PostgreSQL, customer-owned/foreign taxpayer read denial and invoice-list scope, and actual locally running Next.js BFF connected to the actual local .NET API, using only synthetic QA credentials. Migration, MFA/RBAC persistence, earlier synthetic BFF cookie and tenant service tests passed separately.

**Still not accepted:** a populated invoice document cross-tenant IDOR test, an actual browser through the real HTTPS Nginx proxy with a complete administrative MFA journey, and subsequent predeployment checks. Do not promote the three tests into a production release sign-off. Keep `tax.argin.ir` public maintenance isolation, fiscal dispatch disabled, and existing unrelated applications untouched.

The Ubuntu acceptance runner received an exact expected SHA string; an independently captured macOS `git rev-parse HEAD` confirming that SHA has not been included in user-provided evidence, so macOS same-SHA parity remains an **evidence gap** even though its earlier local test manifest passed. Do not state parity is confirmed until documented.

### Step 05 — populated invoice HTTP IDOR closure implementation (2026-11-04)

Following successful operator test of actual Production HTTP tenant and real API+BFF at source SHA `fa757a90860f364a99d3f3a207b5c7151ba63c9c`, the owner requested closure of the **remaining** Step05 work. This change extends `Phase18ProductionHttpTenantAcceptanceTests` to persist two fully synthetic **Draft** invoice_documents + immutable revision snapshots in the same isolated QA PostgreSQL via `EfInvoiceAtomicWriter`, each scoped to its own customer and taxpayer. It then executes actual Production ASP.NET HTTP routes to assert owner GET 200, foreign taxpayer+invoice GET 404, an own-taxpayer + foreign-invoice ID substitution GET 404, missing bearer 401, tenant lists contain only their own synthetic invoice ID, and cross-tenant POST creation returns 404 without passing body validation or creating a record. There is no finalization, signing, authority inquiry, or invoice dispatch. The QA fixtures are only retained in the dedicated disposable database and are explicitly disposable later.

`scripts/phase18-step05-real-api-acceptance.sh` now emits `invoice-http-tenant-authorization PASS_POPULATED_SYNTHETIC_DRAFT_HTTP_IDOR` **only after** the full real HTTP test and actual API/BFF acceptance have passed; it must not be interpreted as PASS before a new operator manifest is received.

**Same-source parity:** `PHASE18_EXPECTED_SHA` ensures the Ubuntu runner checks the supplied hash against Ubuntu HEAD, but this string is still operator-supplied; retain the macOS `git rev-parse HEAD` output and matching compilation/test artifact as independent evidence.

**Operational separation:** Step05 may be closed after this *local* QA passes on Ubuntu and macOS same-source build evidence is recorded. Real public HTTPS + Nginx + full admin MFA browser acceptance is deliberately moved to the **mandatory pre-Go-Live gate after Step06 service staging**, because it cannot be run with a real upstream until Systemd staging exists. It is **not waived** and may not be marked PASS in Step05. `production-go-live BLOCKED` remains mandatory. No migration/production DB changes or source-side Release/Merge at this step.

### Step 05 — FINAL OWNER ACCEPTANCE, same-SHA macOS and Ubuntu (2026-11-04)

**Decision: STEP05 CLOSED / OWNER ACCEPTED for local, isolated, non-public Production readiness.** Owner supplied independently sourced macOS and Ubuntu manifests for exactly the same SHA `2d9a01edb6abfe1bcb7722a741fddea2df38edad`.

macOS private manifest:
`artifacts/phase18-step05-final/2d9a01edb6abfe1bcb7722a741fddea2df38edad/Darwin-20261104T112748Z/manifest.tsv`:
- `browser-bff-runtime PASS`
- `web-boundary-tests PASS`
- `dotnet-tenant-compile PASS`
- `tenant-isolation-db UBUNTU_ONLY` (expected)
- `public-http-auth NOT_TESTED_NO_LIVE_SITE`
- `production-go-live BLOCKED_PENDING_SUBSEQUENT_PHASE18_STEPS`

Ubuntu private manifest:
`artifacts/phase18-step05-e2e/reports/2d9a01edb6abfe1bcb7722a741fddea2df38edad/20261104T112826Z/manifest.tsv`:
- `real-production-http-tenant PASS`
- `qa-api-publish PASS`
- `real-production-api-bff PASS`
- `invoice-http-tenant-authorization PASS_POPULATED_SYNTHETIC_DRAFT_HTTP_IDOR`
- `production-https-browser DEFERRED_TO_SYSTEMD_NGINX`
- `production-go-live BLOCKED`

Older Ubuntu isolated DB VerifyFull PostgreSQL migration/MFA/RBAC and synthetic tenant transfer acceptance also passed. Populated invoice IDOR tested real HTTP GET, filtered lists, and denied foreign POST to the isolated QA DB; no actual fiscal submission, signing, inquiry, SMS or customer data was involved.

**Explicit non-waivable deployment blockers retained:** real public HTTPS/Nginx browser-to-backend login and complete administrative MFA session/CSRF/cookie flow, plus subsequent Step06+ service/deployment checks. Step05 closure **does not authorize enabling** tax dispatch, switching Nginx upstream, taking down maintenance, changing any existing production/shared PostgreSQL, or releasing/merging the Phase18 branch. Keep the dedicated disposable QA PostgreSQL for authorized later acceptance and disposal.

**Next:** Phase18 Step06 Systemd staging/service units with loopback-only sockets and safe rollback, retaining the public maintenance page. Public real-browser acceptance belongs to the pre-Go-Live gate after isolated service staging.

## Step 06 — Systemd staging / supervised Production processes, fail-closed (2026-11-04)

Step05 was signed off after identical macOS+Ubuntu SHA `2d9a01edb6abfe1bcb7722a741fddea2df38edad` and successful isolated QA including synthetic populated invoice IDOR. Step06 source implementation is now prepared under `ops/phase18-systemd/`, with scripts `phase18-step06-systemd-preflight.sh`, `phase18-step06-install-disabled.sh`, `phase18-step06-installed-audit.sh`, `phase18-step06-remove-disabled.sh`, `phase18-step06-local-health.sh` and Node unit contract tests.

**Operator runbook:** [phase-18-step06-systemd.md](./phase-18-step06-systemd.md).

**Acceptance gates:** identical current git SHA Mac and Ubuntu (enforced by Ubuntu preflight); both service unit contracts and Ubuntu `systemd-analyze verify` PASS; Ubuntu loopback ports 15090/15091 free; root installer explicitly invoked with `--install-disabled` and no service name collisions; installed byte-for-byte matching systemd units `DISABLED_AND_INACTIVE`; start approval marker absent; no public Nginx/DB changes. The installer may create a dedicated `argintaxsvc` system identity and empty owned directories but NEVER installs a Production database, starts services, switches release symlink, enables fiscal operations, or routes HTTP through Nginx.

**Status: STEP06 IMPLEMENTED / OWNER ACCEPTANCE PENDING.** Neither the assistant nor the owner has yet executed the new Step06 installation or provided an Ubuntu unit audit. Future actual liveness/readiness/restart runtime checks require approved Production DB+secret custody in Steps07/08 and remain blocked, and external HTTPS browser MFA is still mandatory before Go-Live. Subsequent release/merge remains prohibited until Step18 signoff.

### Step 06 — Ubuntu read-only preflight blocker and isolated Node remediation (2026-11-04)

Operator's `PHASE18_EXPECTED_SHA=c679a5ce44c8272d5c43372a7469ffb504695a27 bash scripts/phase18-step06-systemd-preflight.sh` reported `unit-contract PASS` then refused `System-wide Node.js 20+ required for Next.js Production`. This is an expected safe failure: `argindev`'s user-local Node may be 20+, while the system `/usr/bin/node` is older. **No install, start, public routing, database migration, or service modification took place.**

Corrective implementation: Step06 Web systemd template now runs the root-controlled executable at `/opt/argintaxpayer/runtime/node/bin/node` instead of the global `/usr/bin/env node`; a new explicit `scripts/phase18-step06-install-isolated-node.sh --copy-node /absolute/trusted/node` copies a reviewed existing Node20+ binary into that private directory without using apt or changing other applications' Node versions. Preflight now requires exactly that private executable (root:root, 20+) while separately verifying system ASP.NET Core 9 runtime. Unit contract checks the new path. Read [Step06 Systemd runbook](./phase-18-step06-systemd.md) for commands and safety constraints.

**Acceptance still pending**: operator must run the private Node provisioning (after examining the source path and version), sync latest identical Git SHA on macOS and Ubuntu, rerun read-only preflights, and explicitly install only disabled/inactive services followed by installed audit. Do not report Step06 complete or PASS until those new results arrive. Public site and all production tax endpoints remain blocked.

### Step 06 — Ubuntu disabled Systemd install/audit OWNER PASS (2026-11-04)

Owner supplied Ubuntu installed-audit output for source SHA `0963824eb26f7cebf65cef27ad1134fc2cbb00f4`, stored privately at `artifacts/phase18-step06/0963824eb26f7cebf65cef27ad1134fc2cbb00f4/install-audit-20261104T120515Z/manifest.tsv`. It reports `unit-syntax PASS`, `service-user PASS`, both unit template exact-comparison `PASS`, both units `DISABLED_AND_INACTIVE`, ports 15090 and 15091 `FREE`, `nginx UNCHANGED_BY_STEP06`, `postgresql UNCHANGED_BY_STEP06`, `service-start BLOCKED_UNTIL_LATER_APPROVAL`, and `production-go-live BLOCKED`.

**Decision: STEP06 CLOSED / OWNER ACCEPTED for install-only disabled/inactive Systemd supervision preparation.** This is not evidence of successfully starting real Production API/Next.js processes, live database readiness, restart recovery or external HTTPS browser sessions. These remain future deployment gates after Step07 independent Production PostgreSQL, Step08 secure config/key custody, and later security/functional checks. No activation marker, service enable/start, DNS/Nginx switch, provider charges, authority send, or shared database migration is authorized or claimed. Keep the inactive units and retain disposable QA database unless explicitly cleared.

The logged source SHA was `0963824...`; later Step06 documentation/source changes may have a different HEAD. Before any future template modification or activation, require exact git SHA parity and re-check installed unit equality. Proceed to **Step07 separate Production database and migration dry-run plan**, without changing unrelated PostgreSQL services.

## Step 07 — Isolated Production PostgreSQL (source implementation, 2026-11-04)

User explicitly approved closure of Step06 and authorized implementing Step07. The source now defines a **new separate** PostgreSQL 17 container `argintaxpayer-phase18-production-postgres` with pinned Docker image digest, dedicated named volume, `127.0.0.1:15434` binding and TLS VerifyFull. Database `argintaxpayer_prod` has separate `argin_prod_migrator` and least-privileged `argin_prod_app` credentials, isolated from the shared PostgreSQL `postgres-postgres-1:5432` and existing disposable QA database on `15433`.

Ten historical Phase09/10 migrations previously limited to test/Phase18 QA now additionally admit **only** the exact `argintaxpayer_prod` database with `argin_prod_migrator` and an actual TLS session. No runtime-app migration privilege was added. A strict .NET migration admission test and Node production DB contract test enforce this boundary. All migration scripts remain opt-in; there is **no** automatic migration during application startup or database provisioning.

Source paths:
- `ops/phase18-production-db/compose.yaml`, `ops/phase18-production-db/init/01-init-production.sh`
- `scripts/phase18-step07-source-preflight.sh`
- `scripts/phase18-step07-provision-production-db.sh`
- `scripts/phase18-step07-production-db-audit.sh`
- `scripts/phase18-step07-migration-sql-review.sh`
- `scripts/phase18-step07-backup-restore-drill.sh`
- `scripts/phase18-step07-apply-reviewed-migration.sh`
- `tests/ArginTaxPayer.Tests/Phase18LegacyMigrationAdmissionTests.cs`
- `apps/web/tests/phase18-step07-production-db-contract.test.mjs`

**Detailed runbook:** [Phase18 Step07 Production Database](./phase-18-step07-production-db.md).

**Security/operational gates:** private 0600 secrets and CA; named isolated Docker volume; exact-source SHA parity; VerifyFull and plaintext rejection; migration SQL generated offline, hash matched and manually reviewed; backup and separate recovery-db restore **both before and after initial migration**; no automatic drop/recreate on failure; no real customer identities or unencrypted live-data backup; Step08 production secret custody/rotation and Step12 operational encrypted backups still pending. The initial schema apply will refuse until reviewed SQL hash and pre-migration restore evidence are provided with explicit owner approval.

**Status: STEP07 SOURCE IMPLEMENTED / UBUNTU OPERATOR ACCEPTANCE PENDING.** No live shell execution, Production database creation, Production Migration or restore has been performed by the assistant. Do not mark Step07 PASS or close it until actual owner-provided logs show source tests, new DB provision, role/TLS and non-TLS refusal, manual reviewed migration application, and isolated backup/restore drills. Existing Nginx, maintenance page, disabled systemd units, sandbox/real authority routing and all other databases remain unchanged; Production go-live is BLOCKED.

## Phase18 Step07 owner signoff / Step08 source implementation (2026-11-04)

- **Step07:** Owner explicitly accepted completion. Dedicated PostgreSQL container `argintaxpayer-phase18-production-postgres`, port `127.0.0.1:15434`, distinct volume and limited runtime/migrator roles were reported PASS. Verified read-only role/TLS audit output, initial pre-migration backup/isolated restore PASS; independently reviewed offline EF SQL SHA-256 `181f13e2216e04da42bf0d194d3ea37db45bbb3765995f3e875a7a4528c03346` and operator execution reported `ef_history_count=72` on source SHA `c2fe6c2cf34ddc2364c3fddf9cb8c07dfaa3b0a6`. **Post-migration** audit and **post-migration** restore PASS logs were requested but have not been seen; record them as outstanding additional release evidence rather than implying they were verified. No application services started.
- **Step08 source work:** Added `scripts/phase18-step08-config-custody-preflight.sh`, `scripts/phase18-step08-stage-private-config.sh`, `scripts/phase18-step08-private-config-audit.sh`, `apps/web/tests/phase18-step08-config-custody.test.mjs`, and `docs/phases/phase-18-step08-config-key-custody.md`. The explicit one-time staging script refuses existing private config/CA and writes only root-owned private config with isolated TLS app-role DB credentials and two independent 32-byte base64 cryptographic keys. It keeps identity registration, tax sending, SMS sending, Next UI and web services disabled. No provider secrets are staged and no active VPS service, Nginx or shared Docker resources are touched.
- **Key custody findings:** Identity MFA is AES-256-GCM with `Identity:MfaEncryptionKey`; database wrapping is AES-256-GCM with `SigningKeyProtection:DatabaseMasterKey` and explicit enabled/database provider. API sessions use opaque, DB-backed SHA-256-hashed tokens, not JWT. The root-only configuration is a provisional local custody location, **not** independent backup/escrow; Step12 recovery remains mandatory. The private config and key rotation are never committed or printed. Fiscal-memory-specific Sandbox/live routing remains a Step09/10 gate.
- **Step08 acceptance pending:** same-commit macOS/Ubuntu tests; operator-approved root-only staging (new files only); confidential private config audit; actual controlled restart/decrypt recovery with authorized non-production data; provider/secrets and browser-security checks, and Step12 independent key escrow before go-live. No Step08 PASS, public app startup, Production tax connection, payment or SMS action may be claimed yet.

## Phase18 Step08 owner evidence / Step09 dual-authority audit (2026-11-04)

- **Step08 operator evidence:** Ubuntu source contract and cryptographic unit tests PASS at SHA `49d592c16593d9f89b04f5c682accc8f3f3d5a02`; owner-confirmed macOS tests PASS. Private root-only configuration staging PASS and read-only private configuration audit PASS, including re-audit after new SSH session. Both API and Web systemd units reported `disabled` and `inactive`. Explicit separate key recovery from restarted API, real provider credential checks and escrow remain outstanding release controls, not silently passed.
- **Step09 initial findings:** Existing separate Sandbox table `sandbox_fiscal_memories` and regular `fiscal_memories` exist. Current Sandbox send, inquiry and legal-child workflows enforce Sandbox-specific evidence; several write paths still guard exact database `argintaxpayer_test`, preventing Sandbox functionality on isolated `argintaxpayer_prod`. Current regular `FiscalMemory` has no persisted typed authority-environment property; a unified per-operation environment binding and verified real-authority connector are not yet implemented.
- Added pure `Phase18AuthorityRoutingPolicy` and unit tests for unknown environment, provider mismatch, tenant, fiscal memory and legal-child scope. This foundational domain policy is **not yet wired to DB, send, or inquiry**, and thus grants no operational authority. Added source audit tests, read-only preflight and `docs/phases/phase-18-step09-fiscal-memory-routing.md`.
- **Status:** Step09 implementation not complete; production-go-live remains BLOCKED. Do not bypass historic test DB guards. Additional migrations, server-owned authoritative binding, route enforcement and cross-platform tests are required before Step09 acceptance. No tax authority or billing operations invoked.

## Accelerated deployment track — owner approved (2026-11-04)

The owner requested an accelerated versioned application rollout instead of delaying all domain availability until every Phase18 tax-authority acceptance is complete. This **reorders** the work; it does not silently waive real tax dispatch safety, tenant authorization or key recovery.

A. **Deploy app with provider operations off:** GitHub Actions builds a commit-addressed API/Web release, verifies tests and archives SHA256; the separate stage script writes only a new immutable release directory. After current DB/secrets/security checks, the approved promote script atomically switches the `current` symlink, restarts only the dedicated API/Web units and automatically reverts the application to the previous release if loopback health fails. Nginx's existing tax-only TLS maintenance vhost must be independently switched to loopback Next only after owner approval and browser security checks.

B. **Dual-environment tax acceptance:** Do not bypass existing `argintaxpayer_test` guards; first complete explicit persisted-memory environment binding and test Sandbox on `argintaxpayer_prod`, then authenticated read-only real authority checks, and only then owner-authorized genuine real invoice submission. Fiscal-memory authority routing and signing correctness remain blocking.

C. **Routine updates:** `.github/workflows/phase18-production-release.yml` packages releases automatically on main, with manually triggered environment-approved deployment. GitHub production Environment must have required reviewers, pinned SSH host key and restricted deploy identity. Passwordless sudo for unattended deployment is **not yet provisioned**; workflow deployment job currently cannot be treated as ready for unattended Production. Never use NOPASSWD:ALL. Database migrations are separate reviewed operations and not executed by deploy.

Artifacts:
- `scripts/phase18-release-stage.sh`
- `scripts/phase18-release-promote.sh`
- `apps/web/tests/phase18-fast-release-contract.test.mjs`
- `.github/workflows/phase18-production-release.yml`
- `docs/phases/phase-18-fast-release.md`

**State:** Source implementation committed; no code proven deployed to VPS, no new public runtime started, no GitHub CI PASS observed in this turn, no provider operations performed. Detailed operator review and evidence still needed. Legacy Step09 routing blockers remain.
