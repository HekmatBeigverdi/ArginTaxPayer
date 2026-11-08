# Phase 18 — Fast deployment and routine updates

**Status:** Release packaging, immutable staging and gated promotion scripts added in source. NOT deployed on the VPS. The current site remains in maintenance until actual server evidence is verified.

## One-time deployment

Existing building blocks: nginx HTTPS maintenance site at `tax.argin.ir`; dedicated PostgreSQL 17 at 127.0.0.1:15434 with 72 EF migrations; `argintaxpayer-api.service` and `argintaxpayer-web.service` disabled; root-protected configs at `/etc/argintaxpayer`; and release root `/opt/argintaxpayer/releases`.

1. **Build once**: GitHub Actions `phase18-production-release.yml` builds the same commit's .NET and Next.js release and uploads `api.tar.gz`, `web.tar.gz`, `SHA256SUMS`, `build-info.txt` and release scripts. Changes on `main` build artifacts; they **do not automatically deploy**.
2. **Stage with no downtime**: copy artifact directory intact to Ubuntu as a nonroot operator. Run:
   ```bash
   bash scripts/phase18-release-stage.sh --stage <FULL_40_CHAR_SHA> \
     --bundle-dir "$PWD" --sha256-manifest "$PWD/SHA256SUMS"
   ```
   Run from the copied bundle folder. This verifies checksum and source metadata and stages `/opt/argintaxpayer/releases/<SHA>`. It does not touch live processes or change `current`.
3. **First activation must remain separately authorized**: verify private app/web config, API security and tenant isolation, database role/SSL readiness, public HTTPS and trusted proxy, origin/CSRF behavior and verified backup. Confirm stage contents and prior results. Only after these gates, a privileged operator may install an explicit activation marker and configure the **separate operational/UI switches** according to tested requirements. These are currently false; do not simply switch them on or use the Development environment to bypass restrictions.
4. **Promote**: after authorized activation and tested config, run `PHASE18_OWNER_APPROVED_RELEASE=YES bash scripts/phase18-release-promote.sh --promote <FULL_SHA>`. This changes `current` to the staged release, restarts just these two units and checks localhost liveness, DB readiness and UI; on failure it switches back to the prior app release or stops the initial services. It does not roll back SQL migrations.
5. **Public endpoint**: nginx must proxy only `tax.argin.ir` HTTPS to `127.0.0.1:15091` after an independently verified nginx config and TLS chain. Never expose the API port 15090 or DB port 15434. Preserve the unrelated sites and 8080/8081.
6. **Authority flows**: Sandbox and real tax-service dispatch are NOT enabled by the release scripts. Before testing authority endpoints, resolve the Step09 authoritative per-memory route binding and legacy test-DB-only guards, verify official current SDK API paths in Step10, and approve real taxpayer/memory and invoice separately for real submission. Keep pending/Indeterminate no-resend behavior.

## Routine update workflow

- Feature branch -> PR to main -> GitHub Actions builds and runs .NET and Web tests -> checksummed deploy artifact.
- Trigger **workflow_dispatch** for a reviewed main SHA with `deploy=true`, subject to the **production GitHub Environment required-reviewers** approval and protected deploy SSH secrets.
- Deploy job uploads a verified bundle, stages a unique immutable release and calls gated promote; the host will refuse without the pre-existing activation marker. **Unattended SSH deployment additionally requires a narrowly scoped, pre-reviewed passwordless sudo policy for a fixed root-owned deployment runner**. The current scripts use `sudo -v`, so the workflow's deploy job must be treated as **NOT OPERATIONAL until that safe prerequisite is installed and tested**. Never grant general `NOPASSWD:ALL` or root login to GitHub Actions.
- Git SHA is the release ID; database migrations are separate explicit operations with backup, reviewed SQL and owner approval. Schema changes should use expand/contract compatibility to support app-only rollback.
- Preserve 2–3 prior releases, monitor postdeploy health and ensure the independent encrypted backup/recovery key escrow gate before admitting live users.
- SSH host verification requires a pinned `ARGIN_DEPLOY_KNOWN_HOSTS` value gathered out of band; do not use `StrictHostKeyChecking=no` or accept keys from `ssh-keyscan` inside the workflow.
- Required environment secrets when CI deployment becomes enabled: `ARGIN_DEPLOY_HOST`, `ARGIN_DEPLOY_USER`, `ARGIN_DEPLOY_SSH_KEY` and `ARGIN_DEPLOY_KNOWN_HOSTS`. Never store fiscal keys, DB passwords or provider API tokens in GitHub Actions.

## Acceptance gates

- On macOS and Ubuntu: `bash -n scripts/phase18-release-stage.sh scripts/phase18-release-promote.sh` and contract tests, same SHA build results.
- On Ubuntu: root-only configs; dedicated DB SSL least privilege; no extra nginx public endpoints; staged directory exists; monitored activation; `bash scripts/phase18-step06-local-health.sh --probe-local` PASS.
- Browser: HTTPS certificate and proxy, authenticated login/MFA, owner scoping, subscription and dashboard.
- Sandbox: authorized submission, inquiry, legal child operations and cross-environment negative tests from deployed app.
- Real authority: authenticated read-only check first, then a single authorized real invoice only after explicit separate approval. No fabricated taxpayer or invoice.

**No current stage/promotion/go-live success is claimed.**
