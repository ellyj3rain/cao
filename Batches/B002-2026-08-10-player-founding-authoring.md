# B2 - Player founding authoring

| Field | Record |
|---|---|
| Batch | `B2` |
| Date range | 2026-08-10 18:06-19:46 UTC / 11:06-12:46 PST |
| Name | Player founding authoring |
| Status | Closed append-only batch |
| Threads | [`T-022`](THREADS.md#t-022), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | The closing commit carries the implementation, assembly, receipts, fixture converter, and this governed record. |
| Builds | Two full no-incremental Release builds were byte-identical at SHA-256 `DF57515B1F2A0F4635DB7471DB9460BD5788B545694B41E35538CE73503BB2B4`: 0 errors and the same 12 existing warnings. Both receipt tools built with 0 errors and 0 warnings. |
| Receipts and verification | Player-founding suite passed 44 assertions. World-tendencies causal suite remained green at 179 assertions. The three-lane coherence, correctness, and structural review closed with no Critical or High findings. |
| Corrections | Restored the missing player-facing Culture, native Ideoligion, Political Beliefs, and Founding Arrangement flow; separated carried belief from adopted order and later institutional history; added stable world-owned draft identity, content-aware native-Ideoligion notification, native validity checks, exact one-shot arrangement materialization, and current-practice consumers. |
| Provenance | Follows B1 tip `c78a441b9bda`; implementation branch `mallowfluff/b2-player-founding-authoring`; recovered design evidence in `SETUP_SCOPE_MAP.md` was translated into the current faction and settlement ontology. |
| Source relation | Follows `B1` and forms capability unit `VU-029`; no former-ledger label applies. |

## Record

The colony-creation flow now includes one coordinated Founding society page.
It replaces vanilla's preset-only top-level Ideoligion page while retaining
RimWorld's native preset, load, fixed editing, and fluid editing machinery.
The player directly authors the founders' Culture, Ideoligion, Political
Beliefs, and Founding Arrangement before Starting Pawns.

The player and established-society surfaces share models and vocabulary but
answer different temporal questions. Existing factions and settlements already
have realized social orders, institutions, relations, and history. The player
authors what the founders bring, what they consider proper, and the exact rules
they adopt at landing. The faction generator therefore does not invent a mature
player structure. Later institutions and practice must develop through play.

Political Beliefs remain standards for judging authority, decisions, ownership,
work, support, membership, and conduct. They do not silently become current
organization customs. Current practice is read from realized social order. The
Founding Arrangement independently answers authority, required work, founder
voice, shared supplies, and duration; agreement or disagreement with Political
Beliefs is recorded as meaningful state.

The founding draft belongs to a world component for regional, ordinary, and
forced-map starts. Regional setup retains a serialized projection so the active
fixture remains complete. One stable live object survives Back/Next navigation
and native editor round trips. The native Ideoligion receipt includes a content
and revision signature, so same-ID edits invalidate the prior notification.
Before scenario notification, the coordinated page applies RimWorld's native
structural validity checks for names, incompatible precepts, ritual targets, and
consumable-building rituals.

At game start, confirmed Culture and Political Beliefs are carried into player
faction state and native `Ideo` remains authoritative for Ideoligion. The exact
Founding Arrangement materializes as duration-aware organization relations. A
durable applied-tick receipt prevents regional or map generation from replaying
those terms over institutions that later develop. No broader player social-order
axes are inferred from the four founding questions.

The active authored fixture was converted from the surviving keyed composition
and mirror policy evidence into schema 3 without inventing a player founding
choice. Both runtime surfaces are byte-identical at SHA-256
`40BA69E3B41770EACA0C6C2A2D430F7D7AE0C056967C919F204FA49FD287BF1A` and
retain world identity `alysaliu|1|Algorab Markab`, region `CA-RG-EB596A12`,
candidate `613b1fe44104`, arrival area `389638`, map scale 350, three factions,
four settlements, and nine population groups. The unconfirmed founding draft is
deliberately empty so the player authors it in the restored flow.

The verified assembly was deployed to the live mod root while RimWorld was
closed and is byte-identical to the reproducible build. Static evidence establishes
readiness for the operator's test; it does not claim runtime visual or behavioral
acceptance.

This batch is closed. Later implementation, correction, or verification remains
at its later alphanumeric identifier.
