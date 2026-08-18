# B16 Deployment Receipt

Timestamp: 2026-08-18 13:48 UTC / 2026-08-18 06:48 PDT

| Gate | Result | Evidence |
|---|---|---|
| Process boundary | **PASS** | `RimWorldWin64` was absent immediately before and after deployment; Codex did not launch or manipulate the game |
| Verified candidate | **PASS** | 4,369,920 bytes at SHA-256 `EF56D9BADAAF82DB5E8269A3996F2F56A565BC8E564DB9A970C97F5B72C94342` |
| Worktree assembly | **PASS** | `Assemblies/ColonistAwareness.dll` matches the verified candidate |
| Installed assembly | **PASS** | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness\Assemblies\ColonistAwareness.dll` resolves through the installed worktree junction and matches the candidate |
| Byte identity | **PASS** | Both clean-build candidates, worktree assembly, and installed view have the same size and SHA-256 |
| Governed fixture | **PASS** | Active and mirror plans are byte-identical epoch-14/regional-schema-15 files with Culture schema 11 / registry 3 at 530,039 bytes and SHA-256 `004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF`; 289 compatible former rows remain live and one non-constituent row remains exact legacy evidence; campaign catalog 5 is the separate realized-campaign boundary |
| Retained acceptance | **PASS** | B10 synthetic, B10 75/75, B11 78/78, B12 113/113, B13 67/67, B14 24/24 plus Society execution, B15 execution, B16 54/54, the 258-carrier persistence census, and version replay 4/4 pass |

RimWorld process: **closed**

Byte-identical: **PASS**

Deployment establishes a testable installed artifact. It does not claim visual
or gameplay acceptance.
