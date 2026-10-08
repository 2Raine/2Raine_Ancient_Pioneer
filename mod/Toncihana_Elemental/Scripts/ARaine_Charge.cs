using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// Shared helpers for the Toncihana "Elemental" race.
    ///
    /// Everything here goes through the charge fields that ELECTRICAL GENERATION itself owns.
    /// There is deliberately no Capacitor part anywhere: in Caves of Qud the ElectricalGeneration
    /// mutation is its own charge store (it declares Charge / GetCharge / AddCharge / UseCharge /
    /// GetMaxCharge and handles QueryChargeEvent / TestChargeEvent / UseChargeEvent), which is why
    /// no vanilla creature with the mutation carries a capacitor.
    /// </summary>
    public static class ARaine_Charge
    {
        /// <summary>
        /// Find the charge store on an object.
        ///
        /// THE BUG THIS EXISTS TO AVOID, proven in game from this mod's own diagnostic:
        ///
        ///     charge tick | GetCharge()=3290 | GetMaxCharge()=4000 | lookupByType=0 | lookupPart=False
        ///     charge@.../entry | vanillaPart=False | partByString=False | partByClassName=True
        ///
        /// The charge was there (3290 of 4000) and the part was on the object, yet
        /// GetPart&lt;ElectricalGeneration&gt;() returned null for it AND GetPart("ElectricalGeneration")
        /// did not match it. So this mod's subclass is not reachable through either generic lookup,
        /// and every charge ability therefore read 0 and refused to fire.
        ///
        /// The reliable route is the one call the diagnostic proved works: GetPart by the concrete
        /// class NAME. It is tried first, so it keeps working whether or not the generic lookup
        /// happens to match this build.
        /// </summary>
        public static ElectricalGeneration GetGeneration(GameObject Object)
        {
            if (Object == null)
            {
                return null;
            }

            // Proven to work (partByClassName=True in the diagnostic).
            ElectricalGeneration gen =
                Object.GetPart("A2Raine_Toncihana_Stormcharge") as ElectricalGeneration;
            if (gen != null)
            {
                return gen;
            }

            // Fallbacks, in case a future build makes the generic lookups match subclasses again.
            gen = Object.GetPart<ElectricalGeneration>();
            if (gen != null)
            {
                return gen;
            }
            return Object.GetPart("ElectricalGeneration") as ElectricalGeneration;
        }

        /// <summary>Base chance floor for ability attempts, in percent, so the mechanic is testable.</summary>
        public const int MinAttemptChance = 50;

        /// <summary>
        /// Diagnostic: only complains when the charge store genuinely cannot be found, so a working
        /// game does not fill Player.log with part dumps.
        /// </summary>
        public static void LogChargeState(GameObject Object, string Where)
        {
            if (Object == null)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] charge@" + Where + ": object is null");
                return;
            }

            ElectricalGeneration g = GetGeneration(Object);
            if (g == null)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] charge@" + Where
                    + ": NO charge store found. partByClassName="
                    + (Object.GetPart("A2Raine_Toncihana_Stormcharge") != null)
                    + ", vanillaPart=" + (Object.GetPart<ElectricalGeneration>() != null)
                    + ", partByString=" + (Object.GetPart("ElectricalGeneration") != null));
                return;
            }

            UnityEngine.Debug.LogWarning("[Toncihana] charge@" + Where
                + " OK | runtimeType=" + g.GetType().Name
                + " | charge=" + g.GetCharge() + "/" + g.GetMaxCharge()
                + " | level=" + g.Level
                + " | " + (g.GetCharge() > 0 ? "usable" : "EMPTY"));
        }

        public static int GetCharge(GameObject Object)
        {
            ElectricalGeneration gen = GetGeneration(Object);
            return gen == null ? 0 : gen.GetCharge();
        }

        /// <summary>The face link, whose price is a permanently occupied tenth of the charge store.</summary>
        public const string FaceLinkBlueprint = "2Raine_Toncihana_Link_Face";

        /// <summary>Percent of maximum charge that link holds occupied.</summary>
        public const int FaceLinkOccupiedPercent = 10;

        /// <summary>Is that link implanted on this creature?</summary>
        public static bool HasFaceLink(GameObject Object)
        {
            return A2Raine_Toncihana_StormLinks.HasLink(Object, FaceLinkBlueprint);
        }

        public static int GetMaxCharge(GameObject Object)
        {
            ElectricalGeneration gen = GetGeneration(Object);
            if (gen == null)
            {
                return 0;
            }

            int max = gen.GetMaxCharge();

            // The face link holds a tenth of the store permanently occupied: it is the price of
            // hearing the far voice. Both the recharge ceiling and what the bearer can spend read
            // through here, so the occupied tenth is unavailable on both ends.
            if (HasFaceLink(Object))
            {
                max = max * (100 - FaceLinkOccupiedPercent) / 100;
            }

            return max;
        }

        /// <summary>0..100. Returns 0 when the object has no charge store at all.</summary>
        public static int GetChargePercent(GameObject Object)
        {
            ElectricalGeneration gen = GetGeneration(Object);
            if (gen == null)
            {
                return 0;
            }
            int max = GetMaxCharge(Object);
            if (max <= 0)
            {
                return 0;
            }
            return (int)(100L * gen.GetCharge() / max);
        }

        /// <summary>Spend charge if there is enough; returns false and spends nothing otherwise.</summary>
        public static bool TryUseCharge(GameObject Object, int Amount)
        {
            ElectricalGeneration gen = GetGeneration(Object);
            if (gen == null || Amount < 0)
            {
                return false;
            }
            if (gen.GetCharge() < Amount)
            {
                return false;
            }
            gen.UseCharge(Amount);
            return true;
        }

        /// <summary>The race's own message channel: charge amounts read as current, not as blood.</summary>
        public static void Message(GameObject Object, string Text)
        {
            if (Object != null && Object.IsPlayer())
            {
                MessageQueue.AddPlayerMessage(Text);
            }
        }
    }
}
