# Backup Control Page and Operation Progress Design

## Intent

Bring the backup and recovery page in line with the existing Persian admin console, and make long-running operations easier to follow. The user selected real, stage-based progress. The displayed percentage will count completed operation stages; it will not imply elapsed time or bytes processed.

## Goals

- Use the console's existing page heading, settings cards, spacing, colors, and responsive behavior.
- Give initial page loading a clear console-style loading state.
- Show an accessible live progress panel while backup or recovery work is running, including the current stage, completed-stage percentage, and a moving activity indicator while a stage is active.
- Persist progress in the controller's operation state so polling and controller restarts report a coherent operation status.
- Present completion and failure with the last stage and a clear status.

## Non-goals

- Do not display estimated time remaining or a synthetic continuously increasing percentage.
- Do not claim byte-level progress from `pg_dump`, archive validation, or FTPS when those operations do not currently report it.
- Do not change backup, restore, service-stop, authorization, or recovery safety behavior.

## Approaches

1. **Stage-count progress (selected):** the controller persists named stages and total/completed step counts; the UI displays `completed / total` as a percentage. It is accurate about completed stages but may remain steady while one stage runs.
2. **Byte-level progress:** instrument dump and transfer streams for continuous byte progress. This adds significant work and still does not represent verification, retention, or service lifecycle progress uniformly.
3. **Simulated progress:** animate a percentage in the browser. Rejected because it can report progress the operation has not made.

## Design

Keep the existing Level 1 page gate and same-origin BFF. Restyle the client panel using the existing `.console-settings-card` and console typography conventions, with clearly separated schedule, remote-storage, operation, local-artifact, and remote-artifact sections. Reuse existing console loading styles where possible and add narrowly scoped backup-page styles only where needed.

Extend the controller operation record with current stage, completed stage count, total stage count, and derived percentage. Add an atomic progress update to `BackupOperationStore`, tied to the active operation ID. Backup and restore services update named stages at real boundaries. Manual backup stages cover service stop when applicable, preparation, dump/encryption, archive validation, local publication/retention, and optional remote publication/retention. Restore stages cover artifact retrieval/validation, isolated database restoration, and staging or activation milestones as applicable. Terminal states report 100% only on success; failures preserve the last completed percentage and stage.

The existing operation endpoint exposes these fields. The panel continues polling while the operation is running, renders a native accessible progress element, stage label, step count, and indeterminate spinner/activity treatment for the currently executing stage. It must not imply finer-grained progress than the controller reports.

## Constraints and implementation calls

- The operation record is stored outside the application database, so restore/activation does not erase its progress.
- Progress updates must be serialized with operation-state writes and ignored/rejected if they target a stale operation ID.
- Initial data loading has no meaningful operation percentage; use the shared console loader there.
- Persian copy and right-to-left layout remain consistent with the rest of the console.

## Acceptance

- Page layout is visually consistent with neighboring console settings pages on desktop and mobile.
- Initial loading, running, succeeded, and failed states are distinct and accessible.
- Progress percentage changes only after reported real stages complete; active-stage text/spinner communicates ongoing work between updates.
- Existing Level 1 access gate, same-origin proxy, secret masking, and restore confirmation flows remain intact.
