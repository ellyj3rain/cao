# B15 - Faction Technological Knowledge and Distributed Availability

| Field | Record |
|---|---|
| Batch | `B15` |
| Date | 2026-08-17 to 2026-08-18 UTC / 2026-08-17 PDT |
| Name | Faction Technological Knowledge and Distributed Availability |
| Status | Closed append-only batch |
| Threads | [`T-001`](THREADS.md#t-001), [`T-004`](THREADS.md#t-004), [`T-005`](THREADS.md#t-005), [`T-015`](THREADS.md#t-015), [`T-016`](THREADS.md#t-016), [`T-019`](THREADS.md#t-019), [`T-021`](THREADS.md#t-021), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | The B15 source, governance, and receipt commits on `mallowfluff/b15-technological-knowledge` carry this record and its evidence. |
| Build | Two clean Release rebuilds completed with 0 warnings and 0 errors and emitted byte-identical 4,233,216-byte assemblies at SHA-256 `DE6312FF9CF2F4B052AACDE82E93C113AF231D36494F93667266FB2D415F0764`. |
| Receipts and verification | Executable receipts verify 22/22 three-component Society presets, atomic rollback, saved-profile readback, standard/distributed availability, map-local custody, redundancy, isolated-carrier loss, incapacity/recovery, persistent retention, placement/realization separation, base consumers, supported migrations, current-fixture readback, and settlement realization. Native research mapping covers 122/122 installed projects through an exact table and unknown projects fail closed. Retained B10-B14 and persistence suites are recorded in `Receipts/B15`. |
| Corrections | Replaces hidden `FactionDef` and frozen settlement-tier authority with faction-owned Technological Knowledge; extends Society authoring and presets to all three faction components; keeps native research completion factual; routes research, construction, production, agriculture, habitat, frontier, and autonomous consumers through one knowledge contract; implements experimental pawn/institution/record availability without creating a second technology system. |
| Provenance | Begins from exact B14 closure `e1156de15dd91605f6d27190d770d118ed3c8624`. `B15_TECHNOLOGICAL_KNOWLEDGE_CONTRACT.md`, decompiled RimWorld research and production flow, live installed definitions, the governed fixture, operator correction of the ownership contract, and independent ownership/consumer/distribution audits establish the implemented boundary. |
| Runtime fixture | The active keyed plan and mirror were converted atomically to pending epoch 13 / regional 14 / founding 4 / knowledge 1, remain byte-identical at 407,892 bytes and SHA-256 `A0FF1B43CF3A5AFAFE72DA34360B247B7D06CB03DE903AEA251F8FD5D5297C82`, and preserve the selected world, region, candidate, arrival tile, map scale, 3 factions, 4 settlements, relations, populations, and established programs. The exact schema-13 pair is preserved under `Receipts/B15/evidence`. |
| Deployment | With RimWorld closed, the verified candidate was promoted to `Assemblies/ColonistAwareness.dll`; the worktree and installed junction views are byte-identical to the reproducible build. |
| Source relation | Follows `B14` and forms minor capability unit `VU-042`, deriving `1.6.0.0-alpha`; no former-ledger label applies. |

## Record

B15 makes Technological Knowledge canonical faction state beside Culture and
Political Order. Society remains the existing joint authoring surface over those
three values, and a Society preset remains a reusable snapshot. Applying a
built-in or user Society preset deep-copies and validates all three components,
commits them atomically, and restores every prior value if validation or commit
fails. No Society object or preset identity survives as a campaign owner.
Founding and regional plans stage the three-part composition before realization;
the created faction becomes the canonical owner, and settlements refer to that
state unless a modeled local divergence exists.

The technological model contains nine practical domains and independent
understand, construct, operate, and maintain ranks. Known native research and
origin evidence remain explicit. One audited translation boundary maps
research, buildables, manufactured items, recipes, plants, habitat requirements, and native
compatibility checks to exact domain requirements. Ordinary work without a
research prerequisite still has an explicit base requirement. Broad competence
never marks a native research project complete, and current consumers no longer
read `FactionDef.techLevel` or a saved settlement tier as effective authority.

Standard mode treats faction knowledge as socially available and preserves its
behavior independently of individual pawn custody. Experimental Distributed
Knowledge changes only availability: the same domain-and-competency state is
projected through accessible pawns, institutions, and records at the requesting
map or settlement. Overlapping carriers provide redundancy; loss of the only
carrier removes practical availability; incapacity, recovery, recruitment,
departure, and research update represented custody without rerolling or
regenerating lost knowledge. Later usable humanlike members receive a
skill-bounded projection after initial distribution; this batch does not claim
teaching or apprenticeship behavior. Standard-mode faction transfer does not
move canonical capability through a pawn.

Settlement viability now preserves the boundary between environmental need,
knowledge, and material execution. A selected Society may satisfy the knowledge
part of a difficult biome without being granted labor, buildings, or supplies.
Regional settlement generation, frontier realization, programs, autonomous
building, production, agriculture, medicine, maintenance, and research query the
same current faction contract. The consumer-matrix receipt distinguishes those
implemented execution paths from unused schema capacity. Saved realization
tiers remain receipts only.

Campaign catalog 4 adds Technological Knowledge schema 1 and advances the
faction, founding, regional, pending-plan, and settlement-receipt schemas. The
supported B14-to-B15 migrations seed capability only from represented source
facts, validate before publication, and invent no custody or prior research
history. The current authored fixture is converted rather than discarded and
retains its selected world, region, candidate, arrival area, map scale, three
factions, four settlements, relations, population assignments, and established
programs. Technical closure ends at a byte-verified deployment boundary; the
operator's RimWorld test remains the authority on how it looks and plays.
