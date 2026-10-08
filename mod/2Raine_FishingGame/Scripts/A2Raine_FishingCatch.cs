using System;
using XRL.Rules;

namespace XRL.World
{
    /// <summary>
    /// The catch tables: what a cast can pull out of a given body of water, and how the odds shift
    /// as the region gets more dangerous.
    ///
    /// THE MODEL, as agreed:
    ///   * one table per kind of water;
    ///   * every entry carries a RARITY rank (1 = commonest);
    ///   * an entry may carry a MINIMUM TIER -- below it the entry cannot appear at all;
    ///   * the higher the region's tier, the more weight the rarer entries get, so the same pond
    ///     that yields scrap at tier 0 yields something worth keeping at tier 6.
    ///
    /// Weight of an entry at a given tier:
    ///
    ///     base  = 100 / 2^(rarity-1)          -- 1:100, 2:50, 3:25, 4:12, 5:6, 6:3
    ///     scale = 1 + tier * rarity * 0.5     -- the rarer it is, the faster tier lifts it
    ///     weight= base * scale                -- or 0 when tier &lt; MinTier
    ///
    /// Worked example, salt water, tier 0 -> tier 8, as percentages of the whole table:
    ///
    ///     entry                 rarity  tier 0   tier 8
    ///     Scrap                    1     40%      23%
    ///     Glowfish Corpse          1     40%      23%
    ///     Madpole Corpse           2     20%      21%
    ///     Ghost Perch Corpse       3      --      15%
    ///     Memory Eater Corpse      4      --       9%
    ///     Urchin Belcher Corpse    5      --       6%
    ///     sunken cache             6      1%       3%
    ///
    /// So rarity rises with tier without the common entries ever vanishing, and the hard tier gates
    /// keep the good stuff out of the shallows entirely.
    ///
    /// REGION TIER comes from the zone the water is in: Zone.Tier (Zone.cs:231), which Qud fills from
    /// the blueprint's Tier or, failing that, the terrain's RegionTier tag (Zone.cs:405-412). Joppa's
    /// saltmarsh is RegionTier 0 (WorldTerrain.xml, TerrainSaltmarsh); rivers are 4 (TerrainWater);
    /// Lake Hinnom is 6.
    /// </summary>
    public static class A2Raine_FishingCatch
    {
        /// <summary>
        /// Blueprint sentinel meaning "a sunken cache". Resolved to Chest1..Chest7 by tier at roll
        /// time -- see Resolve. Those are vanilla furniture chests with premade loot
        /// (Furniture.xml:3542 onwards, Inventory Builder="InventoryChestJunk1".."7"), which is
        /// exactly the "open it like a chest on the ground, and it is gone afterwards" behaviour;
        /// being Furniture also means the player cannot walk off with the box itself.
        /// </summary>
        public const string CACHE = "$CACHE";

        public class Entry
        {
            public string Blueprint;
            public int Rarity;
            public int MinTier;

            /// <summary>
            /// Whether this is a fish rather than junk or a box. Only matters when the bite fails:
            /// a fish that was tempted and got away may be provoked into coming at you instead,
            /// and that is a thing only a fish does.
            /// </summary>
            public bool IsFish;

            public Entry(string Blueprint, int Rarity, int MinTier, bool IsFish)
            {
                this.Blueprint = Blueprint;
                this.Rarity = Rarity;
                this.MinTier = MinTier;
                this.IsFish = IsFish;
            }
        }

        /// <summary>
        /// Salt water -- the commonest water in Qud, and the first table built.
        ///
        /// The fish are exactly the five with corpses of their own -- five of the twelve BaseFish
        /// have one: Glowfish, Madpole, Ghost Perch, Memory Eater, Urchin Belcher. They
        /// are ranked by where vanilla puts them -- Glowfish and Madpole share the shallow
        /// Water_Creatures / Saltmarsh_Creatures tables, Ghost Perch is PlacementHint=Aquatic only,
        /// Memory Eater hangs around BaroqueRuins, and Urchin Belcher is Tier 5 and the only corpse
        /// in the game that butchers into Raw Fish Meat.
        ///
        /// Scrap leads because a bare hook drags up junk; it is vanilla (Items.xml:10737).
        /// </summary>
        private static readonly Entry[] SaltWater =
        {
            new Entry("Scrap",                  1, 0, false),
            new Entry("Glowfish Corpse",        1, 0, true),
            new Entry("Madpole Corpse",         2, 2, true),
            new Entry("Ghost Perch Corpse",     3, 2, true),
            new Entry("Memory Eater Corpse",    4, 4, true),
            new Entry("Urchin Belcher Corpse",  5, 5, true),
            new Entry(CACHE,                    6, 0, false)
        };

        /// <summary>
        /// Which table this water uses. Keyed on the blueprint of the liquid pool occupying the cell,
        /// the same key the pole and the summary already use. Unknown water falls back to salt --
        /// a modded pool still fishes rather than refusing.
        /// </summary>
        public static Entry[] TableFor(string PoolBlueprint)
        {
            return SaltWater;
        }

        /// <summary>Weight of one entry at one tier; zero means "cannot appear here at all".</summary>
        public static double WeightOf(Entry e, int Tier)
        {
            if (e == null || Tier < e.MinTier)
            {
                return 0.0;
            }
            double baseWeight = 100.0 / Math.Pow(2.0, e.Rarity - 1);
            double scale = 1.0 + Tier * e.Rarity * 0.5;
            return baseWeight * scale;
        }

        /// <summary>
        /// Rolls the table and returns the winning ENTRY, so the caller can see whether it was a fish
        /// (which decides whether a failed bite can provoke one) before resolving it to a concrete
        /// blueprint. Returns null only if the table is empty or every entry is gated out.
        /// </summary>
        public static Entry RollEntry(string PoolBlueprint, int Tier)
        {
            Entry[] table = TableFor(PoolBlueprint);
            if (table == null || table.Length == 0)
            {
                return null;
            }

            double total = 0.0;
            for (int i = 0; i < table.Length; i++)
            {
                total += WeightOf(table[i], Tier);
            }
            if (total <= 0.0)
            {
                return null;
            }

            // Stat.Random is the game's seeded roller -- the same one SifrahGame uses to decide
            // outcomes -- so fishing stays consistent with the rest of the run.
            double roll = Stat.Random(0, 1000000) / 1000000.0 * total;
            for (int j = 0; j < table.Length; j++)
            {
                double w = WeightOf(table[j], Tier);
                if (w <= 0.0)
                {
                    continue;
                }
                roll -= w;
                if (roll <= 0.0)
                {
                    return table[j];
                }
            }

            // Floating point can leave a sliver over; the last live entry takes it.
            for (int k = table.Length - 1; k >= 0; k--)
            {
                if (WeightOf(table[k], Tier) > 0.0)
                {
                    return table[k];
                }
            }
            return null;
        }

        /// <summary>
        /// Rolls the table and returns a concrete blueprint name, with a cache resolved to the chest
        /// that suits this region's tier.
        /// </summary>
        public static string Roll(string PoolBlueprint, int Tier)
        {
            Entry entry = RollEntry(PoolBlueprint, Tier);
            return (entry == null) ? null : Resolve(entry.Blueprint, Tier);
        }

        /// <summary>
        /// Turns the CACHE sentinel into the chest for this tier. Chest1..Chest7 exist in vanilla
        /// (Furniture.xml:3542-...), each with its own premade loot builder and matching Tier tag, so
        /// the cache a fisher pulls out of a tier-0 pond is junk and the one from a tier-6 lake is
        /// worth the wait.
        /// </summary>
        public static string Resolve(string Blueprint, int Tier)
        {
            if (Blueprint != CACHE)
            {
                return Blueprint;
            }
            int n = Tier;
            if (n < 1)
            {
                n = 1;
            }
            if (n > 7)
            {
                n = 7;
            }
            return "Chest" + n;
        }

        /// <summary>True when this blueprint is a cache, i.e. needs opening rather than carrying.</summary>
        public static bool IsCache(string Blueprint)
        {
            return Blueprint != null && Blueprint.StartsWith("Chest");
        }
    }
}
