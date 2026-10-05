using System;
using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// A testing wish: put spirit stones straight into the player's inventory, so the
    /// eat -> charge -> open the inward eye loop can be exercised without hunting elementals.
    ///
    /// Usage:  wish spiritstone      (one stone)
    ///         wish spiritstone 5    (five stones)
    /// </summary>
    [HasWishCommand]
    public static class A2Raine_SpiritStoneWish
    {
        public const string STONE_BLUEPRINT = "2Raine_SpiritStone_Lightning";

        [WishCommand("spiritstone", null)]
        public static void WishSpiritStone(string Argument)
        {
            if (The.Player == null)
            {
                Popup.ShowFail("No player.");
                return;
            }

            int count = 1;
            if (!string.IsNullOrEmpty(Argument))
            {
                int parsed;
                if (int.TryParse(Argument.Trim(), out parsed) && parsed > 0)
                {
                    count = parsed;
                }
            }

            int made = 0;
            for (int i = 0; i < count; i++)
            {
                GameObject stone = GameObjectFactory.Factory.CreateObject(STONE_BLUEPRINT);
                if (stone == null)
                {
                    break;
                }
                if (The.Player.Inventory != null)
                {
                    The.Player.Inventory.AddObject(stone);
                }
                made++;
            }

            Popup.Show("{{W|" + made + " spirit stone" + ((made == 1) ? "" : "s") + " arrive.}}");
        }
    }
}
