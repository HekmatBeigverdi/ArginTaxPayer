# Backup Control UI and Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the backup console page to match the existing admin UI and report honest stage-count progress during backup and recovery operations.

**Architecture:** Persist the active operation's stage name and completed/total stage counts in the existing controller-owned operation file. Worker services update progress at real phase boundaries; the existing operation endpoint returns it and the client renders a live accessible progress panel while polling.

**Tech Stack:** ASP.NET Core Worker, C# records and file-backed operation store, Next.js 15 App Router, React, TypeScript, shared console CSS.

**Spec:** `docs/superpowers/specs/2026-11-16-backup-control-ui-design.md`

## Global Constraints

- Keep the existing Level 1 page gate, same-origin BFF, secret masking, service-stop rules, restore confirmation, and recovery safety behavior.
- Percent means completed real stages divided by total stages; it does not estimate time or bytes.
- Do not animate a fabricated percentage; show the active stage and activity indicator while a stage is in progress.
- Preserve operation state outside the application database and serialize progress writes against concurrent operation updates.
- Use Persian copy and RTL layout consistent with the console.
- Do not add or run tests as part of this work; run project build/type checks only.

## Review Focus

- An update for a stale operation ID must not overwrite the current operation; verify by inspecting the guarded update path in Task 1.
- Old operation JSON without progress properties must remain readable; verify compatibility in Task 1.
- A failed operation must retain its last completed stage and must not show 100%; verify terminal update logic in Task 1.
- Remote-enabled and local-only backups have different real stage sequences; verify the progress total is set from settings in Task 2.
- A restore operation may pass through nested coordinator methods; verify its step count stays stable and reaches completion only once in Task 3.

---

### Task 1: Persist a compatible operation progress model

**Files:**
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupOperationStore.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupControllerEndpoints.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupWorkQueue.cs`
- Create: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupProgressPlan.cs`

**Interfaces:**
- `BackupOperationRecord` adds `CurrentStage`, `CompletedSteps`, and `TotalSteps` with backward-compatible defaults; expose `ProgressPercent` as a derived property (0–99 while running, 100 only on success, preserve last value on failure).
- `GET /api/backup/operation` returns `currentStage`, `completedSteps`, `totalSteps`, and `progressPercent` alongside its current status fields.
- Add `BackupOperationStore.ReportProgressAsync(Guid operationId, string stage, int completedSteps, int totalSteps, CancellationToken cancellationToken)`.
- Add `BackupOperationStore.StartAsync(string kind, Guid actorId, string mode, CancellationToken cancellationToken, string currentStage, int totalSteps)` for queued operations, initializing them at 0 completed steps before enqueue.
- Progress writes use the existing operation lock and reject stale IDs, non-running records, blank stage names, nonpositive totals, or active-stage counts outside `0..totalSteps-1`.
- Add `BackupProgressPlan` in `src/ArginTaxPayer.Workers/DatabaseBackup/BackupProgressPlan.cs` to define initial stage IDs and totals for manual, scheduled, local-stage, remote-stage, and activation operations.
- Define these stable stage IDs: `stop-services`, `prepare-backup`, `dump-encrypt`, `verify-archive`, `publish-local`, `publish-remote`, `validate-local`, `download-remote`, `validate-remote`, `restore-isolated`, `stage-review`, `pre-restore-backup`, `switch-database`, and `activate-recovery`.

- [x] Add the optional progress fields and derived percentage to the operation record without changing existing JSON field names.
- [x] Implement atomic `ReportProgressAsync` under the existing lock; retain the current state if the ID is no longer active.
- [x] Make successful completion persist the terminal completed count and retain the last reported stage; failed completion retains the last reported stage/count.
- [x] Initialize queued operation stage/total in `QueueOperationAsync` before enqueue, deriving the plan from the queued action and settings.
- [x] Include progress fields in `GET /api/backup/operation` while keeping its current authorization behavior.
- [x] Keep queued work distinct from active execution, mark it running on dequeue, and allow only one running or queued operation.
- [x] Review state compatibility, stale-operation rejection, and failure percentages (less than 100) in the implementation.
- [x] Build the Worker project: `dotnet build src/ArginTaxPayer.Workers/ArginTaxPayer.Workers.csproj --no-restore`.

### Task 2: Report real manual and scheduled backup stages

**Files:**
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupProgressPlan.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupExecutionService.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupOperationStore.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/PostgresBackupRunner.cs`

**Interfaces:**
- Add `IProgressBackupArtifactCreator : IBackupArtifactCreator` with `Task<BackupArtifactMetadata> CreateWithProgressAsync(DatabaseBackupSettings settings, BackupProgressPlan plan, BackupOperationStore operations, Guid operationId, CancellationToken cancellationToken)` so existing artifact creator implementations remain source-compatible.
- Pass the `BackupOperationStore`, operation ID, and stable plan into `PostgresBackupRunner`; do not put progress state in settings or the application database.
- Add `bool reportProgress = true` to `BackupExecutionService.CreateAsync`; activation passes `false` for its nested pre-restore backup so the activation operation keeps its own plan.
- Use stable English stage identifiers in stored state and map them to Persian labels in the web panel.

- [x] Report manual service-stop and preparation milestones from `BackupExecutionService`.
- [x] Report database sizing/key preparation, dump-and-encrypt, archive validation, local publication/retention, and optional FTPS publication/retention from the backup runner/execution service at their actual boundaries.
- [x] Use the ordered manual plan `[stop-services, prepare-backup, dump-encrypt, verify-archive, publish-local]`, adding `publish-remote` only when remote transfer is enabled; use the corresponding four/five-stage plan for scheduled backups without `stop-services`.
- [x] Choose the total stage count before work begins, including the optional remote-transfer phase only when remote backup is enabled.
- [x] Suppress nested backup progress when activation creates the pre-restore backup; activation retains its own fixed plan and advances when that sub-operation completes.
- [x] Preserve existing operation completion, error handling, and API stop semantics.
- [x] Build the Worker project with the command in Task 1.

### Task 3: Report restore stages without resetting nested progress

**Files:**
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/RestoreCoordinator.cs`
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/BackupControllerEndpoints.cs`

**Interfaces:**
- Reuse `BackupOperationStore.ReportProgressAsync` and the operation ID created by the existing queue endpoint.
- For remote stage, initialize a single stable total before downloading; pass the same total and reporter through local validation/staging rather than creating or completing a nested operation.

- [x] Report local artifact selection/integrity validation, isolated PostgreSQL restore, schema/state staging, and terminal review-ready stage.
- [x] Report remote artifact download and manifest/checksum validation before isolated restore, keeping the same operation ID and stable total.
- [x] Use `[validate-local, restore-isolated, stage-review]` for local staging, `[download-remote, validate-remote, restore-isolated, stage-review]` for remote staging, and `[pre-restore-backup, stop-services, switch-database, activate-recovery]` for activation.
- [x] Report activation milestones around pre-restore backup, service stop, database switch, and recovery-only API readiness as applicable; only report success after the existing activation checks complete.
- [x] Preserve current confirmation, isolated-database, and recovery-mode safeguards.
- [x] Review that nested remote staging and pre-restore backup never reset the active operation's total or operation ID.
- [x] Build the Worker project with the command in Task 1.

### Task 4: Restyle the page and render accessible stage progress

**Files:**
- Modify: `apps/web/app/backup-control-panel.tsx`
- Modify: `apps/web/app/(console)/backup-control/page.tsx`
- Modify: `apps/web/app/globals.css`

**Interfaces:**
- Extend the client `operation` type with `currentStage: string | null`, `completedSteps: number`, `totalSteps: number`, and `progressPercent: number`.
- Keep the existing 3-second operation polling; translate stable stage IDs to concise Persian labels.

- [x] Restyle schedule, remote-storage, operation, local-artifact, and remote-artifact areas with existing `.console-settings-card` patterns and responsive console spacing.
- [x] Import `apps/web/app/components/console-loader.tsx` and render its `ConsoleLoader` during initial load; clear retryable errors without hiding successful settings state.
- [x] Render a semantic `<progress>` element, numeric percent, step count, current-stage label, and spinner/activity state while running; use `role="status"` and `aria-live="polite"` without announcing every poll.
- [x] Map stage IDs to these concise Persian labels: `stop-services` → «توقف سرویس برنامه»، `prepare-backup` → «آماده‌سازی بکاپ»، `dump-encrypt` → «دریافت و رمزگذاری داده‌ها»، `verify-archive` → «اعتبارسنجی فایل بکاپ»، `publish-local` → «ذخیره نسخه محلی»، `publish-remote` → «ارسال نسخه به فضای ریموت»، `validate-local` → «بررسی فایل محلی»، `download-remote` → «دریافت فایل ریموت»، `validate-remote` → «اعتبارسنجی نسخه ریموت»، `restore-isolated` → «بازیابی در دیتابیس جداگانه»، `stage-review` → «آماده‌سازی برای بررسی»، `pre-restore-backup` → «تهیه بکاپ پیش از بازیابی»، `switch-database` → «انتخاب دیتابیس بازیابی»، and `activate-recovery` → «فعال‌سازی حالت بررسی بازیابی».
- [x] Render distinct succeeded and failed states; failures retain the last stage and never appear complete.
- [x] Retain explicit manual-backup and restore confirmations, secret masking, local/remote file checks, and recovery-mode UI.
- [x] Run `npm run typecheck` in `apps/web` and build the Worker using the Task 1 command.

### Task 5: Review implementation against the approved design

**Files:**
- Review: `docs/superpowers/specs/2026-11-16-backup-control-ui-design.md`
- Review: all files listed in Tasks 1–4

- [x] Confirm old persisted operation state is readable, active percentages remain below 100, and terminal success is the only 100% state.
- [x] Confirm desktop/mobile layout follows console card and heading patterns and honors reduced-motion styles.
- [x] Confirm user-facing Persian messages explain stage-based progress and do not imply time or byte accuracy.
- [x] Confirm no backup is executed and no service-stop/restore lifecycle is triggered during build/type checks.
