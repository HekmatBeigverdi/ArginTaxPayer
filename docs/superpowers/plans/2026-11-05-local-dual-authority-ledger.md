# Execution ledger

| Tasks | Shared interface / consistency | Finding / ruling |
|---|---|---|
| 1 | Producer returns Phase10LegacyPreparationResult, separate Production reservations | Internally consistent |
| 2 | Router returns Phase10OneClickResult and keeps old Sandbox service | Internally consistent |
| 3 | Connection consumes persisted memory environment, not invoice producer | Internally consistent |
| 4 | Integrates and tests all components | Internally consistent |
| 1 / 2 | Phase18ProductionInvoicePreparationService.PrepareAsync(bearer,taxpayer,invoice,ct) | Exact interface agreed; Task2 consumes it |
| 1 / 4 | PersistenceRegistration and migration SQL | Parent owns DI registration; producer owns new migration only |
| 2 / 3 | Invoice UI and workspace promotion | Task2 owns server workflow, Task3 owns UI; no overlapping files |
| 2 / 4 | Final validators and deployment switches | Existing Sandbox API unchanged in semantics; parent configures local flags only |

Ruling: Work in the existing phase/18-production-deployment checkout — owner requested locally testable changes in this branch; no worktree or server rollout needed.
Ruling: User's endpoint and archival-document clarifications settle the existing legacy protocol choice; execute the requested implementation without another design permission round.
Ruling: Do not submit an actual real invoice automatically; the user will exercise the local flow with their selected data.

Ruling: Extend new producer to modern classified Sandbox memories as well as Production — both environments are offered by existing onboarding, and owner explicitly requires memory-selected endpoints. Legacy separate Sandbox path remains intact.
Ruling: Add explicit Level1/MFA classification and verified activation — all107 current local memories are pending/unclassified and no existing API can activate any memory, so omitting this would leave the requested local flow unusable. Read-only test behavior remains unchanged; activation checks archived enum ACTIVE=0 and exact owner national ID.
