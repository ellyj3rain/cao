# B11 Build Receipt

Date: 2026-08-13 08:50 UTC / 2026-08-13 01:50 PDT
Source commit: `d83fbadb4eec2625ba985fb7fd6f64365bd9c313`

## Release build

| Fact | Evidence |
|---|---|
| Project | `Source/ColonistAwareness.csproj` |
| Configuration | `Release` |
| SDK | .NET SDK 9.0.316 through the user-local `dotnet` host |
| Output | `Assemblies/ColonistAwareness.dll` |
| Size | 3,670,528 bytes |
| SHA-256 | `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` |
| Warnings | **0** |
| Errors | **0** |
| Result | **PASS** |

- Warnings: **0**
- Errors: **0**
- Result: **PASS**

The B10 acceptance, B10 synthetic-state audit, B11 acceptance tool, fixture
generator, and version-model test projects also built in Release with zero
warnings and zero errors. The assembly hash above is the immutable deployment
candidate. Runtime deployment is recorded separately and only while RimWorld is
closed.

## Regression gates at source freeze

| Gate | Result |
|---|---|
| B10 causal acceptance | **75/75 PASS**; active and keyed fixture mirror identical |
| B10 synthetic-state audit | **PASS**; 152 occurrences classified; 0 unresolved Critical/High |
| Version model | **4/4 PASS**; A1-B11 partition and `1.3.0.5-alpha` replay agree |
| B11 executable acceptance before ordered closure receipts | **75/78 PASS**; only build, deployment, and review receipt gates were intentionally not yet published |
