# B14 generation static receipt

This executable receipt inspects the paired current fixture, preserved B13 failure artifacts, live RimWorld Defs, decompiled engine path, current production gate, and compiled assembly. Runtime materialization remains a separate receipt.

| Contract | Result | Evidence |
|---|---|---|
| paired fixture is byte-identical | **PASS** | active=0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A; mirror=0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A |
| current fixture identity and schema survive readback | **PASS** | schema 16; alysaliu exact route; active and mirror XML agree |
| authored composition remains complete | **PASS** | factions=3; settlements=4; population groups=4; every share sum=100 |
| relation provenance is explicit and causal | **PASS** | 1-2 NativeInitial Hostile; 1-3 Authored Hostile; 2-3 NativeInitial Hostile; no legacy boolean |
| failed B13 artifacts are preserved exactly | **PASS** | active=27354E00647007BD54EC5D5E0B29E7E408E40CEEBE4A3CAAADE068D23BDCA101; keyed=3CBD3BE722F516012900F1A15507D745180E9A037E3F7EA473CC9E06A58E4C6A; pair 1-2 was unsupported Neutral |
| implementation follows RimWorld initial-relation path | **PASS** | NewGeneratedFaction invokes TryMakeInitialRelationsWith; engine thresholds and permanent/natural-enemy rules are present |
| fixture faction type supplies native hostile cause | **PASS** | live TribeCannibal FactionDef has permanentEnemy=true |
| production generation owns the same strict prerequisite gate | **PASS** | saved source -> realization validator -> pre-resolution GenStep gate is one production chain |
| actual materializer emits complete causal receipt | **PASS** | GenStep receipt covers slots, ownership, populations, relations, and an executable unsupported-source rejection probe |
| population projection preserves authored group identity | **PASS** | hostility constrains native pawn projection without rewriting the saved group kind, faction key, or label |
| B13 unsupported row fails the current contract | **PASS** | missing schema-11 source loads as Unset and current validation rejects it before faction resolution or settlement creation |
| production assembly exists after clean compile | **PASS** | bytes=4522496; SHA256=6878162A831177694186BADC1C5DB10805B6218D1477576ECE13D0D6BCFC21FC |

Active fixture: `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

Mirror fixture: `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

Overall: **PASS**
