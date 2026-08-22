# B19 Retained Suites Receipt

Re-verification of the retained B10-B18 acceptance suites against B19's changed
shared kernels. Tools built with .NET SDK 9.0.316 (`net8.0`, `RollForward Major`)
and run with the current governed fixture.

| Suite | Result | Evidence |
|---|---|---|
| B19 regional substrate | 34/34 PASS | `B19_REGIONAL_SUBSTRATE_ACCEPTANCE_RECEIPT.md`; verifies the closing corrections in the kernel math |
| B10 synthetic-state audit | PASS | 316 occurrences classified; 0 unresolved Critical/High |
| B14 authoring convergence | 24/24 PASS | regenerated `Receipts/B14/B14_AUTHORING_CONVERGENCE_STATIC_RECEIPT.md` |
| B17 affiliation-epistemic | PASS | current fixture 3 factions / 4 settlements / 3 factionless frontier holdings; no-owner, support-only, population-affiliation, form-transition, and atomic migration contracts PASS; pawn-private observation, report provenance, contradiction, revision, supersession, retention, and readback PASS; 14/14 broader fact families and disabled-mode boundary; run against the real game assembly (`--verify-only`) |
| B18 authoring-state convergence | 9/10 | the one FAIL is "Current operator fixture retains the failed-run composition": the active fixture is now the 4-settlement plan `regional-plan-9bcbba2f...` (region `CA-RG-EB596A12`), which supersedes B18's 3-settlement failed-run fixture `regional-plan-3c7bac0ada...` (region `CA-RG-CB2DF263`). This is fixture evolution by operator selection, not a B19 regression; B18's own closed receipt (`Receipts/B18/B18_AUTHORING_STATE_STATIC_RECEIPT.md`) remains 10/10 against its fixture |

The retained suites that did not require re-running (unchanged source relative to
their batch, or fixture-independent) are not re-listed here. The lines B19 moved
(settlement composition, regional topology, material, affiliation) are re-verified
above. B17 additionally loads the real RimWorld game assembly to test CA's site
contracts against the engine types - integration evidence, not in-game play.
