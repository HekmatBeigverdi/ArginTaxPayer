# Level2 MFA reset from software users

The user selected the software-users table instead of the previously proposed account-page form. Add a Level1-only role filter (all/customer/Level2, including PendingLevel2), role/status metadata and a «ریست MFA» column. A confirmation dialog identifies the target and explains session revocation and re-enrollment.

Use the existing paged customer-owners endpoint with a role query parameter, preserving Level2 ownership scope. Normalize full Iranian mobile search with IranianMobileNumber, retaining partial/name/email/UUID searches. Root listings include Level2 identities even when they lack Customer. Do not hardcode phone-based permissions.

An authenticated active Level1 with enabled MFA may reset another active, verified, password-bearing Level2 account. Reject self/Level1/customer targets. In a serializable transaction, invalidate MFA state, recovery codes and challenges, revoke sessions, and transition Level2 to PendingLevel2. Existing self-enrollment restores Level2 after proof. Pending accounts with enrollment in progress may also be reset; already-reset accounts are a conflict. Preserve commercial ownership, grants, password and profile. Log actor/target IDs without secrets.

Approaches: the account-page form was superseded by the user's explicit table requirement; use the table and existing API/proxy conventions rather than a separate administration page. No new dependencies or schema migration. Test live PostgreSQL role restrictions, reset/re-enrollment and search/pagination behavior, plus frontend proxy and type checks. No unsolicited reset of real accounts during implementation.
