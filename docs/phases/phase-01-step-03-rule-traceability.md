# Phase 01 — Fiscal validation traceability and release gates

**Source of truth:** owner-provided `RC_IITP.IS v7.9` (Tir 1405), `RC_UMGS.ST v1.18` (Ordibehesht 1404), technical connection v1.6 and .NET guides. **Status: specification cross-check, NOT full executable rule-manifest.**

## Explicitly extracted v7.9 items

| Rule | Scope | Official location | Acceptance test |
|---|---|---|---|
| Type/pattern allowlist | `inty` / `inp` | section 4, Table 1 p.14; section 8-7 Table 9 p.31 | reject pattern/type pairs absent from source |
| Main/correction/cancel/return | `ins` | section 5 pp.15–17 and section 8-8 Table 10 p.32 | only permitted transitions and reference chain |
| Excluded return patterns | exports (7), waybill (8), commodity exchange (11) | section 8-8 Table 10 p.32 | reject `ins=4` for these patterns |
| Header/line/payment placement |  JSON paths | Table 2 pp.19–22 | serialized JSON has required structure and field placement |
| Invoice issuance timestamp | `indatim` | section 8-2 Table 4 p.25 | verify date and authority constraints, no guessed statutory deadline |
| Registration timestamp | `indati2m` | section 8-3 Table 5 p.27 | independent of issue date; correct special-case value |
| Internal serial | `inno` | section 8-5 Table 7 p.29 | unique fiscal-memory serial; taxId reproducible |
| Reference tax ID | `irtaxid` | section 8-6 Table 8 p.30 | referenced invoice exists and is status-eligible |
| Header amount controls | `tprdis`, `tdis`, `tadis`, `tvam`, `todam`, `tbill` | sections 8-13–8-18 pp.39–41 | precise decimal computation and consistent totals |
| Cash/deferred settlement | `setm`, `cap`, `insp`, `tvop` | sections 8-22–8-25 pp.45–48 | amounts obey pattern-specific settlement controls |
| Unit code | `mu` | section 8-30 p.52 and RC_UMGS.ST v1.18 pp.3–4 | valid official code including person-month (16134) |
| Article 9 transmission | `insr` | section 6 p.18; section 8-84 Table 86 p.93 | when beyond officially announced permissible window, `insr=1` plus registration date according to rules; do NOT assume generic late transmission succeeds |
| Optional note 1 | `nti1` | section 8-85 Table 87 p.94 | optional string, max 30 characters according to document |
| Optional note 2 | `nti2` | section 8-86 Table 88 p.94 | optional string, max 30 characters according to document |
| JSON example | header, body, payments | appendix 9-3 p.97 | snapshot serializer compatibility test |

**Critical:** `insr` is conditionally mandatory in official v7.9, not universally mandatory. Eligibility is controlled by time condition and official transmission rule. Date-specific legal deadlines cannot be derived as one universal number from this specification.

## Field-rule manifest contract

Each row should contain `specVersion, invoiceType, pattern, subject, fieldPath, dataType, maxLength, precision, presence(Required|Optional|Excluded|Conditional), conditionExpressionId, ruleCode, page, tableNumber, testFixtureId`. Validate:
- completeness: field-by-allowed-combination, including special patterns;
- no contradictory required/excluded cells;
- every conditional rule has an executable predicate with a positive and negative test;
- no monetary `double` in application domain despite illustrative wire-schema examples;
- every new regulation version introduces immutable rule set and regression fixtures;
- external SDK serialization is isolated from application domain arithmetic.

## Remaining scope before marking Step 03 DONE

1. Transcribe every applicability cell from Tables 1–2 with independent review of the original rendered PDF (complex RTL table).
2. Extract all individual validation Tables 3–88: min/max, precision, prohibited/optional, conditional dependencies and notes, with trace references.
3. Review JSON appendix for any mismatched or trailing-space field names and reconcile against official schema (do not silently alter official names).
4. Create automated golden JSON and negative fixture set per accepted combination.
5. Confirm interface with production-approved SDK version in an isolated test environment.

We intentionally do NOT claim a complete 86-table rule extraction when only a source-linked subset has been verified.
