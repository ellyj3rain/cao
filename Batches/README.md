# Batch records

`Batches/` is the project's portable, append-only development record. Every batch lives in this one directory, regardless of letter sequence or publication host.

- Canonical identifiers use a letter and ordinal: `A1` through `A102`, then `B1` onward.
- Filenames zero-pad the ordinal so directory order remains chronological within each letter.
- A closed batch file is not rewritten. Later implementation, correction, or verification remains at its later identifier.
- Each record carries its date, descriptive name, threads, commits, source relation, and recorded build, receipt, correction, and provenance metadata.
- [`../BATCH_LOG.md`](../BATCH_LOG.md) is the chronological catalog.
- [`THREADS.md`](THREADS.md) is the permanent, series-neutral thematic classification over nonadjacent batches.
- [`THREAD_ID_CROSSWALK.md`](THREAD_ID_CROSSWALK.md) resolves the temporary A-series-scoped thematic identifiers.
- [`FORMER_LABELS.md`](FORMER_LABELS.md) is the one historical crosswalk for labels used before the recatalog.
- [`../VERSION_MAP.md`](../VERSION_MAP.md) partitions the chronology into contiguous, tier-bearing version units.

The records in this directory establish the append-only baseline and carry their own historical substance and provenance. Local Git may retain finer-grained engineering history, but neither these records nor their validity depend on a local commit graph or a forge retaining old objects. The immediately preceding generated history tree was a regulatory projection, not a second set of closed batch files.

The A sequence closes at `A102`. The chronology continues through closed batch
`B5`; `B6` is the next development batch. No per-letter history hierarchy is
created.
