# SMS Customer Credit Enforcement Toggle

## Intent

Let the Level 1 administrator turn customer SMS wallet enforcement on or off. It defaults on. When off, dispatches do not reserve, check, or debit customer SMS credit, so a low balance cannot block an SMS. Actual Ghasedak provider expense remains recorded and visible in the SMS usage report. Administrator sends remain as they are.

## Approaches

1. **Disable customer wallet enforcement and charging for new dispatches (selected).** Keep provider expense accounting, and snapshot the choice per dispatch. This avoids negative balances and preserves audit history.
2. Disable only the pre-send check but keep debiting. This requires a debt/overdraft ledger and follow-up collection rules because the existing wallet schema forbids balances below zero.

## Behavior and constraints

- Level 1 controls a switch in SMS settings; initial/default value is enabled.
- The switch affects only dispatches created after a setting change. Each provider dispatch stores whether customer charging was enabled at send time, so registration OTP linking uses the original decision even if settings change before registration completes.
- When disabled, omit wallet reservation, insufficient-credit rejection, customer charge, spend event, and balance mutation. Still persist Ghasedak provider cost and message evidence for reporting and reconciliation.
- When enabled, preserve current reservation, balance gate and post-send customer charge behavior.
- Preserve provider cost-unit validation, OTP redaction, destination duplicate protection, admin unlimited behavior, and SMS-only failure handling.
- Store the switch in the Level 1 SMS settings JSON and immutable setting history. Add the send-time snapshot to provider dispatches with an additive migration. Existing dispatch rows and settings default to enabled.
- Re-enabling billing does not retroactively charge dispatches sent while disabled.

## Implementation status

Source and additive migration `20261102050000_AddSmsCustomerBillingEnforcement` have been authored. The migration was applied to `argintaxpayer_test` on 2026-11-02 after explicit owner approval; history and both default-true columns were confirmed. API build completed successfully while generating EF SQL; no automated tests were run under the approved plan boundary.

## Implementation scope

Update `SmsServiceSettingsService`, `SmsDispatchCoordinator`, the SMS settings endpoint and `sms-service-settings.tsx`; add a migration after the currently applied SMS ledger and SEP migrations; update Phase 12 SMS documentation. Do not alter existing account balances or historical SMS records.

## Verification boundary

No live SMS send is part of this change. Build and automated tests follow the current owner authorization boundary; database migration requires explicit approval for the verified test database.
