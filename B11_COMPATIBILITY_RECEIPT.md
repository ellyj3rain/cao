# B10 to B11 Compatibility Receipt

Date: 2026-08-12

This is the strongest available non-interactive compatibility exercise. It uses the current governed fixture as an identity-preserving payload, synthetic owner-scoped B10 save envelopes for preflight decisions, and the production compatibility/profiler kernels. It is not represented as a true RimWorld save round trip.

| Fact | Evidence |
|---|---|
| Active/mirror agreement | byte and logical match |
| Input SHA-256 | `6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04` |
| Composition | 3 factions; 4 settlements; 4 population groups; 19 established-program facts |
| Stable identity digest | `DA1720BBC8F33025451EDCD944B179CFC58D09D78CAAD9F2BE40ACC9476F6490` before and after |
| First upgrade envelope SHA-256 | `FB15161EDB8219084445AEF4CB4F3F437352A76EEEDB0822FD1A8CB588071E58` |
| Second upgrade envelope SHA-256 | `FB15161EDB8219084445AEF4CB4F3F437352A76EEEDB0822FD1A8CB588071E58` |
| Idempotence | PASS |
| Historical-element count | 8 before; 8 after; no synthetic history added |
| Profiler exercise | 1 enabled call(s); disabled path recorded zero; governed fingerprint identical |
| Emitted-save boundary | exact game/world/map component scope and cardinality validated before SafeSaver swap; repeated nested records validated per occurrence |
| Payload seal | strict terminal SHA-256; altered payload rejected before Scribe owner load |
| Source fixture mutation | none; upgrade operates on a copy and unsupported decisions return before copy mutation |
| Creation derivation | not invoked; compatibility metadata wraps an unchanged state subtree |
| Remaining live proof | Operator creates/saves the retained campaign, later loads that same save under a compatible DLL, inspects migration/log output, continues play, then saves only after validation |

Upgrade provenance is explicit: `initialized at B11 upgrade from represented B10 state; no earlier history inferred`. Stable region, faction, settlement, population-group, and established-program fact/operator identities are included in the digest.
