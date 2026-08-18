# B5 - Contextual cultural expression and settlement development

| Field | Record |
|---|---|
| Batch | `B5` |
| Date | 2026-08-11 UTC / 2026-08-11 PST |
| Name | Contextual cultural expression and settlement development |
| Status | Closed append-only batch |
| Threads | [`T-002`](THREADS.md#t-002), [`T-015`](THREADS.md#t-015), [`T-019`](THREADS.md#t-019), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | Source closure `2bca1e16b240a24a00ddab385909a81674a17cee`; the following governance closure carries the version projections, README twin, and this governed record. |
| Build | Full no-incremental Release build completed with 0 errors and the same 12 existing warnings. The verified assembly is 3,039,232 bytes at SHA-256 `529A4327416070FE4832BF2CFD8514DA45DC1AB5BF6C247816136BD1BD9312C1`. |
| Receipts and verification | Authoring convergence passed 126 assertions against each active fixture; creation-flow interaction passed 65; player founding passed 48; World tendencies passed 185. The unique four-suite total is 424 assertions and the executed total is 550. Correctness, structural, and surface reviews closed with no Critical or High findings; `git diff --check` passed. |
| Corrections | The B4 cultural-furniture recipe model is retired. Culture now means contextual expression rather than an authored object list. The final review also replaced receipt-local expression and migration stand-ins with production-shared typed kernels before closure. |
| Provenance | Follows B4 governance tip `9efcd1df6efae6d7ec85064bbab8a88f680f392c` on `mallowfluff/b3-creation-flow-interaction`. The B5 directive, audited B4 state, current RimWorld-facing implementation, active authored fixture, and review panel supplied the acceptance evidence. |
| Runtime fixture | The keyed and mirror plans are byte-identical at 27,220 bytes and SHA-256 `F60DC069CC30686E4B1694E91B61EF9458F37BD8E6E26FBCF621778FB916D583`: plan schema 4; Culture schema 3; 3 factions; 4 settlements; 9 population groups; world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350. |
| Deployment | RimWorld was closed. The verified assembly replaced the B4 live DLL and the live copy is byte-identical at SHA-256 `529A4327416070FE4832BF2CFD8514DA45DC1AB5BF6C247816136BD1BD9312C1`. |
| Source relation | Follows `B4` and forms the minor capability unit `VU-032`, deriving `1.2.0.0-alpha`; no former-ledger label applies. |

## Record

Culture now owns only the carried background of a population: stable identity,
an optional player name, an optional native visual-tradition source, and authored
provenance. Culture no longer owns furniture recipes or guarantees physical
objects. A missing native style remains a neutral, nonblocking visual fallback.
Schema-2 culture state migrates deterministically to schema 3 through one shared
kernel; the four former recipe profiles retain their carried identity and native
visual source while obsolete practice fields are cleared.

Cultural expression is a deterministic read of people in their actual
circumstances. It draws from carried background, native Ideoligion and its
commitments, political beliefs, population composition and certainty, the
institutions or founding arrangement in force, provisions, settlement role and
form, infrastructure, facilities, economy, trade, geography, faction relations,
buildings, and accumulated history. It produces an Aligned, Adaptive,
Constrained, Plural, or In tension status, a direct summary, five readable
facets, contributing facts, and one deterministic signature. The shared typed
causal kernel is used by production and executable receipts.

Starting Region presents cultural expression primarily at the settlement that
realizes it. Existing-faction views aggregate the distinct expressions of their
settlements. Materialized settlement records persist summary, status, and
signature; map reconciliation reads the actual resident Ideoligions and current
material state rather than rerolling an authoring tendency. World settlement
markers consume the same persisted result. The founding view remains earlier in
time: the founders carry background, Ideoligion, and political beliefs, adopt
only the landing arrangement, and develop local culture and institutions through
play.

Each settlement now owns one relative development profile: Minimal,
Contextual, or Extensive. The profile shifts generated access, services, and
civic development within bounded limits; it does not replace population, land,
trade, history, faction knowledge, or other causal facts. Explicit
infrastructure values remain authoritative. Starting facilities are derived
from the resulting settlement and expose exact Generated, Include, or Omit
ownership per facility. A single reset returns every facility to generated
state, and the final representation reports profile, infrastructure provenance,
generated facilities, explicit overrides, and override count for every
settlement.

Plan schema 3 migrates to schema 4 with the neutral Contextual profile and a
current realization hash. Explicit plan facts are preserved. Both active fixture
surfaces now carry the same current-schema composition and survive parsing,
serialization, and readback without losing factions, settlements, relations,
population assignments, founding state, region identity, arrival area, or map
scale.

The B5 assembly is built, reviewed, deployed, and byte-verified. Operator
runtime judgment remains the next gate for how the corrected Culture and
settlement-development flow looks and plays.

This batch is closed. `B6` is the next ordinary development batch.
