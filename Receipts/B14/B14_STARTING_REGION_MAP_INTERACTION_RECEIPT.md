# B14 Starting Region map interaction receipt

Verified and deployed at 2026-08-16 05:28 UTC / 2026-08-15 22:28 PDT.

## Runtime-facing contract

The RimWorld Starting Region map is now an authoring surface for the broad
world areas already owned by the current regional plan:

- settlement and arrival labels are direct selection targets;
- selected objects can open their existing Details inspector from the map;
- `Move area` reassigns a settlement to visible region land;
- `Move arrival` reassigns arrival to valid, unoccupied region land;
- the diagram continues to author broad world-area membership, not fictional
  exact coordinates on a map that has not yet been generated;
- changing either geography reopens a confirmed draft and invalidates its
  derived settlement realization before the current causes are saved again.

Overlapping labels resolve in reverse paint order, so the label visibly on top
owns the click. Existing feature and neighboring-area selection remains intact.

## Verification

| Gate | Result | Evidence |
|---|---|---|
| Diff integrity | **PASS** | `git diff --check` completed without errors. Existing line-ending notices are repository conversion warnings, not malformed patches. |
| Release build | **PASS** | `dotnet build Source/ColonistAwareness.csproj -c Release --no-restore` completed with 0 warnings and 0 errors. |
| Regional contract harness | **PASS** | `B14RegionalGeographyReceipts` passed 12/12, including the explicit static wiring contract for label hit order, draft reopening, settlement and arrival writes, occupancy separation, and compact-map return. |
| Installed assembly identity | **PASS** | Worktree and installed `ColonistAwareness.dll` are byte-identical at 4,005,888 bytes and SHA-256 `39A3C5C19847B64EF16028CE7D919BF1B7ACAE17948F5A93162BD8035089B8D9`. |
| Safe replacement boundary | **PASS** | No RimWorld process was running when the installed assembly was replaced. Codex did not relaunch the game. |

This receipt establishes source wiring, build health, and deployment identity.
It does not establish the interaction's feel, visual clarity, or generated-map
result. Those remain the operator's next RimWorld runtime test.
