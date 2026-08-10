# B1 - Causal world authoring

| Field | Record |
|---|---|
| Batch | `B1` |
| Date range | 2026-08-10 12:17–12:48 UTC / 05:17–05:48 PST |
| Name | Causal world authoring |
| Status | Closed append-only batch |
| Threads | [`T-002`](THREADS.md#t-002), [`T-019`](THREADS.md#t-019), [`T-021`](THREADS.md#t-021), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-026`](THREADS.md#t-026), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | `94c277655942` (source and assembly), `da19ef6b4e6a` (reference surfaces); the governance close is the commit carrying this record. |
| Builds | Two full no-incremental Release builds were byte-identical at SHA-256 `D7AE79BFC5316A8330FDE8C88CC077D56CEB1686B53E9A140D88CDA4736669D3`: 0 errors and the same 12 existing warnings. Causal-receipt runner: 0 errors and 0 warnings. |
| Receipts and verification | Fixed-seed causal suite passed 179 assertions. The authored fixture is schema 2, byte-identical across both active surfaces at SHA-256 `4D90AD4738F3B0E983BB7D1BC2DBA9E26C984BDE48A157263B913393D735579A`, and contains 3 factions, 4 settlements, and 9 population groups. |
| Corrections | Replaced outcome-shaped controls and parallel frontier realization with cause-owned policy, saved realized facts, structural validation, direct consumers, and concise causal copy. Confirmed plans retain their causal snapshot; generated population shares refresh with a draft policy; relation patterns include settlementless factions; named defaults now equal their visible bands. |
| Provenance | Current GitHub `main` authority at `9a6153a8ce72`; implementation branch `mallowfluff/world-tendencies-causal-convergence`. |
| Source relation | Follows `A102` and matures the `A100-A102` creator line as `VU-028`; no former-ledger label applies. |

## Record

World tendencies now has eleven causal contracts. Each visible control names one
authoring fact, changes one owning variable, states its constraints, and reaches
generation through the same field saved by the current schema. Starting-region
choices replace the matching regional default at that owning surface; unrelated
world policy remains independent.

Major settlements are selected from RimWorld's world-population settlement pool
or an explicit scenario override. Settlement concentration affects placement
before settlement pattern is classified. Urban-growth propensity changes the
support threshold, while population, land, access, services, civic development,
economic capacity, trade connectivity, specialization, regional role, and
history determine realized settlement scale. Reallocation source variety changes
the owners represented among selected source settlements without changing the
pool size or final local-faction decision.

Frontier frequency determines the count of suitable holdings. Frontier size
determines each holding's household, material level, and site form. Regional
plans and ordinary maps both save the realized holding rows before materializing
them. Generation reads those rows and contains no second tendency roll or
hard-coded frontier fallback.

Faction relations, relation pattern, settlement pattern, population groups,
settlement facts, settlement scale, source receipts, and frontier rows are
canonical realized state. Confirmed plans must pass a structural realization
check; an invalid confirmed plan is rejected instead of silently regenerated.
Runtime systems consume the saved state for placement, factions, frontier sites,
population, layout, provisions, political identity, and UI summaries.

The fixed-seed receipt program varies every tendency separately, proves its
direct delta and unrelated-variable stability, exercises relation identity,
round-trips a causal snapshot, checks that later policy does not reroll saved
state, verifies the eleven UI-to-consumer paths, and recomputes the active
fixture's production realization hash and derived settlement facts. The fixture
retains world, region, candidate, arrival area, map scale, three factions, four
settlements, their relations, nine population groups, and its prior tendency
preset under schema 2. The preset is explicit rather than inherited from code
defaults, so band-aligned defaults for new worlds cannot alter this fixture.

Static evidence and deployment establish readiness for the operator's RimWorld
test. They do not claim that the game has been visually or behaviorally approved.

This batch is closed. Later implementation, correction, or verification remains
at its later alphanumeric identifier.
