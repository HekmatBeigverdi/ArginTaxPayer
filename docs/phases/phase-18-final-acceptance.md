# ArginTaxPayer Phase 18 — Final Acceptance and Closeout

**Status:** COMPLETE — owner accepted  
**Acceptance date:** 2026-11-11  
**Scope:** Controlled Production hosting at `https://tax.argin.ir`, operation of Sandbox and Production tax authority memories, and automatic application deployment from `main`.

## Completion evidence

- Main commit `1b7ff365d903989cd1879e0dc3850b46ad1152b4` was packaged and deployed by GitHub Actions run [#42865118907](https://github.com/HekmatBeigverdi/ArginTaxPayer/actions/runs/42865118907). Both `package` and `deploy` jobs succeeded.
- The restricted VPS deployment helper reported the immutable release promoted and loopback health checks PASS.
- The owner confirmed the remaining live-domain and application acceptance checks passed, including the website/login and invoice-list checks, and Sandbox/Production acceptance checks.
- Runtime configuration uses the Production application database `argintaxpayer_prod`; authority selection remains tied to the fiscal memory's persisted environment. Production and Sandbox are not selected by a global UI or ASP.NET environment switch.
- The production deploy workflow uses the protected `production` environment, verified SSH host key, port 2222, and restricted `argin-deploy` deployment helper. It packages and deploys application files only; database migrations and private configuration remain separate server-side operations.

The owner had previously reported successful real Production invoice submission and successful Sandbox invoice operations. This closeout does not claim that the latest Actions run itself sent a tax invoice; its deployment check is application health.

## Release state

The automatic deployment is verified. The GitHub versioned Phase 18 tag and Release have **not yet been created**. Create them after this documentation change is merged and the resulting `main` deployment succeeds. Use the exact then-current `main` SHA so the tag includes this final record.

## Phase boundary

Phase 18 is closed. Any separately deferred roadmap work remains tracked in its respective phase; this closeout does not mark unrelated Phase 16 or Phase 17 scope complete.

## Create the Phase 18 tag and GitHub Release

Run from a clean macOS checkout after merging the documentation closeout PR and confirming the resulting `main` Actions deployment succeeded:

```bash
git fetch origin --prune --tags
git switch main
git pull --ff-only origin main
git status --short --branch
git rev-parse HEAD

git tag -a phase-18-v1.0.0 -m "ArginTaxPayer Phase 18: controlled production deployment and dual-authority acceptance"
git push origin phase-18-v1.0.0

gh release create phase-18-v1.0.0 \
  --target "$(git rev-parse HEAD)" \
  --title "ArginTaxPayer Phase 18 v1.0.0" \
  --notes-file docs/phases/phase-18-final-acceptance.md
```

Before tagging, confirm the working tree is clean and `phase-18-v1.0.0` does not already exist. Do not force-move or overwrite an existing release tag.
