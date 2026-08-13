| Document | Colonist Awareness Overhaul Memory |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.4.1.0-alpha` · closed batch tip `B13` · next `B14` |
<!-- cao:generated:version END -->
| Author | ellyj3rain |
| Repository | `MEMORY.md` |
| Status | ACTIVE - index of every root document and its standing. |

# Memory

Index of every document at the repository root, with what it is and whether it is
current. Nothing at the root is unclassified. Where a disposition is genuinely
unresolved, this file says so rather than guessing — an unresolved status is a
junction for the operator, not a defect in the index.

## Status vocabulary

| Status | Meaning |
|---|---|
| CANONICAL | Current truth. Edit in place when superseded. |
| CANONICAL, INCOMPLETE | Current, and self-declares unfinished scope. |
| APPEND-ONLY | Historical record extended through new entries; prior substance is fixed. |
| REGULATORY | Active organization or index. May be corrected without rewriting the historical records it organizes. |
| IMMUTABLE | Exact historical evidence. Never rewritten. |
| DAMAGED | Content was lost; a recovered copy exists out-of-band and is not restored. |
| SUPERSEDED, UNMIGRATED | A successor exists, but this document still holds content the successor does not. Do not delete. |
| RECORD | Working record or captured artifact. Evidence, not architecture. |
| RECORD, SUPERSEDED | Historical design record retained for context. Not a current contract. |
| SHIM | Pointer file. |

## Canonical doc-pack

| File | Status | Role |
|---|---|---|
| `README.md`, `README.docx` | CANONICAL | Human entry point in working and document formats. |
| `MEMORY.md` | CANONICAL | This index. |
| `CORE.md` | CANONICAL | Project identity, canonical composition, governing constraints. |
| `ARCHITECTURE.md` | CANONICAL | Ratified framework shape; the four pillars. |
| `GOVERNANCE.md` | CANONICAL | Operating discipline and model-facing instruction surface. |
| `DECISION_REGISTRY.md` | CANONICAL, APPEND-ONLY | Ratified decisions DR-1 through DR-168. Later records supersede earlier decisions without rewriting them. |
| `FINDINGS.md` | CANONICAL, APPEND-ONLY | Verified findings F-1…F-135 and F-151…F-164. Former Git labels F-136…F-150 resolve through the former-label crosswalk and are not reused as finding identifiers. |
| `BATCH_LOG.md` | REGULATORY | Chronological index for the single append-only batch sequence and the next-batch declaration. |
| `VERSION_MAP.md` | REGULATORY, GENERATED | Chronological version-unit replay derived from `tools/version-model.mjs`. |
| `ROADMAP.md` | CANONICAL | Thread map, backlog, live gates. |
| `SESSION_STATE.md` | CANONICAL | Current operational reality. Read before claiming where anything stands. |
| `VERSION` | CANONICAL, GENERATED | `1.4.1.0-alpha`, derived from the version replay. |

## Batch record system

| Surface | Status | Role |
|---|---|---|
| `Batches/README.md` | REGULATORY | Portable-history contract for one alphanumeric batch namespace. |
| `Batches/[A-Z]*.md` | APPEND-ONLY | The 102 closed A-sequence records and closed `B1-B13` records in one namespace. |
| `Batches/THREADS.md` | REGULATORY | Permanent series-neutral families and many-to-many threads over nonadjacent batches. |
| `Batches/THREAD_ID_CROSSWALK.md` | REGULATORY | Complete map from temporary `ATF-*` / `AT-*` identifiers to `TF-*` / `T-*`. |
| `Batches/FORMER_LABELS.md` | REGULATORY | Complete map from all 93 former ledger boundaries to current batches. |

The A sequence closes at `A102`; the chronology continues through closed `B13`,
and `B14` is next after the operator runtime-test boundary. The governed batch records, chronological catalog, thematic
crosswalks, version map, decisions, findings, receipts, and source provenance are
the portable project history.

| History surface | Standing |
|---|---|
| Portable project history | Governed records in the current tree; independent of any Git host. |
| Local Git history | Optional engineering continuity and supporting provenance, including pre-publication work when retained. |
| Published forge history | Host-specific distribution record beginning at the canonical snapshot selected for publication. |

The three development layers are separate. Batches record atomic chronological
work. Threads classify related work across any letter sequence. The 40 version
units in `VERSION_MAP.md` partition A1-B13 into contiguous capability runs and
derive the current root version through the Neo four-coordinate odometer.

## Engine and distribution

| File | Status | Role |
|---|---|---|
| `GAME_ARCHITECTURE.md` | CANONICAL | Decompile-grounded study of RimWorld seams and limits. |
| `LICENSE` | CANONICAL | GPL-3.0 full text. Required for shipment. |
| `CREDITS.md` | CANONICAL | Public attribution and per-source how-and-why. Carries a corrected Processor Framework entry: the design is described, nothing is ported. |
| `UPSTREAM_SOURCES.md` | CANONICAL | Machine-checkable provenance — upstream URLs, licences, what was taken, incorporation state. Records that commit hashes are not recoverable from the local archive. |
| `AGENTS.md` | SHIM | Points at `GOVERNANCE.md`. |

## Audits and synthesis

| File | Status | Standing |
|---|---|---|
| `SETTLEMENT_SYNTHESIS_MODEL.md` | CANONICAL | Current faction, settlement, starting-condition, and derived-capability generation model. |
| `START_SURFACE_MAP.md` | CANONICAL | Vanilla colony-creation stages and CA's proper extension point per stage. Supersedes `SETUP_SCOPE_MAP.md`'s framing. |
| `PRIOR_ART_TRACES.md` | CANONICAL | One record of every external implementation investigated. Labels evidence grade per assertion; Simple Warrants is source-verified, Law and Order and Yayo's Bank are operator-supplied. |
| `ECONOMIC_FUNCTION_AUDIT.md` | CANONICAL, INCOMPLETE | DR-87 first pass over 13 economic functions. Self-declares remaining scope unfinished. |
| `ECONOMIC_IMPLEMENTATION_AUDIT.md` | CANONICAL, INCOMPLETE | DR-87 second pass; RimBank deep trace, verdict "split by layer". Closes with "Remaining, not started". |
| `PARALLEL_ONTOLOGY_AUDIT.md` | **DAMAGED** | The current file is an 89-line consolidation. The complete 394-line original was overwritten 2026-08-05 while untracked, recovered from session transcripts, and is held in `recovery-candidates/` in the preservation package. **Restoration is an open decision.** Current verdicts in the file are accurate; rounds 1–2 per-item consumer tracing is missing from it. |
| `SETUP_SCOPE_MAP.md` | RECORD, SUPERSEDED | Historical audit of the removed setup screen. Retained for its dead-control and flow evidence; its schema names are not current contracts. |
| `MODULE_OWNERSHIP.md` | CANONICAL | Module, state, mutation, cadence, index, dependency, consumer, and diagnostics ownership after B13. |
| `SCHEMA_REGISTRY.md` | CANONICAL | Executable campaign schema catalog, pending-authoring distinction, and validation/migration contract. |
| `PERSISTENCE_CENSUS.md` | RECORD | Generated source-to-catalog census of every direct Scribe/nested Expose writer and native persisted owner, including narrow non-campaign exclusions. |
| `CAMPAIGN_COMPATIBILITY.md` | CANONICAL | Operator update, preflight, supported migration, visible failure, backup, and rollback workflow. |
| `AUTHORING_ONTOLOGY_COVERAGE.md` | CANONICAL | Four-layer authoring coverage, mechanics classification, production vocabulary, semantic kinds, category cardinalities, and duplicate-surface audit. |
| `B13_CAUSAL_CONTRACT.md`, `CULTURE_RESEARCH_CORPUS.md` | CANONICAL | Current twenty-four-question Culture registry, authoring contract, causal separations, sources, consumers, historical feedback, and calibration limits. |
| `B12_CAUSAL_CONTRACT.md`, `B12_CULTURE_QUESTION_AUDIT.md` | RECORD | B12's original thirteen-question capability boundary and exact former-subject disposition, retained beneath B13. |
| `CULTURAL_COGNITION_RESEARCH.md` | RECORD | Supplied-package hashes, admitted B12/B13 research constructs, evidence limits, and code provenance. |
| `B13_B12_FIDELITY_AUDIT.md` | RECORD | Pre-change audit proving which B12 contracts were preserved, extended, or corrected. |
| `B13_ACCEPTANCE_RECEIPTS.md`, `B13_FIXTURE_RECEIPT.md` | RECORD | Fixed-seed registry, authoring, causal-isolation, persistence, generation, historical-feedback, and current-fixture results. |
| `B13_BUILD_RECEIPT.md`, `B13_DEPLOYMENT_RECEIPT.md`, `B13_REVIEW_RECEIPT.md` | RECORD | Exact build, deployment, and independent causal/structural/surface review closure evidence. |
| `B12_ACCEPTANCE_RECEIPTS.md`, `B12_FIXTURE_RECEIPT.md` | RECORD | Fixed-seed causal, isolation, persistence, performance, migration, and governed-fixture results. |
| `B12_BUILD_RECEIPT.md`, `B12_DEPLOYMENT_RECEIPT.md`, `B12_REVIEW_RECEIPT.md` | RECORD | Exact build, deployment, and five-lens review closure evidence. |
| `B11_ACCEPTANCE_RECEIPTS.md` | RECORD | Executable B11 ownership, durability, ontology, composition, surface, and regression results. |
| `B11_COMPATIBILITY_RECEIPT.md` | RECORD | Controlled B10-to-B11 preflight, identity, migration, and idempotence evidence. |
| `B11_BUILD_RECEIPT.md`, `B11_DEPLOYMENT_RECEIPT.md`, `B11_REVIEW_RECEIPT.md` | RECORD | Exact build, deployment, and five-lens review closure evidence. |
| `B10_CAUSAL_PROVENANCE_AUDIT.md` | RECORD | Closed B10 ownership, formation, mutation, persistence, and consumer ledger retained as the B11 causal floor. |
| `B10_ACCEPTANCE_RECEIPTS.md`, `B10_SYNTHETIC_STATE_SWEEP.md` | RECORD | Retained executable causal receipts and repository-wide RNG/hash classification regenerated against the current source. |
## Working records

These are retained design records, not current architecture.

| File | Status | Content |
|---|---|---|
| `POLITICAL_ARCHITECTURE_RATIFICATION.md` | RECORD, SUPERSEDED | Earlier political-design proposal. Current contracts live in `ARCHITECTURE.md` and `SETTLEMENT_SYNTHESIS_MODEL.md`. |
| `ONBOARDING_IA.md` | RECORD, SUPERSEDED | Earlier onboarding IA. Current contracts live in DR-91 and `SETTLEMENT_SYNTHESIS_MODEL.md`. |

## Boundary

This index covers current project documents only. It does not cover the preservation
package, the RS-008 save, the live-tree-only `REGIONAL_SPECIMENS.md`, or the
decompiled engine source — those are listed in `SESSION_STATE.md` under
out-of-band artifacts, with their locations and hashes.
