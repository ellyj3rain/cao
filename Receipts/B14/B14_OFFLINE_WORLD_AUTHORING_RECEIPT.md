# B14 offline world authoring receipt

Date: 2026-08-15

The B14 creator can now search exact saved planets outside RimWorld. The
conversation owns the authoring flow; maps are generated as focused decision
receipts for candidate location, footprint, orientation, arrival, and later
settlement placement. Search and rendering do not write pending-plan files.

## Portable world evidence

| Atlas | World identity | Source save SHA-256 | Tiles | Result |
|---|---|---|---:|---|
| Alysaliu | `alysaliu\|1\|Algorab Markab` | `93F023636B976FCDE5A3CCF06DEE4DF470FD9865B8C629CCA34F48528BAC034A` | 590,492 | imported |
| Mass Driver | `mass driver\|1\|Ain-XII` | `D5A3DC690E8904EEAC147B06A8DF428590218155D63882C9F09FA2479DC17302` | 590,492 | imported |

Each atlas retains the saved biome, elevation, hilliness, temperature,
rainfall, swampiness, pollution, feature, road, river, mutator, and world-object
arrays; reconstructed surface topology; active mod fingerprint; definition
catalog; and source identity.

## Exact composition reconstruction

| Historical composition | Request | Expected and realized members | Result |
|---|---|---|---|
| active Alysaliu B14 candidate | root `389638`; extent 10; orientation 2/6 | `389638,389639,32136,389640,389641,170943,170941,488022,389637` | exact; correctly reports only 9 valid members for the requested 10 |
| Mass Driver Enler candidate | root `85717`; extent 12; orientation 4/6 | `85717,448659,448658,216937,41973,448663,114874,246092,114872,448662,246093,333564` | exact 12/12 |

These sequences prove that the offline tool reconstructs CA's selected
connected composition instead of approximating a hex neighborhood.

## Conversational query proof

The operator example was translated into explicit required and preferred facts
for Mass Driver rather than into a new UI vocabulary.

| Query | Distinct sites | Search time | Result |
|---|---:|---:|---|
| tropical-rainforest root with a coastal atoll; extent 12 | 7 | 1.84 s | exact candidates found |
| same composition also containing an archipelago | 0 | 1.58 s | conjunction truthfully rejected |
| tropical-rainforest root with archipelago in the 12-area composition | 8 returned | 2.53 s | alternatives found; identical ranking to the original exhaustive 19.84 s search |

Candidate `29606` is twelve tropical-rainforest areas rooted on a coastal
atoll. Candidate `36420` carries archipelago and coastal-island geography but
spans two tropical-rainforest and ten arid-shrubland areas. The difference is
part of the decision, not something the search hides.

Visual receipts:

- `candidate-29606-landscape.svg` / `candidate-29606-landscape.png`
- `candidate-36420-landscape.svg` / `candidate-36420-landscape.png`

They are stored with the Mass Driver atlas under
`Config/CAOfflineWorldAtlases/mass-driver-ain-xii`. Selected tiles, context,
root, arrival, saved biome composition, roads, rivers, and geographic mutators
are visibly distinct.

The current renderer is landscape-first rather than a labeled hex inventory.
It shows a wider saved-world neighborhood with continuous biome texture,
coastline, relief, roads, and rivers; treats the selected extent as one outlined
region instead of twelve labeled cells; locates the arrival point directly on
that ground; and gives geographic features map callouts plus a linked formation
view. Exact saved-world facts remain visually and textually distinct from a
formation depiction. The renderer does not claim cell-exact terrain,
resources, fertility, or build sites before the production projection kernel
has generated them.

The remaining fidelity boundary is architectural: CA's production
`CARegionalProjectionKernel` already owns the continuous land/water silhouette,
coastline, biome field, relief, and formation realization used by preview and
generation. Reusing that same kernel in the offline creator is required before
the out-of-game arrival-area view can claim the exact deterministic projection
rather than a vivid depiction constrained by the saved geography.

## Readiness boundary

The result model keeps these states separate:

- saved-world composition evidence is complete when selected areas and their
  saved geography resolve without missing definitions;
- a draft is stage-ready only after current `CARegionalPlan` materialization,
  exact natural-rock resolution, and production validation are connected and
  pass.

The atlas search and visual comparison are usable now. Candidate drafts remain
explicitly not ready to save; no active or keyed pending-plan surface was
changed during this work.

## Verification

- `CAOfflineWorldAuthoring` Release build: 0 warnings, 0 errors.
- Creator skill updated so conversation is primary and visuals are summoned at
  spatial decision points. Its representation standard now requires linked
  landscape and arrival-area views and forbids presenting a flat hex inventory
  as the candidate; `quick_validate.py`: `Skill is valid!`.
- RimWorld was closed throughout write/build work.
- Worktree and installed mod DLL remained byte-identical and unchanged:
  3,999,232 bytes; SHA-256
  `8FE532A0F9A92581000C42F05DA577C7ADA9406D919E157F92B535C3D0399D25`.

Overall: **search and visual candidate authoring pass; production plan staging
remains an explicit open integration boundary.**
