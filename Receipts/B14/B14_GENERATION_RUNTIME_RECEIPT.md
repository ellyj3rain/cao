# B14 generation runtime receipt

This receipt records a fresh production-path RimWorld run after the B14 generation repair. It is runtime generation evidence, not operator visual or play acceptance.

## Controlled route

| Surface | Selected state |
|---|---|
| New colony | Crashlanded |
| Storyteller | Cassandra Classic |
| Difficulty | Community builder |
| Save mode | Reload anytime |
| World seed | `alysaliu` |
| Coverage | 100% |
| Arrival tile | 389638 |
| Candidate | `613b1fe44104` |
| Region | `CA-RG-EB596A12` |
| Authored composition | 3 factions; 4 settlements; 4 populated settlements; 1 minority population group |
| Founding state | Sea Commons; Free Path; 13 political mechanisms; shared survival |

## Fresh runtime result

Recorded at `2026-08-14T04:18:50.0715793Z` after the controlled process start:

```text
[CA][B14][GenerationReceipt] PASS region=CA-RG-EB596A12 gate=pass expected=4 materialized=4 slots=0,1,2,3 ownership=4/4 populations=4/4 relations=1-2:NativeInitial:Hostile,1-3:Authored:Hostile,2-3:NativeInitial:Hostile unsupportedSourceProbe=rejected
```

The runtime materializer produced all four settlement slots, resolved all four ownership and population projections, retained each relation's represented source, and rejected the executable unsupported-source probe.

## Preserved artifacts

| Artifact | Bytes | SHA-256 | Meaning |
|---|---:|---|---|
| `runtime/active-schema12-before-passing-runtime.xml` | 236737 | `00CE3B495DDA48D551ABD63D575DD3599B20D728D41AFC8A7AD76075C00E15F2` | Input plan before the passing run |
| `runtime/keyed-schema12-after-passing-runtime.xml` | 416147 | `68C6675D61925CA4B4AD2A2B4170E79B3D2CB5865E6EAC5D037415355747A67D` | Confirmed and materialized runtime mirror |
| `runtime/CA-B14-regional-generation-receipt.txt` | 252 | `977432124293AC77AAD2A151C0506A1663ED0E8CC54B7F2A816DA0A9B9E008E0` | Exact fresh receipt line |

## Assembly and operator-ready fixture

| Contract | Result | Evidence |
|---|---|---|
| Verified source assembly | **PASS** | 3980288 bytes; `9301BB5A00D7BCE17C372000F15AAF18C0EB81BABC0A3468823844BCCE15E7AC` |
| Deployed assembly byte identity after exit | **PASS** | deployed DLL has the same byte count and SHA-256 |
| RimWorld closed before fixture reset | **PASS** | no RimWorld process remained |
| Active fixture restored to an unconfirmed test boundary | **PASS** | schema 12; 3 factions; 4 settlements; exact relation sources retained |
| Active and keyed fixture mirrors agree | **PASS** | both hash to `00CE3B495DDA48D551ABD63D575DD3599B20D728D41AFC8A7AD76075C00E15F2` |

Overall generation-path result: **PASS**.

