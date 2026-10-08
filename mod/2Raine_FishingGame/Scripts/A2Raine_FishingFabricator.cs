using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// The bait fabricator: the furniture you walk up to and craft fishing gear at.
    ///
    /// SHAPE OF THE INTERACTION, two menus deep:
    ///
    ///   use the fabricator  ->  "make bait" / "make floats" / "make a pole"      (Popup.PickOption)
    ///                       ->  the recipes in that family, each showing its       (Popup.PickOption,
    ///                           needs and effect on three lines                    RespectOptionNewlines)
    ///                       ->  spend the materials, hand over the item
    ///
    /// Both menus are Popup calls. The base game's cooking does exactly this -- Campfire.CookFromRecipe
    /// (XRL/World/Parts/Campfire.cs:1173 and :1191) is two PickOption calls, the second confirming, and
    /// it passes RespectOptionNewlines: true so a recipe can occupy several lines. Nothing here draws
    /// its own screen.
    ///
    /// CHOOSING WHICH ITEM PAYS. An ingredient may match by tag -- "any corpse" is Corpse (Items.xml:8566)
    /// -- and a pack can easily hold several corpses of different worth. Spending an arbitrary one would
    /// quietly eat the best fish in the bag, so when a pack holds MORE matches than the recipe needs,
    /// the player picks which. When it holds exactly what is needed, it just takes them.
    /// </summary>
    [Serializable]
    public class A2Raine_FishingFabricator : IPart
    {
        public const string COMMAND = "UseBaitFabricator";

        public override bool SameAs(IPart p)
        {
            return false;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            if (!base.WantEvent(ID, cascade)
                && ID != GetInventoryActionsEvent.ID
                && ID != CanSmartUseEvent.ID
                && ID != CommandSmartUseEvent.ID)
            {
                return ID == InventoryActionEvent.ID;
            }
            return true;
        }

        /// <summary>
        /// Declares that this object can be used with the Smart Use key -- which is what the player
        /// presses SPACE for when standing next to a piece of furniture.
        ///
        /// WITHOUT THIS PAIR THE MACHINE IS MOUSE-ONLY. GetInventoryActionsEvent above is the item
        /// menu, and right-clicking reaches it, but SPACE goes through CanSmartUseEvent /
        /// CommandSmartUseEvent and never touches that menu. Campfire is the model for a piece of
        /// furniture used in place: it answers false here and does its work in CommandSmartUseEvent
        /// (Campfire.cs:89-99). Container answers false only for a player and the same otherwise.
        ///
        /// Returning false reads backwards but is correct: the handler means "I have handled it, do not
        /// look further", which is how an object advertises itself as usable.
        /// </summary>
        public override bool HandleEvent(CanSmartUseEvent E)
        {
            return false;
        }

        public override bool HandleEvent(CommandSmartUseEvent E)
        {
            if (UseTheMachine(E.Actor))
            {
                E.Actor.UseEnergy(1000, "Bait Fabricator");
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            E.AddAction(COMMAND, "use the bait fabricator", COMMAND, null, 'u', FireOnActor: false, Default: 0);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == COMMAND && UseTheMachine(E.Actor))
            {
                E.Actor.UseEnergy(1000, "Bait Fabricator");
                E.RequestInterfaceExit();
            }
            return base.HandleEvent(E);
        }

        // ------------------------------------------------------------------ the two menus

        private bool UseTheMachine(GameObject Actor)
        {
            if (Actor == null)
            {
                return false;
            }

            // ---- menu 1: which family -------------------------------------------------------
            List<string> families = A2Raine_FishingRecipes.Categories();
            if (families.Count == 0)
            {
                Actor.Fail("The fabricator's racks are bare.");
                return false;
            }

            string[] familyLabels = new string[families.Count];
            for (int i = 0; i < families.Count; i++)
            {
                familyLabels[i] = A2Raine_FishingRecipes.CategoryLabel(families[i]);
            }

            int family = Popup.PickOption("What do you want to make?", null, "", "Sounds/UI/ui_notification",
                familyLabels, null, null, null, null, null, null, 0, 60, 0, -1, AllowEscape: true);
            if (family < 0 || family >= families.Count)
            {
                return false;
            }

            // ---- menu 2: which recipe -------------------------------------------------------
            //
            // EVERY recipe in the family is listed, affordable or not. The base game's cooking menu
            // instead hides the ones it cannot pay for and reports only a count (Campfire.cs:1162-1166),
            // but here seeing what is still missing is the point: the machine doubles as a shopping list.
            // Describe colours the shortfall in red.
            List<A2Raine_FishingRecipes.Recipe> recipes = A2Raine_FishingRecipes.InCategory(families[family]);
            if (recipes.Count == 0)
            {
                Actor.Fail("The fabricator has nothing to make in that line.");
                return false;
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < recipes.Count; i++)
            {
                labels.Add(A2Raine_FishingRecipes.Describe(recipes[i],
                    A2Raine_FishingRecipes.HasIngredients(Actor, recipes[i])));
            }

            int choice = Popup.PickOption("Choose what to make", null, Popup.SPACING_DARK_LINE.Replace('=', '÷'),
                "Sounds/UI/ui_notification", labels.ToArray(), null, null, null, null, null, null,
                1, 72, 0, -1, AllowEscape: true, RespectOptionNewlines: true);
            if (choice < 0 || choice >= recipes.Count)
            {
                return false;
            }

            return Make(Actor, recipes[choice]);
        }

        // ------------------------------------------------------------------ spending and making

        /// <summary>
        /// Pays for the recipe and hands the item over. Every ingredient is settled BEFORE anything is
        /// spent, so a recipe can never half-spend: an earlier ingredient is not consumed until the
        /// later ones have been chosen too.
        /// </summary>
        private bool Make(GameObject Actor, A2Raine_FishingRecipes.Recipe R)
        {
            if (R == null)
            {
                return false;
            }

            // ---- agree on exactly which objects pay -------------------------------------------
            List<GameObject> toSpend = new List<GameObject>();
            for (int i = 0; i < R.Needs.Length; i++)
            {
                A2Raine_FishingRecipes.Ingredient need = R.Needs[i];
                List<GameObject> candidates = A2Raine_FishingRecipes.FindMatching(Actor, need);
                int held = A2Raine_FishingRecipes.TotalMatching(Actor, need);

                // Piles, not items: three stacks of one vinewafer is three vinewafers.
                if (held < need.Count)
                {
                    Actor.Fail("You need " + need.Count + " " + need.Label + ".");
                    return false;
                }

                if (held == need.Count)
                {
                    // Exactly enough -- no choice to make. toSpend may hold the same object more than
                    // once, because SpendOne peels a single item off a pile each time it is called.
                    for (int c = 0; c < candidates.Count; c++)
                    {
                        for (int n = 0; n < candidates[c].Count; n++)
                        {
                            toSpend.Add(candidates[c]);
                        }
                    }
                    continue;
                }

                // More than enough. Taken from the front of the inventory, no questions asked.
                //
                // THERE USED TO BE A PICKER HERE, for the "any corpse" case, and it was removed by
                // request in favour of "if the pack has enough, just spend it". The trade is worth
                // stating plainly: if the pack holds both a junk fish and the best fish you own, this
                // takes whichever the inventory lists first. Spending is by pile and by item -- a pile
                // of three asked for two contributes two entries, and SpendOne peels one item per entry.
                int taken = 0;
                for (int c = 0; c < candidates.Count && taken < need.Count; c++)
                {
                    for (int n = 0; n < candidates[c].Count && taken < need.Count; n++)
                    {
                        toSpend.Add(candidates[c]);
                        taken++;
                    }
                }
            }

            // ---- now spend -------------------------------------------------------------------
            for (int i = 0; i < toSpend.Count; i++)
            {
                SpendOne(Actor, toSpend[i]);
            }

            GameObject made = null;
            for (int n = 0; n < R.Amount; n++)
            {
                GameObject one = GameObject.Create(R.Output);
                if (one == null)
                {
                    continue;
                }
                Actor.ReceiveObject(one);
                made = one;
            }
            if (made == null)
            {
                Actor.Fail("Something goes wrong at the forge and nothing comes out.");
                return true;
            }
            Actor.PlayWorldSound("sfx_interact_artifact_windUp");
            IComponent<GameObject>.AddPlayerMessage("The fabricator works, and "
                + ((R.Amount > 1) ? (R.Amount + " ") : "") + made.DisplayName + " is done.");
            return true;
        }

        /// <summary>
        /// Takes one of a stack. SplitStack(1) is how the base game peels a single item off a stack
        /// (TinkeringScreen.cs:678), and it answers null when there was nothing to split -- that is,
        /// when the object was already a single item, in which case the object itself is the one to
        /// remove.
        /// </summary>
        private static void SpendOne(GameObject Actor, GameObject Obj)
        {
            if (Obj == null)
            {
                return;
            }
            GameObject one = Obj.SplitStack(1, Actor);
            if (one == null)
            {
                one = Obj;
            }
            UnityEngine.Debug.Log("[Fishing] fabricator spent " + (one.Blueprint ?? "?"));
            one.Destroy();
        }
    }
}
