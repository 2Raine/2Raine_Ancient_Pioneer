using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Units;

namespace XRL.World.Parts
{
    /// <summary>
    /// The inward eye: an activated ability that opens the link screen.
    ///
    /// WHY AN ABILITY AND NOT A SKILL TREE
    /// -----------------------------------
    /// A &lt;skill&gt; in XML puts a tree in the Skills screen that reads "already learned" the
    /// moment the part exists. These unlocks are not bought with skill points, so a tree is pure
    /// noise -- the same reason A2Raine_Toncihana_StormLinks is a plain part and no longer a
    /// BaseSkill. An activated ability just appears in the ability list and does nothing until
    /// invoked.
    ///
    /// Shape copied from Becoming's Evolve.cs:311-408 -- ability registered in Initialize, command
    /// taken through CommandEvent, options built with Popup.PickOption, unavailable ones dimmed,
    /// and the requirements re-checked after the pick so a stale screen cannot slip a purchase
    /// through.
    ///
    /// LOCKED ENTRIES ARE SHOWN, NOT HIDDEN
    /// ------------------------------------
    /// The screen opens at any charge level and lists everything not yet implanted, dimmed with the
    /// amount still needed. Nothing is spent -- reaching the cost is the whole gate.
    /// </summary>
    public class A2Raine_Toncihana_InwardEye : IPart
    {
        public static readonly string COMMAND_NAME = "CommandA2Raine_Toncihana_InwardEye";

        public Guid ActivatedAbilityID = Guid.Empty;

        /// <summary>
        /// Names and blurbs, in A2Raine_Toncihana_StormLinks.AbilityOrder. The names are repeated
        /// from the carriers' own AbilityName (StormCalling.cs) because a not-yet-unlocked ability
        /// has no carrier instance to ask.
        /// </summary>
        public static readonly string[] AbilityLabel = {
            "Thunder Lord's Decree",
            "Thunder Step",
            "Thunder-Fire",
        };

        public static readonly string[] AbilityBlurb = {
            "Melee strikes carry the storm.",
            "Walk the current to a place in sight.",
            "Set the air alight with the charge you hold.",
        };

        public override void Initialize()
        {
            ActivatedAbilityID = AddMyActivatedAbility("Inward Eye", COMMAND_NAME, "Skill");
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
                OpenInwardEye();
            }
            return base.HandleEvent(E);
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        private void OpenInwardEye()
        {
            GameObject who = ParentObject;
            if (who == null)
            {
                return;
            }

            int charge = A2Raine_Toncihana_StormLinks.Charge(who);
            string[] links = A2Raine_Toncihana_StormLinks.LinkOrder;
            string[] keys = A2Raine_Toncihana_StormLinks.AbilityOrder;

            List<int> shown = new List<int>();
            List<string> options = new List<string>();

            // Every un-implanted link costs the same: the price is for the NEXT unlock, not for a
            // particular link.
            int cost = A2Raine_Toncihana_StormLinks.NextUnlockCost(who);

            for (int i = 0; i < links.Length && i < keys.Length && i < AbilityLabel.Length; i++)
            {
                if (A2Raine_Toncihana_StormLinks.HasLink(who, links[i]))
                {
                    continue;
                }
                string label = AbilityLabel[i] + " - " + AbilityBlurb[i];
                if (charge >= cost)
                {
                    label = label + " ({{C|" + cost + " charge}})";
                }
                else
                {
                    label = "{{K|" + label + " - needs " + cost + ", you hold " + charge + "}}";
                }
                options.Add(label);
                shown.Add(i);
            }

            if (options.Count == 0)
            {
                Popup.Show("{{W|The inward eye finds nothing left unopened. Every link is already formed.}}");
                return;
            }

            int choice = Popup.PickOption(
                Title: "Inward Eye",
                Intro: "Spirit charge: " + charge
                    + ". Each link asks for more than the one before it.",
                Options: options,
                AllowEscape: true,
                RespectOptionNewlines: true);

            if (choice < 0 || choice >= shown.Count)
            {
                return;
            }

            int index = shown[choice];
            int need = A2Raine_Toncihana_StormLinks.NextUnlockCost(who);
            if (charge < need)
            {
                Popup.ShowFail("The storm in you has not gathered that deeply. This link asks "
                    + need + " charge, and you hold " + charge + ".");
                return;
            }

            GameObject implanted = new GameObjectCyberneticsUnit
            {
                Blueprint = links[index],
            }.Implant(who);

            if (implanted == null)
            {
                Popup.ShowFail("The link would not take. Check that the slot it wants is free.");
                return;
            }

            Popup.Show("{{W|" + AbilityLabel[index] + " opens.}}");
        }
    }
}
