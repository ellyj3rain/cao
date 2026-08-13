# Campaign compatibility

Status: canonical for `1.4.1.0-alpha` / B13
Current campaign boundary: `1`
Current schema catalog: `3`

B11 is CAO's first durable campaign boundary. A world confirmed under this
boundary is historical state. Later code may govern future events differently,
but it does not silently rerun creation derivations or replace established
facts merely because an algorithm, type name, or source layout changed.

## Boundary behavior

Before RimWorld begins Scribe loading, CAO streams the save XML and decides one
of four outcomes:

| Outcome | Evidence | Result |
|---|---|---|
| Current | boundary 1, complete manifest for the saved catalog, compatible versions, exact owner/component scope and cardinality, valid nested payloads, terminal SHA-256 seal | load; validate owners; owner-specific compatible upgrades may publish the next catalog only after success |
| Controlled B10 migration | no boundary, all legacy live-owner epochs are 10 | load existing represented state; add boundary/owner/catalog receipts on the next successful save |
| Additive bootstrap | no prior CA campaign state | initialize current owners through normal new-world/current-fact paths; record no earlier history |
| Unsupported | future/unknown boundary or key, incompatible version, conflicting/unknown legacy state, malformed manifest | abort load visibly before owner mutation; refuse save while blocked |

Preflight is read-only. It does not rewrite, rename, or copy the source save.
It also requires each inline owner version to agree with the manifest and each
world owner to occur exactly once, every map owner once per saved map, every
game/world/map payload at its declared scope, and every required collection or
deep record for every repeated parent occurrence. A complete record cannot mask
a truncated sibling. Owner migrations validate semantic state
before and after change, publish the owner/catalog version only after success,
are additive and idempotent, and are recorded once. The runtime profiler is
excluded from the save and no simulation branch reads it.

Catalog 2 is B12's additive extension of the B11 boundary. A valid catalog-1
save is not required to contain catalog-2 owners. Culture and the affected
existing owners validate and migrate their own compatible payloads; cultural
cognition, political cognition, and proposition knowledge initialize only from
facts represented at the upgrade tick. The compatibility component publishes
catalog 2 only after every current owner validates and each new family has an
initialization receipt. No B12 owner backdates pawn attitudes, coalitions,
institutional appraisals, propositions, or research history.

Catalog 3 is B13's current fidelity boundary. It retains the same durable owner
set and raises cultural cognition to schema 2 so attention, inherited-prior
strength, perceived social pressure, and observation likelihood persist as
separate facts. Nested Culture payloads require question registry 2. The
experimental catalog-2/schema-1 cognition payload is below the compatible floor:
preflight rejects it visibly instead of inventing values for the separated
causes. The current pending authoring fixture is converted directly under its
own schema and is not a realized campaign migration.

The same streaming validator runs on the closed XML candidate after
`ScribeSaver.FinalizeSaving` and before SafeSaver replaces the prior file. Only
a complete current-boundary candidate receives the exact terminal SHA-256 seal.
Validation or sealing failure throws inside SafeSaver, removes the candidate,
and leaves the prior campaign file in place. The validator and digest operate
without loading the save as one byte array. XML outside a recognized durable
owner streams directly. Each recognized component or native record is buffered
only for its structural validation and is capped at 1,000,000 elements and
67,108,864 text characters; exceeding either ceiling fails visibly before
Scribe owner load or SafeSaver replacement.

An accepted UI load arms the later engine load with the exact validated
terminal payload digest, file length, and last-write time. The engine consumes
that authorization once. If any fingerprint differs, it discards the token and
runs the complete preflight again, so a filename cannot authorize different
bytes between the pre-disposal and native-load calls.

`PERSISTENCE_CENSUS.md` supplies the independent source-to-catalog proof. It
discovers direct `Scribe`/nested `Expose` writers and native persisted owners
from production source, then requires each carrier to resolve to a catalog key
or a narrow non-campaign exclusion. The executable reverse check separately
requires every catalog key to reach a concrete component, nested-record, or
native-class validator.

## Pending authoring versus a realized campaign

Pending authoring is an unconfirmed proposal. The active/mirror Starting Region
plan, unconfirmed founding draft, preset files, and preview caches may be
regenerated or rejected according to pending-authoring epoch 12 and their own
current schema. The governed regional plan is schema 11; Culture is schema 10;
Political Beliefs remains schema 9.

A realized campaign begins when the world and founding state are confirmed and
materialized. Its regions, settlements, residents, domestic units, Culture and
belief history, organizations, relations, offices, programs, provision,
capabilities, knowledge, reactions, behavior intents, work receipts and other
registered facts are preserved. Creation derivation is not a repair path for
those facts.

The former `CAAuthoringDataEpoch` is now `CAPendingAuthoringDataEpoch` at the
pending authoring surfaces. Live owners recognize epoch 10 only as B10 upgrade
evidence. B11 saves do not write the legacy epoch and never discard realized
state because it differs.

Culture schema 10 replaces former meaning rows as current normative authority
with population distributions over explicit questions. Schema-9 meanings map
only through exact ordered-question adapters; every former dimension and every
unmatched subject remains explicit legacy evidence. No convenient substitute is
invented. The B11 rule also remains in force: an old subject-shaped practice is
converted only when saved longitudinal evidence identifies the corresponding
conduct; otherwise it is invalid pending state, not a durable practice preserved
through an alias. Political schema 9 converts old
ownership and economy `mixed` values into every mechanism their B10 descriptions
explicitly named. B10 `mixed support` did not identify which systems coexisted;
preflight rejects that ambiguous record before owner load rather than inventing
a pair. This is a pre-campaign correction: no retained durable campaign exists,
and the governed fixture already records explicit current support mechanisms.
Partial belief and current-order sets are copied into their destination and are
never persistent shared owners.

## Stable identity and future behavior

Stable semantic IDs survive source reorganization and compatible updates:

- region ID, member tiles, settlement `regionalId + slot`;
- faction and organization keys;
- population-group keys and residence pawn IDs;
- domestic-unit identity and continuity pawn;
- Culture ID/locality, question keys/population scopes and transition revisions;
- program signature, operator identity and asset/thing/zone IDs;
- provision operator/node IDs;
- behavior episode/job IDs and social fact identity.

An algorithm change applies to new formation or later transitions. Existing
state changes only through represented gameplay events or an explicit compatible
migration. A newly added subsystem may derive an initial snapshot from facts
that exist at upgrade time, but its receipt must say exactly that; it cannot
fabricate events before the upgrade.

## Between-session update procedure

1. Save the retained campaign and note the save name.
2. Close RimWorld. Do not replace a loaded assembly.
3. Back up the save before any schema-changing build.
4. Clean-build CAO from the intended source commit.
5. Run the relevant static, receipt, serializer and migration tests.
6. Record the source commit, build path, size and SHA-256.
7. Deploy that exact verified DLL while RimWorld remains closed.
8. Hash the deployed DLL and require byte equality with the verified build.
9. Launch RimWorld; compatibility preflight runs before the selected save loads.
10. Load the same campaign and inspect the compatibility report and log for
    migration receipts or exceptions.
11. Validate the gameplay surface affected by the update.
12. Save under the new schema only after preflight, migration and runtime
    validation succeed.
13. Continue play against that same accumulated history.

The debug actions under **Colonist Awareness** can enable/reset profiling, log
or reset its snapshot, and log the campaign compatibility report. Profiling is
disabled by default and is for bounded campaign evidence, not player-facing
state.

## Rollback

Keep the pre-update DLL hash and the pre-migration save backup as one pair. If
preflight, migration, or runtime validation fails:

1. close RimWorld without saving the incompatible load;
2. restore the previous DLL;
3. restore the corresponding pre-migration save;
4. verify both hashes/schema versions correspond;
5. load and validate that pair before continuing.

Never combine a post-migration save with a DLL that does not understand its
manifest. Never treat assembly hot-swapping while RimWorld is open as a
supported update mechanism.

## Unsupported or destructive changes

Unsupported state fails visibly. CAO logs the exact reason, opens an operator
message after the long event, stops the load before simulation owners mutate,
and guards `SaveGame` while the process is blocked. The source save is not
changed.

A future destructive live-state migration is exceptional and requires all of:

- a governed incompatibility finding and exact affected schema keys;
- a pre-migration backup requirement;
- a documented migration and rollback pair;
- operator-visible warning and authorization;
- proof that no supported additive/idempotent migration exists.

## Remaining live proof

B13's non-interactive evidence uses the current authored regional fixture, the
catalog-1 B11 upgrade envelope, and the retained controlled B10 envelope. It
proves preflight decisions, exact
component scope/cardinality, repeated-record validation, digest corruption
rejection, stable IDs, deterministic additive metadata, idempotence,
source-input immutability, exact Culture evidence migration, independently
owned and causally separated cultural cognition, political cognition,
proposition knowledge and organization appraisals, no creation rerun, no
invented prior history, retained B10/B11/B12 regression, and build/deployment
identity.

It does not pretend to automate a true RimWorld save round trip. The operator's
first retained campaign is the live proof: create and save the campaign, close
RimWorld, deploy a later compatible build through the procedure above, load the
same save, inspect the compatibility receipt, and continue play before saving
the updated schema.

## Future batch record

Every later implementation batch states:

- module impact: parent, additive/subordinate, state, lifecycle, cadence and
  dependency effects;
- campaign compatibility: none, additive default, explicit migration,
  future-only behavior, or operator-authorized destructive incompatibility;
- performance impact: operations/cadence, bounded work, scans, index/cache
  effects and profiler keys.

The next action after B13 is the operator's RimWorld runtime test and, once
accepted, the first retained campaign save.
