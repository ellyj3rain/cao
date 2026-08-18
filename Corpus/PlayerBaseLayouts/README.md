# Player-base layout corpus boundary

This directory is deny-by-default for Git. Raw saves and snapshots, normalized
layouts, per-layout manifests, screening/evaluation records, partitions, caches,
and private compliance accounting remain local.

The only tracked files permitted below this directory are this boundary and the
non-reconstructive aggregate profile at
`external/real-ruins-broad-profile.json`. High-level provenance and dataset
accounting live in `DATASET_GOVERNANCE.md` and
`PLAYER_BASE_PATTERN_CORPUS.md`.

`tools/PlayerBaseLayoutExtractor` writes to `local/generated/` by default and
uses `.cache/real-ruins/raw/` for acquisition caching. Both locations are
ignored. Explicit output paths remain available for controlled local work; do
not use Git or Git LFS to publish substantive corpus payload.

The ignored local compliance ledger belongs at
`.cache/governance/dataset-governance.local.json`. It records exact local cohort
and model/build use identity without becoming distributable training data.
