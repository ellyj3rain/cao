# B19 Review Receipt

| Area | Finding |
|---|---|
| Wrong coupling repaired | district/quarter count was keyed to realized scale so `urbanGrowthPropensity` (a naming preference) changed how much was built; repaired so quarters derive from population and support against the unmoved threshold. Verified by `quarters-follow-facts-not-the-naming-bar`. |
| False closure reverted | a population-projection change duplicated `CAPopulationProjection.Apply` and was overridden by it; the duplicate and its commit-message claim are reverted, the ontology record corrected. The revert is preserved in the commit chronology. |
| False-absence discipline | three false-absence claims this session: two became duplicate implementations (now reverted), one from a truncated multi-pattern grep read as absence (calling two live accessors dead). Both traps (truncation-as-absence; duplicate-before-reading-the-canonical-path) are in evidence-discipline memory. |
| Governed boundary respected | `CASettlementCapabilities.Level` is deliberately uncalled - gate C55 ("Read models are not sole causes") fails Critical if a derived capability rating feeds operative state. Removing it would delete the thing the gate names. Not dead code. |
| Anchor boundary recorded, not softened | vanilla/mod anchor settlements carry `CARegionalWorldSettlementState` but no `CARegionalSettlementRecord`; the inspect-string population is a real derived fact, not a defect to weaken. |
| Chronology preserved | the 134 B19 commits and the reverted false closure are preserved as committed; no history rewrite, squash, or rebase. |
| Build-toolchain debt | the governed 8.0.423 reproducible-build receipt is environmental debt (SDK absent, no network); a 9.0.316 substitute proves 0/0 compile and byte-identical reproducibility at the available SDK. |
| Operator boundary | in-game visual/gameplay acceptance is genuinely operator-blocked; deterministic and game-assembly evidence does not settle it. |
