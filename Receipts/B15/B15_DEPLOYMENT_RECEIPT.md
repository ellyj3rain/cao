# B15 Deployment Receipt

Timestamp: 2026-08-18 06:27 UTC / 2026-08-17 23:27 PDT

| Gate | Result | Evidence |
|---|---|---|
| Process boundary | **PASS** | `RimWorldWin64` was absent immediately before deployment; Codex did not launch or manipulate the game afterward |
| Verified candidate | **PASS** | 4,233,216 bytes at SHA-256 `DE6312FF9CF2F4B052AACDE82E93C113AF231D36494F93667266FB2D415F0764` |
| Worktree assembly | **PASS** | `Assemblies/ColonistAwareness.dll` matches the verified candidate |
| Installed assembly | **PASS** | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness\Assemblies\ColonistAwareness.dll` resolves through the installed worktree junction and matches the candidate |
| Byte identity | **PASS** | Candidate, worktree, and installed views have the same size and SHA-256 |
| Governed fixture | **PASS** | Active and mirror plans are byte-identical schema-14/4/1 files at 407,892 bytes and SHA-256 `A0FF1B43CF3A5AFAFE72DA34360B247B7D06CB03DE903AEA251F8FD5D5297C82` |
| Retained acceptance | **PASS** | B10 synthetic, B10 75/75, B11 78/78, B12 113/113, B13 67/67, B14 24/24, B15 execution, and the 257-carrier persistence census pass |

RimWorld process: **closed**

Byte-identical: **PASS**

Deployment establishes a testable installed artifact. It does not claim visual
or gameplay acceptance.
