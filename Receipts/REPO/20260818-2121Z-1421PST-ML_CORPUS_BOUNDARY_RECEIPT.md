# ML corpus boundary receipt

| Field | Value |
|---|---|
| Timestamp | 2026-08-18 21:21 UTC / 14:21 PST |
| Classification | `[REPO]` repository maintenance; no development batch or version movement |
| Baseline | `28ed19859382f7ff16315c5f0939c6129aa39803` |
| Branch | `mallowfluff/repo-ml-corpus-boundary` |
| B17 isolation | Work performed in a separate worktree; active B17 source and Git index were not changed. |

## Boundary established

The current repository tree no longer tracks raw saves, Real Ruins snapshots,
normalized per-layout records, exact cohort membership, screening manifests,
partitions, caches, or private dataset-accounting state. The only tracked files
below `Corpus/PlayerBaseLayouts/` are:

- `README.md`, which declares the deny-by-default boundary; and
- `external/real-ruins-broad-profile.json`, which contains aggregate counts and
  quantiles without per-layout rows.

Thirteen previously tracked substantive files were removed from the Git index:
the operator corpus manifest, two per-layout Real Ruins manifests, nine
normalized operator layouts, and the operator layout index. `git rm --cached`
was used. The files were not deleted from either local corpus copy.

`.gitignore` now denies the complete corpus tree by default and explicitly
re-admits only those two public files. Representative raw, normalized,
acquisition, training, validation, test, screening, and private-governance paths
all resolve as ignored.

## Local evidence retained

The active local corpus, excluding its new governance ledger, remains exactly:

| Measure | Result |
|---|---|
| Files | 12,505 |
| Bytes | 670,171,319 |
| Inventory digest | `D59A1FF437C4B8B3328BAEE8347312BA5AC6B7254C19AA88EE0D0DDCD0882B2E` |
| Inventory algorithm | SHA-256 over newline-terminated, case-insensitively path-sorted `relativePath\|byteLength\|fileSHA256` rows using Windows separators and uppercase file hashes |
| Raw Real Ruins snapshots | 12,420 `.bp` files; 596,127,961 bytes |
| Operator source saves | 62 `.rws` files; 2,323,978,940 bytes outside the repository |

The inventory count, byte count, and digest match the pre-maintenance census.
The ignored private ledger is
`Corpus/PlayerBaseLayouts/.cache/governance/dataset-governance.local.json`.
It records exact cohort locators, hashes, partitions, source counts, direct-
identifier screening, and an initially empty model/build use ledger.

## Dataset governance retained

`DATASET_GOVERNANCE.md` is the canonical public accounting surface. It records
three identified strata:

| Cohort | Source | Approximate scale | First admitted |
|---|---|---:|---|
| `PB-OP-20260728-V1` | Operator-contributed first-party saves | 62 candidates; 9 admitted layouts | 2026-07-28 |
| `PB-RR-SMOKE-20260816-V1` | Real Ruins public community archive | 13 snapshots; 12 complete | 2026-08-16 |
| `PB-RR-BROAD-20260817-V1` | Real Ruins public community archive | 10,000 candidates; 4,238 complete unique layouts | 2026-08-17 |

For each stratum, the public record preserves source/owner, ML purpose, record
scale, data-point types and labels, IP status, acquisition basis, personal and
aggregate-consumer-information assessment, processing, collection period,
first-admission date, synthetic-data status, and cohort identity. Exact row
membership remains local. No trained model-weights artifact is tracked or
shipped, and the private model/build use ledger is empty at this boundary.

This accounting preserves the fields described by California Civil Code
Sections 3110-3111 without asserting that CAO is currently within the law's
scope or that examples must be published. Official sources: the
[Section 3110 definitions](https://leginfo.legislature.ca.gov/faces/billNavClient.xhtml?bill_id=202320240AB2013)
and the [current Section 3111 fields](https://leginfo.legislature.ca.gov/faces/billNavClient.xhtml?bill_id=202520260AB1170).

## Direct-identifier screening

A read-only scan covered all 62 operator source saves, all 12,420 raw Real Ruins
snapshots after decompression, and all 85 normalized/index JSON files. It tested
for email addresses, Steam ID64 values, Windows user-directory paths, and macOS
or Linux user-directory paths. No affected file was detected in any stratum.

Player-authored in-game names and Real Ruins art text remain substantive source
content and therefore stay local. The scan does not treat game names as proven
real-world identities and does not manufacture a privacy deletion where direct
personal information was not found.

## Tooling and packaging

- `PlayerBaseLayoutExtractor` defaults normalized output to ignored
  `Corpus/PlayerBaseLayouts/local/generated/` and acquisition cache output to
  ignored `Corpus/PlayerBaseLayouts/.cache/real-ruins/raw/`.
- An extraction invoked without `--output` produced one 424,554-byte normalized
  layout plus its 440-byte index under the ignored default path; neither appeared
  in Git status.
- `tools/ci/verify-repository.mjs` enforces the two-file corpus whitelist,
  substantive extension rejection, required governance records, representative
  ignore probes, aggregate-profile shape, and absence of corpus references from
  every tracked workflow, C# project/build file, and CI shell or command script.
- The release workflow continues to package only
  `ColonistAwareness.dll` and `ci-build-summary.txt`.

## Clean-clone verification

A short-path clean clone of the staged tree contained exactly the two permitted
corpus files and no local payload. It passed repository/version replay and built
without the local corpus using pinned .NET SDK `8.0.423`:

| Gate | Result |
|---|---|
| Repository verification | PASS; version replay, corpus boundary, 23 tracked C# projects, 18 declared support projects, and locked dependencies |
| Production builds | PASS; two clean native Windows Release rebuilds, zero warnings and zero errors |
| Production identity | 4,368,384 bytes; SHA-256 `2710DDDAC506B4BA6910456F4EF965207C54431EA2D25E53CAFAA78E20047545` |
| Tracked assembly | PASS; byte-identical to both clean builds |
| Support projects | PASS; all 18 compile with warnings treated as errors |
| Portable receipt checks | PASS; B10 synthetic-state and B11 persistence census; no generated-file drift |

The WSL-to-Windows invocation of `verify-dotnet.sh` was not used as reproducible
build evidence because its `/tmp` translation compares a Linux path with a
Windows-host output path. The same commands were run natively against the same
clean clone, and hosted forge CI remains the authoritative execution of the
shell adapter itself.

## Historical Git-object boundary

The current tree and clean clone contain no substantive corpus payload. The
parent history still makes the thirteen former payload blobs reachable through
the B14 commit that originally tracked them. This maintenance does not claim a
history purge. Removing those old objects from every local and published ref
would require a coordinated destructive history rewrite, protected-branch force
update, contributor rebase, and forge-cache review. That operation was not
performed inside this independent maintenance lane because it would rewrite the
active B17 ancestry.

This distinction is explicit so current-tree protection is not mistaken for a
claim that old Git objects have already been purged.
