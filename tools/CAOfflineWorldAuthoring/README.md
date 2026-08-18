# CA offline world authoring

This tool turns a RimWorld save into a portable, read-only authoring atlas and
queries that atlas for Starting Region candidates. It exists so the CA creator
can remain conversational: the operator describes a place, the agent searches
real saved-world facts, and only the candidates requiring visual judgment are
rendered.

The atlas is evidence from one exact saved planet. Its manifest records the
world identity, game build, active mod order, source hash, and topology. A draft
is not written into CA's pending-plan surfaces until the operator selects and
confirms a composition through the governed creator.

Commands:

```text
import  --save <file.rws> --out <atlas-directory> --game-root <RimWorld>
search  --atlas <atlas-directory> --intent <intent.json> [--limit 8]
compose --atlas <atlas-directory> --root <tile> --extent <4|6|8|10|12>
        [--orientation 0..5] [--arrival <member-tile>] [--out result.json]
render  --atlas <atlas-directory> --composition <result.json> --out view.svg
inspect --atlas <atlas-directory> --tile <tile>
```

The agent translates ordinary language into the small `intent.json` query
contract. The contract describes desired facts; it is not a UI vocabulary.
Unknown modded geography remains explicit. A result distinguishes three
boundaries instead of collapsing them into one claim:

- searchable atlas: exact saved-world facts can be queried;
- complete composition evidence: the selected connected areas and their
  saved geography are resolved;
- stage-ready draft: current `CARegionalPlan` materialization, natural-rock
  resolution, and production validation are connected and pass.

Search and visual comparison do not write a CA plan. Until the production
application gate is connected, every result remains an unapplied draft even
when its saved-world composition evidence is complete.

Candidate renders use two linked scales. The main map is exact saved-world
evidence: nearby coast, biomes, relief, roads, rivers, the selected region, and
the arrival point. The formation panel makes saved geography such as an atoll,
archipelago, cove, or coastal island visually legible. It is not a claim about
cell-exact landing terrain. That claim belongs only to a render produced by the
same `CARegionalProjectionKernel` used by in-game preview and generation.
