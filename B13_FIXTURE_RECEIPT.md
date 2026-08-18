# B13 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Mirror plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| SHA-256 | `6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04` on both files |
| Identity | `CA-RG-EB596A12` / `613b1fe44104` / arrival `389638` / map `350` |
| Composition | 3 factions / 4 settlements / 4 population groups / 19 established program facts |
| Culture | 8 schema-10 registry-2 records / 290 distributions / 25 preserved source records |
| Completeness | Every Culture contains the same 24 root question identities as `CACultureQuestionRegistry`; the 2 constituent-scoped local distributions remain separate. |
| Readback | Faction ownership resolves, settlement population shares total 100, and all Culture state survives XML serialization/readback. |

The current runtime fixture retains 290 Culture distributions and 25 source-evidence records, preserves the current plan identity and 3-faction/4-settlement composition, and is byte-identical to its governed mirror.