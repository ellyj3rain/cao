# B12 Build Receipt

Date: 2026-08-13 15:25 UTC / 2026-08-13 08:25 PDT
Source commit: `5e721c71e7b41c92ec69a5916cda45b431b067e9`

## Release build

| Fact | Evidence |
|---|---|
| Project | `Source/ColonistAwareness.csproj` |
| Configuration | `Release` |
| SDK | .NET SDK 9.0.316 through the user-local `dotnet` host |
| Output | `Assemblies/ColonistAwareness.dll` |
| Size | 3,911,680 bytes |
| SHA-256 | `577B814FB026E7903BF70C4499C4B47B1E029AB53C568AEA9464D9166FDFC251` |
| Warnings | **0** |
| Errors | **0** |
| Deterministic rebuild | **PASS** - rebuilding the frozen source left size and SHA-256 unchanged |
| Result | **PASS** |

The B10 acceptance, B10 synthetic-state audit, B11 acceptance, B12 acceptance,
and B12 fixture-generator projects also built in Release with zero warnings and
zero errors. The assembly above is the immutable deployment candidate. Runtime
deployment is recorded separately and only while RimWorld is closed.

## Regression gates at source freeze

| Gate | Result |
|---|---|
| B10 causal acceptance | **75/75 PASS**; active and keyed fixture mirror identical |
| B10 synthetic-state audit | **PASS**; 152 occurrences classified; 0 unresolved Critical/High |
| B11 acceptance | **78/78 PASS** |
| B12 acceptance | **113/113 PASS** |
| Fixture migration | **PASS**; byte-idempotent at SHA-256 `5862A5F810454CB3537836289051E3528A1F6888BF3D11495CD3CC53EAD876B9` |
| Version model | **4/4 PASS**; 39 units cover A1-B12 and derive `1.4.0.0-alpha`; B13 remains next |
