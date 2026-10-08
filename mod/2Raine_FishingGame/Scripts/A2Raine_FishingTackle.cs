using System;

namespace XRL.World.Parts
{
    /// <summary>
    /// Shared base for everything that can be slotted onto a fishing pole.
    ///
    /// THE POINT OF THE BASE CLASS is that the pole's socket does not care whether it is holding a
    /// bait or a float: both are just "a tackle with three numbers on it". Slotting, unslotting,
    /// describing and reading all run through one code path, so adding a third kind of tackle later
    /// (scent, weight, lure) costs one blueprint and one empty subclass -- see the note on
    /// A2Raine_FishingBait below, which is literally nothing but the marker.
    ///
    /// XML FIELDS MATCH THE C# FIELD NAMES exactly (that is how Qud binds &lt;part&gt; attributes), so the
    /// blueprints read SpeedBonus= / CatchBonus= / TreasureBonus= and nothing else is needed.
    /// </summary>
    [Serializable]
    public abstract class A2Raine_FishingTackle : IPart
    {
        /// <summary>
        /// Milliseconds taken off the wait before something bites. The base wait is set by the pole;
        /// a bait subtracts from it. Both baits use this one, the better one simply carries a bigger
        /// number -- which is exactly the "beginner bait vs advanced bait" split in the plan.
        /// </summary>
        public int SpeedBonus;

        /// <summary>
        /// Percentage points added to the chance of landing a fish. This is the FLOAT's field: the
        /// plan gives "bright flasher" the job of raising the bite rate.
        /// </summary>
        public int CatchBonus;

        /// <summary>
        /// Weight added to the treasure entry when the catch is rolled. This is the "treasure hunter"
        /// float's field.
        ///
        /// NOT YET WIRED TO ANYTHING: a treasure catch needs a portable chest, and Qud's Chest is
        /// Inherits="Furniture" (Furniture.xml:3509) -- furniture, not loot. The field is defined and
        /// readable now so the data is already in place when a catchable cache is added.
        /// </summary>
        public int TreasureBonus;

        /// <summary>
        /// Slotted tackle never merges: two baits that happen to roll the same numbers are still two
        /// separate objects sitting in two separate poles.
        /// </summary>
        public override bool SameAs(IPart p)
        {
            return false;
        }
    }

    /// <summary>
    /// A bait. Intentionally empty: everything it does is a number supplied by its blueprint, and
    /// the behaviour lives in the pole that reads those numbers. Keeping it a distinct type is what
    /// lets the socket tell "this goes in the bait slot" from "this goes in the float slot".
    /// </summary>
    [Serializable]
    public class A2Raine_FishingBait : A2Raine_FishingTackle
    {
    }

    /// <summary>
    /// A float. Empty for the same reason as the bait; distinct only so it lands in the other slot.
    /// </summary>
    [Serializable]
    public class A2Raine_FishingFloat : A2Raine_FishingTackle
    {
    }
}
