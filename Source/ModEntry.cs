using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public class AwarenessSettings : ModSettings
    {
        public bool eatSmart = true;
        public bool criticalHauling = true;
        public bool lifeSafety = true;
        public bool rescueOutsiders = false;
        public bool areaDuty = true;
        public bool autoUnforbidCorpses = true;
        public bool autoDigGraves = true;
        public bool raidResponse = true;
        public bool draftChain = true;
        public bool bodyWeight = true;
        public bool enemyRestraint = true;
        public bool buddyCarry = true;
        public bool commsSystem = true;
        public bool gossip = true;
        public int defaultAutonomy = 2;
        public bool survivalResponses = true;
        public bool fieldMedicine = true;
        public bool ambushStrikes = true;
        public bool animalCare = true;
        public bool fireResponse = true;
        public bool weaponTransitions = true;
        public bool withdrawals = true;
        public bool movingFire = true;
        public bool renderArms = true;
        public bool dualWield = true;
        public bool holdOrders = true;
        public bool dragOrders = true;
        public bool trapAwareness = true;
        public bool battleDrills = true;
        public bool knowledgeContacts = true;
        public bool authorityObedience = true;
        public bool operationalAccess = true;
        public bool autonomousHomePlanning = false;
        public int autonomousHomePlanningResetGeneration;
        // A behavior system without its causal receipts cannot be evaluated during
        // ordinary play. Tracing is therefore the default operating posture; the
        // player may still disable it explicitly from the mod settings.
        public bool traceBehavior = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eatSmart, "eatSmart", true);
            Scribe_Values.Look(ref criticalHauling, "criticalHauling", true);
            Scribe_Values.Look(ref lifeSafety, "lifeSafety", true);
            Scribe_Values.Look(ref rescueOutsiders, "rescueOutsiders", false);
            Scribe_Values.Look(ref areaDuty, "areaDuty", true);
            Scribe_Values.Look(ref autoUnforbidCorpses, "autoUnforbidCorpses", true);
            Scribe_Values.Look(ref autoDigGraves, "autoDigGraves", true);
            Scribe_Values.Look(ref raidResponse, "raidResponse", true);
            Scribe_Values.Look(ref draftChain, "draftChain", true);
            Scribe_Values.Look(ref bodyWeight, "bodyWeight", true);
            Scribe_Values.Look(ref enemyRestraint, "enemyRestraint", true);
            Scribe_Values.Look(ref buddyCarry, "buddyCarry", true);
            Scribe_Values.Look(ref commsSystem, "commsSystem", true);
            Scribe_Values.Look(ref gossip, "gossip", true);
            Scribe_Values.Look(ref defaultAutonomy, "defaultAutonomy", 2);
            Scribe_Values.Look(ref survivalResponses, "survivalResponses", true);
            Scribe_Values.Look(ref fieldMedicine, "fieldMedicine", true);
            Scribe_Values.Look(ref ambushStrikes, "ambushStrikes", true);
            Scribe_Values.Look(ref animalCare, "animalCare", true);
            Scribe_Values.Look(ref fireResponse, "fireResponse", true);
            Scribe_Values.Look(ref weaponTransitions, "weaponTransitions", true);
            Scribe_Values.Look(ref withdrawals, "withdrawals", true);
            Scribe_Values.Look(ref movingFire, "movingFire", true);
            Scribe_Values.Look(ref renderArms, "renderArms", true);
            Scribe_Values.Look(ref dualWield, "dualWield", true);
            Scribe_Values.Look(ref holdOrders, "holdOrders", true);
            Scribe_Values.Look(ref dragOrders, "dragOrders", true);
            Scribe_Values.Look(ref trapAwareness, "trapAwareness", true);
            Scribe_Values.Look(ref battleDrills, "battleDrills", true);
            Scribe_Values.Look(ref knowledgeContacts, "knowledgeContacts", true);
            Scribe_Values.Look(ref authorityObedience, "authorityObedience", true);
            Scribe_Values.Look(ref operationalAccess, "operationalAccess", true);
            Scribe_Values.Look(ref autonomousHomePlanning, "autonomousHomePlanning", false);
            Scribe_Values.Look(ref autonomousHomePlanningResetGeneration,
                "autonomousHomePlanningResetGeneration", 0);
            Scribe_Values.Look(ref traceBehavior, "traceBehavior", true);
        }
    }

    public class AwarenessMod : Mod
    {
        public static AwarenessSettings Settings;

        private Vector2 settingsScrollPosition;
        private float settingsScrollHeight;

        public AwarenessMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AwarenessSettings>();
            var harmony = new Harmony("ellyj3rain.colonistawareness");
            harmony.PatchAll();
            PortraitMenuPatch.TryInstall(harmony);
            BioWeightBadge.TryInstall(harmony);
            EnemyRestraintModule.TryInstall(harmony);
            StealExclusionPatch.TryInstall(harmony);
            StashPreservationPatch.TryInstall(harmony);
            // Arm nodes parked: operator is authoring arm art manually. Machinery lives in
            // ArmsNodeModule.cs - drop the textures in and restore this install to revive.
            // ArmsNodePatch.TryInstall(harmony);
            OffhandPatches.TryInstall(harmony);
            GearTabOffhandPatch.TryInstall(harmony);
            TrapAwarenessPatch.TryInstall(harmony);
            StackDisciplinePatch.TryInstall(harmony);
            KnowledgePatch.TryInstall(harmony);
            CAViolencePatches.TryInstall(harmony);
            EquipTransitionDespawnPatch.TryInstall(harmony);
            GunshotKnowledgePatch.TryInstall(harmony);
            AssaultAwarenessPatch.TryInstall(harmony);
            CARegionalCompatibility.TryInstall();
            LongEventHandler.ExecuteWhenFinished(
                CARegionalCompatibility.TryInstall);
            Log.Message("[Colonist Awareness] loaded; behavior trace "
                + (Settings != null && Settings.traceBehavior ? "on" : "off")
                + ".");
            Log.Message("[CA][Regional] starting-region authoring, projected "
                + "geography, strategic overview, and saved settlement "
                + "overlays installed.");
        }

        public override string SettingsCategory()
        {
            return "Colonist Awareness";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 16f,
                Mathf.Max(settingsScrollHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            var l = new Listing_Standard();
            var listingRect = viewRect;
            listingRect.height = 99999f;
            l.Begin(listingRect);
            l.CheckboxLabeled("Eat smart", ref Settings.eatSmart,
                "Hold off on kibble, raw food, and corpses while a proper meal is actively being cooked. A routine food job also avoids an exposed food destination while that pawn has a fresh nearby hostile contact; inventory, close, sheltered, and urgently needed food remain available.");
            l.Gap(6f);
            l.CheckboxLabeled("Criticality hauling", ref Settings.criticalHauling,
                "Deteriorating valuables get hauled ahead of general hauling. If no storage accepts the item it goes under the nearest roof.");
            l.Gap(6f);
            l.CheckboxLabeled("Life safety", ref Settings.lifeSafety,
                "A pawn must first see a baby or downed person in dangerous temperatures before automatically carrying them to a safer bed. A valid same-faction report instead gives a Proactive+ pawn the teller's remembered cell to check; it does not confer live tracking. The native rescue action then revalidates the current visible position, bed, area, forbid, reachability, and reservation.");
            l.Gap(6f);
            l.CheckboxLabeled("Life safety covers outsiders", ref Settings.rescueOutsiders,
                "Also rescue downed strangers and enemies from freezing or cooking. Proactive+ pawns do this regardless of the setting.");
            l.Gap(6f);
            l.CheckboxLabeled("Area duty", ref Settings.areaDuty,
                "A pawn's assigned allowed area counts as their responsibility: awareness tasks inside it come first, ordered by value. Movement is never restricted.");
            l.Gap(6f);
            l.CheckboxLabeled("Unforbid corpses", ref Settings.autoUnforbidCorpses,
                "Humanlike corpses are unforbidden automatically once the map is calm.");
            l.Gap(6f);
            l.CheckboxLabeled("Auto-plan graves", ref Settings.autoDigGraves,
                "When unburied corpses outnumber open graves, new grave blueprints are planned beside the existing graveyard.");
            l.Gap(6f);
            l.CheckboxLabeled("Raid response", ref Settings.raidResponse,
                "When they know a contact, Proactive+ fighters recognize and react on RimWorld's native 30-tick constant-think lane. Against a currently visible contact, a ranged fighter compares nearby firing cells by shot quality, directional cover, clear friendly lane, useful-range mutual support, health-adjusted local force, separation from allies, gas/fire hazard, and travel. Wounded fighters enter a persistent recovery posture and do not close on a retreating target. A drafted native watch with no active threat yields to known casualty work without silently undrafting a directly controlled pawn. A current direct player job still owns the pawn. The slower planner may assign home-area posts beside player-built partial cover. Non-fighters receive a persistent enclosed-room shelter intent until the active threat ends; routine food outside that room does not release them.");
            l.Gap(6f);
            l.CheckboxLabeled("Operational access and equipment", ref Settings.operationalAccess,
                "When Default autonomy is Proactive or Autonomous, shared starting cargo arrives allowed instead of red-X forbidden. Whenever a Proactive or Autonomous colonist is present, ordinary visible non-quest items across the colony map are kept allowed. On a known threat, those pawns may take a suitable weapon and, while danger is not close, put on compatible protection. Existing red Xs from the former blanket-protection policy are cleared once; items you explicitly forbid afterward stay forbidden. Category and action permissions remain future controls.");
            l.Gap(6f);
            bool homePlanningBefore = Settings.autonomousHomePlanning;
            l.CheckboxLabeled("Autonomous home planning", ref Settings.autonomousHomePlanning,
                "Opt in to domestic planning. A threat-unaware Proactive colonist with construction work enabled may plan one native blueprint at a time for missing sleeping capacity, an eating table, and minimal seating inside claimed, enclosed Home rooms. An Autonomous colonist may then add household seating, lighting, and one usable recreation building; skill and disposition choose the planner. The Zone menu's Plan colony space editor paints named player-authored programs with a purpose, planning style, maximum occupancy, and optional sleep-capacity requirement. Current production Bed fulfillment uses Barracks sleep programs. Player-authored Bedroom Bed requirements remain evaluation-only in production; an exact developer regression may exercise one concrete resident-bound Bed cause. Austere operative programs use floor sleeping spots; a non-austere Bedroom with exactly two eligible adult love partners may evaluate one native double bed while both ideoligions permit sharing; remaining needs prefer single beds and fall back to spots. A new Bed proposed by an Autonomous colonist inside an operative player-authored sleep program re-ranks valid cells through the saved settlement context: program legibility, actual resident relationships and ideoligions, environmental fit, operational access, existing furnishing history, and settlement topology contribute separately and are exposed in the planning receipt. This re-ranking does not move completed construction, revise a retained objective, or redraw the authored program. Bed candidates also receive a receipted Feng Shui score for wall alignment, open approaches, opposite-row alignment, and regular bank intervals. Once a bed pattern exists, a lower-pattern candidate is rejected unless grounded, actionable evidence supports it; the current recognized exception is proximity to an existing assigned bed for the next resident's bonded animal. The concise reason appears in the native planning message and the detailed evidence persists in the planning receipt. Each sleep program has a separately authored resident roster. Player assignment, removal, or Keep current roster preserves that roster, including an intentionally empty one; Let residents decide hands it back. With an eligible Autonomous colonist, unassigned and resident-authored rosters fill without evicting valid residents: native love clusters seed Bedrooms and mutual opinion guides Barracks choices. A pawn belongs to at most one sleep program and its roster determines required capacity. Available unowned provisions inside the program may receive eligible residents immediately; completed CA-planned provisions receive them through RimWorld's native bed ownership after the same relationship, ideoligion, and assignment checks. Dining, Kitchen, and Recreation programs are preferred for tables and seating. Tables maintain a two-cell buffer from mech chargers, worktables, batteries, power plants, and turrets; room and cell scoring prefer greater functional separation while retaining usable service cells. A Barracks may receive a table and seats only as a fallback after valid unprogrammed shelter and only where the table has usable service cells, stays off the room center, and preserves existing sleep approaches; recreation remains compatible. Player-authored cells and parameters are not redrawn by pawns, program residence does not restrict movement, and unprogrammed maps retain the prior bounded furnishing behavior. Research, reachable construction skill, work settings, and native jobs remain authoritative. An authorized provision retains its exact material demand when loose stock is short, including while its author is temporarily unavailable. Existing allowed, reachable stock is used first. If no native destination accepts a deficient construction material, the objective may establish a small Important-priority native construction reserve filtered to that material and sized from the outstanding deficit without covering the planned building; existing player storage wins, player edits take control, and deleting an active automatic reserve vetoes recreation for that exact objective until Home planning is turned off and on. This is demand staging, not speculative inventory stockpiling. Durable inventory remains the native stockpile and storage-building system: its filters, priorities, footprints, and mixed contents are canonical. Colonist Awareness creates no category-named durable stockpile; any native zones retained from the former differentiated-inventory projection are transferred intact to player authority without changing their cells, filters, priorities, labels, or contents. Native hauling owns every item move. The first producer is wood, for which a qualified Proactive+ plant cutter may mark a bounded set of harvestable trees through native work. The source policy rejects fogged, forbidden, burning, cultivated, important, protected, already designated, or authored-program trees; preserves Home-perimeter and defensive cover; respects ideoligion, allowed areas, and player cancellation; and suspends when its responsible forester knows an active threat. Forestry judgment derives from Plants, Intellectual, initiative, and discipline and shapes thinning distance, source ranking, and defensive-cover reserve. Unsupported material producers remain visibly blocked. Canceling an automatic blueprint suppresses that kind for one in-game day. Deconstructing a completed automatic furnishing vetoes that kind until you turn Home planning off and on. This slice furnishes existing shelter; it does not create rooms or walls, infer privacy beyond native love clusters, originate pawn-authored space overlays, or claim full mining/crafting/trade logistics.");
            if (homePlanningBefore != Settings.autonomousHomePlanning)
            {
                if (Settings.autonomousHomePlanning)
                    Settings.autonomousHomePlanningResetGeneration++;
                if (Current.ProgramState == ProgramState.Playing)
                {
                    for (int i = 0; i < Find.Maps.Count; i++)
                    {
                        AutonomousHomeMapComponent.For(Find.Maps[i])
                            ?.NotifyPlanningSettingChanged(
                                Settings.autonomousHomePlanning,
                                Settings.autonomousHomePlanningResetGeneration);
                        CAStorageProgramMapComponent.For(Find.Maps[i])
                            ?.NotifyPlanningSettingChanged(
                                Settings.autonomousHomePlanning,
                                Settings.autonomousHomePlanningResetGeneration);
                    }
                }
            }
            l.Gap(6f);
            l.CheckboxLabeled("Draft follows the leader", ref Settings.draftChain,
                "Drafting a squad leader also drafts squad members at Proactive+ autonomy. Each propagated draft records its leader and episode; undrafting the leader releases only draft state CA owns, while a direct pawn toggle takes ownership back.");
            l.Gap(6f);
            l.CheckboxLabeled("Body weight", ref Settings.bodyWeight,
                "Long-run eating habits shift adult colonists' weight: emaciated/underweight/fit/overweight/obese. Weight affects speed, carrying, hunger rate, ruck utilization, and the body graphic at the extremes.");
            l.Gap(6f);
            l.CheckboxLabeled("Enemy restraint", ref Settings.enemyRestraint,
                "Raiders normally ignore passive colony animals. An animal that is fighting, pursuing a target, or has just engaged becomes a combatant; a stable rare exception remains for unusually unrestrained attackers. Downed people and children are still devalued according to the attacker's disposition.");
            l.Gap(6f);
            l.CheckboxLabeled("Buddy carry", ref Settings.buddyCarry,
                "Carrying a downed casualty is faster with an able ally adjacent: +25% assist, +50% with a strong helper on decent ground. Works for rescues and raider extractions alike.");
            l.Gap(6f);
            l.CheckboxLabeled("Comms and command capacity", ref Settings.commsSystem,
                "Voice reaches same-faction listeners nearby without recipient slots. A pawn in CA's concealed or ordered-quiet posture uses a reduced five-cell quiet-voice range instead of becoming inexplicably unable to communicate with someone standing beside them. A deliberate combat order can also pass between adjacent, mutually visible pawns by gesture; that route carries the command only and does not create shared threat knowledge. Remote radio requires an Airwire, Array, or Integrator headset at both ends; their native +3/+6/+9 offsets currently supply 3/6/9 human radio lines. Every same-faction spawned pawn with an active mechlink joins one synchronized, silent mental network across the current map while CA comms is enabled. Native free mech bandwidth remains mechanitor capacity and does not allocate human mental recipients. An engine-reported electricity outage, including Solar Flare, takes remote radio and mental delivery offline while local voice and gesture remain available. Headset-radio reports retain explicit squad, fire-team, or Lord-group routes; the mental network itself is map-wide. The current provisional Intellectual, Social, and trait command span applies only to voice and headset radio; gesture and mental delivery do not consume it. Obedience remains a separate authority decision.");
            l.Gap(6f);
            l.CheckboxLabeled("Gossip", ref Settings.gossip,
                "Colonists praise and badmouth third parties. Whether it lands depends on the talker's Social, the listener's trust of them, and the listener's Intellectual skepticism. Compounds with repetition; poison overheard by its subject backfires. All visible as social memories.");
            l.Gap(6f);
            l.CheckboxLabeled("Dual wielding", ref Settings.dualWield,
                "Right-click a one-handed weapon (pistol, SMG, light melee): equip it in the off hand. Both hands fire on the same target with independent aim cycles; cooldowns slow for both hands, less with skill; offhand shots track from the offhand side. Stow it via the pawn's gizmo. With Weapon transitions enabled, Autonomous colonists and generic settlement assaulters may draw a valid offhand on a known contact and stow it after calm. Bespoke - no other mod needed.");
            l.Gap(6f);
            l.CheckboxLabeled("Hands", ref Settings.renderArms,
                "Hands on the grip of every drawn weapon - skin-tinted, two on long guns, one on pistols and melee, one per weapon when dual wielding. All armed humanlikes, colonist or raider.");
            l.Gap(6f);
            l.CheckboxLabeled("Moving fire", ref Settings.movingFire,
                "Flagged shooters may fire during an existing movement job and pay a skill-scaled accuracy penalty. Withdrawals flag it automatically; drafted shooters get a toggle gizmo. This toggle governs firing during locomotion, not tactical repositioning. With it off, a fighter may still move to better ground, plant, complete a weapon cycle, and reassess.");
            l.Gap(6f);
            l.CheckboxLabeled("Threat knowledge", ref Settings.knowledgeContacts,
                "A threat contact identifies a hostile from firsthand sight, a same-faction voice report, a valid headset-radio or mechlink-mental report carrying the teller's remembered cell and time, or being hit by that hostile. Gunfire is separate ambiguous evidence: humanlikes retain an approximate area through RimWorld's hearing, room, and open-door propagation and may relay it; animals retain firsthand sound only and respond once per acoustic event according to vigilance, nerve, attachment, and defensive drive. Animals neither receive reports nor infer a shooter. A weapon report never becomes a hostile contact without corroboration. Repeated unexpected gunfire, or one heavy report, in the local tactical area invalidates a synchronized ambush that is still waiting for its close visual trigger; the group releases together and the ordinary bounded sound-response lane may investigate. Contacts go stale after about three hours unrefreshed. Off: no new contacts or audible cues are acquired or relayed; awareness consumers act only on hostiles the pawn currently sees within that behavior's operating range, while vanilla animal danger responses remain untouched.");
            l.Gap(6f);
            l.CheckboxLabeled("Command authority", ref Settings.authorityObedience,
                "Group orders through the with-who menu are relayed in the world. Whether a squadmate follows depends on the relayer's command standing (an assigned squad leader ranks highest; a fire-team leader has delegated standing), the follower's discipline and conformity, and their opinion of the relayer. Refusals and reluctance show a reason. An assigned leader and direct report sharing one CA tactical duty still need sight or a valid voice, headset radio, or mechlink check-in to confirm presence. Missed check-ins become overdue on a disposition-shaped cadence. Proactive leaders may check a nearby last-confirmed position; Autonomous leaders may travel farther. The selected pawn always obeys a direct player order.");
            l.Gap(6f);
            l.CheckboxLabeled("Behavior trace (diagnostic)", ref Settings.traceBehavior,
                "Logs why awareness behaviors fired or didn't. During an active threat, the session-local combat flight recorder also captures every successful humanlike job transition plus 30-tick pawn, path, duty, intent, need, health, equipment, trait, direct-relation, audible-cue, active-game-condition, age-adjusted contact, communication-route, and local-environment snapshots using both game clocks. The existing combat-topology-export command writes both causal and spatial JSON without changing pawns, jobs, maps, saves, or time. CA-owned decisions carry causal origin, controller, shared episode, and issuer when that intent exists; the recorder separately captures the actual job giver, think tree, player-forced flag, lord, duty, both game clocks, map, stable actor ID, and event-time cells without inventing missing ownership. Native BattleLog remains the authority for shots, impacts, downs, and deaths. Operator observation remains the authority for how the result looks and plays.");
            l.Gap(6f);
            l.CheckboxLabeled("Battle drills", ref Settings.battleDrills,
                "Multi-select a team, right-click a door: Stack on door (flat against the wall on your side, point man tightest to the frame, alternating sides), then Breach and clear (through the door - hooks left and right along the inside wall first, the rest deepen; every clearer breaches with the first-strike bonus).");
            l.Gap(6f);
            l.CheckboxLabeled("Trap awareness", ref Settings.trapAwareness,
                "A sprung trap is information: smart raiders (Intellectual 5+) infer the surrounding ground is seeded and path around every trap within the lane for about a day. Dull raiders keep walking in. Your own pawns already know their traps.");
            l.Gap(6f);
            l.CheckboxLabeled("Drag casualties", ref Settings.dragOrders,
                "Right-click a downed pawn: drag them a short distance fast (25% faster than a carry) - out of the fire lane, behind cover, to the medic, toward extraction. Rougher than a carry: handlers below Medicine 4 can bruise the casualty. Buddy carry assists stack.");
            l.Gap(6f);
            l.CheckboxLabeled("Hold position", ref Settings.holdOrders,
                "Right-click ground with a fighter selected at any autonomy level: defend that local position. The assigned cell is an anchor, not a paralysis tile; holders immediately reacquire visible threats, may reposition within the six-cell duty envelope for a shot or bounded melee intercept, and return after displacement without pursuing beyond the position. The standing order persists through saves. Release hold or a direct player move ends one; starvation also releases it rather than pinning a pawn into collapse. Proactive+ pawns may break off when flanked, overrun, bleeding badly, or locally collapsing, then report through the squad leader when in command contact. Directed and Standard holders otherwise preserve the order until the player changes it.");
            l.Gap(6f);
            l.CheckboxLabeled("Suppressive and bounded movement", ref Settings.withdrawals,
                "Open CA direct combat orders on reachable ground: suppressive movement (bound, halt, fire at the nearest threat, bound again) or covered bounded movement with a partner you name. The partner menu shows eligibility and physical delivery before click; the named partner then separately resolves obedience, and refusal never silently becomes a solo order. Each bound and hold retains the original operator/relay episode in the trace. Works advancing or falling back and fires only inside weapon range. An automatic health-based break from a shared Hold may pair only with a co-holder from that same order episode.");
            l.Gap(6f);
            l.CheckboxLabeled("Weapon transitions", ref Settings.weaponTransitions,
                "An enemy closing inside a ranged weapon's dead zone triggers the switch: sidearm at close range, melee when they're on top of you or melee is the pawn's stronger skill - then back to the primary when the fight opens up. Applies to undrafted Proactive+ colonists and generic settlement assaulters; forced player work always overrides. Uses Simple Sidearms' own swap machinery when that mod is present.");
            l.Gap(6f);
            l.CheckboxLabeled("Ambush strikes", ref Settings.ambushStrikes,
                "CA direct combat orders can place at least two accepted fighters into a synchronized ambush. The menu exposes why a pawn is ineligible; every member must have a unique reachable concealment slot before anyone is ordered, and every assigned member must arrive before the readiness deadline. Cardinality collapse, discovery, injury, replacement, or timeout aborts the entire episode. One close visible trigger then releases every ready ambusher on the same game tick. Each actor uses the equipped weapon only with a valid target and a safe friendly lane. Doorframes and wall corners count as concealment.");
            l.Gap(6f);
            l.CheckboxLabeled("Animal care", ref Settings.animalCare,
                "Handlers feed starving colony animals from whatever they can eat (kibble and hay first) - patient-fed if downed, dropped at their feet otherwise - and rescue downed animals to open animal beds. Proactive+ handlers already act at urgently hungry.");
            l.Gap(6f);
            l.CheckboxLabeled("Fire response", ref Settings.fireResponse,
                "Fire response: Proactive+ colonists drop unforced walks, swims, and recreation when native emergency firefighting can answer a Home-area fire. They may directly fight safely reachable fires on player buildings outside Home. Firefighting work, direct orders, areas, and native duty priority remain authoritative.");
            l.Gap(6f);
            l.CheckboxLabeled("Field medicine", ref Settings.fieldMedicine,
                "A capable Proactive+ medic acts from a pawn-private welfare fact, never a whole-map casualty scan. Firsthand sight records gross downed state; valid voice, headset-radio, and mechlink-mental reports preserve the teller's remembered cell and age. The medic goes to that saved cell and visibly assesses before acquiring severity. Medicine drives speed and precision while Intellectual helps, so low skill leaves broad uncertainty. Medicine 4+ stabilizes an assessed urgent bleed and Medicine 6+ may act on less immediate bleeding, using permitted reachable medicine when available. Between actor-local exchanges, a materially degraded drafted Proactive+ fighter who can care and manipulate may perform one finite emergency self-tend under combat-recovery ownership. A Man in Black keeps the incident's separate beneficiary authorization and the same local clinical limits. Neither bounded self-treatment path changes your global self-tend policy.");
            l.Gap(6f);
            l.CheckboxLabeled("Survival responses", ref Settings.survivalResponses,
                "A violent hit gives a Proactive+ colonist or generic settlement assaulter an immediate individual survival check unless a current direct player job owns them. Pain, consciousness, movement, manipulation, breathing, blood pumping, blood loss, bleed rate, vital injury, death horizon, cover, ability to engage, skill, and disposition decide whether to improve position, hold, or break contact. A seen incoming explosive may interrupt a CA-authored commitment so the pawn can sprint beyond its predicted blast or gas envelope; direct and queued player work remain authoritative. During raids, non-fighters still respond per who they are.");
            l.Gap(6f);
            l.Label("Default autonomy for undialed colonists: " + AutonomyComponent.LevelNames[Settings.defaultAutonomy],
                tooltip: "Colonists you haven't set individually use this level. Per-pawn dials always override.");
            Settings.defaultAutonomy = (int)l.Slider(Settings.defaultAutonomy, 0f, 3f);
            settingsScrollHeight = l.CurHeight;
            l.End();
            Widgets.EndScrollView();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
