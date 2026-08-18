# B14 player-base layout acquisition receipt

The 2026-08-17 acquisition expanded the Real Ruins extractor smoke test into a broad native-layout screening corpus. This receipt proves acquisition, structural completeness, privacy reduction, deterministic partitioning, and current evidence limits. It does not claim that anonymous archive rows are mature, high-quality colonies.

| Contract | Result | Evidence |
|---|---|---|
| selected cohort is durable | **PASS** | 10,000 external IDs fixed before download; local cohort manifest bytes=3,134,502; SHA-256=`39E2612DE5A55ACE54152885D80C5CC24C881C4DD86894BB81ACDBDA9B0C98C1` |
| native structured data was acquired | **PASS** | 9,998 `.bp` snapshots cached; 483,749,316 compressed source bytes; two downloads failed after retries |
| complete bases remain distinct from fragments | **PASS** | 4,238 complete byte-unique snapshots; 5,760 source objects ending mid-XML quarantined as recoverable fragments; zero parser failures |
| exact lineage separation exists | **PASS** | 4,238 complete rows; 4,238 source hashes; 4,238 lineage hashes |
| environment breadth is represented | **PASS** | 80 exact biome definitions and 16 source version strings among complete rows |
| structural range is represented | **PASS** | placed things: min=42, P25=686, median=1,339, P75=2,718, P90=4,721, P99=11,864, max=47,551 |
| partitions are deterministic and lineage-safe | **PASS** | train=3,365; validation=424; test=449; SHA-256 lineage modulo 100 owns the split |
| portable cohort definition exists | **PASS** | `real-ruins-broad-clean-manifest.json` bytes=2,670,711; SHA-256=`422C2AFB9BDDA2EC1158B072E9376707BD0756D75DA63A33BE65B5AE6F6E9884` |
| aggregate profile exists | **PASS** | `real-ruins-broad-profile.json` bytes=3,487; SHA-256=`B2BD2D5D272A3212939EF15FE965FAA905C68492F0F31EF34BC2781AC9412ADF` |
| generation is reproducible | **PASS** | a second profile/manifest generation from the same acquisition index was byte-identical |
| privacy-reduced catalog excludes source identity internals | **PASS** | portable row keys are external ID, source/lineage hashes, partition, relative scale, version, biome, capture/map geometry, and structural counts; source seed, raw game ID, pawn names/XML, and art text are absent |
| scale is not mislabeled as progression or quality | **PASS** | size is recorded only as a cohort-relative quartile; author, pawn, research, progression, and quality labels remain unresolved |
| operator evidence keeps its proper role | **PASS** | nine normalized operator saves remain the explicit early-establishment/combat-precarity stratum rather than being discarded or misrepresented as general mature coverage |
| additional operator checkpoints remain available without automatic promotion | **PASS** | all 62 current local `.rws` files extract with zero failures; the nine canonical normalized records regenerate byte-identically, while 53 additional checkpoints remain local screening candidates |
| tooling builds cleanly | **PASS** | `PlayerBaseLayoutExtractor` Release build: 0 warnings, 0 errors |

The ignored local acquisition index is 46,234,893 bytes with SHA-256 `FCBCAEEB5447D351E82FA149EABB051F1D03112F17B36B9D241380B577600349`. Raw archive snapshots remain local; the canonical repository records the exact clean cohort and its limits.

Overall: **PASS**
