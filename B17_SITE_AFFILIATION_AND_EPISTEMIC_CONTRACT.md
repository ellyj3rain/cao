# B17 Site Affiliation and Pawn Epistemics Contract

## Canonical site relationship

Every persistent inhabited site owns one explicit relationship record. Its
ownership and material support are separate typed facts:

| Fact | Meaning |
|---|---|
| Site owner | The faction, if any, that owns the site. `None` is a complete value. |
| Material supporter | The faction, if any, that supplies or materially supports the site. Support does not imply ownership. |
| Resident affiliation | The affiliation of each represented population group. It does not infer the site's owner or supporter. |
| Local authority | How the site's residents govern the site and share responsibilities. It is independent of faction ownership. |

No key, failed lookup, fake faction, or hidden placeholder represents absence.
`CASiteFactionLinks` stores the two site relationships with explicit reference
kinds. Population groups store their own affiliations. Native `Faction` objects
are resolved only at the RimWorld adapter boundary where a native API requires
one.

The contract applies to major settlements, frontier holdings, frontier
settlements, cabins, and homesteads. Their legitimate mechanical differences
remain in their existing models; their affiliation semantics are shared.

## Social state without a faction owner

A factionless inhabited site remains a complete society. The site can own a
`CASiteLocalSocietyState` containing Political Order, Technological Knowledge,
represented institutions, and an explicit local-divergence marker. Culture and
Ideoligion remain population facts and are resolved from represented resident
groups. Organizations, offices, authority, practice, programs, provisions,
material state, and population continue through their existing owners.

For a faction-owned site, faction state remains canonical unless an explicitly
modeled local divergence exists. Removing the owner snapshots the currently
effective local state exactly once before detachment. A later edit changes the
local owner, not a dormant faction copy. Assigning material support never copies
the supporter's Culture, Political Order, Technological Knowledge, institutions,
or resident affiliation.

| Site state | Canonical resolution |
|---|---|
| Owned, no local divergence | Owning faction plus population-local Culture and Ideoligion |
| Owned, explicit local divergence | Site-local Political Order, knowledge, and institutions plus population-local Culture and Ideoligion |
| No owner | Complete site-local state plus population-local Culture and Ideoligion |
| Support only | Same as no owner; supporter supplies only the represented support relationship |

## Materialization and development

Validation resolves ownership and support independently and fails on a typed
reference that names no existing faction. `None` never performs a lookup.
Generation, habitat viability, civic and service derivation, programs,
provisions, pawn generation, naming, relations, and summaries consume the same
site record. A native adapter may use an owner when one exists; an absent owner
remains absent in CA's social state even when a native construction path needs a
bounded compatibility fallback.

Cabin, homestead, frontier-holding, frontier-settlement, and major-settlement
transitions preserve ownership and support exactly. Growth does not create a
faction. Support does not become ownership. Any later affiliation change must be
an explicit causal event.

Authoring exposes the direct facts: choose a faction owner or `No faction`, then
set material support separately when it is relevant. Settlement authority is a
different choice.

## Optional broader pawn knowledge

The existing proposition-knowledge owner provides the reusable epistemic
substrate. Broader pawn knowledge is disabled by default. Existing tactical,
welfare, triage, and other pawn-private knowledge required for causal correctness
continues in either mode.

When enabled, each pawn can hold a separate typed record of represented facts:

`world evidence -> pawn observation -> pawn record -> communication -> separate receiver record -> behavior or inspection`

Each record can retain subject and fact kind, typed payload, direct or reported
source, original source, immediate reporter, acquisition time, source-event
time, confidence, uncertainty, revision, contradiction, supersession, staleness,
spatial and social scope, and provenance. Fact adapters use only the fields their
semantics require.

A report copies the teller's record. It does not query current world truth. The
receiver therefore retains the teller's source age, value, uncertainty, and
provenance while recording the teller as immediate reporter. A later observation
by the teller does not mutate the receiver. New evidence must reach the receiver
before their record changes.

Known same-subject versions can disagree. Contradiction links preserve that
state. A newer supported revision can supersede an older record without deleting
its provenance. Retention is fact-specific: transient facts can expire while
durable knowledge of people, sites, institutions, technologies, events,
geography, and routes survives serialization until revised or explicitly
forgotten.

The production fact audit covers only already represented meaningful facts:
pawn identity and relationships; site existence and geography; settlement and
frontier location; ownership, support, and explicit no-owner state; resident
composition and affiliation; Culture; Ideoligion; Political Order; institutions;
organizations and officeholders; Technological Knowledge and availability;
research; important social and political events; conflict; routes; and
represented resource or site knowledge. Unknown semantics are not guessed.

The pawn inspection surface describes what that pawn thinks is true, its
confidence and uncertainty, source and reporter, event and acquisition age,
contradiction, supersession, and staleness. It does not expose registry internals
or claim live truth. Player-view information masking remains a separate future
presentation choice.

## Persistence and migration

Pending authoring epoch 15 and regional plan schema 16 introduce explicit site
relationships, population-affiliation schema 2, and frontier-holding schema 2.
Campaign catalog 6 introduces site-faction-links and site-local-society schema 1,
regional-settlement record schema 10, frontier-map plan schema 3, and proposition-
knowledge schema 2.

Only the adjacent B16 authored draft is converted. The conversion validates the
complete proposed graph before assigning any field or schema stamp. A failed
site, population, Culture, Political Order, Technological Knowledge, institution,
or relationship validation leaves the predecessor graph intact. Realized
campaign compatibility follows the catalog preflight and backup contract in
`CAMPAIGN_COMPATIBILITY.md`.

## Acceptance boundary

Executable B17 receipts cover owned and factionless major sites, unaffiliated
frontier forms, support without ownership, mixed resident affiliation, local
social state without a faction, Ideoligion and institutions independent from
ownership, scale transitions, Scribe readback, atomic migration rollback,
pawn-private observations, relayed provenance, stale and contradictory records,
explicit correction, fact-specific retention, the default-off boundary, and the
broader fact-family audit. Build and receipt evidence does not establish visual
or gameplay acceptance; the operator runtime test remains the next boundary.
