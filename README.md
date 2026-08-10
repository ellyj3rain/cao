# Colonist Awareness Overhaul

Colonist Awareness is a RimWorld 1.6 overhaul. Pawns act on what they know. Settlements persist as real places with people, supplies, services, beliefs, and authority. Factions make decisions and carry consequences forward instead of resetting to isolated game events.

<!-- cao:generated:version BEGIN -->
Current version: `0.12.4.0-alpha`. Implementation is complete through batch `B1`; `B2` is the next development batch. The verified assembly is deployed for operator runtime testing. Static verification does not substitute for how the game looks and plays.
<!-- cao:generated:version END -->

## Framework

The project is organized around four connected concerns:

- **Disposition** - individual judgment shaped by traits, skills, condition, relationships, and experience.
- **Knowledge** - actors respond to what they can observe, remember, receive, and trust.
- **Authority** - roles, organizations, beliefs, legitimacy, and player control determine who may decide and act.
- **Doctrine** - tactical and operational behavior uses the same knowledge and authority substrate.

The current regional model is direct: regions contain factions and settlements; settlements contain population groups and organizations. Culture, Ideoligion, political beliefs, development, services, facilities, population, and provisions are authored or generated where they materially exist. World tendencies author causes; placement, relations, settlement pattern, frontier holdings, and settlement scale are realized once, saved, and consumed by generation.

## Requirements

- RimWorld `1.6`
- Harmony
- Biotech
- Ideology

The package identifier is `ellyj3rain.colonistawareness`. Dependencies and load order are declared in [`About/About.xml`](About/About.xml).

## Build

From the project root:

```powershell
dotnet build Source/ColonistAwareness.csproj -c Release
```

The Release assembly is written to `Assemblies/ColonistAwareness.dll`. DLL changes require a full RimWorld restart.

## Project map

| Path | Purpose |
|---|---|
| `Source/` | C# implementation |
| `Defs/`, `Patches/` | RimWorld definitions and integration patches |
| `Languages/English/` | Player-facing English text |
| `About/` | Mod metadata and dependency declaration |
| `Batches/` | Append-only batch records in one alphanumeric namespace |
| `BATCH_LOG.md` | Chronological batch catalog |
| `Batches/THREADS.md` | Series-neutral thematic families and threads |
| `VERSION_MAP.md` | Chronological, tier-bearing version units |
| `tools/version-model.mjs` | Version replay, generated stamps, and structural checks |
| `ARCHITECTURE.md` | Current framework architecture |
| `GOVERNANCE.md` | Project operating rules |
| `SESSION_STATE.md` | Current build, deployment, and runtime-test boundary |

## Project history

The governed batch and provenance records are the project's portable history. They travel with the current tree and do not depend on a particular Git host. `A1` through `A102` and `B1` share one sequence under [`Batches/`](Batches/); a new letter does not create a separate history hierarchy. [`BATCH_LOG.md`](BATCH_LOG.md) is the chronological view. [`Batches/THREADS.md`](Batches/THREADS.md) classifies related work across nonadjacent batches with permanent `TF-*` and `T-*` identifiers. [`VERSION_MAP.md`](VERSION_MAP.md) partitions chronological work into contiguous capability units under the four-coordinate odometer.

Local Git may retain earlier engineering history. The published forge history is a separate distribution record: it begins at the canonical snapshot selected for publication and continues with later published changes.

Run the governance check with:

```powershell
node tools/version-model.mjs --check
```

## Status and verification

The code, setup flow, generation, saves, definitions, and documentation use the same current concepts: factions, settlements, population groups, organizations, starting conditions, and generated provisions. The eleven World tendencies controls each own one direct causal variable, and fixed-seed receipts check consumer reach, one-variable isolation, serialization, readback, and override ownership. Earlier experimental save formats are not supported.

The Release project builds against the RimWorld 1.6 reference assemblies. Regional authoring, world generation, settlement placement, population, geography, and provisioning still require operator runtime acceptance in the real game flow. See [`SESSION_STATE.md`](SESSION_STATE.md) for the exact gate.

## License and provenance

Colonist Awareness is licensed under GPL-3.0. See [`LICENSE`](LICENSE), [`CREDITS.md`](CREDITS.md), and [`UPSTREAM_SOURCES.md`](UPSTREAM_SOURCES.md) for attribution and incorporated-source provenance.
