using System;
using System.Collections.Generic;
using XRL;
using XRL.Rules;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// Promotes a creature to a LEGENDARY one with a percentage chance, and dresses it.
    ///
    /// WHY A PART AND NOT A ZONE BUILDER
    /// ---------------------------------
    /// The first attempt spawned elementals from a custom ZoneBuilder registered onto the salt desert
    /// zone ids through ZoneManager.AddZonePostBuilder. The registration reported success -- the log
    /// said "legendary elemental builder attached to 130 salt desert zones" -- and not one elemental
    /// ever appeared. Rather than keep guessing at zone-builder timing, this uses the two mechanisms
    /// already proven to work in this mod:
    ///   * SPAWNING is left to the ordinary population table, the same mechanism vanilla uses for
    ///     every other desert creature (SaltDesertPerSector, Number="1-2").
    ///   * PROMOTION happens in AfterObjectCreatedEvent, which is the hook A2Raine_BornEquipped
    ///     already relies on and which GameObjectFactory.CreateObject definitely fires.
    ///
    /// The zone builder, its registrar and the Api file are gone; there is exactly one path now.
    ///
    /// WHAT MakeHero DOES FOR US
    /// -------------------------
    /// HeroMaker.MakeHero turns the ordinary creature into a legendary one and, in doing so:
    ///   * replaces GivesRep, which is the sole gate on the water ritual -- SKIPPED here on purpose,
    ///     see the HeroNoWaterRitual tag below;
    ///   * attaches a ConversationScript so the creature can be talked to;
    ///   * appends an honorific and applies the hero name and tile colours;
    ///   * sets the Hero property, so a second call is a no-op.
    /// </summary>
    [Serializable]
    public class A2Raine_LegendaryChance : IPart
    {
        /// <summary>
        /// Percent chance to promote. FIVE is the vanilla rate, not a guess: vanilla builds legendary
        /// spawns as a "HeroOrNot" pickone group weighted 95 / 5 (see GoatfolkParty in
        /// PopulationTables.xml) and its standalone hero entries use Chance="3" and Chance="5".
        /// </summary>
        public int Chance = 5;

        /// <summary>Extra hit points the legendary gets on top of the base creature's.</summary>
        public int BonusHP = 20;

        /// <summary>Flat bonus to each of the six attributes.</summary>
        public int AttributeBonus = 2;

        /// <summary>
        /// Mutations the legendary gets at a higher level. uses the CLASS name, as creature blueprints
        /// do -- vanilla mutation XML carries a spaced display Name and a Class, and it is the Class
        /// that creature blueprints reference.
        /// </summary>
        public string BoostMutations = "ElectricalGeneration,ElectromagneticPulse";
        public int BoostedMutationLevel = 2;

        /// <summary>
        /// A SECOND item for the legendary. The ordinary creature wears one already; this adds another
        /// on a different body part.
        ///
        /// Two floating slots are needed because BodyPart._Equipped holds a single GameObject, so one
        /// part cannot carry two stones. The Elemental anatomy has three floating slots: two declared
        /// in Bodies.xml plus one that XRL.World.Anatomy.ApplyTo adds to every anatomy unconditionally.
        /// </summary>
        public string ExtraItem;
        public string ExtraItemSlot = "Floating Nearby";
        public int ExtraItemSlotIndex = 1;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == AfterObjectCreatedEvent.ID;
        }

        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            // Only act for our own object; the event reaches every part on the creature.
            if (E.Object != ParentObject)
            {
                return base.HandleEvent(E);
            }
            if (Chance.in100())
            {
                Promote();
            }
            return base.HandleEvent(E);
        }

        private void Promote()
        {
            try
            {
                // Withhold the water ritual. MakeHero adds GivesRep, and GivesRep is the only gate on
                // the water ritual; the setting is that these elementals have not awakened enough to
                // share water. The tag is read by MakeHero itself, which then leaves GivesRep off.
                // Conversation is unaffected -- the conversation script is set up separately -- and a
                // future "awakened" elemental would simply omit this line.
                ParentObject.SetStringProperty("HeroNoWaterRitual", "true");

                // Buff BEFORE promotion so the hero template builds on the raised values. HP is a
                // delta because the base value comes from the blueprint.
                if (BonusHP != 0)
                {
                    ParentObject.GetStat("Hitpoints").BaseValue += BonusHP;
                }
                if (AttributeBonus != 0)
                {
                    string[] stats = { "Strength", "Agility", "Toughness", "Intelligence", "Willpower", "Ego" };
                    foreach (string stat in stats)
                    {
                        ParentObject.GetStat(stat).BaseValue += AttributeBonus;
                    }
                }

                RaiseMutations();

                // The engine's own promotion. No-ops if the creature is already a hero.
                HeroMaker.MakeHero(ParentObject);

                UnityEngine.Debug.Log("[Toncihana] promoted " + ParentObject.Blueprint
                    + " to legendary: " + ParentObject.DisplayName);

                GiveExtraItem();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] could not promote an elemental to legendary: " + ex);
            }
        }

        /// <summary>
        /// Raise the listed mutations, adding any the creature lacks.
        ///
        /// AddMutation(Class, Level) rather than a setter because there is no public "set level" on
        /// Mutations; ChangeLevel is used for one that already exists, since that is the mutation's own
        /// growth path and keeps its internals consistent.
        /// </summary>
        private void RaiseMutations()
        {
            if (string.IsNullOrEmpty(BoostMutations))
            {
                return;
            }
            Mutations mutations = ParentObject.GetPart<Mutations>();
            if (mutations == null)
            {
                return;
            }
            foreach (string name in BoostMutations.Split(','))
            {
                string mutation = name.Trim();
                if (mutation.Length == 0)
                {
                    continue;
                }
                if (mutations.HasMutation(mutation))
                {
                    BaseMutation existing = mutations.GetMutation(mutation);
                    if (existing != null && existing.Level < BoostedMutationLevel)
                    {
                        existing.ChangeLevel(BoostedMutationLevel);
                    }
                    continue;
                }
                mutations.AddMutation(mutation, BoostedMutationLevel);
            }
        }

        /// <summary>
        /// Wear the extra item on a specific body part.
        ///
        /// Resolves the part by index, because the string overload of ForceEquipObject finds its slot
        /// with GetFirstPart -- so it would target the part the first item already occupies and the
        /// second one would bounce back into the inventory.
        /// </summary>
        private void GiveExtraItem()
        {
            if (string.IsNullOrEmpty(ExtraItem) || ParentObject.Body == null)
            {
                return;
            }
            BodyPart body = ParentObject.Body.GetBody();
            if (body == null)
            {
                return;
            }
            List<BodyPart> slots = body.GetPart(ExtraItemSlot, new List<BodyPart>());
            if (slots == null || ExtraItemSlotIndex < 0 || ExtraItemSlotIndex >= slots.Count)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] legendary has no '" + ExtraItemSlot
                    + "' part at index " + ExtraItemSlotIndex + "; extra item skipped.");
                return;
            }
            GameObject item = GameObjectFactory.Factory.CreateObject(ExtraItem);
            if (item == null)
            {
                return;
            }
            if (!ParentObject.ForceEquipObject(item, slots[ExtraItemSlotIndex], Silent: true))
            {
                if (ParentObject.Inventory != null)
                {
                    ParentObject.Inventory.AddObject(item);
                }
            }
        }
    }
}
