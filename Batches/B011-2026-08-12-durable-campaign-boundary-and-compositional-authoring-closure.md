# B11 - Durable Campaign Boundary and Compositional Authoring Closure

| Field | Record |
|---|---|
| Batch | `B11` |
| Date | 2026-08-12 to 2026-08-13 UTC / 2026-08-12 to 2026-08-13 PDT |
| Name | Durable Campaign Boundary and Compositional Authoring Closure |
| Status | Closed append-only batch |
| Threads | [`T-001`](THREADS.md#t-001), [`T-002`](THREADS.md#t-002), [`T-004`](THREADS.md#t-004), [`T-019`](THREADS.md#t-019), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | Source closure `d83fbadb4eec2625ba985fb7fd6f64365bd9c313`; governance closure contains this append-only record; the following deployment-receipt commit records the byte-verified active DLL. |
| Build | Clean Release rebuild, exact assembly size and SHA-256 recorded in `B11_BUILD_RECEIPT.md`. |
| Receipts and verification | `B11AcceptanceReceipts` covers the 28 ownership/durability assertions plus 50 authoring-ontology assertions. Retained `B10AcceptanceReceipts` and `B10SyntheticStateAudit` verify the closed causal model. `B11_REVIEW_RECEIPT.md` records the production-breadth, semantic-distinction, composition, control-surface, and causal/durability reviews. |
| Corrections | Establishes explicit module, mutation, cadence, cache, schema, and migration ownership; adds bounded disabled-by-default profiling; removes duplicate settlement traversal; separates pending authoring resets from live history; distinguishes social subjects, meanings, and repeated practices; expands production vocabulary from actual mechanics; makes Political Beliefs and current order independently compositional; replaces full political profiles with explicit partial copy-on-apply sets; removes sparse source-shaped navigation and duplicate/raw-registry authoring surfaces. |
| Provenance | Begins from exact baseline `bd7f757e4825a921c4449062d41df45e474805c1` on `mallowfluff/b3-creation-flow-interaction`. The B11 directive and addendum, final B10 tree, executable schema catalog, native RimWorld state, governed fixture, five review lenses, and retained B10 receipts supplied the evidence. |
| Runtime fixture | Pending authoring epoch 11; regional plan schema 11; Culture and Political Beliefs schema 9. The current plan preserves its world, region, candidate, arrival, and map-scale identity with 3 factions, 4 settlements, 9 population groups, and 19 explicit established-program facts. Its eight Culture practice rows use concrete `practiceKey` identities and its active and keyed surfaces are byte-identical. |
| Deployment | `B11_DEPLOYMENT_RECEIPT.md` records closed-process replacement. The deployed DLL is byte-identical to the verified build at 3,670,528 bytes and SHA-256 `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1`. |
| Source relation | Follows `B10` and forms corrective patch unit `VU-038`, deriving `1.3.0.5-alpha`; no former-ledger label applies. |

## Record

B11 establishes the first durable CAO campaign boundary. The executable schema
catalog names one owner and version for every live state family. A streaming
preflight decides compatibility before Scribe mutates loaded state. Supported
B10 state retains stable semantic identities, represented facts, and historical
content while receiving an idempotent boundary and owner manifest. Additive
subsystems may initialize only from facts represented at the upgrade tick and
record that provenance. Unsupported live state fails visibly; pending creation
data remains governed by its separate replaceable authoring epoch.

The module manifest records authoritative state, authorized mutation, lifecycle,
cadence, dependencies, consumers, caches, invalidation, and diagnostics. The
post-B10 review retains cohesive owners and decomposes only actual ownership or
lifecycle conflicts. `AxisMaterializationModule` is removed as a former catch-all
surface; pure settlement wealth remains with settlement realization. Campaign
compatibility, pending authoring, profiling, and settlement wealth each have a
narrow owner. UI inspection remains read-only and one shared settlement traversal
serves consumers that previously repeated the same world scan.

`CAModuleProfiler` is disabled by default, bounded to a fixed production key set,
excluded from persistence, resettable, and exportable through diagnostics. It
records calls, elapsed time, maxima, examined and accepted objects, cache results,
deferred work, and failures at meaningful operation boundaries. Enabling it does
not alter the governed compatibility or fixture fingerprints.

The authoring addendum closes an ontology-capacity defect before that durable
boundary becomes authoritative. Representational capacity, current production
vocabulary, runtime realization, and the player control surface are recorded as
four separate layers. The active mechanics audit classifies 36 observable fact
families with no unclassified core mechanic. Production Culture grows from 12
social-subject examples to 45 grounded referents and from no distinct practice
vocabulary to 30 concrete repeated practices. Each practice names conduct,
actors, trigger or cadence, operator, authority, setting, material conditions,
evidence, consumer, provenance, and implicated subjects.

Social subjects are interpretive referents. Meanings are population-scoped
evaluations of those referents. Practices are repeated conduct supported by
represented evidence. The three are different records and candidate universes.
A B10 subject-shaped practice is converted only when actual longitudinal
evidence proves the corresponding conduct; invalid pending records are not kept
through aliases. The Culture composer presents one coherent operation with
distinct constituents, meanings, practices, disagreements, continuity, and
transition views. Internal source domains no longer become one-item tabs;
categories appear as navigation only when several human-relevant groups each
contain at least four items.

Political Beliefs remain normative. Current order remains instituted fact.
Offices, institutions, property, exchange, work, security, membership, and
self-identification keep their own factual identities. Both political records
support several compatible mechanisms per subject. Only an explicit absence
mechanism excludes standing leadership, local-order, or defense mechanisms on
the same subject. The synthetic `mixed` values and first-value compatibility
views are gone. Twelve belief sets and ten current-order sets are explicit
partial patches: application adds only listed mechanisms, preserves unlisted and
already-authored compatible facts, and copies values so later global edits cannot
change an authored world. No active Political Profile type remains.

The governed fixture is translated through the current schema rather than
preserving obsolete pending structures. Precisely described ownership and economy
`mixed` values expand to every mechanism they named; ambiguous B10 support is
rejected before owner load instead of being converted to an invented pair. Active and mirror files retain
the intended 3-faction, 4-settlement, 9-group, 19-operation composition. B10
causal receipts and the synthetic-state sweep remain the regression floor.

This batch closes only after clean build, five-lens review, publication, and
byte-identical deployment. Static evidence does not establish how the game looks
or plays. The next action is the operator's campaign start and retention test;
`B12` remains the next ordinary development batch.
