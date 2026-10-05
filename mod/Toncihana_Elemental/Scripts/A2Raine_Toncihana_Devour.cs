using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// Devour: swallow every spirit stone in the pack in one action.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// Vanilla's eat path (Food.cs:124-217) handles one item per action and prints a line for each,
    /// so working through a pile of stones is a lot of keystrokes for no decision-making. This does
    /// the same arithmetic in one go: sum the Amount of every A2Raine_SpiritStoneMeal in the
    /// inventory, destroy them the way Food does (ParentObject.Destroy, Food.cs:215), add the total
    /// to the charge counter, and charge the same single action's energy Food would
    /// (UseEnergy(1000, "Item Eat"), Food.cs:211).
    ///
    /// WHAT IT DELIBERATELY SKIPS
    /// --------------------------
    /// It does not run Food's secondary effects -- satiation (Food.cs:169-176), Thirst, Healing,
    /// the illness roll. For spirit stones those are all no-ops already: Satiation is "Snack" but
    /// the stone's Healing/Thirst are 0 and IllOnEat is unset. If a future stone carries a real
    /// food effect, revisit this rather than assuming it applies.
    /// </summary>
    public class A2Raine_Toncihana_Devour : IPart
    {
        public static readonly string COMMAND_NAME = "CommandA2Raine_Toncihana_Devour";

        public Guid ActivatedAbilityID = Guid.Empty;

        public override void Initialize()
        {
            ActivatedAbilityID = AddMyActivatedAbility("Devour", COMMAND_NAME, "Skill");
            base.Initialize();
        }

        public override void Remove()
        {
            RemoveMyActivatedAbility(ref ActivatedAbilityID);
            base.Remove();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                DevourStones();
            }
            return base.HandleEvent(E);
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        private void DevourStones()
        {
            GameObject who = ParentObject;
            if (who == null)
            {
                return;
            }

            // Same gate Food uses before it will let anything be eaten (Food.cs:132-137).
            if (who.GetPart<Stomach>() == null)
            {
                Popup.ShowFail("You are unable to consume food.");
                return;
            }

            List<GameObject> stones = who.GetInventoryDirect(
                (GameObject go) => go.HasPart<A2Raine_SpiritStoneMeal>());

            if (stones == null || stones.Count == 0)
            {
                Popup.ShowFail("You are carrying no spirit stones.");
                return;
            }

            int count = 0;
            int total = 0;
            foreach (GameObject stone in stones)
            {
                A2Raine_SpiritStoneMeal meal = stone.GetPart<A2Raine_SpiritStoneMeal>();
                if (meal == null)
                {
                    continue;
                }
                total += meal.Amount;
                count++;
                stone.Destroy();
            }

            if (count == 0)
            {
                Popup.ShowFail("You are carrying no spirit stones.");
                return;
            }

            who.ModIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY, total);
            UnityEngine.Debug.Log("[Toncihana] devoured " + count + " spirit stone(s); +"
                + total + " charge, now " + who.GetIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY));

            who.UseEnergy(1000, "Item Eat");
            Popup.Show("{{W|You swallow " + count + " spirit stone"
                + ((count == 1) ? "" : "s")
                + " at once. Something in you takes all of it and keeps it.}}");
        }
    }
}
