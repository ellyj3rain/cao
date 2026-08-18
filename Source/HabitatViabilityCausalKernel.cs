using System;
using System.Collections.Generic;

namespace ColonistAwareness
{
    [Flags]
    public enum CAHabitatRequirement
    {
        None = 0,
        EnclosedShelter = 1 << 0,
        ThermalControl = 1 << 1,
        ReliableFood = 1 << 2,
        SecuredFoodSupply = 1 << 3,
        FoodReserve = 1 << 4,
        MedicalCare = 1 << 5,
        WaterTreatment = 1 << 6,
        ArtificialLight = 1 << 7,
        HazardProtection = 1 << 8,
        BreathableInterior = 1 << 9
    }

    public enum CAHabitatFoodRoute : byte
    {
        OutdoorCultivation = 0,
        ForageAndHunt = 1,
        ProtectedLowLightCultivation = 2,
        ProtectedCultivation = 3,
        StoredAndSupported = 4
    }

    public sealed class CAHabitatEnvironmentInput
    {
        public int GrowingTwelfths;
        public float FoodSupport;
        public float ThermalPressure;
        public float DiseasePerYear;
        public float PollutionPressure;
        public float ScariaPressure;
        public bool PermanentDarkness;
        public bool ToxicWater;
        public bool FarmingCampsAllowed = true;
        public bool Vacuum;
    }

    public sealed class CAHabitatRequirementProfile
    {
        public int RequirementMask;
        public int RequiredCapabilityTier;
        public CAHabitatFoodRoute FoodRoute;

        public bool Requires(CAHabitatRequirement requirement)
        {
            return (RequirementMask & (int)requirement) != 0;
        }
    }

    public sealed class CAHabitatCapabilityInput
    {
        public int TechnologyTier;
        public bool EnclosedShelter;
        public bool ThermalControl;
        public bool ReliableFood;
        public bool SecuredFoodSupply;
        public bool FoodReserve;
        public bool MedicalCare;
        public bool WaterTreatment;
        public bool ArtificialLight;
        public bool HazardProtection;
        public bool BreathableInterior;
    }

    public sealed class CAHabitatViabilityResult
    {
        public int CapabilityMask;
        public int MissingRequirementMask;
        public bool Viable;

        public bool Missing(CAHabitatRequirement requirement)
        {
            return (MissingRequirementMask & (int)requirement) != 0;
        }
    }

    // Physical ground declares requirements. A population or site supplies
    // capabilities. Neither side invents the other, and a label or score can
    // never stand in for a missing survival function.
    public static class CAHabitatViabilityCausalKernel
    {
        public static CAHabitatRequirementProfile Requirements(
            CAHabitatEnvironmentInput input)
        {
            input = input ?? new CAHabitatEnvironmentInput();
            CAHabitatRequirement mask = CAHabitatRequirement.EnclosedShelter
                | CAHabitatRequirement.ReliableFood;

            bool protectedFood = input.PermanentDarkness || input.Vacuum
                || input.GrowingTwelfths <= 2
                || (!input.FarmingCampsAllowed
                    && input.GrowingTwelfths < 8)
                || input.FoodSupport < 0.20f;
            bool thermal = input.ThermalPressure >= 0.25f || input.Vacuum;
            bool medical = input.DiseasePerYear >= 1f
                || input.ScariaPressure >= 0.20f
                || input.PollutionPressure >= 0.10f;
            bool hazard = input.Vacuum
                || input.ScariaPressure >= 0.25f
                || input.PollutionPressure >= 0.15f;

            if (thermal) mask |= CAHabitatRequirement.ThermalControl;
            if (protectedFood)
                mask |= CAHabitatRequirement.SecuredFoodSupply;
            if (protectedFood || input.FoodSupport < 0.55f)
                mask |= CAHabitatRequirement.FoodReserve;
            if (medical) mask |= CAHabitatRequirement.MedicalCare;
            if (input.ToxicWater)
                mask |= CAHabitatRequirement.WaterTreatment;
            if (input.PermanentDarkness)
                mask |= CAHabitatRequirement.ArtificialLight;
            if (hazard) mask |= CAHabitatRequirement.HazardProtection;
            if (input.Vacuum)
                mask |= CAHabitatRequirement.BreathableInterior;

            int tier = 0;
            if (thermal && input.ThermalPressure >= 0.55f) tier = 1;
            if (medical && input.DiseasePerYear >= 2f) tier = 1;
            if (protectedFood || input.ToxicWater || hazard) tier = 2;
            if (input.Vacuum) tier = 3;

            CAHabitatFoodRoute route;
            if (protectedFood)
                route = input.PermanentDarkness && !input.Vacuum
                    ? CAHabitatFoodRoute.ProtectedLowLightCultivation
                    : CAHabitatFoodRoute.ProtectedCultivation;
            else if (input.GrowingTwelfths >= 6)
                route = CAHabitatFoodRoute.OutdoorCultivation;
            else if (input.FoodSupport >= 0.65f)
                route = CAHabitatFoodRoute.ForageAndHunt;
            else
                route = CAHabitatFoodRoute.StoredAndSupported;

            return new CAHabitatRequirementProfile
            {
                RequirementMask = (int)mask,
                RequiredCapabilityTier = tier,
                FoodRoute = route
            };
        }

        public static int CapabilityMask(CAHabitatCapabilityInput input)
        {
            if (input == null) return 0;
            CAHabitatRequirement mask = CAHabitatRequirement.None;
            if (input.EnclosedShelter)
                mask |= CAHabitatRequirement.EnclosedShelter;
            if (input.ThermalControl)
                mask |= CAHabitatRequirement.ThermalControl;
            if (input.ReliableFood)
                mask |= CAHabitatRequirement.ReliableFood;
            if (input.SecuredFoodSupply)
                mask |= CAHabitatRequirement.SecuredFoodSupply;
            if (input.FoodReserve)
                mask |= CAHabitatRequirement.FoodReserve;
            if (input.MedicalCare)
                mask |= CAHabitatRequirement.MedicalCare;
            if (input.WaterTreatment)
                mask |= CAHabitatRequirement.WaterTreatment;
            if (input.ArtificialLight)
                mask |= CAHabitatRequirement.ArtificialLight;
            if (input.HazardProtection)
                mask |= CAHabitatRequirement.HazardProtection;
            if (input.BreathableInterior)
                mask |= CAHabitatRequirement.BreathableInterior;
            return (int)mask;
        }

        public static CAHabitatViabilityResult Evaluate(
            CAHabitatRequirementProfile requirements,
            CAHabitatCapabilityInput capability)
        {
            requirements = requirements ?? new CAHabitatRequirementProfile();
            int available = CapabilityMask(capability);
            int missing = requirements.RequirementMask & ~available;
            return new CAHabitatViabilityResult
            {
                CapabilityMask = available,
                MissingRequirementMask = missing,
                Viable = missing == 0 && capability != null
                    && capability.TechnologyTier
                        >= requirements.RequiredCapabilityTier
            };
        }

        // A frontier plan materializes these functions itself. The tier says
        // which of those functions its supporting population can actually
        // construct and maintain; it is not a generic development score.
        public static CAHabitatCapabilityInput FrontierCapability(int tier)
        {
            int value = Clamp(tier, 0, 3);
            return new CAHabitatCapabilityInput
            {
                TechnologyTier = value,
                EnclosedShelter = true,
                ThermalControl = value >= 1,
                ReliableFood = true,
                SecuredFoodSupply = value >= 2,
                FoodReserve = true,
                MedicalCare = value >= 1,
                WaterTreatment = value >= 2,
                ArtificialLight = true,
                // The current compact-site materializer does not yet prove
                // protective apparel, filtration, or a defended animal-safe
                // envelope. Technology alone cannot satisfy that physical
                // requirement.
                HazardProtection = false,
                // No frontier materializer currently proves a sealed,
                // breathable interior. Spacer technology by itself is not a
                // building, so vacuum sites remain ineligible until an actual
                // life-support path exists.
                BreathableInterior = false
            };
        }

        // Settlement placement asks whether an established population can
        // author the required programs and structures on this ground. This is
        // potential capacity only: confirmation still evaluates the exact
        // saved settlement programs. Frontier sites use FrontierCapability
        // instead because their compact materializer must prove every
        // function before residents appear.
        public static CAHabitatCapabilityInput SettlementPotentialCapability(
            int tier)
        {
            int value = Clamp(tier, 0, 3);
            return new CAHabitatCapabilityInput
            {
                TechnologyTier = value,
                EnclosedShelter = true,
                ThermalControl = value >= 1,
                ReliableFood = true,
                SecuredFoodSupply = value >= 2,
                FoodReserve = true,
                MedicalCare = value >= 1,
                WaterTreatment = value >= 2,
                ArtificialLight = true,
                HazardProtection = value >= 2,
                BreathableInterior = value >= 3
            };
        }

        public static IEnumerable<CAHabitatRequirement> Enumerate(int mask)
        {
            foreach (CAHabitatRequirement value in Enum.GetValues(
                typeof(CAHabitatRequirement)))
                if (value != CAHabitatRequirement.None
                    && (mask & (int)value) != 0)
                    yield return value;
        }

        public static string RequirementWords(CAHabitatRequirement value)
        {
            switch (value)
            {
                case CAHabitatRequirement.EnclosedShelter:
                    return "enclosed shelter";
                case CAHabitatRequirement.ThermalControl:
                    return "heating or cooling";
                case CAHabitatRequirement.ReliableFood:
                    return "a reliable food route";
                case CAHabitatRequirement.SecuredFoodSupply:
                    return "protected food production or outside supply";
                case CAHabitatRequirement.FoodReserve:
                    return "preserved food stores";
                case CAHabitatRequirement.MedicalCare:
                    return "local medical care";
                case CAHabitatRequirement.WaterTreatment:
                    return "safe water treatment";
                case CAHabitatRequirement.ArtificialLight:
                    return "artificial lighting";
                case CAHabitatRequirement.HazardProtection:
                    return "environmental hazard protection";
                case CAHabitatRequirement.BreathableInterior:
                    return "a sealed breathable interior";
                default: return value.ToString();
            }
        }

        public static string FoodRouteWords(CAHabitatFoodRoute route)
        {
            switch (route)
            {
                case CAHabitatFoodRoute.ForageAndHunt:
                    return "foraging and hunting";
                case CAHabitatFoodRoute.ProtectedLowLightCultivation:
                    return "protected low-light cultivation";
                case CAHabitatFoodRoute.ProtectedCultivation:
                    return "protected cultivation";
                case CAHabitatFoodRoute.StoredAndSupported:
                    return "stored food and outside support";
                default: return "outdoor cultivation";
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum
                : value > maximum ? maximum : value;
        }
    }
}
