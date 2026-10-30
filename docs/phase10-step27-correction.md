# Phase 10 — Step 27 — Legal correction invoice

## Archived sources
- RC_IITP.IS V7.9, sections on subject `2`, reference invoice eligibility, immutable identity, quantity correction vs actual sales return, VAT and monetary rules.
- `src/ArginTaxPayer.Domain/Invoices/InvoiceCorrectionRules.cs`: `OfficialV79CorrectionRules` is the existing domain rule reference.
- Archived SDK `references/tax-sdk/modern-2.0.32/.../TaxCollectData.Library/Dto/HeaderDto.cs` exposes `ins` and `irtaxid`; `InvoiceDto.cs` has distinct `Header` and `Body`. Legacy 0.0.34 packets are reused through the existing vetted `Phase10LegacyInvoicePreparationService` and worker.
- Correction transmission uses `ins=2`, predecessor `irtaxid`, a new 22-character `taxid`, identical goods identifiers and VAT rates. Do not rewrite the original's signed packet or fiscal ID.

## Work done
- `20261030140000_AddSandboxCorrectionWorkspaces`: separate, tenant-scoped resumable workspace with optimistic revision and partial unique active index.
- Backend `Phase10SandboxCorrectionService`: open/resume, save with exact expected revision, discard, submit. Submit freezes a **distinct** document revision, adds legal reference relation, then delegates a single external dispatch attempt/inquiry to the existing application orchestrator.
- Corrected snapshot line identities and VAT remain immutable. Unit price, discounts and quantity may change; quantity decreases require affirmative original-mistake evidence reference. A real return must instead use the future sales-return workflow.
- Backend recalculates official financial preview; full cash/credit settlement amounts are recalculated for pure cash or pure credit. Mixed payments require explicit matching values. Unsupported or unverified changes fail closed.
- `Phase10LegacyFrozenInvoiceProjector` accepts `InvoiceSubject.Correction`, generating `ins=2` and `irtaxid` from its frozen server snapshot, not from an untrusted browser tax ID.
- API: `POST/GET/PUT/DELETE /dispatch/correction-draft`; `POST /dispatch/correction-send-and-inquire`; all tenant-scoped, Development loopback only.
- Next.js BFF authenticates the caller and enforces same-origin checks for mutations. Distinct categorized correction modal with Save, Resume, Discard and Submit; main invoice list displays `در حال ویرایش اصلاحی` and `ادامه ویرایش`.
- The original root row retains its human-friendly ID; legal successors remain separately stored and are shown in lineage accordions. Overview projects the latest related legal document's subject and authority status.

## Invariants
- Only confirmed authority SUCCESS predecessors are eligible.
- Only a fresh, unambiguous legal correction draft can be submitted. A stale optimistic revision is rejected. Unique index rejects two active edit drafts.
- No modifications to the original document, fiscal tax ID or immutable revision history.
- A prepared/acknowledged/indeterminate correction is not auto-resubmitted, and a failed response should be reconciled by reference inquiry.
- A SUCCESS acknowledgment is distinct from authority SUCCESS.
- Production database may not send Sandbox correction. Required flags remain `SandboxInvoiceLegacy:Enabled` and `SandboxInvoiceLegacy:OperatorApprovedEndpoint`.

## Mac acceptance commands
Run on macOS after confirming AutoSSH port `15432` and `argintaxpayer_test` DB:
```zsh
cd ~/projects/ArginTaxPayer
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
zsh scripts/phase10-step27-mac-verify.zsh
zsh scripts/dev-up.zsh
```
Migration: `20261030140000_AddSandboxCorrectionWorkspaces`, applied by verify script.

## Real Sandbox manual acceptance (operator, mandatory)
1. Select an existing Type1/Pattern1 original with latest authority inquiry `SUCCESS` and valid encrypted Sandbox signing key.
2. Click `اصلاح`, change one unit price, save; close dialog. Verify overview shows `در حال ویرایش اصلاحی`.
3. Click `ادامه ویرایش`; verify saved values persist. Edit and save again, verify revision changes and stale browser/tab saving gets conflict.
4. Check settlement value; pure cash/credit is recalculated from new totals. In review compare before/after values.
5. Submit once, record resulting successor invoice ID, new unique 22-character fiscal tax ID, reference number and inquiry status. On ambiguous result, **do not press send again**.
6. Query authority using the successor's reference until a definitive `SUCCESS` / `FAILED`. Only authority `SUCCESS` completes this scenario.
7. Verify original human system reference remains; one overview row only; subject `اصلاحی` on accepted correction, and two expanded stage snapshots showing their distinct historical prices and IDs.
8. Negative tests: no changes; VAT mismatch; missing official code; illegal reduced quantity; missing original-mistake evidence; stale revision; other tenant; draft discard; duplicate submit; unavailable authority inquiry; `Indeterminate`.
9. Never use Ubuntu for this step. Cross-platform parity is deferred to Step29.

## Acceptance status
Implementation committed for macOS validation. **Build, integration runtime, and official Sandbox acknowledgment/inquiry have not been observed by this assistant.** Approval requires operator evidence above. Limitations: initial supported profile is Type1/Pattern1; consecutive correction and contracting profile remain future hardening.
