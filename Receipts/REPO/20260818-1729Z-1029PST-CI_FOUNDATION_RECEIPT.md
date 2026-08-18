# Repository CI Foundation Receipt

Timestamp: 2026-08-18 17:29 UTC / 10:29 PST
Updated: 2026-08-18 17:51 UTC / 10:51 PST

| Fact | Evidence |
|---|---|
| Scope | Pure `[REPO]` maintenance over merged B16 baseline `788fcd1ed009010f3e12a7dfbfaccde003e456d1`; B17 remains unconsumed. |
| Build authority | `global.json` pins .NET SDK `8.0.423`; `Source/packages.lock.json` locks RimWorld references `1.6.4871`, Harmony `2.4.2`, and the net472 reference graph. |
| Production build | Two clean Release rebuilds completed with 0 warnings and 0 errors and emitted byte-identical assemblies. |
| Shipping identity | `ColonistAwareness.dll` is 4,368,384 bytes at SHA-256 `2710DDDAC506B4BA6910456F4EF965207C54431EA2D25E53CAFAA78E20047545`; both clean builds, the tracked assembly, and the installed junction view are byte-identical. |
| Support projects | All 18 projects declared by `tools/ci/projects.txt` compile with warnings treated as errors. Older conversion-only fixture generators remain historical provenance rather than being rewritten against the current ontology. |
| Generated evidence | B10 synthetic-state audit reports 166 classified occurrences and 0 unresolved Critical/High findings; the 258-carrier persistence census reports 0 unclassified or invalid routes; both regenerate without governed-file drift. |
| Dependencies | NuGet audit reports 0 direct or transitive vulnerabilities and 0 high or critical findings. |
| GitHub execution | PR `#5` final head `7e87dca60bf6ec9193cc8824ae51a3c850dce7d4` passed `ci-verify` run `32167378903`, `dependency-scan` run `32167378889`, and `codeql-csharp` run `32167378883`. The final jobs carry 0 annotations and code scanning reports 0 open alerts. GitHub merged the signed squash as `f71c1dbc33b22a2550de0294ac7ded4962cdea68`. |
| GitHub enforcement | `main` strictly requires `ci-verify` and `dependency-scan` through a pull request. Signed commits, linear history, conversation resolution, and admin enforcement are enabled; force pushes and deletion are disabled. Squash is the only merge method and merged branches delete automatically. Secret scanning, push protection, vulnerability alerts, automated security fixes, and Dependabot security updates are enabled. |
| GitLab | `.gitlab-ci.yml` calls the same repository-owned verification contract. No CAO GitLab remote is configured in this checkout, and the configured private GitLab API was unreachable during this run. |
| Runtime boundary | RimWorld is closed. CI did not launch or operate the game, edit a save, or claim visual/gameplay acceptance. The DLL change establishes a pinned-compiler binary baseline from unchanged B16 source. |

Result: **PASS**
