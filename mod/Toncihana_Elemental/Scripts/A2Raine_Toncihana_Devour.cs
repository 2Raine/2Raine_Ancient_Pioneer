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
    /// WHY A PILE IS ONE OBJECT
    /// ------------------------
    /// A spirit stone stacks, so one GameObject can be an entire pile. Its quantity is
    /// GameObject.Count (XRL/World/GameObject.cs:617 -- the Stacker's Number, or 1 when it has no
    /// Stacker), and the charge owed is Amount x Count. Charging Amount alone was why devouring a
    /// pile paid out a single point and emptied the whole stack.
    ///
    /// Stones are taken from GetInventoryAndEquipment, so both loose ones and ones sitting in an
    /// equipment slot count (a spirit stone inherits Floating Glowsphere, so it can occupy a
    /// body's Floating Nearby).
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
            int stones = 0;
            CollectStones(who, toEat, ref total, ref stones);

            if (toEat.Count == 0)
            {
                Popup.ShowFail("You are carrying no spirit stones.");
                return;
            }

            UnityEngine.Debug.Log("[Toncihana] devour: " + stones + " stone(s) in "
                + toEat.Count + " stack(s), total " + total + " charge.");

            foreach (GameObject stone in toEat)
            {
                TakeWholeStack(stone);
            }

            int count = stones;

            who.ModIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY, total);
            UnityEngine.Debug.Log("[Toncihana] devoured " + count + " spirit stone(s); +"
                + total + " charge, now " + who.GetIntProperty(A2Raine_SpiritStoneMeal.CHARGE_PROPERTY));

            who.UseEnergy(1000, "Item Eat");
            Popup.Show("{{W|You swallow " + count + " spirit stone"
                + ((count == 1) ? "" : "s")
                + " at once. Something in you takes all of it and keeps it.}}");
        }

        /// <summary>
        /// Destroys an ENTIRE stack, not just one item of it.
        ///
        /// GameObject.Destroy does not know about stacks: destroying a pile of five takes one and
        /// leaves four behind, which is exactly the "devour again and eat one less each time" bug.
        /// Stacker.RemoveOne is the supported way down -- it decrements the pile and hands back the
        /// one it took, or hands back the object itself when only one remains
        /// (Stacker.cs:62-80) -- so this walks the pile and destroys each part.
        /// </summary>
        private static void TakeWholeStack(GameObject Stack)
        {
            if (Stack == null)
            {
                return;
            }

            // Bounded so a misbehaving RemoveOne can never spin forever.
            for (int guard = 0; guard < 100000; guard++)
            {
                if (Stack.Count <= 0)
                {
                    return;
                }

                GameObject one = Stack.RemoveOne();
                if (one == null)
                {
                    return;
                }

                one.Destroy();

                // With a single item left, RemoveOne returns the object itself, which we just
                // destroyed -- the pile is gone.
                if (one == Stack)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Gathers the spirit stones the creature carries -- loose in the pack, or equipped (a
        /// spirit stone inherits Floating Glowsphere, so it can occupy a body's Floating Nearby).
        ///
        /// One GameObject may be a whole pile: the stone stacks, so its quantity is
        /// GameObject.Count (XRL/World/GameObject.cs:617 -- Stacker.Number, or 1 with no Stacker),
        /// and the charge owed is Amount x that, not just Amount.
        /// </summary>
        private static void CollectStones(GameObject Host, List<GameObject> Into, ref int Total,
            ref int Stones)
        {
            List<GameObject> carried;

            try
            {
                carried = Host.GetInventoryAndEquipment();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] devour: could not read what "
                    + Host.Blueprint + " carries: " + e);
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
                    int stack = item.Count;
                    if (stack < 1)
                    {
                        stack = 1;
                    }

                    Into.Add(item);
                    Total += meal.Amount * stack;
                    Stones += stack;
                }
            }
        }
    }
}
