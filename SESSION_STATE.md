| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.4-alpha` · closed batch tip `B10` · next `B11` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session State

Updated 2026-08-12 UTC / 2026-08-12 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, review, deployment, and operator runtime evidence remain separate.

## Version Control and Governed History

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Exact B10 baseline | `f372c6b50294f631a370d111b1641536bade7876` |
| B10 source closure | `3d93e4405ddbcdd5e842f9dd0245de8fe157b974` |
| B10 governance closure | `a531da1d719d6daf4f0b1fea7d260cf5589c2975` carries the append-only B10 record, generated version projections, evidence reports, and README twin. |
| B10 deployment receipt | The following receipt commit records the byte-verified active DLL after the required post-publication deployment. |
| Publication | Source and governance fast-forwarded to the current remote branch before deployment; the final receipt follows by another normal fast-forward. |

The portable chronology contains 112 closed batches: `A1-A102` and `B1-B10`.
Thirty-seven contiguous version units cover that chronology exactly once. B10 is
`VU-037`, a patch unit deriving `1.3.0.4-alpha`. Series-neutral `T-*` threads
classify work without changing chronology. `B11` remains next; feature work does
not begin before aggregate operator runtime validation.

## Current Causal Contract

| Surface | Current state |
|---|---|
| Domestic identity | Persistent factual units form from represented partner, kin, residence, or explicit co-residence evidence; unlinked residents self-provision individually |
| Residence | Typed persistent assignments reconcile native faction and settlement transitions before social and institutional consumers |
| Practiced capability | Medicine, production, logistics, civic, research, security, commerce, and communications summarize direct domain evidence without random jitter |
| World tendencies | Read models or generation pressures at their owning surface; they never instantiate programs or institutions |
| Settlement programs | Explicit need and exact operator, standing, knowledge, labor, material, target, access, funding or maintenance, runtime, and failure contract |
| Established-program authoring | Starting Region settlement Details establishes or removes one operational fact for one exact population-group operator |
| Program assets | Every placed Thing or zone role persists exact program, operator, signature, role, and native identity; rebuilding atomically rebinds it |
| Provision | Exact domestic, communal, or authority operator; materialization is preflighted and atomic; stock is live, unique, local, and reachable |
| Security | Live Defense program, exact assets and operator, armed typed-resident labor, and persisted assignments; patrols use only live assigned guards |
| Research and later work | Exact program/operator/signature survives research, cultivation, roads, repair, rebuilding, and completion revalidation |
| Culture and Political Beliefs | Culture shapes meaning and participation; Political Beliefs remain normative; neither creates current order, authority, work, knowledge, or material |
| Confirmation | Consumes persisted facts and production realization; it does not reroll identities, programs, provisions, Culture, relations, or institutions |

One-option setup fields remain facts rather than controls. Ordinary player UI
does not expose generation provenance, schema language, receipt keys, or raw
authored/observed implementation identifiers.

## Governed Runtime Fixture

| Fact | Value |
|---|---|
| Active plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Keyed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Fixture SHA-256 | `F539239672875C668F750A09CCC370B4B6AFD8165B0F6FA7D37543AFE8391440` on both files |
| File size | 65,717 bytes on both files |
| Schema | authoring epoch 10; regional plan 10; settlement record 8; program and entry 4; operational fact 3; program asset 1; provision 5; Culture and Political Beliefs 8 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 9 population groups; 19 explicit established-program facts |
| Megaeth | Food preparation, Storage, Medicine, Production, Gathering |
| Red Cervexa | Food preparation, Storage, Medicine, Production, Custody, Gathering |
| Tascan Bramble | Food preparation, Storage, Medicine, Gathering |
| Black Delta | Food preparation, Storage, Medicine, Gathering |

The recovery masks are historical evidence only. The fixture generator translates
the supported composition through the production authoring kernel and omits
derived programs, provisions, capability, domestic units, and world summaries.
Runtime `RefreshDraftRealization` owns derivation. Both surfaces parse, retain the
same identity and composition, and agree byte for byte.

## Verification and Deployment

| Gate | Result |
|---|---|
| Causal-provenance audit | Complete ownership and program-by-program ledger; no unresolved Critical or High finding |
| Synthetic-state sweep | PASS - 152 active RNG/hash occurrences classified; 0 unresolved Critical/High |
| B10 acceptance | PASS - 75/75 |
| Fixture round-trip | PASS - 3 factions, 4 settlements, 9 groups, 19 operational facts; active and mirror byte-identical |
| Module-impact review | New and changed owners record state, mutation, lifecycle, cadence, dependencies, consumers, receipts, and B11 review status |
| Release build | PASS - 0 warnings; 0 errors |
| Assembly | 3,415,040 bytes; SHA-256 `DD0EC6DB5C7D0C3B607C1D6FFBE813F6469CD4796CAB10E5C0D70D4223FFDF60` |
| Deployment | PASS - RimWorld was closed; the active Steam mod junction resolves to the project root, whose DLL is byte-identical to the verified build at 3,415,040 bytes and SHA-256 `DD0EC6DB5C7D0C3B607C1D6FFBE813F6469CD4796CAB10E5C0D70D4223FFDF60` |

Static evidence establishes the source, serialization, fixture, and deployment
candidate. It does not select options, advance the game, or establish visual and
gameplay acceptance.

## Operator Runtime Boundary

B10 is the aggregate pre-runtime convergence batch. After publication and
byte-verified deployment, the next action is the operator test. Codex must not
advance creation, choose authoring values, start the game, alter saves, or claim
how the game looks and plays on the operator's behalf.

| Observation | Operator check |
|---|---|
| Creation load | Open the current pending plan and confirm all 3 factions, 4 settlements, 9 population groups, and 19 established operations remain present. |
| Program authoring | Establish and remove a settlement operation; confirm the named population-group operator and unavailable-contract explanation remain intelligible. |
| Map generation | Generate the selected region and confirm persisted operations materialize without rerolling or dropping the composition. |
| Program assets | Inspect multi-node programs and confirm every visible node belongs to the intended operation; destroy/rebuild only if useful to the test. |
| Domestic provision | Confirm residents without factual shared-unit evidence do not become arbitrary households and retain usable self-provision. |
| Provision | Confirm operators, nodes, stock, and access agree with the authored settlement and no one stock pile appears to supply several arrangements incorrectly. |
| Security | Confirm patrols and security activity use actual armed assigned residents rather than any convenient pawn. |
| Culture and order | Confirm Culture, Political Beliefs, current order, and actual institutions remain distinct and legible. |
| Capability | Confirm capability summaries explain actual actors, work, knowledge, material, history, and blockers without creating new programs. |
| Save/load | Save and reload the generated state; confirm membership, program/operator/asset identity, provision, security assignments, and blockers remain stable. |
| Runtime health | Inspect the first-hour log and report any error, dropped work, impossible access, or visibly synthetic relation. |

## Environment and Preserved Evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed; active runtime state is not asserted without a launch.
- `B10_CAUSAL_PROVENANCE_AUDIT.md`, `B10_ACCEPTANCE_RECEIPTS.md`, and
  `B10_SYNTHETIC_STATE_SWEEP.md` are the current static closure evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
