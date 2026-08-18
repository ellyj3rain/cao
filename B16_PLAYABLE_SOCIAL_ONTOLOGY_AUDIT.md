# Playable Social Ontology Audit

## Current ownership

| Fact | Canonical owner | Runtime meaning |
|---|---|---|
| Explicit doctrine | Ideoligion | Memes, precepts, sacred or prohibited conduct, rituals, roles, and religious or social prescriptions selected through RimWorld's native Ideology system. |
| Population appraisal | Culture | How a represented population regards conduct and social arrangements, including distribution, disagreement, confidence, visibility, and historical change. |
| Legitimate order | Political Order | What political and economic arrangements a population regards as proper. |
| Rules in force | Institutions | Offices, authority, membership, property, procedure, and other mechanisms actually in force. |
| Repeated conduct | Practice | What represented people actually and repeatedly do. A single event remains an occurrence until the evidence supports a repeated practice. |

These facts can agree or conflict. No one layer silently overwrites another.
The native mechanic remains the executor of native conduct. CA observes exact
facts and supplies social interpretation; it does not replace the Ideoligion
precept, ritual, role, thought, job, or action that produced them.

## Reverse audit route

The playable ontology is audited in both directions:

`RimWorld Core and DLC mechanics -> CA mechanics -> supported mod mechanics -> semantic ownership -> exact adapter or explicit native-only boundary`

`Culture construct -> evidence route -> cognition and historical state -> represented consumer`

The first route prevents a closed research taxonomy from overlooking mechanics
that already matter in play. The second prevents the registry from accumulating
questions that have no causal use.

The B16 reverse pass expands the Culture registry from 24 to 48 questions in 12
player decision categories. Twenty-four is retained as the historical B13
boundary, not as a computational or design quota. Forty-eight is the present
coverage result, not a permanent quota. Several native precepts can express one
Culture construct, and some native definitions remain entirely Ideoligion-owned.

The added Culture constructs cover sexual conduct, marriage naming, childhood
protection, age standing, doctrinal pluralism, xenotype hierarchy, work
expectations, predatory acquisition, violence acceptance, sex-specific body
exposure, bodily alteration and integrity, meanings of pain, treatment of human
remains, human-flesh acceptance, animal-food acceptance, food adaptability,
recreational drug use, animal moral standing, resource stewardship, settlement
permanence, comfort expectations, and machine delegation.

## Exact semantic adapters

`CAIdeoligionSemanticAdapterRegistry` maps only an exact package ID, definition
kind, and `defName` to a Culture question and bounded pressure. Known Core,
Ideology, Biotech, Odyssey, and supported-mod definitions receive explicit
mappings. Labels, descriptions, prefixes, and similar names never establish
semantics. An unknown mod definition remains a native Ideoligion fact until its
meaning is audited.

`CANativeCultureEventAdapterRegistry` applies the same rule to native
`HistoryEventDef` occurrences. Its Harmony integration is a postfix: RimWorld
executes and records the native event first. CA then retains exact provenance
and may recognize repeated occurrences as practice evidence. Occurrence does
not establish cultural approval.

## Cannibalism example

| Layer | Recorded fact |
|---|---|
| Ideoligion | A native cannibalism precept prescribes, permits, disfavors, or prohibits eating human flesh. |
| Culture | `food.humanFleshAcceptance` records the surrounding population distribution from abhorrent through prestigious or expected. Native doctrine is one evidence pressure, not the Culture value itself. |
| Practice | Exact native human-meat consumption and human-butchery events can establish repeated conduct with pawn, faction, map, and time provenance. |
| Institutions | Any represented ration, punishment, ritual, or custody rule remains an institutional fact rather than being inferred from doctrine or conduct. |

A cannibal Ideoligion can therefore exist within a population that still finds
the conduct abhorrent, and a population can normalize the conduct without a
religious prescription. Actual eating remains separately observable.

## Supported and candidate mods

`Ideology: More Precepts` (`llunak.MorePrecepts`) is the first supported
semantic extension. Its mappings were audited against upstream commit
`33eab9398d7acc608e11dd7390476fefc4f433f5`. It adds mechanically executed
distinctions around age, compassion, newcomer attitudes, violence, taking from
downed people, alcohol and drug possession, nomadism, and comfort. Each supported
definition identity has an exact adapter; an unknown identity fails closed. The
runtime does not fingerprint an installed mod checkout, so a future semantic
change under the same package/kind/`defName` identity requires re-audit rather
than being automatically detected.

Vanilla Ideology Expanded and Alpha Memes remain research candidates. They are
not installed, required, or claimed as supported by B16. A candidate qualifies
for support when it adds mechanically consequential social distinctions with
auditable execution seams, not merely additional names, textures, or decorative
ritual material.

## Persistence and fixture boundary

Culture question registry version 3 is additive for compatible state. The 289
compatible authored rows and their valid population scopes remain unchanged.
The governed fixture also contained one sparse row scoped to a population that
is no longer a constituent of its settlement; that row is removed from live
scope coverage and preserved with its exact identity and signature as legacy
evidence. Missing B16 questions enter valid represented scopes at a neutral,
low-confidence position explicitly marked as lacking authored or historical
evidence. Runtime authoring and presets can then replace those neutral values
through the same canonical Culture object. The active and mirror fixtures remain
one atomic byte-identical pair. Generic compatible registry-2 migration remains
additive-only and does not weaken validation to admit incomplete scopes.

Native practice occurrences persist in the Culture longitudinal map component
with exact package, event, practice, pawn, faction, owning map, locality, and tick
provenance. Retention is bounded by distinct occurrence within each
map/faction/locality/practice bucket, so multiple native emissions of one act do
not displace separate acts. Streaming preflight can validate the persisted map
identifier but cannot recover the enclosing RimWorld map's runtime `uniqueID`;
the map owner therefore rejects any event whose `mapId` differs from its own map
during both current-state validation and predecessor migration. The ledger does
not authorize native behavior and does not manufacture witness knowledge from a
global event.
