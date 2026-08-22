# Machine-Learning Dataset Governance

| Field | Value |
|---|---|
| Timestamp | 2026-08-18 21:21 UTC / 14:21 PST |
| Status | Canonical dataset-accounting and corpus-boundary record |
| Scope | Player-authored RimWorld base-layout evidence used for machine-learning development, screening, testing, or validation |
| Local authority | `Corpus/PlayerBaseLayouts/.cache/governance/dataset-governance.local.json` |

This record accounts for the player-base layout corpus without publishing the
training or evaluation examples. It is not the corpus. Raw saves, downloaded
snapshots, normalized layouts, per-layout screening records, partitions, caches,
and other records from which a layout can be reconstructed remain local and
ignored by Git. Code, schemas, methodology, aggregate statistics, receipts, and
non-reconstructive provenance remain eligible for publication.

California Civil Code Sections 3110-3111 define a high-level training-data
documentation regime for qualifying publicly available generative-AI systems,
including development testing, validation, and fine-tuning. This project
preserves the enumerated information so a future disclosure can be generated if
the law applies. Applicability to CAO has not been determined. The record does
not state that underlying examples must be published and is not legal advice.
See the official legislative text for
[Section 3110's definitions](https://leginfo.legislature.ca.gov/faces/billNavClient.xhtml?bill_id=202320240AB2013)
and the [current Section 3111 fields](https://leginfo.legislature.ca.gov/faces/billNavClient.xhtml?bill_id=202520260AB1170).

## Publication boundary

| Class | Location and treatment |
|---|---|
| Raw sources | RimWorld `.rws` saves and Real Ruins `.bp` snapshots remain local. |
| Normalized records | Per-layout schema-2 JSON, layout indexes, and coordinates remain local. |
| Screening and partitions | Per-layout cohort, screening, clean, train, validation, and test records remain local. |
| Private accounting | Exact local file inventory, cohort locators, hashes, admission state, and model/build use ledger remain in the ignored local compliance manifest. |
| Public governance | This record, `PLAYER_BASE_PATTERN_CORPUS.md`, the B14 receipt, extractor source/schema, and the aggregate Real Ruins profile are non-reconstructive. |

The tracked aggregate profile is
`Corpus/PlayerBaseLayouts/external/real-ruins-broad-profile.json`. It contains
only cohort totals, quantiles, partition totals, biome counts, version counts,
and the hash of the local acquisition index. It carries no per-layout entries.

## Dataset strata

### `PB-OP-20260728-V1` - operator full-save layouts

| Required fact | Record |
|---|---|
| Source or owner | The CAO operator supplied first-party RimWorld saves from the operator's own campaigns. |
| ML purpose | Early-establishment, combat-precarity, terrain-led growth, facility relationships, circulation, fallback, and expansion evidence for autonomous construction. |
| Approximate records | 62 parseable local source-save candidates; nine admitted normalized layouts. |
| Data points and labels | Map geometry; built, planned, and framed entities; zones; biome evidence; population counts; mod/scenario context; source and lineage identity; evidence-boundary labels. |
| Protected IP | Yes or potentially yes. Saves contain RimWorld and mod definition identifiers and player-authored spatial arrangements. They are not asserted to be public domain. |
| Acquisition basis | Contributed directly by the operator; not purchased and not acquired under a separate dataset licence. |
| Personal or aggregate consumer information | No email address, Steam ID, machine-user path, or direct real-world identity was detected in normalized records. Raw saves can contain player-authored in-game names and remain local. No aggregate consumer information was identified. |
| Processing | Read-only XML extraction; source hashing; omission of source paths and pawn names; deterministic normalization; checkpoint lineage grouping; exact duplicate and evidence-value screening. |
| Collection period | Available by 2026-07-28; re-extracted and broadened through 2026-08-17. Additional local candidates may be screened later. |
| First admitted | 2026-07-28 for comparative ML-development evidence; current schema-2 cohort established in B14 on 2026-08-17. |
| Synthetic data | None in this stratum. |
| Cohort and use identity | Exact member hashes and normalized-file identities remain in the ignored local manifest and local `corpus-manifest.json`. No trained weights artifact is currently tracked or shipped. |

### `PB-RR-SMOKE-20260816-V1` - Real Ruins extractor screening

| Required fact | Record |
|---|---|
| Source or owner | Real Ruins' public community snapshot service; individual layout authors are anonymous to CAO. Real Ruins is maintained by woolstrand. |
| ML purpose | Validate structured-snapshot extraction and establish which native fields can support spatial screening. |
| Approximate records | Thirteen snapshots: twelve structurally complete candidates and one rejected truncation. |
| Data points and labels | Exact placed-item, terrain, roof, capture-bound, biome, map, year, source-hash, completeness, and rejection-reason fields. |
| Protected IP | Yes or potentially yes. Snapshots can contain game/mod identifiers, user-authored layouts, and art-description text. |
| Acquisition basis | Publicly accessible, opt-in community archive; not purchased. Public accessibility is not recorded as a licence or public-domain dedication. |
| Personal or aggregate consumer information | Real Ruins states that player information is not retained. CAO detected no direct identity fields in its screening manifest. Raw snapshots may contain user-authored art text and remain local. No aggregate consumer information was identified. |
| Processing | GZip/XML parsing; source hashing; completeness screening; privacy-reduced manifest generation; no incomplete snapshot admitted as complete evidence. |
| Collection period | 2026-08-16; not a continuously running CAO collection job. The source archive itself is dynamic. |
| First admitted | 2026-08-16 for extractor testing and screening only. |
| Synthetic data | None. |
| Cohort and use identity | Exact external IDs and hashes remain in the ignored local screening manifest. No trained weights artifact is currently tracked or shipped. |

### `PB-RR-BROAD-20260817-V1` - Real Ruins broad screening cohort

| Required fact | Record |
|---|---|
| Source or owner | Real Ruins' public community snapshot service; individual contributors are not identified to CAO. |
| ML purpose | Broad biome and layout-structure coverage for screening, experimental lineage-safe partitioning, and autonomous-building evidence selection. |
| Approximate records | 10,000 metadata candidates; 9,998 downloaded snapshots; 4,238 complete byte-unique layouts; 5,760 fragments quarantined; two download failures. |
| Data points and labels | Per-layout source and lineage hashes, train/validation/test partition, relative size quartile, version, biome, map/capture geometry, and structural counts; raw snapshots contain exact placed items, terrain, and roof cells. |
| Protected IP | Yes or potentially yes for the same game, mod, layout, and user-authored-content reasons as the smoke cohort. |
| Acquisition basis | Publicly accessible, opt-in community archive; not purchased. No separate dataset-reuse licence has been established in the governed record. |
| Personal or aggregate consumer information | No email, Steam ID, machine-user path, or direct identity was detected in the local indexes or normalized metadata. Real Ruins states that it retains anonymous game data rather than player information. Raw art text remains local. No aggregate consumer information was identified. |
| Processing | Metadata selection; download caching; SHA-256 source and lineage hashing; completeness and truncation screening; exact deduplication; deterministic lineage-safe 80/10/10 partitioning; aggregate profiling. |
| Collection period | Acquired 2026-08-17; CAO collection is not continuously running. Future cohorts require a new identity. |
| First admitted | 2026-08-17 for ML development screening and validation design. Complete rows were not thereby labeled high quality or mature. |
| Synthetic data | None. |
| Cohort and use identity | Local acquisition-index SHA-256 `FCBCAEEB5447D351E82FA149EABB051F1D03112F17B36B9D241380B577600349`; partitions are train 3,365, validation 424, test 449. Exact row membership remains local. No trained weights artifact is currently tracked or shipped. |

## Processing and minimization

The extractor retains coordinates and entity definitions because spatial
relationships are the learned evidence. It removes source paths and pawn names
from normalized records. Real Ruins acquisition metadata omits world seed and
raw game ID, replacing related-checkpoint identity with a SHA-256 lineage key.
Incomplete files remain a separate fragment pool. Exact duplicates and related
checkpoints cannot cross partitions.

The 2026-08-18 audit found no email address, Steam ID, Windows user path, or
direct real-world identity in the normalized operator records or tracked/local
screening manifests. Settlement names, Ideoligion names, and other in-game
authored strings are still substantive source content and therefore stay local.
Raw sources were not deleted or rewritten. If direct personal information is
later found, it is recorded and segregated at the local compliance layer before
retention or minimization is decided; absence of detected personal information
does not create a deletion requirement.

## Model and build linkage

The local compliance manifest is the authoritative ledger connecting an exact
corpus version or cohort to a training, validation, testing, or evaluation run.
Each future run records its model/build identity, cohort IDs, exact manifest
hashes, partitions used, date, purpose, and synthetic-data status.

### Use record: spatial-relationship evidence aggregate (2026-08-19)

The first governed corpus use is a statistical evidence aggregate, not a
trained model. `tools/PlayerBaseLayoutExtractor relationships` measured
thirteen named spatial relationships (trip chains, protection of food and
power, hazard separation, medical access from defense, door passability,
envelope density, expansion reserve, edge affinity of defense, same-def
alignment) over the `PB-RR-BROAD-20260817-V1` TRAIN partition only (3,365
layouts measured), aggregated as quantile bands per condition bucket (biome
group, cohort scale quartile) with a 30-layout minimum per band. The
held-out TEST partition (449 layouts, never used for the bands) evaluated
coverage of each 80% band between 0.733 and 0.970 -- recorded in the
tracked receipt `Receipts/B18/Settlement/SPATIAL_EVIDENCE_HOLDOUT.json`.
The shipped artifact is the non-reconstructive aggregate
`Evidence/spatial-relationships.xml`: quantiles only, no layout
coordinates, identities, or reconstructable geometry. At runtime it ranks
otherwise-valid construction candidates and never gates validity. The
validation partition remains unused and reserved. No trained model-weights
artifact is tracked or shipped.

## Maintenance contract

`tools/ci/verify-repository.mjs` enforces the tracked whitelist, representative
ignore paths, required governance records, aggregate-only public profile, and
absence of corpus references from CI artifact packaging. The extractor defaults
to ignored output and cache paths. A clean clone builds every declared project
without the local corpus.
