# Contracting original preparation repair

Read-only inspection of invoice 00048-05-ACR: Type1/Pattern4, Sandbox, Finalized/Approved; source workspace revision5 has contractNumber, immutable snapshot revision1 has no contractNumber. History is NotPrepared with no attempts or receipts; print preview exposes a reserved fiscal ID.

Root causes: promotion omitted contractNumber; frozen projector admitted Pattern1 only. Fixed promotion to preserve the field and original Type1/Pattern4 projection to pass crn into the existing domain mapper. Missing contract number now stops both preparation paths before credentials/authority IO, with an explicit not-sent error.

Existing invoice was not mutated or submitted. Its reserved fiscal ID binds the old snapshot revision/hash. Routine return/resave is insufficient: controlled snapshot/reservation recovery remains necessary for this historical invoice. Do not bypass reservation immutability or copy a draft field into the outgoing wire without an audited revision.

Verification: reproducer failed on old projector, then passed; .NET full suite1624 passed,55 environment-gated skipped; npm747 passed; frontend typecheck passed.
