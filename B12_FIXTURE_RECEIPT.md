# B12 Authored Fixture Receipt

| Field | Evidence |
|---|---|
| Active plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Mirror plan | `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| SHA-256 | `004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF` on both files |
| Identity | `CA-RG-EB596A12` / `613b1fe44104` / arrival `389638` / map `350` |
| Composition | 3 factions / 4 settlements / 4 population groups / 19 established program facts |
| Culture | 8 schema-11 / registry-3 records / 577 exact question distributions / 26 preserved B11 source records |
| Current-schema rerun | schema-11 / registry-3 state validates and remains byte-identical at the recorded hash |
| Pair replacement | injected failure after active replacement rolls both files back to their original bytes |

Both runtime-consumed plan surfaces parse, are byte-identical, preserve the authored composition and plan identity, and serialize only the current Culture question model. The shared exact adapter gives only weighted approval and salience current question semantics. Every other former field remains evidence; a source meaning without an exact current question receives no invented replacement. The migration and second-run hash establish a byte-idempotent current-schema path.