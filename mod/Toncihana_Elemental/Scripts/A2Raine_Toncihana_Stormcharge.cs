using System;
using XRL.World;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts.Mutation
{
    /// <summary>
    /// "Overcharged Electrical Generation" -- vanilla Electrical Generation that accrues charge
    /// FASTER_PER_LEVEL times as fast per mutation level.
    ///
    ///     level 1 -> 2x the vanilla rate   (200 charge/turn at baseline Willpower)
    ///     level 2 -> 3x                    (300)
    ///     level 5 -> 6x                    (600)
    ///
    /// WHY THIS IS NOT AN OVERRIDE OF GetChargePerTurn
    /// ----------------------------------------------
    /// Vanilla's GetChargePerTurn(int) is public but NON-virtual, so it cannot be overridden; csc
    /// rejects it with CS0506. Vanilla's own charging is therefore left completely alone and the
    /// extra charge is added on top.
    ///
    /// HOW THE RATE IS COMPUTED (and the bug this replaces)
    /// ---------------------------------------------------
    /// The previous version let vanilla accrue, measured how much the charge went up, and added
    /// (measured x (multiplier - 1)). That was wrong twice over:
    ///
    ///   * GetOverchargeMultiplier kept evaluating to 1, so the bonus was always 0 and the rate
    ///     stayed exactly vanilla -- confirmed from this mod's own log, which printed
    ///     "charge tick | normalGain=70 extra=0" on every single tick.
    ///   * Reading it through the difference also coupled the result to whatever else happened to
    ///     the charge that tick (discharge, spending, EMP), so the number was never trustworthy.
    ///
    /// It now asks vanilla directly: GetChargePerTurn() is a public instance method that returns the
    /// per-turn figure after vanilla's own Willpower clamping, so multiplying THAT is both simpler
    /// and keeps vanilla's Willpower scaling intact.
    ///
    /// Warning for future me: do not "measure the difference" again. A zero here is silent -- the
    /// mutation still works, it just quietly pays the vanilla rate.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_Stormcharge : ElectricalGeneration
    {
        /// <summary>
        /// How much faster than vanilla the charge accrues. A FLAT 200%, at every level.
        ///
        /// There used to be a MULTIPLIER_PER_EXTRA_LEVEL that made this climb (level 1 -> 2x,
        /// level 2 -> 3x, level N -> N+1). That is gone: the description was showing a different
        /// number every time the mutation levelled, which read as though the ability scaled with
        /// level, and the rate was not meant to be a progression track. Simplifying it to a
        /// constant also removes the whole class of bug where the printed figure and the applied
        /// figure could drift apart.
        ///
        /// This is the single source of truth for BOTH the charging code and the description text.
        /// </summary>
        public const int OVERCHARGE_PERCENT = 200;

        /// <summary>Total multiplier: always 2x, whatever level is asked about.</summary>
        public static int GetOverchargeMultiplier(int ForLevel)
        {
            return OVERCHARGE_PERCENT / 100;
        }

        /// <summary>Multiplier at this mutation's current level (also always 2x).</summary>
        public int GetOverchargeMultiplier()
        {
            return GetOverchargeMultiplier(Level);
        }

        /// <summary>
        /// Let vanilla do its own accrual (so every clamp, cap, Willpower term and EMP interaction
        /// stays vanilla), then top the charge up to MULTIPLIER x the rate vanilla actually used.
        ///
        /// RACE LOCK: the top-up only happens for an object carrying A2Raine_Toncihana_Physiology,
        /// i.e. an actual Toncihana. Anything else that somehow ends up with this mutation silently
        /// falls back to the plain vanilla rate, so the bonus can never leak out of this race.
        /// </summary>
        public override void TurnTick(long TimeTick, int Amount)
        {
            base.TurnTick(TimeTick, Amount);

            if (ParentObject == null || !ParentObject.HasPart<A2Raine_Toncihana_Physiology>())
            {
                return;
            }

            // Vanilla's own per-turn figure, after its Willpower clamp. This is the number the
            // character would have accrued this tick at 1x.
            int vanillaRate = GetChargePerTurn();
            if (vanillaRate <= 0)
            {
                return;
            }

            int multiplier = GetOverchargeMultiplier();
            if (multiplier <= 1)
            {
                return;
            }

            // Top up to the overcharged rate: vanilla already added vanillaRate, so only the
            // remaining (multiplier - 1) x vanillaRate is missing.
            int extra = vanillaRate * (multiplier - 1);
            if (extra > 0)
            {
                AddCharge(extra);
            }
        }

        /// <summary>
        /// The rate sentence is FIXED TEXT and deliberately does not read Level.
        ///
        /// It used to print the multiplier for whichever level was being shown, so levelling the
        /// mutation changed the number in the description and made the ability look like it was
        /// scaling when it was not. The rate is a flat 200% of vanillla at every level, so the text
        /// says exactly that and never moves.
        ///
        /// The figure comes from OVERCHARGE_PERCENT, the same constant the charging code uses, so
        /// the text cannot drift away from the behaviour again.
        /// </summary>
        public override string GetLevelText(int Level)
        {
            return base.GetLevelText(Level)
                + "\nCharge accrues at {{C|" + OVERCHARGE_PERCENT + "%}} the usual rate.";
        }

        public override string GetDescription()
        {
            return "You are a storm given shape, and the current gathers in you faster than it "
                + "gathers in mortal flesh.";
        }
    }
}
