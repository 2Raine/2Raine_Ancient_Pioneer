using System;
using System.Collections.Generic;

namespace XRL.World
{
    /// <summary>
    /// What the bait fabricator can make, and out of what.
    ///
    /// KEPT IN C# RATHER THAN XML, by decision: the list is read three times over -- once to show a
    /// name and its ingredients, once to check whether the player has them, and once to spend them --
    /// and splitting that across an XML table plus code would mean three places to keep in step. A
    /// plain array is also far easier to tune, which is mostly what this will be used for.
    ///
    /// INGREDIENTS CAN BE NAMED OR TAGGED. "Any corpse" cannot be written as a blueprint name, so an
    /// Ingredient may match on a tag instead: the vanilla Corpse base carries &lt;tag Name="Corpse" /&gt;
    /// (Items.xml:8566) and every corpse inherits it.
    /// </summary>
    public static class A2Raine_FishingRecipes
    {
        /// <summary>One thing a recipe consumes: either a specific blueprint or anything with a tag.</summary>
        public class Ingredient
        {
            /// <summary>Blueprint name, or null when this matches by Tag.</summary>
            public string Blueprint;

            /// <summary>Tag to match when Blueprint is null, e.g. "Corpse" for any corpse.</summary>
            public string Tag;

            public int Count;

            /// <summary>What to show for it in the ingredient line.</summary>
            public string Label;

            public static Ingredient Of(string Blueprint, int Count, string Label)
            {
                return new Ingredient { Blueprint = Blueprint, Count = Count, Label = Label };
            }

            public static Ingredient Tagged(string Tag, int Count, string Label)
            {
                return new Ingredient { Tag = Tag, Count = Count, Label = Label };
            }
        }

        /// <summary>
        /// One craftable item.
        ///
        /// Category groups the first menu ("Make bait" / "Make floats" / "Make poles"); Name and
        /// Effect become the two-line entry in the second menu, which is the shape the base game uses
        /// for cooking recipes (CookingRecipe.GetCampfireDescription, CookingRecipe.cs:172).
        /// </summary>
        public class Recipe
        {
            public string Category;
            public string Output;
            public string Name;
            public string Effect;
            public Ingredient[] Needs;

            /// <summary>
            /// How many come out of one run. 1 unless stated; the baits make five at a time, because a
            /// single bait is spent on a cast (or a few) and one-per-craft would mean standing at the
            /// machine far more often than fishing.
            /// </summary>
            public int Amount;

            public Recipe(string Category, string Output, string Name, string Effect, Ingredient[] Needs, int Amount = 1)
            {
                this.Category = Category;
                this.Output = Output;
                this.Name = Name;
                this.Effect = Effect;
                this.Needs = Needs;
                this.Amount = Amount;
            }
        }

        public const string CAT_BAIT = "Bait";
        public const string CAT_FLOAT = "Float";
        public const string CAT_POLE = "Pole";
        public const string CAT_FISH = "Fish";
        public const string CAT_OTHER = "Other";

        /// <summary>
        /// The tables. Ingredient counts and the advanced-bait / float recipes are first drafts -- the
        /// beginner bait and the pole recipe are as specified; the rest are proposals.
        /// </summary>
        private static readonly Recipe[] All =
        {
            // ---- bait -------------------------------------------------------------------------
            new Recipe(CAT_BAIT, "2Raine_BeginnerBait", "beginner bait",
                "Bites about 2 seconds sooner.",
                new Ingredient[]
                {
                    Ingredient.Of("Vinewafer Sheaf", 2, "vinewafer sheaf"),
                    Ingredient.Tagged("Corpse", 1, "any corpse")
                }, 5),

            new Recipe(CAT_BAIT, "2Raine_AdvancedBait", "advanced bait",
                "Bites much sooner, and bites more often. (materials are a first proposal)",
                new Ingredient[]
                {
                    Ingredient.Of("Glowfish Corpse", 1, "glowfish corpse"),
                    Ingredient.Of("Voider Gland", 1, "voider gland")
                }, 5),

            // ---- floats -----------------------------------------------------------------------
            new Recipe(CAT_FLOAT, "2Raine_BrightFlasher", "bright flasher",
                "Raises the chance something takes the hook. (materials are a first proposal)",
                new Ingredient[]
                {
                    Ingredient.Of("Copper Nugget", 2, "copper nugget"),
                    Ingredient.Of("Vinewafer", 1, "vinewafer")
                }),

            new Recipe(CAT_FLOAT, "2Raine_TreasureHunterFloat", "treasure hunter's float",
                "Favours a sunken cache, at the cost of a slower bite. (materials are a first proposal)",
                new Ingredient[]
                {
                    Ingredient.Of("Copper Nugget", 3, "copper nugget"),
                    Ingredient.Of("Glowfish Corpse", 1, "glowfish corpse")
                }),

            // ---- dressing fish ----------------------------------------------------------------
            //
            // THE BASE GAME HAS NO BUTCHERY PATH FOR FISH. Raw Fish Meat is produced by exactly one
            // thing in the whole of vanilla -- Urchin Belcher Corpse, whose Butcherable part sits at
            // Creatures.xml:6539 and nowhere else -- so every other fish in Qud can only be eaten as a
            // corpse (and Corpse carries IllOnEat, so that makes you sick).
            //
            // Rather than bolt Butcherable onto the shared Fish Corpse blueprint, which would change
            // vanilla data for every player and every mod, the bench does it. Costs nothing in skills,
            // and lets each fish yield its own meat. The urchin keeps its vanilla butchery as well;
            // both routes exist now.
            new Recipe(CAT_FISH, "2Raine_RawGlowfishMeat", "dress a glowfish",
                "Yields raw glowfish meat.", new Ingredient[]
                {
                    Ingredient.Of("Glowfish Corpse", 1, "glowfish corpse")
                }, 2),

            new Recipe(CAT_FISH, "2Raine_RawMadpoleMeat", "dress a madpole",
                "Yields raw madpole meat.", new Ingredient[]
                {
                    Ingredient.Of("Madpole Corpse", 1, "madpole corpse")
                }, 2),

            new Recipe(CAT_FISH, "2Raine_RawGhostPerchMeat", "dress a ghost perch",
                "Yields raw ghost perch meat.", new Ingredient[]
                {
                    Ingredient.Of("Ghost Perch Corpse", 1, "ghost perch corpse")
                }, 2),

            new Recipe(CAT_FISH, "2Raine_RawMemoryEaterMeat", "dress a memory eater",
                "Yields raw memory eater meat.", new Ingredient[]
                {
                    Ingredient.Of("Memory Eater Corpse", 1, "memory eater corpse")
                }, 2),

            new Recipe(CAT_FISH, "2Raine_RawUrchinMeat", "dress an urchin belcher",
                "Yields raw belcher meat.", new Ingredient[]
                {
                    Ingredient.Of("Urchin Belcher Corpse", 1, "urchin belcher corpse")
                }, 2),

            // ---- pole -------------------------------------------------------------------------
            new Recipe(CAT_POLE, "2Raine_FishingPole", "beginner's pole",
                "A plain pole. Bait and float attach to it.",
                new Ingredient[]
                {
                    Ingredient.Of("Vinewafer", 2, "vinewafer"),
                    Ingredient.Of("Copper Nugget", 1, "copper nugget")
                })
        };

        public static Recipe[] AllRecipes()
        {
            return All;
        }

        /// <summary>
        /// The categories to offer in the first menu, in a fixed order, skipping any that would be
        /// empty. Order is fixed rather than derived so the menu does not reshuffle as tables change.
        /// </summary>
        public static List<string> Categories()
        {
            string[] order = { CAT_BAIT, CAT_FISH, CAT_FLOAT, CAT_POLE, CAT_OTHER };
            List<string> result = new List<string>();
            for (int i = 0; i < order.Length; i++)
            {
                for (int j = 0; j < All.Length; j++)
                {
                    if (All[j].Category == order[i])
                    {
                        result.Add(order[i]);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>Human-facing label for a category, used as the menu entry.</summary>
        public static string CategoryLabel(string Category)
        {
            if (Category == CAT_BAIT)
            {
                return "make bait";
            }
            if (Category == CAT_FLOAT)
            {
                return "make floats";
            }
            if (Category == CAT_POLE)
            {
                return "make a pole";
            }
            if (Category == CAT_FISH)
            {
                return "dress a fish";
            }
            return "make something else";
        }

        public static List<Recipe> InCategory(string Category)
        {
            List<Recipe> result = new List<Recipe>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Category == Category)
                {
                    result.Add(All[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// One entry of the recipe menu: three lines, colour-coded by whether the fisher can afford
        /// it. The base game does the same kind of thing for cooking recipes
        /// (CookingRecipe.GetCampfireDescription, CookingRecipe.cs:172), and the multi-line shape is
        /// what RespectOptionNewlines exists for.
        ///
        /// AFFORDABLE IN WHITE, SHORT IN RED. The base game's cooking menu hides recipes it cannot pay
        /// for and just counts them; showing all of them with the shortfall in red is more useful here,
        /// because the whole point of the machine is to tell you what to go and gather.
        /// </summary>
        public static string Describe(Recipe R, bool Enough)
        {
            string name = Enough ? ("{{W|" + R.Name + "}}") : ("{{K|" + R.Name + "}}");
            string needs = Enough ? ("{{y|" + IngredientLine(R) + "}}") : ("{{R|" + IngredientLine(R) + "}}");
            string effect = Enough ? ("{{w|" + R.Effect + "}}") : ("{{K|" + R.Effect + "}}");
            return name + "\n  needs: " + needs + "\n  " + effect + "\n";
        }

        public static string IngredientLine(Recipe R)
        {
            string s = "";
            for (int i = 0; i < R.Needs.Length; i++)
            {
                if (s != "")
                {
                    s += ", ";
                }
                s += R.Needs[i].Label;
                if (R.Needs[i].Count > 1)
                {
                    s += " x" + R.Needs[i].Count;
                }
            }
            return s;
        }

        // ------------------------------------------------------------------ matching

        /// <summary>Does this object satisfy the ingredient? Matches a tag when one is given, else the blueprint.</summary>
        public static bool Matches(Ingredient Need, GameObject Obj)
        {
            if (Need == null || Obj == null)
            {
                return false;
            }
            if (!Need.Blueprint.IsNullOrEmpty())
            {
                return Obj.Blueprint == Need.Blueprint;
            }
            return !Need.Tag.IsNullOrEmpty() && Obj.HasTag(Need.Tag);
        }

        /// <summary>Everything in the actor's pack that satisfies the ingredient, in inventory order.</summary>
        public static List<GameObject> FindMatching(GameObject Actor, Ingredient Need)
        {
            List<GameObject> found = new List<GameObject>();
            if (Actor == null || Actor.Inventory == null || Need == null)
            {
                return found;
            }
            foreach (GameObject obj in Actor.Inventory.GetObjects())
            {
                if (Matches(Need, obj))
                {
                    found.Add(obj);
                }
            }
            return found;
        }

        /// <summary>
        /// Whether the actor carries everything the recipe needs.
        ///
        /// COUNTS ITEMS, NOT PILES. A stack of five vinewafers is ONE GameObject, so counting the
        /// list would call two vinewafers short for a recipe needing two whenever they happened to be
        /// stacked -- and the ingredients here are vinewafers and corpses, which stack (the Preservable
        /// base carries tag AlwaysStack, Foods.xml:49). GameObject.Count is the pile size (GameObject.cs:617),
        /// summed the way the base game sums it in RandomAltarBaetyl.cs:714.
        /// </summary>
        public static bool HasIngredients(GameObject Actor, Recipe R)
        {
            if (R == null)
            {
                return false;
            }
            for (int i = 0; i < R.Needs.Length; i++)
            {
                if (TotalMatching(Actor, R.Needs[i]) < R.Needs[i].Count)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// How many individual items the actor holds that satisfy the ingredient -- a pile of five
        /// counts as five. This is the number to compare against Ingredient.Count; FindMatching's own
        /// Count is only how many piles there are.
        /// </summary>
        public static int TotalMatching(GameObject Actor, Ingredient Need)
        {
            int total = 0;
            List<GameObject> piles = FindMatching(Actor, Need);
            for (int i = 0; i < piles.Count; i++)
            {
                total += piles[i].Count;
            }
            return total;
        }
    }
}
