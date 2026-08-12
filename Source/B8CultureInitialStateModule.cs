using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // Established NPC Culture begins from observed population history and
    // material practice, independently of Political Beliefs and Ideoligion.
    // Those systems may contribute later interpretations, but neither is a
    // substitute for Culture(T0).
    internal static class CACultureInitialState
    {
        internal static bool EnsureForFaction(CACulture culture,
            Faction faction)
        {
            if (culture == null || faction?.def == null) return false;
            int settlements = 0;
            try
            {
                settlements = Find.WorldObjects?.Settlements?
                    .Count(value => value?.Faction == faction) ?? 0;
            }
            catch { }
            int hostile = 0;
            try
            {
                hostile = Find.FactionManager?.AllFactionsListForReading?
                    .Count(value => value != null && value != faction
                        && faction.RelationKindWith(value)
                            == FactionRelationKind.Hostile) ?? 0;
            }
            catch { }
            return Ensure(culture, faction.Name, faction.def, settlements,
                hostile, "world faction " + faction.loadID);
        }

        internal static bool EnsureForRegional(CACulture culture,
            CARegionalPlan plan, CARegionalFactionPlan faction)
        {
            if (culture == null || faction == null) return false;
            FactionDef def = faction.ResolvedFactionDef;
            if (def == null) return false;
            int settlements = plan?.settlements?.Count(value => value != null
                && value.factionKey == faction.key) ?? 0;
            int hostile = plan?.relations?.Count(value => value != null
                && value.relation == FactionRelationKind.Hostile
                && (value.leftFactionKey == faction.key
                    || value.rightFactionKey == faction.key)) ?? 0;
            return Ensure(culture, CARegionalPlanUtility.FactionName(faction),
                def, settlements, hostile,
                "starting-region faction " + faction.key);
        }

        private static bool Ensure(CACulture culture, string populationName,
            FactionDef def, int settlements, int hostile, string owner)
        {
            CACultureModel.Normalize(culture);
            if (HasSubstance(culture)) return false;
            if (culture.name.NullOrEmpty())
                culture.name = (populationName.NullOrEmpty()
                    ? "Unnamed faction" : populationName) + " culture";
            CACultureModel.EnsureGenerated(culture,
                owner + ":culture-t0", null);
            if (culture.constituents.Count == 1
                && culture.constituents[0] != null)
            {
                culture.constituents[0].cultureId = culture.id;
                culture.constituents[0].label = culture.name;
                culture.constituents[0].share = 100;
            }

            bool trades = (def.caravanTraderKinds?.Count ?? 0) > 0
                || (def.visitorTraderKinds?.Count ?? 0) > 0
                || def.canRequestTraders || def.canRequestOrbitalTrader;
            bool organizedDefense = !def.raidsForbidden
                && (def.canStageAttacks || def.canSiege
                    || def.techLevel >= TechLevel.Industrial);
            bool researchContinuity = def.techLevel >= TechLevel.Industrial
                || (def.startingResearchTags?.Count ?? 0) > 0;
            string facts = "settlements=" + settlements + "; hostile="
                + hostile + "; tech=" + def.techLevel + "; trade=" + trades
                + "; organizedDefense=" + organizedDefense;

            AddMeaning(culture, CASocialSubjectRegistry.PublicGathering,
                settlements > 0 ? 30 : 5,
                Math.Min(85, 42 + settlements * 8), 12,
                Math.Min(80, 45 + settlements * 6), owner,
                "established settlement distribution; " + facts);
            AddMeaning(culture, CASocialSubjectRegistry.OutsiderContact,
                trades ? 45 : -20, trades ? 68 : 32,
                trades ? 28 : -8, trades ? 62 : 42, owner,
                "faction trade and visitor capabilities; " + facts);
            AddMeaning(culture, CASocialSubjectRegistry.DefendedBoundary,
                organizedDefense ? 48 : 12,
                organizedDefense ? 72 : 38,
                organizedDefense ? 30 : 5,
                Math.Min(85, 48 + hostile * 8), owner,
                "military configuration and known relations; " + facts);
            AddMeaning(culture, CASocialSubjectRegistry.ResearchWork,
                researchContinuity ? 42 : 8,
                researchContinuity ? 65 : 30,
                researchContinuity ? 34 : 5,
                researchContinuity ? 58 : 30, owner,
                "technology and starting research continuity; " + facts);

            if (trades)
                AddPractice(culture, CASocialSubjectRegistry.OutsiderContact,
                    "Traders and visitors sustain contact beyond the faction's settlements",
                    60, owner, facts);
            else if (researchContinuity)
                AddPractice(culture, CASocialSubjectRegistry.ResearchWork,
                    "Established knowledge is taught and maintained",
                    58, owner, facts);
            culture.temporalBasis = "Inherited before the scenario from "
                + "established population and material evidence.";
            culture.maturity = CACultureMaturity.Inherited;
            return true;
        }

        private static bool HasSubstance(CACulture culture)
        {
            return culture.inheritedMeanings.Count > 0
                || culture.localMeanings.Count > 0
                || culture.inheritedPractices.Count > 0
                || culture.practices.Count > 0;
        }

        private static void AddMeaning(CACulture culture, string subject,
            int approval, int normality, int prestige, int salience,
            string owner, string evidence)
        {
            culture.inheritedMeanings.Add(new CACulturalMeaning
            {
                subjectKey = subject,
                populationScope = "*",
                approval = approval,
                normality = normality,
                prestige = prestige,
                salience = salience,
                provenance = "inherited from established evidence",
                sourceIdentity = owner,
                evidenceSignature = CASocialPatternKernel.StableHash(
                    evidence + "|" + subject),
                firstRecordedTick = -1,
                lastChangedTick = -1,
                weight = 100
            });
        }

        private static void AddPractice(CACulture culture, string subject,
            string summary, int strength, string owner, string evidence)
        {
            culture.inheritedPractices.Add(new CACulturePractice
            {
                subjectKey = subject,
                summary = summary,
                strength = strength,
                firstRecordedTick = -1,
                lastObservedTick = -1,
                sourceSignature = CASocialPatternKernel.StableHash(
                    owner + "|" + evidence + "|" + subject),
                sourcePeriod = "before scenario start"
            });
        }
    }
}
