# B4 - Culture, politics, and responsive authoring

| Field | Record |
|---|---|
| Batch | `B4` |
| Date range | 2026-08-10–2026-08-11 UTC / 2026-08-10 PST |
| Name | Culture, politics, and responsive authoring |
| Status | Closed append-only batch |
| Threads | [`T-002`](THREADS.md#t-002), [`T-015`](THREADS.md#t-015), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | Source closure `60aa03b5bec83d780533cc3e5abaf9325d624e17`; the following governance closure carries the generated version projections, README twin, and this governed record. |
| Builds | Full no-incremental Release build completed with 0 errors and the same 12 existing warnings. The verified assembly is 3,008,000 bytes at SHA-256 `4517255101815744D11A2FF657F5D1E689B470AA8A314B913539296A4B454459`. |
| Receipts and verification | Authoring-convergence suite passed 157 assertions; creation-flow interaction suite passed 65; player-founding suite passed 33; World-tendencies causal suite passed 100. The 355 assertions cover the keyed fixture and the supported width/UI-scale matrix. Correctness, structural, and surface reviews closed with no Critical or High findings. |
| Corrections | Regional validation now rejects incompatible faction cultures; cultural furnishing reports failed placements and contains exceptions per practice domain; native Ideoligion inspection no longer mutates established ideologies merely by opening or leaving the page; detail expansion no longer leaves an inert control in Expanded mode; short-screen dialogs reserve a fixed native footer outside their scroll body. |
| Provenance | Follows B3 tip `dbc2d4fad9b54cdf9bfc1fbebfbc6c3b88bf5074` on `mallowfluff/b3-creation-flow-interaction`. The B4 implementation directive, operator screenshots, current code, RimWorld definitions, and keyed runtime fixture supplied the acceptance evidence. |
| Runtime fixture | The keyed plan `regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` remains the active authority: 3 factions, 4 settlements, and 9 population groups at SHA-256 `728900FBE6ABAC2DA7E29A22BC1B5DEA90494B6EFA0E74485A24C2C9A96C3872`. |
| Source relation | Follows `B3` and forms the minor capability unit `VU-031`, deriving `1.1.0.0-alpha`; no former-ledger label applies. |

## Record

Information detail is now a persistent presentation preference with Compact,
Standard, and Expanded levels. It is owned by the mod settings, defaults to
Standard, and is consumed through one presentation service. Compact retains
identity, immediate effects, and all warnings or blockers; Standard adds system
relationships and important provenance; Expanded adds causal explanations,
constraints, and generation detail. Local expansion is available where useful,
and no detail setting participates in generation or changes saved world state.

Culture now records four independent practice domains alongside its native
visual-style source: public gathering, hospitality, communal meals, and public
memory. Each domain has a stable key, per-field authored and preset provenance,
deterministic generation, a shared founding/established composer, and a concrete
starting-facility or furnishing consumer. The four built-in profiles carry
stable machine identities, display labels, descriptions, multiple positions,
and distinct native visual sources. Schema 2 migrates the former display-name
identities and the former `waymeet` phrase without changing existing culture
IDs or field meanings. Compatibility gates validate native style definitions
and every materialized key before confirmation.

Political beliefs and realized faction structure continue to use the same
thirteen saved axes without collapsing their meanings. Built-in political
profiles now have stable keys separate from their labels, and legacy labels
migrate through aliases. One grouped composer organizes the preserved axes as
Governance, Civic participation, Property/economy, Membership/order, and
Security/conflict. Existing factions expose one norm-versus-practice comparison
instead of two undifferentiated forms. The limited terms adopted at landing
remain separate from both normative beliefs and mature faction structure.

Culture and political profile construction, application, current-selection
logic, summaries, details, and user-profile management now live in shared
authoring machinery. User profiles persist in global mod settings and support
save, load, rename, duplicate, and delete. Applying a profile copies its state;
later world edits do not mutate the library, and deleting a library entry does
not invalidate a world that already applied it.

The native Ideoligion chooser remains the first-class RimWorld stage. CA's
scenario-sensitive Starting arrangements page follows it and retains the
founding Culture, political beliefs, and landing terms through fixed, fluid,
loaded, customized, Back, and Continue paths. Merely opening or backing out of
the flow does not materialize unrelated established Ideoligions.

Starting Region now selects Wide, Medium, or Compact layouts from effective
available width. Wide retains the object rail, map, and inspector; Medium keeps
a viable map while tightening secondary detail; Compact uses explicit Objects,
Map, and Details navigation while preserving the existing map selection model.
Cards, inspector values, provenance, category tabs, and short-screen dialogs
measure and wrap their content. Faction technological knowledge is shown
separately from settlement-local material capability and research.

The current keyed fixture was migrated in place to culture and political schema
2 while retaining its plan identity, region, candidate, arrival area, map scale,
three factions, four settlements, nine population groups, ownership, belief
sources, and population assignments. The four executable receipt suites parse
and round-trip that authority. The built B4 DLL remains only in the branch
checkout; the live mod root is still the byte-distinct B3 assembly. Runtime
visual and behavioral acceptance therefore remains the operator's next gate
after a deliberate deployment.

This batch is closed. `B5` is the next ordinary development batch.
