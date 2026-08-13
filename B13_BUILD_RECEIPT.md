# B13 Build Receipt

Timestamp: 2026-08-13 23:30 UTC / 16:30 PST
Source commit: `e89d9367b83652c6250a62d2419d605c4e2f9b03`

## Release build

| Fact | Evidence |
|---|---|
| Project | `Source/ColonistAwareness.csproj` |
| Configuration | `Release` |
| SDK | .NET SDK 9.0.316 through the user-local `dotnet` host |
| Output | `Assemblies/ColonistAwareness.dll` |
| Size | 3,973,120 bytes |
| SHA-256 | `889F81BD696627359F6EFB096304303CAE3066D7870F001E1316DAD2D4324372` |
| Warnings | **0** |
| Errors | **0** |
| Deterministic rebuild | **PASS** - two no-incremental builds from the frozen source produced identical size and SHA-256 |
| Result | **PASS** |

The B10, B11, B12, and B13 acceptance projects, the B13 fixture generator,
and the net472 production-DLL receipt also built in Release with zero warnings
and zero errors. The production receipt loaded the assembly and executed Culture
normalization, exhaustive built-in expertise-domain coverage, deterministic
direct-question uptake, and direct source/moral appraisal. The assembly above
is the immutable deployment candidate. Runtime deployment is recorded
separately and only while RimWorld is closed.

## Regression gates at source freeze

| Gate | Result |
|---|---|
| B10 causal acceptance | **75/75 PASS**; active and keyed fixture mirror identical |
| B11 acceptance | **78/78 PASS** |
| B12 acceptance | **113/113 PASS** |
| B13 Culture completion | **67/67 PASS**; production DLL is the executed model authority |
| Fixture | **PASS**; 3 factions, 4 settlements, 9 population groups, 19 program facts, 8 Culture records, and 194 distributions; active and mirror SHA-256 `27354E00647007BD54EC5D5E0B29E7E408E40CEEBE4A3CAAADE068D23BDCA101` |
| Version model | **4/4 PASS**; 40 units cover A1-B13 and derive `1.4.1.0-alpha`; B14 remains next |
