# B19 Regional Substrate Acceptance Receipt

Generated from current source by `tools/B19RegionalSubstrateReceipts` (deterministic
kernel mathematics; no engine, no UI, no randomness beyond the kernel's own
deterministic hashing). Built and run with .NET SDK 9.0.316 against the shared
kernels `RegionalTopologyKernel`, `WorldTendencyCausalKernel`,
`WorldCharacterValues`, `ConstructionMaterialsKernel`, `SettlementProgramCausalKernel`,
and `OperationalRoleProgramKernel`.

| Contract | Result | Evidence |
|---|---:|---|
| coverage-and-disjointness | PASS | 10800/10800 eligible tiles assigned exactly once; 0 duplicate assignments |
| member-resolution-single-valued | PASS | each member tile maps to exactly its own region root |
| region-connectivity | PASS | 0 of 7487 regions are disconnected |
| determinism | PASS | second run reproduces 7487 regions byte-for-byte |
| seed-sensitivity | PASS | seed change moves the partition |
| land-share-0.10 | PASS | realized 0.102 against target 0.10 |
| land-share-0.40 | PASS | realized 0.407 against target 0.40 |
| land-share-0.80 | PASS | realized 0.810 against target 0.80 |
| span-band | PASS | 0 joined regions above the ceiling; 0 sub-floor joined regions remain, 0 of them mergeable (must be 0) |
| share-zero-all-single | PASS | 10800 regions, all single |
| pre-assignment-exclusion | PASS | partition covers 10700 of 10700 unreserved tiles and touches no reserved tile |
| fragmented-geography | PASS | 1800/1800 island tiles covered; 0 disconnected regions |
| carve-components | PASS | 2 components of sizes [12,12], deterministic ordering holds |
| unit-hash | PASS | 1000 draws bounded to [0,1), reproducible, salt-sensitive |
| same-and-different-region-members | PASS | Region(A)==Region(B)==73; Region(C)=3855 differs |
| concentration-placement | PASS | spread favored at 0.10 (2.91>1.47), cluster at 0.90 (2.79>1.35), neutral at 0.50 |
| concentration-at-world-scale | PASS | raw 25 vs 60 tiles both saturate identically (3.0253=3.0253, the defect); scaled by 35.4-tile spacing the band separates - spread favors far (3.03>2.50), cluster favors near (1.75>1.23) |
| world-character-separation | PASS | 9 characters over one fixed geography, compared across every authored dimension; every pair separable on at least one |
| material-reach-separates-site-kinds | PASS | reach rises across a lone cabin (1), an established homestead (2), a village (2) and a city (3) - holdings are not pinned to the floor |
| material-ambition-bounded-by-capacity | PASS | development 5 on 2 residents still reaches only 1, its capacity |
| material-suitability-ordering | PASS | at reach 2 worked stone (11) beats timber (7) and out-of-reach metal (9); local stone beats the same stone imported (10) |
| culture-moves-what-a-place-builds-toward | PASS | same village, same means: rooted reaches 2, mobile reaches 1; a culture never asked moves nothing (0) |
| culture-never-outbuilds-capacity | PASS | a rooted culture on 2 residents still reaches 1, and on a full city still reaches 3 - its capacity in both cases |
| stewardship-moves-only-the-tie-break | PASS | unrestricted extraction prefers its own rock (11 over 10); strict stewardship is indifferent between them (10 and 10) and still builds stone over timber (6) |
| quarters-follow-facts-not-the-naming-bar | PASS | the same settlement builds 2 quarters whatever the bar is called, while its standing still moves (3 to 4); a hamlet builds 1 and a city 4 |
| support-reaches-treeless-ground | PASS | on treeless ground a supplied holding can build in timber and an unsupplied one cannot; wooded ground needs no supplier |
| support-is-not-an-economy | PASS | a supplied holding with no economy gets no metal; metal is worked where metallurgy is known or bought where the place can pay; rock without masonry is a quarry |
| every-operational-role-builds-something | PASS | 7 declarable roles, all of them build works |
| roles-compose-without-repeating | PASS | an outpost that also watches builds defensive works once (1); a depot that also collects casualties builds 3 distinct programs; declaring nothing builds nothing |
| variety-target | PASS | target 1 at 0.10 -> 5 at 0.95, bounded by pool |
| urban-threshold | PASS | support 62 at population 600: scale 3 at 0.10 -> 4 at 0.90; support 20 stays 3 at 1.00 |
| frontier-frequency-and-size | PASS | 1 -> 5 holdings of 6 sites; 1 -> 5 residents; poor land caps material at 0 whatever the tendency |
| offmap-budget | PASS | 2 of 20 act at 0.10 -> 18 at 0.90 |
| world-settlement-facts | PASS | population 450 reproducible; support 4 poor -> 108 rich |

B19 regional substrate acceptance: 34/34 PASS.

This receipt verifies B19's closing corrections in the kernel mathematics:
`quarters-follow-facts-not-the-naming-bar` (the wrong-coupling repair),
`support-reaches-treeless-ground` and `support-is-not-an-economy` (material
support reaches material), and `world-character-separation` (the front-door
rework). It is deterministic and fixture-independent.
