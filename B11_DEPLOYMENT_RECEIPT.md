# B11 Deployment Receipt

| Field | Evidence |
|---|---|
| Deployment time | 2026-08-13 08:57 UTC / 2026-08-13 01:57 PDT |
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Source closure | `d83fbadb4eec2625ba985fb7fd6f64365bd9c313` |
| Governance closure | `8abfd501a9887748b5ca78bd02bf7aa92bebc549` |
| Verified source | `Assemblies/ColonistAwareness.dll` |
| Deployment target | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness\Assemblies\ColonistAwareness.dll` |
| Source size | 3,670,528 bytes |
| Target size | 3,670,528 bytes |
| Source SHA-256 | `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` |
| Target SHA-256 | `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` |

RimWorld process: **closed**

Byte-identical: **PASS**

Result: **PASS**

The verified B11 assembly was copied only after the RimWorld process had
closed. A post-copy read compared the exact source and target byte lengths and
SHA-256 digests. No game window, save, or operator-authored runtime state was
opened or changed.
