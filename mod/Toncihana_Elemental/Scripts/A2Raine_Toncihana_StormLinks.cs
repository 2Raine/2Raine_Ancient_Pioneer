using System;
using XRL;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts.Skill;

namespace XRL.World.Parts
{
    /// <summary>
    /// The storm abilities' activated-ability handles, plus the one table that says which link
    /// opens which ability and what it costs.
    ///
    /// WHY THIS IS A PLAIN PART AND NO LONGER THE SKILL
    /// -----------------------------------------------
    /// These handles used to live on A2Raine_Toncihana_StormCalling, a BaseSkill. A skill part is
    /// exactly what makes a skill tree appear in the Skills screen, so the tree sat there reading
    /// "already learned" while the abilities were withheld. The abilities are meant to be unlocked
    /// by implanting a link, not bought with skill points, so the tree was pure noise.
    ///
    /// Registration never needed a BaseSkill: AddMyActivatedAbility and RemoveMyActivatedAbility are
    /// declared on IComponent (qud_src/XRL/World/IComponent.cs:3902), and vanilla registers
    /// abilities from plain parts (Digging, Cloneling, LongBladesCore).
    ///
    /// THE UNLOCK STATE IS NOT STORED -- IT IS DERIVED
    /// ----------------------------------------------
    /// "Which abilities does this character have" is answered by "which links are implanted", read
    /// fresh from the body each time. One source of truth, so the inward-eye screen, the links and
    /// the repair path cannot disagree.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_StormLinks : IPart
    {
        // Handles. Same five, same order as the old skill used; field order is append-only.
        public Guid DecreeAbilityID = Guid.Empty;
        public Guid ThunderFireAbilityID = Guid.Empty;
        public Guid ThunderStepAbilityID = Guid.Empty;
        public Guid LightningSnakeAbilityID = Guid.Empty;
        public Guid ThunderBreathAbilityID = Guid.Empty;

        /// <summary>Where the eat-a-spirit-stone counter lives. A2Raine_SpiritStoneMeal writes it.</summary>
        public const string CHARGE_PROPERTY = "2Raine_SpiritCharge";

        public const int FirstUnlockCost = 5;
        public const int UnlockCostStep = 10;

        /// <summary>
        /// The unlock rules, in full: link implant -> ability key, cost climbing by UnlockCostStep
        /// from FirstUnlockCost. Editing these two arrays IS "changing the unlock rules" -- the
        /// inward-eye screen and the links both read them, so there is one table and not three.
        /// </summary>
        public static readonly string[] LinkOrder = {
            "2Raine_Toncihana_Link_Hands",
            "2Raine_Toncihana_Link_Feet",
            "2Raine_Toncihana_Link_Arm",
        };

        public static readonly string[] AbilityOrder = {
            "Decree",
            "ThunderStep",
            "ThunderFire",
        };

        public static int CostOf(int Index)
        {
            return Index < 0 ? 0 : FirstUnlockCost + Index * UnlockCostStep;
        }

        public static int CostOfLink(string Blueprint)
        {
            if (Blueprint == null)
            {
                return 0;
            }
            for (int i = 0; i < LinkOrder.Length; i++)
            {
                if (LinkOrder[i] == Blueprint)
                {
                    return CostOf(i);
                }
            }
            return 0;
        }

        public static int Charge(GameObject Who)
        {
            return Who == null ? 0 : Who.GetIntProperty(CHARGE_PROPERTY);
        }

        /// <summary>Is that link currently implanted on this creature?</summary>
        public static bool HasLink(GameObject Who, string Blueprint)
        {
            if (Who == null || Who.Body == null)
            {
                return false;
            }
            foreach (BodyPart part in Who.Body.LoopParts())
            {
                if (part.Cybernetics != null && part.Cybernetics.Blueprint == Blueprint)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsUnlocked(string Key)
        {
            if (Key == "Decree") return DecreeAbilityID != Guid.Empty;
            if (Key == "ThunderFire") return ThunderFireAbilityID != Guid.Empty;
            if (Key == "ThunderStep") return ThunderStepAbilityID != Guid.Empty;
            if (Key == "LightningSnake") return LightningSnakeAbilityID != Guid.Empty;
            if (Key == "ThunderBreath") return ThunderBreathAbilityID != Guid.Empty;
            return false;
        }

        /// <summary>
        /// Register every ability whose link is implanted. Idempotent, so it is safe as both the
        /// implant-time hook and the load-time repair path.
        /// </summary>
        public void EnsureAbilities(GameObject Who)
        {
            if (Who == null)
            {
                return;
            }
            for (int i = 0; i < LinkOrder.Length && i < AbilityOrder.Length; i++)
            {
                if (HasLink(Who, LinkOrder[i]))
                {
                    Register(Who, AbilityOrder[i]);
                }
            }
        }

        private void Register(GameObject Who, string Key)
        {
            if (IsUnlocked(Key))
            {
                return;
            }
            A2Raine_Toncihana_StormLinks links = Who.GetPart<A2Raine_Toncihana_StormLinks>();
            if (links == null)
            {
                return;
            }

            if (Key == "Decree")
            {
                A2Raine_Toncihana_ThunderLordDecree carrier = Who.RequirePart<A2Raine_Toncihana_ThunderLordDecree>();
                if (!IsAbilityRegistered(Who, carrier.AbilityCommand))
                {
                    links.DecreeAbilityID = AddMyActivatedAbility(
                        Name: carrier.AbilityName,
                        Command: carrier.AbilityCommand,
                        Class: "Skill",
                        Description: carrier.AbilityDescription,
                        Toggleable: true,
                        DefaultToggleState: false);
                }
                carrier.AbilityID = links.DecreeAbilityID;
            }
            else if (Key == "ThunderStep")
            {
                A2Raine_Toncihana_ThunderStep carrier2 = Who.RequirePart<A2Raine_Toncihana_ThunderStep>();
                if (!IsAbilityRegistered(Who, carrier2.AbilityCommand))
                {
                    links.ThunderStepAbilityID = AddMyActivatedAbility(
                        Name: carrier2.AbilityName,
                        Command: carrier2.AbilityCommand,
                        Class: "Skill",
                        Description: carrier2.AbilityDescription,
                        UITileDefault: carrier2.AbilityIcon);
                }
                carrier2.AbilityID = links.ThunderStepAbilityID;
            }
            else if (Key == "ThunderFire")
            {
                A2Raine_Toncihana_ThunderFire carrier3 = Who.RequirePart<A2Raine_Toncihana_ThunderFire>();
                if (!IsAbilityRegistered(Who, carrier3.AbilityCommand))
                {
                    links.ThunderFireAbilityID = AddMyActivatedAbility(
                        Name: carrier3.AbilityName,
                        Command: carrier3.AbilityCommand,
                        Class: "Skill",
                        Description: carrier3.AbilityDescription,
                        UITileDefault: carrier3.AbilityIcon);
                }
                carrier3.AbilityID = links.ThunderFireAbilityID;
            }
            else if (Key == "LightningSnake")
            {
                A2Raine_Toncihana_LightningSnake carrier4 = Who.RequirePart<A2Raine_Toncihana_LightningSnake>();
                if (!IsAbilityRegistered(Who, carrier4.AbilityCommand))
                {
                    links.LightningSnakeAbilityID = AddMyActivatedAbility(
                        Name: carrier4.AbilityName,
                        Command: carrier4.AbilityCommand,
                        Class: "Skill",
                        Description: carrier4.AbilityDescription,
                        UITileDefault: carrier4.AbilityIcon);
                }
                carrier4.AbilityID = links.LightningSnakeAbilityID;
            }
            else if (Key == "ThunderBreath")
            {
                A2Raine_Toncihana_ThunderBreath carrier5 = Who.RequirePart<A2Raine_Toncihana_ThunderBreath>();
                if (!IsAbilityRegistered(Who, carrier5.AbilityCommand))
                {
                    links.ThunderBreathAbilityID = AddMyActivatedAbility(
                        Name: carrier5.AbilityName,
                        Command: carrier5.AbilityCommand,
                        Class: "Skill",
                        Description: carrier5.AbilityDescription,
                        UITileDefault: carrier5.AbilityIcon);
                }
                carrier5.AbilityID = links.ThunderBreathAbilityID;
            }

            UnityEngine.Debug.Log("[Toncihana] link unlocked '" + Key + "' for " + Who.Blueprint);
        }

        private static bool IsAbilityRegistered(GameObject Who, string Command)
        {
            if (Who == null || string.IsNullOrEmpty(Command))
            {
                return false;
            }
            ActivatedAbilities abilities = Who.GetPart<ActivatedAbilities>();
            if (abilities == null)
            {
                return false;
            }
            return abilities.GetAbilityByCommand(Command) is ActivatedAbilityEntry;
        }
    }

    /// <summary>
    /// Goes on a link item. When the link is implanted, ask the implantee's A2Raine_Toncihana_StormLinks
    /// to register whatever is now unlocked.
    ///
    /// ImplantedEvent is fired on the ITEM, with the implantee in E.Implantee -- the same shape
    /// vanilla's CyberneticsPropertyModifier uses to apply its Props.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_Link : IPart
    {
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == ImplantedEvent.ID;
        }

        public override bool HandleEvent(ImplantedEvent E)
        {
            if (E.Implantee != null)
            {
                E.Implantee.RequirePart<A2Raine_Toncihana_StormLinks>().EnsureAbilities(E.Implantee);
            }
            return base.HandleEvent(E);
        }
    }
}
