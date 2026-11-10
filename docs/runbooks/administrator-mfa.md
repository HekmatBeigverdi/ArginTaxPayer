# Administrator MFA enrollment (development)

The API requires the original, stable `Identity__MfaEncryptionKey` (32 bytes,
base64 encoded) in its private environment before startup. Do not replace this
key when `identity_mfa_states` already contains encrypted secrets. A missing key
is reported by `/health/dev-identity` as `mfaConfigured: false`, and authenticated
enrollment requests return 503 instead of suggesting that MFA is already enabled.

## Enrollment

1. A Level1 administrator opens `/account` and nominates an existing, verified,
   password-bearing customer by mobile number for Level2 administration.
2. The target's sessions are revoked. An account without confirmed MFA receives
   `PendingLevel2`, which grants no administrative permissions. The user signs in
   with their password, then opens the MFA section in `/account`.
3. The browser renders the `otpauth://` URI as a local QR image. Scan it in Google
   Authenticator and submit its six-digit code. Scanning alone does not activate MFA.
4. Successful proof atomically enables MFA, activates the nominated role, removes
   the pending assignment and revokes previous sessions. Save the once-displayed
   recovery codes and sign in again with password plus Authenticator.

An already-enrolled Level1/Level2 administrator can reveal the same QR again from
`/account` after proving a fresh, unused Authenticator code. That proof code is
consumed, failed attempts are throttled, and each successful reveal is logged
without the secret. Anyone who scans the QR receives the same login factor;
separate admin accounts with separate factors are safer when several people use
the software.

An already-enrolled nominee can receive the active role immediately. Ordinary
customers cannot access the status, enroll or confirm HTTP endpoints. Existing
customer MFA remains enforced at login; this change does not disable existing factors.
Trusted operator and test code can still call the internal enrollment service
directly; public HTTP requests must pass the administrator/invitation role gate.

## Owner-authorized Level1 nomination in the test database

The operator CLI provides a separate, explicit action restricted to
`argintaxpayer_test`. It is never exposed over HTTP and cannot run during startup:

```zsh
set -a
source ./.env.dev.local
set +a
dotnet run --project tools/ArginTaxPayer.IdentityOperator -- nominate-level1-test <mobile>
```

It assigns `PendingLevel1` until the user confirms MFA, or `Level1` when MFA is
already confirmed, and revokes prior sessions. Repeated nomination is a no-op.
No database migration is required; invitations use existing role-assignment rows.

## Controlled reset and re-enrollment (test database)

When the original encryption key is lost, do not delete `identity_mfa_states`
manually and do not create a new key in place. Use the trusted operator command
below for the exact mobile-owned Level1 account:

```zsh
set -a
source ./.env.dev.local
set +a
dotnet run --project tools/ArginTaxPayer.IdentityOperator -- reset-enroll-level1-test <mobile>
```

The command refuses any database other than `argintaxpayer_test`, verifies the
active Level1 password, requires the literal confirmation `RESET-MFA`, revokes
active sessions, and performs old-MFA removal plus new enrollment in one
transaction. A rejected new Authenticator code rolls the reset back. The
provisioning URI and one-time recovery codes are printed only to the trusted
local operator; never paste them into chat or logs.

## Verification

`IdentityMfaOnboardingTests` exercises actual loopback API enrollment, customer and
Level2 denial, Level1 nomination, pending-role restrictions, missing-key errors,
QR secret stability, wrong-code rejection, role activation and session revocation
against the dedicated PostgreSQL test database. `mfa-proxy.test.mjs` exercises the
actual Next.js handler with a mocked backend, including empty enrollment POSTs,
same-origin checks, secret-response caching and removal of revoked session cookies.

## Level1 recovery of Level2 from Software Users

A signed-in Level1 with enabled MFA can open Software Users, select the Level2
role filter (including pending Level2 enrollment), and use the «ریست MFA» column.
The confirmation identifies the account and explains the temporary loss of
administrative access. Authorization is role-based, not tied to a phone number.

`POST /api/dev/identity/mfa/level2/reset` accepts `{ "targetUserId": "<uuid>" }`.
The API revalidates the live actor session, Level1 role and enabled MFA inside a
serializable transaction. It rejects self, Level1, ordinary customer, inactive,
unverified and passwordless targets. It deletes old MFA/challenges/recovery
codes, revokes target sessions, and changes Level2 to PendingLevel2. Ownership,
passwords and fiscal grants are preserved. A completed operation produces the
structured `IDENTITY_LEVEL2_MFA_RESET` log with actor and target IDs, without
secrets. An already-reset account returns 409; an in-progress pending enrollment
can be reset again. Enrollment and reset share a user-row lock and public
enrollment rechecks the session after acquiring it.

The target signs in with the existing password and configures a new Authenticator
in Account and Security. Successful proof restores Level2 and invalidates the
pending login session. No SMS bypass or administrator access is granted before
that proof. These endpoints retain the existing Development/loopback boundary.
