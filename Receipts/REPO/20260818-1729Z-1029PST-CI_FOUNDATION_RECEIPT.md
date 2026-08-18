# Repository CI Foundation Receipt

Timestamp: 2026-08-18 17:29 UTC / 10:29 PST
Updated: 2026-08-18 17:39 UTC / 10:39 PST

| Fact | Evidence |
|---|---|
| Scope | Pure `[REPO]` maintenance over merged B16 baseline `788fcd1ed009010f3e12a7dfbfaccde003e456d1`; B17 remains unconsumed. |
| Build authority | `global.json` pins .NET SDK `8.0.423`; `Source/packages.lock.json` locks RimWorld references `1.6.4871`, Harmony `2.4.2`, and the net472 reference graph. |
| Production build | Two clean Release rebuilds completed with 0 warnings and 0 errors and emitted byte-identical assemblies. |
| Shipping identity | `ColonistAwareness.dll` is 4,368,384 bytes at SHA-256 `2710DDDAC506B4BA6910456F4EF965207C54431EA2D25E53CAFAA78E20047545`; both clean builds, the tracked assembly, and the installed junction view are byte-identical. |
| Support projects | All 18 projects declared by `tools/ci/projects.txt` compile with warnings treated as errors. Older conversion-only fixture generators remain historical provenance rather than being rewritten against the current ontology. |
| Generated evidence | B10 synthetic-state audit reports 166 classified occurrences and 0 unresolved Critical/High findings; the 258-carrier persistence census reports 0 unclassified or invalid routes; both regenerate without governed-file drift. |
| Dependencies | NuGet audit reports 0 direct or transitive vulnerabilities and 0 high or critical findings. |
| GitHub | PR `#5` exercised the new workflows on commit `bf3d445764eddd49eedac42939ea4383a2e2e1d1`: `ci-verify` run `32166461257`, `dependency-scan` run `32166461292`, and `codeql-csharp` run `32166461236` all passed. Code scanning reports 0 open alerts. Dependabot, CODEOWNERS, and the pull-request contract are present. `main` protection follows the merge. |
| GitLab | `.gitlab-ci.yml` calls the same repository-owned verification contract. No CAO GitLab remote is configured in this checkout, and the configured private GitLab API was unreachable during this run. |
| Runtime boundary | RimWorld is closed. CI did not launch or operate the game, edit a save, or claim visual/gameplay acceptance. The DLL change establishes a pinned-compiler binary baseline from unchanged B16 source. |

Result: **LOCAL AND HOSTED CHECKS PASS / MAIN PROTECTION PENDING**
