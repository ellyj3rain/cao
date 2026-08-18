# B13 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Mirror plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| SHA-256 | `004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF` on both files |
| Identity | `CA-RG-EB596A12` / `613b1fe44104` / arrival `389638` / map `350` |
| Composition | 3 factions / 4 settlements / 4 population groups / 19 established program facts |
| Culture | 8 schema-11 registry-3 records / 577 distributions / 26 preserved source records |
| Completeness | Every Culture contains the same 48 root question identities as `CACultureQuestionRegistry`; constituent-scoped distributions remain separate. |
| Readback | Faction ownership resolves, settlement population shares total 100, and all Culture state survives XML serialization/readback. |

The current runtime fixture retains 577 Culture distributions and 26 source-evidence records, preserves the current plan identity and 3-faction/4-settlement composition, and is byte-identical to its governed mirror.