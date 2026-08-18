# B14 AI-harness creator receipt

Recorded 2026-08-15 20:39 UTC / 13:39 PDT with `RimWorldWin64` closed.

## Corrected creator boundary

The CAO creators are interactive AI-agent-harness skills. Their PowerShell
catalog and manifest scripts provide fresh read-only seed evidence; script
output is not the creator interface.

| Skill | Creator stages | Canonical skill surface |
|---|---|---|
| `cao-backend-game-creator` | Location candidate, existing region, settlements, final validation and paired staging | `C:/Users/jleyv/Peanut Butter/Marshmallow Meta Commons/codex/skills/cao-backend-game-creator` |
| `cao-colony-creator` | Founders' Culture, native Ideoligion choice, Political Beliefs, Founding Arrangement, belief/practice review | `C:/Users/jleyv/Peanut Butter/Marshmallow Meta Commons/codex/skills/cao-colony-creator` |

The two skills now require one continuous harness flow with Back/Continue state
and one final Review action. The interface maintains a structured
`caoCreatorDraft` but performs no filesystem writes. The final action returns
the complete draft to the agent; only then may the backend skill re-read the
live files, reject stale source identities, validate through current models,
preserve recovery copies, and atomically stage the active/keyed pair.

RimWorld retains scenario, native Ideology editing, and starting-pawn ownership.
The backend creator does not author the player's founding state; the colony
creator does not rewrite established regional societies.

## Current harness seed

The interactive creator was seeded from the closed-game catalog rather than a
hand-authored substitute.

| Status | World and candidate | Current substance |
|---|---|---|
| Active current-schema pair | `alysaliu|1|Algorab Markab`; `613b1fe44104`; region `CA-RG-EB596A12`; arrival `389638`; map scale `350` | Schema/epoch 12; 9 members; 3 factions; 4 settlements; 9 population groups. Active and keyed files are byte-identical at SHA-256 `00CE3B495DDA48D551ABD63D575DD3599B20D728D41AFC8A7AD76075C00E15F2`. |
| Available current-schema location | `alysialiu|1|Alathfar Minutus`; `28395e8ebd7e`; Wet Panda region; arrival `354370` | Schema/epoch 12; 12 members; no factions or settlements authored. It remains inactive until selected and confirmed through the creator. |
| Prior-schema conversion source | `mass driver|1|Ain-XII`; `149c95e7ed68`; Enler Isle region; arrival `85717` | Schema/epoch 10; 12 members; 1 faction; 3 unnamed plotted settlements; 3 population groups. The operator may select it as conversion evidence; only a validated schema-12 translation may be staged. |

The active plotted settlements remain:

| Slot | Settlement | Member tile | Faction key |
|---:|---|---:|---:|
| 0 | Megaeth | 488022 | 1 |
| 1 | Red Cervexa | 389640 | 1 |
| 2 | Tascan Bramble | 389637 | 2 |
| 3 | Black Delta | 170943 | 3 |

## Founding seed and supported decisions

| Fact | Current value |
|---|---|
| Founders' Culture | `Sea Commons` / `culture:founders-sea-commons` |
| Native Ideoligion | `Astro-Manporkism`, native ID `12`, recorded content signature |
| Political Beliefs | `politics:b8-founders-sea-commons`; 13 current mechanisms |
| Saved arrangement | `single-founder+faction`; no permanent leader, work required, all founders decide, shared supplies, no expiry |
| Temporal state | `establishedStart=False`; no prior player settlement history represented |
| Confirmation state | Regional `False`; founding `False` |

The creator exposes the current arrangement fields directly:
`leaderRule`, `workRequired`, `foundersDecide`, `sharedSupplies`, and
`durationDays`. It also exposes the current production presets: shared survival,
emergency command, ancestral commons, and single founder. Changing a preset
field creates an authored custom draft rather than changing only display copy.

## Interaction verification

- The creator traversed Location -> Region -> Settlements -> Founders bring ->
  Founders adopt -> Review without JavaScript or console errors.
- Both the active Algorab composition and the empty current-schema Wet Panda
  candidate reached Review with their distinct saved facts intact.
- Enler Isle is selectable as a recovery source. Its selection explicitly owns
  a schema-10 to schema-12 conversion, retains unsupported values as evidence,
  and never treats old serialization as an active runtime plan.
- `Selected for this draft` follows the operator's radio choice. `Currently
  staged in CAO` is separate provenance and does not masquerade as selection.
- The interaction remained within its root width at 736 px and every step at
  320 px; the settlement table contains its own narrow-width overflow.
- Review exposes one `Send this plan to Codex` action. In a harness with the
  host bridge, it sends the exact structured draft through
  `window.openai.sendFollowUpMessage` and requests both creator skills.
- The handoff requires a live catalog/manifest reread, source-hash comparison,
  current-schema validation, timestamped recovery copies, atomic active/keyed
  staging, readback verification, false confirmation flags, and no RimWorld
  launch.

## Mutation boundary

- RimWorld was closed and remained closed.
- No Config file, pending plan, savegame, CAO source file, or DLL was changed.
- No candidate was activated and no composition was staged; those actions remain
  behind the creator's operator-confirmed final Review action.
- The deployed B14 DLL remains the existing runtime-test candidate. Creating the
  harness skills required no rebuild or redeployment.
