# B14 settlement environment static receipt

This executable receipt varies one environmental input set at a time, inspects the production wiring, and anchors the facts in RimWorld's decompiled terrain path. Operator runtime acceptance remains separate.

| Contract | Result | Evidence |
|---|---|---|
| same terrain responds to different environments | **PASS** | temperate=3; desert=2; extreme=1; ice=1 |
| terrain and water remain independent hard constraints | **PASS** | mountain=1; unusable=0 |
| seasonal exposure changes the intended capacity only | **PASS** | mild=0.10; severe=0.90; capacity=1; food=0.6525 |
| habitat requirements gate frontier capability | **PASS** | mild tier-0=True; dark tier-0=False; dark tier-2=True; vacuum tier-3 without materialized life support=False |
| one environmental cause adds only its owned requirement | **PASS** | endemic disease delta mask=32; expected medical=32 |
| unmaterialized hazard protection fails closed | **PASS** | industrial capability without apparel, filtration, or a defended envelope remains nonviable |
| advanced settlement potential remains distinct from frontier | **PASS** | industrial populations may author the required established settlement programs, while the current compact frontier materializer cannot claim unbuilt hazard protection |
| efficient and ordered construction evidence composes | **PASS** | composed candidate preserves equal terrain, adjacency, throughput, and cost while improving circulation, expansion, buffering, defense, and visual order |
| environment kernel cannot author social state | **PASS** | pure kernel accepts terrain, growing, ecology, and temperature facts only |
| world facts feed one shared settlement capacity | **PASS** | RimWorld season and biome facts -> shared environment adapter -> realization and placement capacity |
| environment participates in deterministic realization identity | **PASS** | member biome, climate, ecology, and mutators enter the saved settlement realization source hash; no random draw exists |
| live regional planning reads represented source tiles | **PASS** | projected cells -> constituent tiles -> saved planning context -> thermally informed placement |
| existing proactive behavior consumes the profile | **PASS** | Home planning prefers indoor recreation under seasonal exposure and ranks essential sleep placement by current room safety |
| frontier occupancy follows verified habitat materialization | **PASS** | saved supporter and exact tile requirements are validated; shelter, food route, stores, and required functions are materialized before residents |
| frontier capability belongs to exact saved site state | **PASS** | the canonical local knowledge supplies capability; optional owner and support references are resolved separately and materialization rechecks the saved tier |
| frontier realization identity rejects stale causes | **PASS** | saved policy, environment, capability, supporter, count, and map identity are revalidated before a plan is reused |
| autonomous building uses composable pattern evidence | **PASS** | one multi-axis evidence vector ranks whole-site candidates; the unified extractor reads full saves and structured snapshots while preserving source limits, exact ground, and rejection evidence; the corpus admits relationships, not copied layouts or a style toggle |
| broad native layout corpus is substantial and lineage safe | **PASS** | 4,238 complete source-unique and lineage-unique native layouts span 80 biome definitions; deterministic partitions cover every clean row |
| environmental realization survives save and readback | **PASS** | planning context Scribes environmental facts and includes them in its stable evidence signature |
| environment contract matches RimWorld terrain facts | **PASS** | decompiled terrain UI and engine expose the same season, plant, forage, and disease inputs |
| production assembly exists after compilation | **PASS** | bytes=4522496; SHA256=6878162A831177694186BADC1C5DB10805B6218D1477576ECE13D0D6BCFC21FC |

Overall: **PASS**
