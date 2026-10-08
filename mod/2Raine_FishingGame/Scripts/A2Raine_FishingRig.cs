using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// The pole's rigging: which bait and which float are currently loaded.
    ///
    /// WHERE THE TACKLE LIVES: in the pole's own Inventory part, not in a field on this part. That
    /// means one container to add, one list to read, and nothing to keep in sync -- the game already
    /// knows how to move an object between containers, and the tackle shows up in the pole's
    /// contents for free. The alternative (a GameObject field plus a hidden holder, the way
    /// EnergyCellSocket keeps its Cell at EnergyCellSocket.cs:83) is more code for no benefit here,
    /// since nothing needs to reach the tackle except this part.
    ///
    /// ONE VERB, TWO SLOTS. "rig the pole" opens a single list containing everything loadable plus an
    /// entry to clear whatever is already on. Which slot an item goes into is decided by its type
    /// (A2Raine_FishingBait -> bait slot, A2Raine_FishingFloat -> float slot), so the player never has
    /// to pick a slot explicitly and adding a third kind of tackle needs no new verb.
    /// </summary>
    [Serializable]
    public class A2Raine_FishingRig : IPart
    {
        public const string COMMAND = "RigFishingPole";

        /// <summary>
        /// Whether the loaded bait and float are shown in the pole's DISPLAY NAME -- the
        /// "battery in the device" look, e.g. "fishing pole [beginner bait][bright flasher]".
        ///
        /// This is the half that was missing first time round. EnergyCellSocket uses two flags for two
        /// different jobs and it matters which is which:
        ///   VisibleInDisplayName -> GetDisplayNameEvent, E.AddTag(...)   (EnergyCellSocket.cs:200-214)
        ///   VisibleInDescription -> GetShortDescriptionEvent, E.Postfix  (EnergyCellSocket.cs:216-224)
        /// The first is the "[chem cell]" suffix on the item's name; the second is a line underneath in
        /// the description body. The request was for the first.
        /// </summary>
        public bool VisibleInDisplayName = true;

        /// <summary>
        /// Kept for the same reason EnergyCellSocket has it, but the description no longer repeats the
        /// loaded names -- the display name already carries them, and saying it twice is noise.
        /// </summary>
        public bool VisibleInDescription;

        public override bool SameAs(IPart p)
        {
            return false;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            if (!base.WantEvent(ID, cascade)
                && ID != GetInventoryActionsEvent.ID
                && ID != GetShortDescriptionEvent.ID
                && ID != PooledEvent<GetDisplayNameEvent>.ID)
            {
                return ID == InventoryActionEvent.ID;
            }
            return true;
        }

        /// <summary>
        /// Appends the loaded tackle to the item's name, the way a powered item reads as
        /// "laser pistol [chem cell]".
        ///
        /// BOTH SLOTS GO INTO ONE TAG rather than two AddTag calls. AddTag takes an OrderAdjust for
        /// ordering several tags, and EnergyCellSocket passes -5 with only ever one tag to place, so
        /// nothing in the base game pins down what -5 versus -6 would actually do. One tag sidesteps
        /// the question entirely: bait first, then float, always.
        /// </summary>
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (VisibleInDisplayName && !E.Reference && E.Understood())
            {
                A2Raine_FishingBait bait = GetBait();
                A2Raine_FishingFloat flt = GetFloat();
                int baits = CountBait();

                string suffix = "";
                if (bait != null)
                {
                    suffix += "[" + bait.ParentObject.DisplayName + ((baits > 1) ? (" x" + baits) : "") + "]";
                }
                if (flt != null)
                {
                    suffix += "[" + flt.ParentObject.DisplayName + "]";
                }
                if (suffix.Length == 0)
                {
                    suffix = "[{{K|no bait}}][{{K|no float}}]";
                }
                E.AddTag("{{y|" + suffix + "}}", -5);
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (VisibleInDescription && E.Understood())
            {
                A2Raine_FishingBait bait = GetBait();
                A2Raine_FishingFloat flt = GetFloat();
                E.Postfix.Append("\n{{rules|bait: ")
                    .Append(bait == null ? "none" : bait.ParentObject.ShortDisplayNameStripped)
                    .Append("}}");
                E.Postfix.Append("\n{{rules|float: ")
                    .Append(flt == null ? "none" : flt.ParentObject.ShortDisplayNameStripped)
                    .Append("}}");
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            // Unconditional: the verb should be visible even if the Inventory part were somehow
            // missing, so that the failure is a sentence ("you have nothing to rig it with") rather
            // than a menu entry that silently never appears.
            E.AddAction(COMMAND, "rig the pole", COMMAND, null, 'r', FireOnActor: false, Default: 0);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == COMMAND && RigPole(E.Actor))
            {
                E.RequestInterfaceExit();
            }
            return base.HandleEvent(E);
        }

        // ------------------------------------------------------------------ what is loaded

        /// <summary>The bait on the hook, or null. Read by the cast to adjust wait and bite rate.</summary>
        public A2Raine_FishingBait GetBait()
        {
            return (A2Raine_FishingBait)FindLoaded(typeof(A2Raine_FishingBait));
        }

        /// <summary>How many baits are loaded -- they stack in the pole's inventory.</summary>
        public int CountBait()
        {
            return CountLoaded(typeof(A2Raine_FishingBait));
        }

        /// <summary>How many floats are loaded.</summary>
        public int CountFloat()
        {
            return CountLoaded(typeof(A2Raine_FishingFloat));
        }

        private int CountLoaded(Type Kind)
        {
            if (ParentObject == null || ParentObject.Inventory == null)
            {
                return 0;
            }
            int n = 0;
            foreach (GameObject obj in ParentObject.Inventory.GetObjects())
            {
                A2Raine_FishingTackle tackle = obj.GetPartDescendedFrom<A2Raine_FishingTackle>();
                if (tackle != null && Kind.IsInstanceOfType(tackle))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>The float on the line, or null.</summary>
        public A2Raine_FishingFloat GetFloat()
        {
            return (A2Raine_FishingFloat)FindLoaded(typeof(A2Raine_FishingFloat));
        }

        private A2Raine_FishingTackle FindLoaded(Type Kind)
        {
            if (ParentObject == null || ParentObject.Inventory == null)
            {
                return null;
            }
            foreach (GameObject obj in ParentObject.Inventory.GetObjects())
            {
                // GetPartDescendedFrom, NOT GetPart: GetPart compares with GetType() == typeof(T)
                // (GameObject.cs:9886-9897), an EXACT type match, so asking it for the abstract base
                // A2Raine_FishingTackle while the instance is an A2Raine_FishingBait always yields
                // null. That mistake is what made "rig the pole" report an empty pack.
                A2Raine_FishingTackle tackle = obj.GetPartDescendedFrom<A2Raine_FishingTackle>();
                if (tackle != null && Kind.IsInstanceOfType(tackle))
                {
                    return tackle;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ loading

        private bool RigPole(GameObject Actor)
        {
            if (Actor == null)
            {
                return false;
            }

            List<GameObject> offered = new List<GameObject>();
            List<string> labels = new List<string>();
            List<char> hotkeys = new List<char>();
            char hotkey = 'a';

            // Whatever is already on, offered as a way to take it off.
            A2Raine_FishingBait bait = GetBait();
            if (bait != null)
            {
                offered.Add(bait.ParentObject);
                labels.Add("remove the " + bait.ParentObject.DisplayName);
                hotkeys.Add(hotkey++);
            }
            A2Raine_FishingFloat flt = GetFloat();
            if (flt != null)
            {
                offered.Add(flt.ParentObject);
                labels.Add("remove the " + flt.ParentObject.DisplayName);
                hotkeys.Add(hotkey++);
            }

            // Anything in the pack that is tackle and is not this pole.
            foreach (GameObject obj in Actor.Inventory.GetObjects())
            {
                if (obj == ParentObject)
                {
                    continue;
                }
                // See the note in FindLoaded: GetPart<Base> would never match a derived instance.
                A2Raine_FishingTackle tackle = obj.GetPartDescendedFrom<A2Raine_FishingTackle>();
                if (tackle == null)
                {
                    continue;
                }
                offered.Add(obj);
                labels.Add(obj.DisplayName + "  " + Describe(tackle));
                hotkeys.Add(hotkey++);
            }

            if (offered.Count == 0)
            {
                Actor.Fail("You have nothing to rig the pole with.");
                return false;
            }

            int chosen = Popup.PickOption("How do you rig the pole?", null, "", "Sounds/UI/ui_notification",
                labels.ToArray(), hotkeys.ToArray(), null, null, null, null, null, 0, 60, 0, -1, AllowEscape: true);
            if (chosen < 0 || chosen >= offered.Count)
            {
                return false;
            }

            GameObject picked = offered[chosen];

            // Already loaded -> unload it back to the pack.
            if (picked == (bait == null ? null : bait.ParentObject) || picked == (flt == null ? null : flt.ParentObject))
            {
                Actor.ReceiveObject(picked);
                Actor.PlayWorldSound("sfx_interact_artifact_windDown");
                IComponent<GameObject>.AddPlayerMessage("You take the " + picked.DisplayName + " off the line.");
                return true;
            }

            // Load it. Adding to the pole's inventory pulls it out of the pack by itself.
            ParentObject.Inventory.AddObject(picked);
            Actor.PlayWorldSound("sfx_interact_artifact_windUp");
            IComponent<GameObject>.AddPlayerMessage("You rig the pole with " + picked.DisplayName + ".");
            return true;
        }

        /// <summary>One short line of what a piece of tackle actually does, for the picker list.</summary>
        private static string Describe(A2Raine_FishingTackle tackle)
        {
            List<string> bits = new List<string>();
            if (tackle.SpeedBonus != 0)
            {
                bits.Add((tackle.SpeedBonus / 1000f).ToString("0.#") + "s faster");
            }
            if (tackle.CatchBonus != 0)
            {
                bits.Add("+" + tackle.CatchBonus + "% bite");
            }
            if (tackle.TreasureBonus != 0)
            {
                bits.Add("+" + tackle.TreasureBonus + " treasure");
            }
            return (bits.Count == 0) ? "" : ("(" + string.Join(", ", bits.ToArray()) + ")");
        }
    }
}
