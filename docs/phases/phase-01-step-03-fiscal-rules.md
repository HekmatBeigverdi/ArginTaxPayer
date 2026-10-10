# Phase 01 — Step 03: Fiscal invoice rule matrix v1

Status: SOURCE-BASED MATRIX DRAFT; full 86-field-by-pattern truth table and executable validators remain pending.
Primary source: owner-provided **RC_IITP.IS v7.9 (Tir 1405)**, especially sections 3–9, Table 1 (p.14), Table 2 (pp.19–22), Tables 3–88 and JSON appendix (p.97). Also RC_UMGS.ST v1.18 (pp.3–4). Do not silently apply legacy SDK field requirements to a newer specification.

## Invoice types

- Type 1: complete seller/buyer and goods/services information; buyer tax credit may be recognized subject to authority acceptance and confirmation (section 3-1, p.11).
- Type 2: seller and goods/services data; buyer data optional; does not convey buyer tax credit (section 3-2, p.11).
- Type 3: card terminal/payment gateway receipt (section 3-3, p.12); **not** an arbitrary JSON invoice sent by this product. Model it for reporting/import only pending the official API scope.
- Paper/non-electronic invoice: category documented in section 3-4; not equivalent to an accepted electronic invoice.

## Pattern allowlist (section 8-7, Table 9, p.31)

| inp | Pattern | Type 1 | Type 2 |
|---|---|---|---|
| 1 | General sale | yes | yes |
| 2 | Currency sale | yes | no |
| 3 | Gold, jewelry and platinum | yes | yes |
| 4 | Contracting | yes | no |
| 5 | Utility bill | yes | no |
| 6 | Airline ticket | yes | no |
| 7 | Export | yes | no |
| 8 | Waybill | yes | no |
| 9 | Petroleum products | yes | yes |
| 11 | Commodity-based securities exchange | yes | no |
| 13 | Insurance services | yes | yes |
| 14 | Chain sale | yes | no |

Pattern 10 and 12 are **not on the cited allowlist** and must not be invented.

## Subject matrix (section 5 and section 8-8, Table 10, p.32)

| ins | Subject | General support | Restrictions |
|---|---|---|---|
| 1 | Original | allowed | validate pattern/type |
| 2 | Correction | conditional | requires eligible original/reference, subject and field constraints |
| 3 | Cancellation | conditional | requires eligible reference; cannot use cancellation as future reference |
| 4 | Sales return | conditional | limited to compatible patterns and valid reference; **not supported** for export (7), waybill (8) and commodity-based securities exchange (11) under Table 10 |

For correction, cancellation or return: persist `irtaxid`, reference tax ID, reference status and source version; validate chain before submit. Per sections 5-2 through 5-4, reference eligibility and buyer field exclusions depend on subject and existing authority state. Do not treat all subjects as a generic replacement invoice. Full or total sales return generally entails cancellation per section 5-4.

## Core JSON header / line / payment schema

| Scope | Fields (selected) | Purpose | Source |
|---|---|---|---|
| Header identity | `taxid`, `inno`, `indatim`, `indati2m`, `inty`, `inp`, `ins`, `irtaxid` | tax number, serial, issuance/registration times, type/pattern/subject/reference | sections 8-1..8-8 pp.24–32 |
| Parties | `tins`, `tinb`, `bid`, `tob`, `sbc`, `bpc`, `bbc` | seller/buyer identities, branches/postal code | section 8-9 pp.33–36 |
| Totals | `tprdis`, `tdis`, `tadis`, `tvam`, `todam`, `tbill` | discounts, VAT and other tax, gross bill | sections 8-13..8-18 pp.39–41 |
| Settlement | `setm`, `cap`, `insp`, `tvop` | payment terms / cash and deferred amounts | sections 8-22..8-25 pp.45–48 |
| Line | `sstid`, `sstt`, `am`, `mu`, `fee`, `prdis`, `dis`, `adis`, `vra`, `vam`, `tsstam` | item code, quantity, tax and value | sections 8-27..8-51 |
| Payment | `iinn`, `acn`, `trmn`, `trn`, `pcn`, `pid`, `pdt` | transaction information when appropriate | sections 8-52..8-60 |
| 1405 revision | `insr`, `nti1`, `nti2` | transmission rule and two notes | sections 8-84..8-86 pp.93–94; JSON appendix p.97 |

**Conditional requiredness:** consult Table 1 and Table 2 cell-by-cell. This selected field table is *not* an exhaustive mandatory-field schema.

## Version-aware validations

1. Composite validator key: `(specVersion, inty, inp, ins, fieldName, scenario)`, outcome = required/optional/prohibited with type/precision/constraints and official document reference. Versioned interpretations never retroactively mutate historical invoices.
2. Amount consistency: line and header totals recomputed deterministically using `decimal`, explicit rounding and currency (IRR). Never use float/double for money.
3. Invoice tax ID requires fiscal memory + issue date + unique internal serial/check digit. Explicit uniqueness for fiscalMemory+serial and `taxid`.
4. `indatim` issuance date, `indati2m` registration date and `insr` special rule are separate; do not conflate or assume deadlines. July 1405 v7.9 changelog adds these rule/note fields; section 6 covers Article 9 special handling.
5. Official unit codes sourced from RC_UMGS.ST v1.18 (102 entries, last includes person-month). Persist original submitted code and reference version.
6. Reference invoices must be accepted/eligible under the precise status rules. Avoid correction chains that violate section 5; no independent edits after final transmission except follow-on fiscal subjects.
7. Distinguish application validation errors, transport submission reference number, and final authority outcome. Persist request + response evidence.
8. SDK with-certificate guide (Farvardin 1405) and without-certificate guide have different integration flows and possibly older field names. The official invoice specification governs business field requirements, while each SDK adapter must demonstrate wire-level compatibility.

## Test vectors to implement in Phase 07

- All pattern/type combinations in allowlist pass schema selection; invalid combinations reject **before** network call.
- Correction, cancellation, return reference workflow and disallowed return for 7/8/11.
- Gold: `consfee`, `spro`, `bros`, `tcpbs` special field rules from section 8-7/Table 9.
- Validation of official units (including new person-month), identities, totals, due/paid settlement fields.
- 1405 `insr` with `indati2m` and notes; do not mark rule 1 universally valid or required.
- Fiscal number determinism/uniqueness, timezone conversion, decimal rounding, and submission idempotency.
- Historical invoice created under old specification remains preserved after a rule update.

## Work still required before acceptance

- Extract and verify every field applicability cell of **Table 1**, **Table 2** and per-field **Tables 3–88** into a machine-readable rule manifest with traceable page/table references; this v1 is an indexed outline only.
- Generate golden JSON fixture for each permitted pattern/subject and negative tests for every prohibited combination.
- Check version compatibility and test a real sandbox flow under approved test identity.
