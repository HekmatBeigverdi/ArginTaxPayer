# ArginTaxPayer — Phase 18: Controlled Production Deployment & Dual Tax-Authority Environments

**Status:** Steps 01 and 02 OWNER-APPROVED (2026-11-04); Step 03 read-only preflight script and hardening plan PREPARED; remote execution/operational hardening PENDING owner-run evidence. No production rollout.  
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
