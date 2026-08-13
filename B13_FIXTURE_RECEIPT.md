# B13 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Mirror plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| SHA-256 | `27354E00647007BD54EC5D5E0B29E7E408E40CEEBE4A3CAAADE068D23BDCA101` on both files |
| Identity | `CA-RG-EB596A12` / `613b1fe44104` / arrival `389638` / map `350` |
| Composition | 3 factions / 4 settlements / 9 population groups / 19 established program facts |
| Culture | 8 schema-10 registry-2 records / 194 distributions / 26 preserved source records |
| Completeness | Every Culture contains the same 24 root question identities as `CACultureQuestionRegistry`; the 2 constituent-scoped local distributions remain separate. |
| Readback | Faction ownership resolves, settlement population shares total 100, and all Culture state survives XML serialization/readback. |

The B13 converter retained all 22 B12-authored distributions and 26 migration-evidence records, filled only absent root questions, preserved the current plan identity and 3-faction/4-settlement composition, and atomically wrote byte-identical active and mirror files.