# B12 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Mirror plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| SHA-256 | `5862A5F810454CB3537836289051E3528A1F6888BF3D11495CD3CC53EAD876B9` on both files |
| Identity | `CA-RG-EB596A12` / `613b1fe44104` / arrival `389638` / map `350` |
| Composition | 3 factions / 4 settlements / 9 population groups / 19 established program facts |
| Culture | 8 schema-10 records / 22 exact question distributions / 26 preserved B11 source records |
| Current-schema rerun | schema-10 state validates and remains byte-identical at the recorded hash |
| Pair replacement | injected failure after active replacement rolls both files back to their original bytes |

Both runtime-consumed plan surfaces parse, are byte-identical, preserve the authored composition and plan identity, and serialize only the current Culture question model. The shared exact adapter gives only weighted approval and salience current question semantics. Every other former field remains evidence; a source meaning without an exact current question receives no invented replacement. The migration and second-run hash establish a byte-idempotent current-schema path.