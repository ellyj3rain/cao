# B17 Site Affiliation Execution Receipt

Generated: 2026-08-19 00:04:25 UTC / 2026-08-18 17:04:25 -07:00

Assembly: `Assemblies/ColonistAwareness.dll`
SHA-256: `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218`

| Executed contract | Result |
|---|---|
| Current authored fixture | PASS - schema 16 Scribe-load and readback preserve 3 factions, 4 settlements, 3 relations, and 3 frontier holdings |
| Fixture pair | PASS - active and mirror are byte-identical (`0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A`) |
| No faction | PASS - typed `None` ownership validates without a sentinel or native faction |
| Support | PASS - support-only state retains no owner |
| Independent settlement | PASS - direct authoring creates canonical local Political Order, Technological Knowledge, and institutions |
| Population | PASS - faction-affiliated and unaffiliated groups survive beside an independently typed site owner |
| Frontier forms | PASS - cabin and homestead transitions preserve ownership and support exactly |
| Migration transaction | PASS - a late invalid site leaves an earlier predecessor population and plan schema untouched |
| Persistence | PASS - ownership, support, population affiliation, and local state survive Scribe readback |

Semantic fingerprint: `CA-RG-EB596A12|613b1fe44104|3|3|0:RegionalFaction:1:None:1:True:1;1:RegionalFaction:1:None:1:True:1;2:RegionalFaction:2:None:1:True:2;3:RegionalFaction:3:None:1:True:3|0:None:RegionalFaction:1:1;1:None:RegionalFaction:1:1;2:None:RegionalFaction:1:1`

This executable receipt verifies data behavior; operator gameplay remains the runtime acceptance boundary.
