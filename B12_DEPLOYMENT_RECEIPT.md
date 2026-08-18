# B12 Deployment Receipt

| Field | Evidence |
|---|---|
| Deployment time | 2026-08-13 15:37 UTC / 2026-08-13 08:37 PDT |
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Source closure | `5e721c71e7b41c92ec69a5916cda45b431b067e9` |
| Governance closure | `57ba7907367a02a0f8b810f1edd394d0cae6ceb6` |
| Verified source | `Assemblies/ColonistAwareness.dll` |
| Deployment target | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness\Assemblies\ColonistAwareness.dll` |
| Source size | 3,911,680 bytes |
| Target size | 3,911,680 bytes |
| Source SHA-256 | `577B814FB026E7903BF70C4499C4B47B1E029AB53C568AEA9464D9166FDFC251` |
| Target SHA-256 | `577B814FB026E7903BF70C4499C4B47B1E029AB53C568AEA9464D9166FDFC251` |
| Replaced target SHA-256 | `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` (verified B11 assembly) |

RimWorld process: **closed**

Byte-identical: **PASS**

Result: **PASS**

The verified B12 assembly was copied only after a final deterministic Release
rebuild and a closed-process check. A post-copy read compared the exact source
and target byte lengths and SHA-256 digests. No game window, save, or
operator-authored runtime state was opened or changed.
