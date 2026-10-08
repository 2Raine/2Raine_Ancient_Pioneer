using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// Devour: swallow every spirit stone you carry in one action.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// Vanilla's eat path (Food.cs:124-217) handles one item per action and prints a line for each,
    /// so working through a pile of stones is a lot of keystrokes for no decision-making. This does
    /// the same arithmetic once: sum the Amount of every A2Raine_SpiritStoneMeal carried, destroy
    /// them the way Food does (ParentObject.Destroy, Food.cs:215), add the total to the charge
    /// counter, and charge one action's energy (UseEnergy(1000, "Item Eat"), Food.cs:211).
    ///
    /// WHY IT WALKS CONTAINERS BY HAND
    /// -------------------------------
    /// Qud's GameObject.GetInventory* family has no recursive form -- every overload stops at the
    /// top level (GameObject.cs:5367-5504). Two earlier versions used those and both ate exactly
    /// one stone out of a pile, because stones sitting inside a carried container are invisible to
    /// all of them. CollectStones therefore recurses into anything that has an Inventory of its
    /// own, and takes equipped stones too (a spirit stone inherits Floating Glowsphere, so it can
    /// occupy a body's Floating Nearby).
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
            ActivatedAbilityID = AddMyActivatedAbility("Devour", COMMAND_NAME, "Skill",
                "Absorb every spirit stone you carry, all at once.");
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

            // Collect first, destroy after: never mutate the world while walking the list we took
            // from it.
            List<GameObject> toEat = new List<GameObject>();
            int total = 0;
            CollectStones(who, toEat, ref total);

            if (toEat.Count == 0)
            {
                Popup.ShowFail("You are carrying no spirit stones.");
                return;
            }

            UnityEngine.Debug.Log("[Toncihana] devour: found " + toEat.Count + " stone(s), total "
                + total + " charge.");

            foreach (GameObject stone in toEat)
            {
                stone.Destroy();
            }

            int count = toEat.Count;

            who.ModIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY, total);
            UnityEngine.Debug.Log("[Toncihana] devoured " + count + " spirit stone(s); +"
                + total + " charge, now " + who.GetIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY));

            who.UseEnergy(1000, "Item Eat");
            Popup.Show("{{W|You swallow " + count + " spirit stone"
                + ((count == 1) ? "" : "s")
                + " at once. Something in you takes all of it and keeps it.}}");
        }

        /// <summary>
        /// Gathers every spirit stone the creature carries -- equipped, loose in the pack, or
        /// nested any number of containers deep -- because none of Qud's own inventory helpers
        /// descends into containers.
        /// </summary>
        private static void CollectStones(GameObject Host, List<GameObject> Into, ref int Total)
        {
            List<GameObject> carried;

            try
            {
                carried = Host.GetInventoryAndEquipment();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] devour: could not read the contents of "
                    + Host.Blueprint + ": " + e);
                return;
            }

            if (carried == null)
            {
                return;
            }

            foreach (GameObject item in carried)
            {
                if (item == null)
                {
                    continue;
                }

                A2Raine_SpiritStoneMeal meal = item.GetPart<A2Raine_SpiritStoneMeal>();
                if (meal != null)
                {
                    Into.Add(item);
                    Total += meal.Amount;
                    continue;
                }

                // Not a stone -- but if it is a container, its contents count as carried too.
                if (item.HasPart<Inventory>())
                {
                    CollectStones(item, Into, ref Total);
                }
            }
        }
    }
}
