using System;
using UnityEngine;

namespace ColonistAwareness
{
    // WHAT A SETTLEMENT IS AT THE START OF A GAME.
    //
    // The record carries seven authored development axes -- landCapacity,
    // economicCapacity, tradeConnectivity, specialization,
    // historicalDevelopment, urbanSupport, operationalRoleMask -- and each is
    // computed, saved, and consumed by other systems. NONE of them reached
    // SettlementMorphology, MorphologyAdapter or SettlementAxes. The physical
    // settlement read exactly one authored value, the technology level, so a
    // hamlet and a regional capital were the same size with the same street
    // pattern, and everything the player set about development, economy,
    // trade, specialisation and role was discarded before a wall was placed.
    //
    // Two consequences, both visible in the game today:
    //   * BodyArea scaled a settlement purely by FORM (0.30 tribal, 0.38
    //     medieval, 0.42 industrial), so development changed nothing.
    //   * districts came from region.settlements.Count -- a settlement's
    //     internal urban complexity was a property of how many OTHER
    //     settlements the region happened to contain.
    //
    // This derives the settlement's own standing from its own authored axes.
    // It is the starting state that later autonomous development grows FROM,
    // so the same profile is the thing runtime growth should raise.
    internal sealed class CASettlementDevelopment
    {
        // 0 hamlet, 1 village, 2 town, 3 city, 4 regional capital.
        internal int Level;
        internal string LevelWord;
        // Multiplier on the form's body-area fraction.
        internal float Density = 1f;
        // Districts belong to THIS settlement's urban standing.
        internal int Districts = 1;
        // A wall is a security decision, not a default.
        internal bool Walled;
        internal string Provenance = "unresolved";

        private static readonly string[] Words =
        {
            "hamlet", "village", "town", "city", "regional capital"
        };

        // An axis is -1 when it was never resolved. Unresolved axes must not
        // read as zero, or an unauthored settlement would be forced to the
        // smallest form rather than left at its neutral middle.
        private static int Axis(int value, int neutral)
        {
            return value < 0 ? neutral : value;
        }

        internal static CASettlementDevelopment For(
            CARegionalSettlementRecord record)
        {
            var profile = new CASettlementDevelopment();
            if (record == null)
            {
                profile.LevelWord = Words[1];
                profile.Level = 1;
                return profile;
            }

            // MEASURED SCALES, not assumed ones. The first live run printed
            // urban 50-64 and population 605 for every settlement: urban
            // support is a 0-100 score, not a 0-4 band, and the baseline
            // population fields carry a region-wide figure, not this
            // settlement's residents. Fed raw, both saturated the formula and
            // every settlement became an identical walled regional capital --
            // uniformity again, this time from a scale mismatch.
            float urban = record.urbanSupport < 0 ? 1f
                : record.urbanSupport > 8
                    ? Mathf.Clamp(record.urbanSupport / 25f, 0f, 4f)
                    : Mathf.Clamp(record.urbanSupport, 0, 4);
            int history = Axis(record.historicalDevelopment, 1);
            int land = Axis(record.landCapacity, 1);
            int economy = Axis(record.economicCapacity, 1);
            int trade = Axis(record.tradeConnectivity, 1);
            int population =
                record.populationBaseline > 0
                    && record.populationBaseline <= 200
                ? record.populationBaseline
                : record.residentPopulation > 0
                    && record.residentPopulation <= 200
                ? record.residentPopulation : 10;

            // Urban standing is what the place has become: the support it can
            // sustain and the history behind it, corroborated by how many
            // people actually live there.
            float standing = urban + history
                + Mathf.Clamp(population / 12f, 0f, 2f);
            profile.Level = Mathf.Clamp(Mathf.RoundToInt(standing / 1.6f),
                0, 4);
            profile.LevelWord = Words[profile.Level];

            // Extent follows the land the settlement actually holds, how
            // densely its urban standing packs that land, and how many people
            // it actually supports. FORM IS TEXTURE, STANDING IS SCALE: a
            // tribal society can be vast -- the gradient runs the full
            // spectrum for every form, so the ceiling belongs to standing and
            // population, never to the form's style.
            profile.Density = Mathf.Clamp(
                0.55f + land * 0.18f + profile.Level * 0.14f
                    + population / 40f, 0.5f, 3.2f);

            // Districts are this settlement's own complexity. A hamlet is one
            // place; a capital is several joined by through-streets. Trade and
            // economy add a district before urban standing alone would.
            int commercial = (economy + trade) >= 4 ? 1 : 0;
            profile.Districts = Mathf.Clamp(
                1 + profile.Level / 2 + commercial, 1, 4);

            // A wall is raised when the settlement has both a reason and the
            // means: a security role or real standing, plus the capacity to
            // build and hold one. Not every settlement is walled, and none is
            // walled merely because it exists.
            bool securityRole = (record.operationalRoleMask & 1) != 0;
            profile.Walled = (securityRole || profile.Level >= 2)
                && land >= 1;

            profile.Provenance = "urban " + urban + "; history " + history
                + "; land " + land + "; economy " + economy + "; trade "
                + trade + "; population " + population + " -> "
                + profile.LevelWord + ", density "
                + profile.Density.ToString("F2") + ", districts "
                + profile.Districts + (profile.Walled ? ", walled" : ", open");
            return profile;
        }
    }
}
