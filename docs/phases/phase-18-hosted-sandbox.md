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
