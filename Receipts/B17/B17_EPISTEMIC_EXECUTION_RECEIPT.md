# B17 Broader Pawn Knowledge Execution Receipt

Generated: 2026-08-19 00:04:25 UTC / 2026-08-18 17:04:25 -07:00

Assembly: `Assemblies/ColonistAwareness.dll`
SHA-256: `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218`

| Executed contract | Result |
|---|---|
| Default boundary | PASS - experimental broader pawn knowledge defaults off |
| Private observation | PASS - an observed site fact exists only for the observing pawn |
| Report | PASS - receiver records the teller as immediate reporter while retaining original source and event time |
| No omniscience | PASS - relay copies the teller's typed payload; later source revision does not mutate the receiver |
| Contradiction | PASS - incompatible same-revision reports coexist with explicit contradiction links |
| Revision | PASS - newer source events supersede older receiver records without deleting their provenance |
| Correction | PASS - a false owner report can be revised to no owner |
| Retention | PASS - stale transient memory expires while durable knowledge remains |
| Serialization | PASS - typed payload, source, reporter, event time, revision, and supersession survive Scribe readback |
| Inspection | PASS - pawn UI describes records, confidence, uncertainty, contradiction, age, and provenance rather than live truth |

Observed records: pawn A `3`; pawn B `4`; contradiction links `1`; active B records after revision `1`.
Readback fingerprint: `EC0FAE6FAE3C1A7A`

This mode extends the proposition store. It does not replace tactical private contacts or standard-mode social capability.
