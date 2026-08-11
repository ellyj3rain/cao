| Document | Colonist Awareness Overhaul Governance |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.1-alpha` · closed batch tip `B7` · next `B8` |
<!-- cao:generated:version END -->
| Design authority | ellyj3rain |
| Repository | `GOVERNANCE.md` |
| Status | ACTIVE - operating discipline and model-facing instruction surface. |

# Colonist Awareness Overhaul — governance

Single instruction surface for any model working in this project. `AGENTS.md` is
the autoload shim pointing here. Host identity is a vantage, not
authority; the operator (ellyj3rain) is the design authority and the judge of how
the game LOOKS and PLAYS — code-level verification never settles that question.

`README.md` is the human entry point. This project carries the GZDS canonical
doc-pack: `MEMORY.md`, `CORE.md`,
`ARCHITECTURE.md`, `GOVERNANCE.md`, `DECISION_REGISTRY.md`, `FINDINGS.md`,
`BATCH_LOG.md`, `VERSION_MAP.md`, `ROADMAP.md`, `SESSION_STATE.md`, `VERSION`. Batch records live
under `Batches/` in one alphanumeric sequence. `MEMORY.md` indexes every root
document and states which surfaces are canonical, regulatory, append-only, or
superseded.

## What this is

A RimWorld 1.6 mod building a genuinely orthogonal framework over the game — not
compatibility patching. Four pillars (see `ARCHITECTURE.md`): **Disposition**
(psychology), **Knowledge** (epistemics), **Authority** (roles/politics/ideology),
**Doctrine** (tactical execution). Doctrine ships first and rides the game's native
substrate; the other three are what make the mod a framework.

## Ground rules

- **Verified APIs only.** Ground truth is the decompiled game source at
  `../rimworld-decompiled/` (sibling folder, NEVER committed or copied here) and
  `Data/*/Defs/`. Compile against the real ref assembly as arbiter. Never assert
  engine behavior from memory.
- **The game's own nomenclature and seams.** Think-tree insertion hooks, DutyDefs,
  Lords, JobDrivers, stats, hediffs. Harmony only where no seam exists.
- **Operator terminology.** Military behavior uses the operator's terms, gaps
  filled from authoritative sources (Ranger Handbook). Never invent doctrine or
  attribute invented intent.
- **Declarations are promises.** Settings copy and in-game text must match shipped
  behavior exactly.
- **Author causes, inspect consequences.** Simulation complexity does not justify
  exposing every analytical category as a player control. Authoring surfaces own
  concrete persistent facts with real consumers; continuous, relational,
  historical, and derived outcomes remain in the simulation and are explained
  where the relevant decision or inspection occurs.
- **No universal explanation mode.** Contextual explanation belongs to the object
  or decision that owns it. Presentation policy may not replace causal copy with
  global Compact, Standard, or Expanded authoring states or repeated detail
  toggles.
- **Social systems keep their boundaries.** Culture is persistent longitudinal
  social history; Ideoligion is RimWorld's native religious and moral substrate;
  Political Beliefs are normative commitments; institutions and adopted rules
  are realized order; observed practice is what people actually do. Agreement
  and contradiction remain representable rather than being collapsed for UI
  convenience.
- **Publishable-portable.** No operator-specific hardcoding; license-clean bespoke
  implementations (reference reading of other mods is fine; copying is not).
- **DLL changes need a full game restart; Defs XML hot-reloads in dev mode.**
- **Test-save continuity.** Never overwrite or delete the operator's real game, an
  original test baseline, or a deliberately preserved checkpoint. Work from a
  derived test save and save meaningful state changes without waiting for an
  explicit request; ordinary continuation should overwrite that working save. When
  an exact earlier state is worth revisiting, first create a separate, specific,
  non-generic checkpoint named for what it proves, then continue from the working
  save. Pure observation with no meaningful state change may close unsaved.

## Governance

- `CORE.md` — project identity and canonical component set. `MEMORY.md` — index
  of every root document with its canonical/superseded status. `SESSION_STATE.md`
  — current operational state and immediate next work; read it before claiming
  where anything stands.
- `ARCHITECTURE.md` — ratified shape. `GAME_ARCHITECTURE.md` — decompile-grounded
  study of the engine. `ROADMAP.md` — thread map + backlog.
- **Durable project records contain current ratified decisions, verified facts,
  active invariants, and unresolved junctions.** Mutable canonical specifications
  replace superseded interpretations in place. The governed batch and provenance
  system is the portable project history: closed record substance is append-only,
  while catalogs, crosswalks, threads, and version projections are regulatory.
- `DECISION_REGISTRY.md` and `FINDINGS.md` append substantive decisions, findings,
  corrections, and supersession records. Project convergence may normalize an
  obsolete tool identity, filename, or path in current regulatory metadata when
  the recorded decision or finding does not change. Substantive changes require a
  later correction or supersession record.
  `BATCH_LOG.md` is the chronological index to the append-only records in
  `Batches/`. `Batches/THREADS.md` is the permanent, series-neutral thematic
  classification. `VERSION_MAP.md` is the chronological version-unit projection.
  `Batches/FORMER_LABELS.md` and `Batches/THREAD_ID_CROSSWALK.md` resolve former
  batch and temporary thematic identifiers. These indexes and projections are
  regulatory; batch substance and its recorded provenance are historical
  evidence. Local Git commits are supporting engineering evidence when retained.
- Batch identifiers share one namespace across letter eras. The A sequence is
  closed at `A102`; development continues with `[B#]` in the same directory and
  log. `B7` is closed and `B8` is next. A new letter does not create another
  history tree, generator, catalog, or projection layer.
- The records now under `Batches/` establish this append-only contract. The
  immediately preceding generated history tree was a regulatory projection, not
  a second set of closed batch records. Local Git may retain that engineering
  state; the portable project history does not depend on it.
- A closed batch's date, name, substance, commits, receipts, corrections, and
  provenance are not rewritten. Current regulatory references in its metadata and
  navigation, including series-neutral thread identifiers and forge locators, may
  be corrected without changing
  that substance. Later implementation, correction, or verification receives the
  next batch identifier. Small commits remain split by layer (governance / source /
  defs / reference), and subjects open with the development-batch prefix.
- Pure project maintenance uses `[REPO]`. It may correct navigation, current
  documentation, filenames, or regulatory indexes without changing runtime
  behavior, adding a version unit, or consuming the next batch.
- Automated tools do not receive Co-Authored-By trailers in commits.
- The operator owns: architectural ratification, public remotes, destructive ref
  work, version-policy changes.

## Project history and publication

| Surface | Authority and purpose |
|---|---|
| Current local project tree | Canonical implementation, content, documentation, and governed records. |
| Governed batch and provenance system | Portable project history. `Batches/`, `BATCH_LOG.md`, thematic indexes, the version map, decisions, findings, receipts, and source provenance travel with the project. |
| Local Git history | Optional engineering continuity and supporting evidence. It may retain work that predates the published snapshot, but the portable project history does not require that commit graph. |
| Published forge history | Distribution history for the current hosted snapshot and later publications. It begins at the canonical snapshot selected for publication; a forge or remote is not the project identity. |

GitHub is the current forge. A later GitHub reset, GitLab mirror, or replacement
host changes publication history, not the governed project history. Commit hashes
recorded in batch files remain local engineering provenance even when a forge does
not expose those objects.

## Batch, thread, and version discipline

- **Batch** is the atomic chronological development record. The closed history is
  `A1` through `A102`, followed by `B1` through `B7`; `B8` is the next identifier.
- **Thread and family** are permanent, series-neutral semantic classifications.
  `T-*` and `TF-*` may connect nonadjacent batches across any letter sequence, and
  one batch may participate in several threads.
- **Version unit** is a chronological capability unit containing one or more
  contiguous batches. Version units partition the batch chronology without gaps,
  overlap, or reordering. Each unit carries one tier and one resulting root
  version. Related work that returns after another version unit remains later.

The root hierarchy follows the Neo odometer mechanism:
`major > minor > kohai > patch > hotfix`, rendered as
`major.minor.kohai.patch-maturity`. Hard caps are `MINOR=12`, `KOHAI=16`, and
`PATCH=24`; overflow rolls mechanically to the next coordinate. `hotfix` uses
patch arithmetic. `minor` establishes a new player-visible simulation capability
or authoring/runtime contract. `kohai` extends, integrates, or structurally
matures an existing capability. `patch` repairs or verifies in place. `major`
marks a formal release, project-identity, or supported-compatibility boundary.
Maturity advances independently through `pre-alpha`, `alpha`, `beta`, `rc`, and
GA.

`tools/version-model.mjs` is the executable replay source. It derives `VERSION`,
the generated version stamps, and `VERSION_MAP.md`; validates exact A1-B7
coverage; checks the thematic namespace; and fails if the declared next batch has
already been consumed. Mechanically derived version facts are generated and
gated, not hand-typed. Thirty-four evidenced version units derive
`1.3.0.1-alpha`.

When new development begins, the first batch opens the next contiguous version
unit. Immediately following batches may share that version only when they form one
continuous capability unit and the grouping reason is recorded. The unit's full
content determines its tier. A later return to the same thread opens a later
version unit; thread continuity never moves a batch backward or merges
noncontiguous time.
