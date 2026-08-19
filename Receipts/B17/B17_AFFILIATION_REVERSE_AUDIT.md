# B17 Site Affiliation Reverse Audit

Generated: 2026-08-19 00:04:25 UTC / 2026-08-18 17:04:25 -07:00

Assembly: `Assemblies/ColonistAwareness.dll`
SHA-256: `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218`

| Scale or consumer | Canonical relationship | Result |
|---|---|---|
| Major regional settlement | typed owner + independent support + local or owner-resolved social state | PASS |
| Frontier cabin/homestead | no owner by default; optional support; local Culture, Political Order, knowledge, institutions, population, residence | PASS |
| Population groups | primary status, affiliation, Ideoligion, and Political Order source remain independent | PASS |
| Generation recipe | `generationFactionDefName` selects native content only; it cannot express ownership | PASS |
| Habitat viability | environment, local knowledge/material capacity, and outside support are queried separately | PASS |
| Pawn generation | native faction is supplied only by the explicit owner; support does not own residents | PASS |
| Threats and security | factionless sites compare threats against actual represented residents | PASS |
| Organizations, patrols, roads, and programs | site residents and typed affiliation replace implicit owner requirements | PASS |
| UI | no-owner settlement, no support, and unaffiliated population are direct authoring choices | PASS |
| Transition | changing owner or frontier form preserves or snapshots canonical state explicitly and never mints a faction | PASS |

The reverse pass starts at each inhabited-scale consumer and traces back to the same typed site contract. No fake faction or negative-key ownership semantics are admitted.
