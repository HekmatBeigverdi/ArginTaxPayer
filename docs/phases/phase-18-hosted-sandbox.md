# Hosted Sandbox at tax.argin.ir

The requested deployment is the complete existing Sandbox application over
HTTPS, including identity, SMS and the business workspace. It is distinct from
the earlier dashboard-only rollout and from implementing real tax submission.

The public 404 was reproduced on 2026-11-05 Tehran time. The API and Next.js were
running, but the tax-specific Nginx location still used maintenance `try_files`.
Only that location was changed to the existing reviewed Next.js proxy template;
its previous configuration is retained in `/var/backups/argintaxpayer/`.
The public identity page subsequently returned HTTP 200 with the login HTML.

## Runtime configuration

Keep ASP.NET Core and Next.js in Production mode, with the existing loopback
ports, HTTPS origin checks, secure cookies, MFA and tenant authorization.
Enable the complete operational API and set both dashboard-only switches false.
SMS must use the actual private provider configuration and the persistent OTP
pepper; an API key alone is insufficient without the existing SMS settings and
account ownership in the database.

The historical Sandbox workflows require the database name `argintaxpayer_test`.
`ArginTaxPayer:HostedSandboxEnabled=true` admits this name only on the separate
container at `127.0.0.1:15434`, with `argin_prod_app`, TLS VerifyFull and the
dedicated CA. It never admits the local/shared database tunnel on port 15432.
Its authority origin must be exactly `https://sandboxrc.tax.gov.ir/req/api/`,
and real tax dispatch must remain disabled. The existing repository guards,
Sandbox payload validation, scoped signing and no-resend behavior remain active.

## Data and keys

Copying existing accounts/data requires the owner's data-transfer decision.
Restore into a new independent database, never over the source database.
If encrypted MFA or signing data is copied, preserve its matching encryption
keys and OTP pepper in private runtime configuration. Do not generate replacement
keys for existing ciphertext. Preserve the prior deployment configuration for
rollback, and never place a database dump, provider key or private environment
file in Git or a release bundle.

The two additive environment migrations must be applied to the restored copy
before the current API starts; historical fiscal environments remain NULL.
Do not infer real fiscal destinations or mutate historical invoice evidence.

## Acceptance

Normal local health requires login HTTP 200 and unauthenticated identity API/BFF
HTTP 401. Redirecting a disabled login is a failure. Disabled staging is reported
as `STAGED_ONLY` and does not establish application availability.
Verify public HTTPS separately, then actual login/MFA, SMS and the requested
business workflows. Passing build/tests is not evidence of those user journeys.

## Observed deployment, 2026-11-05 Tehran time

Owner approved copying existing accounts/data and the original MFA information.
An independent restore in the dedicated container contains 135 accounts and 74
migrations; the original shared database remains unchanged. Enabled MFA records
are byte-identical to the source. Both decryptable original MFA records,
including the actual Level1 account, were verified using the original key.
Ten inherited synthetic/unreadable MFA fixtures were preserved rather than
rewritten. The snapshot is encrypted and the recovery key is root-only.

Release `2d7e72266a6405f565055d6fd71474424b165d2e` activated the complete
application. A synthetic acceptance identity passed password + TOTP login,
Secure/HttpOnly/SameSite cookies, nine protected pages, authenticated taxpayer
BFF and cross-origin rejection through the actual public HTTPS/Nginx route.
That identity was removed afterward. Ghasedak account authentication and
available credit were verified read-only. No SMS or tax invoice was sent by
deployment acceptance. The SMS root is bound to the original Level1 owner of
the restored SMS settings to avoid ambiguity from historical test identities.

The final legacy regression exposed three incorrect regular-memory lookups in
Sandbox send/UID/reference inquiry. Their dispatch FK points to
`sandbox_fiscal_memories`, not `fiscal_memories`. Read-only PostgreSQL tests
exercise the actual queries using CTE fixtures with no regular-memory row,
and retain Production-operation and foreign-owner denials. They also cover
historical unclassified Sandbox operations without modifying prior evidence.

The VPS has SDK 9.0.121 while the repository pins 9.0.202; its full source build
therefore cannot be claimed to pass. The tested portable API package was built
on macOS and the web package was built on Ubuntu; activation verified the real
ASP.NET runtime, VerifyFull database readiness and public authenticated routes.
