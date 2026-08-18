# Continuous integration

CAO uses the same two-check merge gate as Neo, adapted to this repository's
RimWorld and governed-history contracts.

| Check | Contract |
|---|---|
| `ci-verify` | Replays the version model, checks committed diff hygiene and portable project references, restores the locked production graph, produces two byte-identical clean builds, proves the tracked shipping DLL is current, compiles every current or retained executable support project declared in `tools/ci/projects.txt`, and proves the synthetic-state and persistence censuses regenerate without drift. |
| `dependency-scan` | Restores the locked graph with NuGet auditing, records direct and transitive vulnerability evidence, and rejects high or critical findings. |
| `codeql-csharp` | Performs advisory C# code scanning on pull requests, `main`, and the weekly schedule. |

`ci-verify` and `dependency-scan` are required on `main`. CodeQL remains an
additional security signal so an external service delay does not erase the
repository's deterministic merge boundary.

The verified assembly and build-identity receipt are retained as workflow
artifacts for 30 days. CI never deploys into the operator's RimWorld installation,
launches the game, edits a save, or claims visual or gameplay acceptance.

The same repository-owned scripts are called by `.gitlab-ci.yml`; forge
configuration is a publication mechanism, not a second build contract.

Older fixture-conversion projects may remain tracked as historical provenance.
They are not silently rewritten to consume the current ontology and do not enter
the executable manifest unless they again become a supported tool.
