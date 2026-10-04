using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// The Elemental race's physical identity, attached by the 2Raine_AncientPioneer_Body blueprint.
    ///
    /// EVERYTHING here is scoped to this race, because the part only ever exists on
    /// 2Raine_AncientPioneer_Body. No other character, creature or mod is affected.
    ///
    /// Implemented here (C#):
    ///   * physical damage halved
    ///   * heat and cold DAMAGE multiplied (without touching temperature change rate)
    ///   * metallic missiles deflected, chance by level
    ///   * harm bleeds off stored charge instead of liquid blood, with random arcs
    ///   * natural healing rate scales with current charge percentage
    ///
    /// Implemented in XML instead (Toncihana_Bodies.xml), because the base game already has a
    /// first-class mechanism for each and re-implementing them in C# would only add failure modes:
    ///   * electric immunity   -> ElectricResistance = 100  ("At 100, you are immune to electrical
    ///                            damage and you do not conduct electricity" -- shipped Manual.xml)
    ///   * poison immunity     -> EffectResistance Values="Poison,PoisonGasPoison"
    ///
    /// WHY HEAT/COLD DAMAGE IS DONE IN CODE AND NOT WITH THE RESISTANCE STATS:
    ///   The HeatResistance / ColdResistance stats govern BOTH how much damage you ablate AND how
    ///   fast your temperature moves (Manual.xml: "how much cold damage you ablate and how
    ///   insulated you are from effects that reduce your temperature"). Setting them to -100 would
    ///   therefore also have doubled how fast the character freezes and burns. Doing the
    ///   multiplication on the damage event instead leaves the resistance stats at their default 0,
    ///   so temperature behaviour is completely vanilla while heat/cold damage is doubled.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_Physiology : IPart
    {
        /// <summary>Physical damage multiplier, as a percentage. 50 = half damage.</summary>
        public int PhysicalDamagePercent = 50;

        /// <summary>Heat damage multiplier, as a percentage. 200 = double damage.</summary>
        public int HeatDamagePercent = 200;

        /// <summary>Cold damage multiplier, as a percentage. 200 = double damage.</summary>
        public int ColdDamagePercent = 200;

        /// <summary>
        /// When true, the engine's own liquid bleeding is suppressed for this object and replaced
        /// by a charge leak. The blueprint also sets Bleeds=0, which is what actually stops the
        /// liquid; this flag is the switch for the replacement behaviour.
        /// </summary>
        public bool ReplaceBleedingWithChargeLeak = true;

        /// <summary>Charge lost per turn while bleeding, before the bleed roll is factored in.</summary>
        public int ChargeLostPerBleedTick = 100;

        /// <summary>Damage roll of the random arcs a bleeding character throws off.</summary>
        public string BleedArcDamage = "1d4";

        /// <summary>Chance per turn that bleeding throws an arc, in percent.</summary>
        public int BleedArcChance = 50;

        /// <summary>Chance to bleed at all in a given turn, matching vanilla's bleed cadence.</summary>
        public int BleedTickChance = 50;

        /// <summary>Chance to deflect at level 1, in percent.</summary>
        public int DeflectionBaseChance = 20;

        /// <summary>Levels per step of the deflection chance.</summary>
        public int DeflectionLevelsPerStep = 5;

        /// <summary>Chance added per step, in percent.</summary>
        public int DeflectionStepChance = 10;

        /// <summary>Hard ceiling on the deflection chance, in percent.</summary>
        public int DeflectionChanceCap = 90;

        /// <summary>
        /// Deflection chance in percent: 20% at level 1, then +10% for every full 5 levels, capped
        /// at 90%. So 20 / 30 / 40 ... reaching the 90% cap at level 36.
        /// </summary>
        public int GetDeflectionChance()
        {
            int level = Math.Max(1, ParentObject == null ? 1 : ParentObject.Stat("Level"));
            int steps = level / Math.Max(1, DeflectionLevelsPerStep);
            return Math.Min(DeflectionChanceCap,
                DeflectionBaseChance + steps * DeflectionStepChance);
        }

        /// <summary>Lower bound of the charge-scaled natural healing multiplier.</summary>
        public int MinHealingPercent = 10;

        /// <summary>Upper bound of the charge-scaled natural healing multiplier.</summary>
        public int MaxHealingPercent = 110;

        /// <summary>
        /// Last computed healing multiplier, 10..110, recomputed each turn by UpdateHealingPercent.
        /// Kept as a field so the healing path stays allocation-free and does no part lookups.
        /// </summary>
        public int HealingPercent = 100;

        // =====================================================================================
        // SERIALIZATION LAYOUT -- READ BEFORE TOUCHING ANY FIELD IN THIS CLASS
        // =====================================================================================
        // The engine serializes a part's fields IN DECLARATION ORDER into a fixed byte layout, with
        // no field names in the stream. Reordering fields, or inserting one anywhere except at the
        // very END, shifts every later value onto the wrong slot.
        //
        // That is exactly what broke this part twice:
        //   * ProjectileHitsSeen was inserted in the middle, so the reader handed a later field's
        //     bytes to the wrong field -> "Object of type 'System.String' cannot be converted to
        //     type 'System.Int32'" (the string was BleedArcDamage).
        //   * The first attempt at a fix made every field [NonSerialized] and wrote nothing at all,
        //     which desynced the reader completely -> "deserializing 'unknown type'" and a hard
        //     crash in SerializationReader.ReadTokenizedType.
        //
        // RULES:
        //   1. NEVER reorder the fields above the END marker below.
        //   2. New fields go BELOW that marker, i.e. at the end of the serializable list.
        //   3. Even marking a field [NonSerialized] changes the byte layout as much as deleting it,
        //      so do not do that to an existing field either.
        // =====================================================================================

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == BeforeApplyDamageEvent.ID
                || ID == BeforeProjectileHitEvent.ID
                || ID == EndTurnEvent.ID;
        }

        // ------------------------------------------------------- physical / heat / cold damage
        public override bool HandleEvent(BeforeApplyDamageEvent E)
        {
            if (E.Object != ParentObject || E.Damage == null || E.Damage.Amount <= 0)
            {
                return base.HandleEvent(E);
            }

            Damage d = E.Damage;

            // NO electric case here on purpose, and that is a deliberate decision rather than an
            // omission. Electric immunity is left to the body's ElectricResistance stat.
            //
            // A blanket "if (d.IsElectricDamage()) { d.Amount = 0; }" was written and then removed.
            // Damage.IsElectricDamage() returns true when the object carries ANY of several electric
            // attributes, and IsHeatDamage() has the same shape, so a single damage object carrying
            // both Electric and Heat would have had its heat half silently swallowed along with the
            // electric half. Sampling every DamageAttributes/Attributes string in the base data finds
            // no vanilla case that mixes Electric with another type, so the collision is theoretical
            // TODAY -- but a modded attacker could produce one, and quietly eating damage that was
            // never granted immunity to is a far worse failure than taking battery damage.
            //
            // If electric immunity through liquid ever needs to be airtight, the correct lever is
            // conductivity (GameObject.BaseElectricalConductivity / the Body's per-part
            // GetTotalElectricalConductivity), not the damage pipeline.
            if (d.IsHeatDamage())
            {
                Scale(d, HeatDamagePercent);
            }
            else if (d.IsColdDamage())
            {
                Scale(d, ColdDamagePercent);
            }
            else if (IsPhysical(d))
            {
                Scale(d, PhysicalDamagePercent);
            }

            return base.HandleEvent(E);
        }

        /// <summary>
        /// Apply a percentage multiplier, never rounding a real hit away to nothing. Qud has no
        /// Slashing/Bludgeoning/Piercing damage attributes at all -- a melee weapon's type is its
        /// Skill= (Axe, Cudgel, ...) -- so an untyped hit IS the physical case.
        /// </summary>
        private static void Scale(Damage D, int Percent)
        {
            if (Percent == 100)
            {
                return;
            }
            int scaled = D.Amount * Percent / 100;
            if (scaled < 1)
            {
                scaled = 1;
            }
            D.Amount = scaled;
        }

        private static bool IsPhysical(Damage D)
        {
            return !D.IsHeatDamage()
                && !D.IsColdDamage()
                && !D.IsElectricDamage()
                && !D.IsAcidDamage()
                && !D.IsLightDamage()
                && !D.IsDisintegrationDamage();
        }

        // ------------------------------------------------------------- metallic missile deflection
        // Verified in game. Deflects metal projectiles and thrown weapons before armour penetration,
        // at a level-scaled chance; see GetDeflectionChance and IsMetallicProjectile.
        public override bool HandleEvent(BeforeProjectileHitEvent E)
        {
            if (E.Object != ParentObject || E.Projectile == null)
            {
                return base.HandleEvent(E);
            }

            ProjectileHitsSeen++;

            if (!IsMetallicProjectile(E.Projectile))
            {
                return base.HandleEvent(E);
            }

            int chance = GetDeflectionChance();
            if (chance > 0 && Stat.Random(1, 100) <= chance)
            {
                E.Hit = false;
                ARaine_Charge.Message(ParentObject,
                    "{{C|The " + E.Projectile.DisplayNameOnlyStripped
                    + " glances off your charge-field and clatters away.}}");
            }
            return base.HandleEvent(E);
        }


        /// <summary>
        /// Is this projectile metallic? Two layers, because the game marks the two cases differently.
        ///
        /// THROWN WEAPONS (Dagger, Hand Axe, Battle Axe, Pickaxe, Grenade, ...) are ordinary items
        /// that get thrown, and they DO carry the Metal part -- measured on the shipped blueprints:
        /// 79 blueprints have a ThrownWeapon part, and the metal ones among them all carry
        /// &lt;part Name="Metal" /&gt;. Throwing uses MissileWeapon.SetupProjectile and
        /// CalculateMissilePath, the same path as fired ammunition, so they reach this hook too.
        ///
        /// FIRED AMMUNITION is the awkward case. Sampling every vanilla Projectile* blueprint finds
        /// ZERO carrying a Metal part, and even BaseLeadSlugProjectile has no Metal tag -- the
        /// ammunition blueprints simply are not marked. So for those the blueprint name is the only
        /// evidence available, and the earlier assumption that the engine could answer
        /// "is this metal?" was wrong: MagneticPulse.CanManipulate turned out to be a
        /// "may the magnetic skill pick this up" consent test whose IsNatural() gate rejects every
        /// projectile, and it reported "lead slug" as NOT METAL in the live log.
        /// </summary>
        private static bool IsMetallicProjectile(GameObject Projectile)
        {
            // 1. Explicit markers, which thrown weapons reliably have.
            if (Projectile.HasPart("Metal") || Projectile.HasTag("Metal")
                || Projectile.HasTag("Metallic") || Projectile.HasPart("Metallic"))
            {
                return true;
            }

            // 2. Ammunition: name markers, matching only metal SUBSTANCES and ammunition SHAPES.
            // Deliberately excludes "Arrow" and "Bolt" on their own, because wooden and bone
            // arrows are a real thing in Qud and should not be deflected.
            string blueprint = BlueprintNameOf(Projectile);

            string[] substance =
            {
                "Steel", "Iron", "Carbide", "Crysteel", "Zetachrome", "Fullerite",
                "Metal", "Bronze", "Copper", "Lead", "Silver", "Gold",
            };
            foreach (string s in substance)
            {
                if (blueprint.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            string[] shape =
            {
                "Bullet", "Slug", "Shell", "Musket", "Cannonball", "Flechette",
                "Shuriken", "Needle", "Dart", "Rail",
            };
            foreach (string s in shape)
            {
                if (blueprint.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BlueprintNameOf(GameObject Object)
        {
            try
            {
                return Object.GetBlueprint() == null ? string.Empty : Object.GetBlueprint().Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        // =====================================================================================
        // TUNING VALUES: defined ONCE here, then pinned onto the fields after every load.
        // =====================================================================================
        // Change a number HERE and nowhere else -- PinTuningValues() reads these, so the field
        // declarations and this table can never disagree.
        private const int CFG_PhysicalDamagePercent = 50;
        private const int CFG_HeatDamagePercent = 200;
        private const int CFG_ColdDamagePercent = 200;
        private const bool CFG_ReplaceBleedingWithChargeLeak = true;
        private const int CFG_ChargeLostPerBleedTick = 100;
        private const string CFG_BleedArcDamage = "1d4";
        private const int CFG_BleedArcChance = 50;
        private const int CFG_BleedTickChance = 50;

        private const int CFG_DeflectionBaseChance = 20;
        private const int CFG_DeflectionLevelsPerStep = 5;
        private const int CFG_DeflectionStepChance = 10;
        private const int CFG_DeflectionChanceCap = 90;

        private const int CFG_MinHealingPercent = 10;
        private const int CFG_MaxHealingPercent = 110;
        private const int CFG_BaseHealingPerTick = 1;
        private const int CFG_HealingPerToughnessMod = 1;
        private const int CFG_HealingPerWillpowerModPercent = 25;
        private const int CFG_HealingTickChance = 50;

        // =====================================================================================
        // WHY THE VALUES ARE PINNED AFTER EVERY LOAD -- do not remove Read()
        // =====================================================================================
        // The engine serializes a part's fields POSITIONALLY: IComponent<T>.Write walks the fields
        // in declaration order, and Read walks them back in the same order. There are no field
        // names in the stream, and the version guard (FastSerialization.FieldSaveVersionInfo) only
        // covers the game's own assembly -- mod parts get no protection at all.
        //
        // So when this class gained fields, a save written under the older layout handed the wrong
        // numbers to the wrong fields, and the pollution was then written back on the next save and
        // preserved forever. Observed live, with the source declaring base=20 and cap=90:
        //
        //     Stat(Level)=41 | base=5 per=75 step=10 cap=110 | chance=5%
        //
        // base had picked up DeflectionLevelsPerStep's 5, cap had picked up MaxHealingPercent's 110,
        // and the deflection chance collapsed to 5% at character level 41.
        //
        // Re-assigning here makes the code the source of truth for these values whatever the save
        // holds. It does NOT move, remove or re-type any field, so the byte layout is untouched --
        // which is the whole point, see the note on [NonSerialized] below.
        //
        // NOTE ON [NonSerialized]: the engine's Read and Write apply the SAME attribute mask
        // (Attributes & 208) to decide what to skip, so marking a field [NonSerialized] REMOVES ITS
        // BYTES from the layout -- it is exactly as disruptive as deleting the field. That is safe
        // only for a field that was never serialized in any released version. Every field in this
        // class already is, so none of them may be marked now.
        private void PinTuningValues()
        {
            PhysicalDamagePercent = CFG_PhysicalDamagePercent;
            HeatDamagePercent = CFG_HeatDamagePercent;
            ColdDamagePercent = CFG_ColdDamagePercent;
            ReplaceBleedingWithChargeLeak = CFG_ReplaceBleedingWithChargeLeak;
            ChargeLostPerBleedTick = CFG_ChargeLostPerBleedTick;
            BleedArcDamage = CFG_BleedArcDamage;
            BleedArcChance = CFG_BleedArcChance;
            BleedTickChance = CFG_BleedTickChance;

            DeflectionBaseChance = CFG_DeflectionBaseChance;
            DeflectionLevelsPerStep = CFG_DeflectionLevelsPerStep;
            DeflectionStepChance = CFG_DeflectionStepChance;
            DeflectionChanceCap = CFG_DeflectionChanceCap;

            MinHealingPercent = CFG_MinHealingPercent;
            MaxHealingPercent = CFG_MaxHealingPercent;
            BaseHealingPerTick = CFG_BaseHealingPerTick;
            HealingPerToughnessMod = CFG_HealingPerToughnessMod;
            HealingPerWillpowerModPercent = CFG_HealingPerWillpowerModPercent;
            HealingTickChance = CFG_HealingTickChance;
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            base.Read(Basis, Reader);
            PinTuningValues();
            VerifyTuningValues();
        }

        /// <summary>
        /// Self-check: if a future layout change pollutes a tuning field again, say so in the log
        /// rather than letting it show up as "the odds feel wrong".
        /// </summary>
        private void VerifyTuningValues()
        {
            string bad = null;
            if (DeflectionBaseChance != CFG_DeflectionBaseChance)
                bad += " DeflectionBaseChance=" + DeflectionBaseChance;
            if (DeflectionLevelsPerStep != CFG_DeflectionLevelsPerStep)
                bad += " DeflectionLevelsPerStep=" + DeflectionLevelsPerStep;
            if (DeflectionStepChance != CFG_DeflectionStepChance)
                bad += " DeflectionStepChance=" + DeflectionStepChance;
            if (DeflectionChanceCap != CFG_DeflectionChanceCap)
                bad += " DeflectionChanceCap=" + DeflectionChanceCap;

            if (bad != null)
            {
                UnityEngine.Debug.LogError("[Toncihana] tuning drift detected despite pinning:"
                    + bad);
            }
        }

        // ------------------------------------------------------- bleeding = leaking stored current
        public override bool HandleEvent(EndTurnEvent E)
        {
            UpdateHealingPercent();

            if (ReplaceBleedingWithChargeLeak && ParentObject.HasEffect<Bleeding>())
            {
                LeakCurrent();
            }

            ProcessChargeScaledHealing();
            return base.HandleEvent(E);
        }

        private void UpdateHealingPercent()
        {
            int chargePercent = ARaine_Charge.GetChargePercent(ParentObject);
            HealingPercent = MinHealingPercent
                + (MaxHealingPercent - MinHealingPercent) * Math.Max(0, Math.Min(100, chargePercent)) / 100;
        }

        /// <summary>
        /// One tick of "bleeding": spill charge instead of blood, and sometimes throw an arc.
        ///
        /// Because the blueprint sets Bleeds=0 the engine will never spray a liquid, so this is the
        /// only thing bleeding does to an Elemental. If the character has no charge store at all
        /// (never took Electrical Generation) then there is nothing to leak and nothing happens --
        /// an Elemental simply does not bleed.
        /// </summary>
        private void LeakCurrent()
        {
            int bleedRoll = 1;
            Bleeding bleeding = ParentObject.GetEffect<Bleeding>();
            if (bleeding != null && !string.IsNullOrEmpty(bleeding.Damage))
            {
                bleedRoll = Math.Max(1, Stat.RollMax(bleeding.Damage));
            }

            // Vanilla bleeding only lands on roughly half of turns; match that cadence for the
            // charge leak so a bleed drains roughly the same total as it would have dealt damage.
            if (Stat.Random(1, 100) > BleedTickChance)
            {
                return;
            }

            int loss = ChargeLostPerBleedTick * bleedRoll;
            int lost = LeakCharge(loss);
            if (lost > 0)
            {
                ARaine_Charge.Message(ParentObject,
                    "{{C|Current leaks from the wound, bleeding off " + lost + " charge.}}");
            }

            if (Stat.Random(1, 100) <= BleedArcChance)
            {
                ThrowBleedArc();
            }
        }

        /// <summary>Spend up to Amount charge; returns however much was actually available.</summary>
        private int LeakCharge(int Amount)
        {
            ElectricalGeneration gen = ARaine_Charge.GetGeneration(ParentObject);
            if (gen == null || Amount <= 0)
            {
                return 0;
            }
            int available = gen.GetCharge();
            int spent = Math.Min(available, Amount);
            if (spent > 0)
            {
                gen.UseCharge(spent);
            }
            return spent;
        }

        /// <summary>
        /// The "randomly release a small arc" half of the bleed: pick one hostile thing standing
        /// next to the character and shock it for a small amount. Deliberately allies-safe --
        /// IsHostileTowards is the game's own opinion, so pets and neutral NPCs are never hit.
        /// </summary>
        private void ThrowBleedArc()
        {
            Cell here = ParentObject.CurrentCell;
            if (here == null)
            {
                return;
            }

            List<GameObject> targets = new List<GameObject>();
            foreach (Cell cell in here.YieldAdjacentCells(1, LocalOnly: true))
            {
                foreach (GameObject obj in cell.GetObjectsInCell())
                {
                    if (obj != null
                        && obj != ParentObject
                        && obj.IsCombatObject()
                        && obj.IsHostileTowards(ParentObject)
                        && !targets.Contains(obj))
                    {
                        targets.Add(obj);
                    }
                }
            }

            if (targets.Count == 0)
            {
                return;
            }

            GameObject victim = targets[Stat.Random(0, targets.Count - 1)];
            int damage = Math.Max(1, Stat.Roll(BleedArcDamage));
            int damageRoll = damage;

            victim.TakeDamage(ref damageRoll, "from %t leaking current!", "Electric",
                (string)null, ParentObject, ParentObject, (GameObject)null, (GameObject)null,
                (GameObject)null, (string)null, false, false, false, false, false, false, false,
                false, false, 0, (string)null);

            ARaine_Charge.Message(ParentObject,
                "{{C|An arc snaps out of the wound and earths itself in "
                + victim.DisplayNameOnlyStripped + ".}}");
        }

        // ---------------------------------------------------------- charge-scaled natural healing
        //
        // This used to be a Harmony patch on Stomach.ProcessNaturalHealing. That was a mistake, and
        // an expensive one: the patch's signature did not match the target (the real method is
        // `bool ProcessNaturalHealing(int)`, and the prefix was declared `void ... (ref int)`),
        // Harmony rejected it, and because a rejected patch aborts the ENTIRE mod assembly, every
        // other class in this mod silently failed to load as well -- which is why neither the
        // mutation nor the four abilities ever appeared in game.
        //
        // It is now plain code in the EndTurn handler below, so this mod applies no Harmony patches
        // at all and a bug in one feature can never take the rest of the mod down with it.
        //
        // The base game's own natural healing is switched off on the blueprint with the vanilla
        // DisabledNaturalHealing part; this is its replacement.

        /// <summary>
        /// Health regained per natural-healing tick at 100% charge.
        ///
        /// Caveat: the base game's natural healing formula is not documented and lives in code, so
        /// this is an approximation of it rather than a reproduction. It is derived from the two
        /// things the game itself says drive natural healing (Toughness and Willpower, per
        /// Genotypes.xml's own chargen text) and is meant to be tuned in play.
        /// </summary>
        public int BaseHealingPerTick = 1;

        /// <summary>Extra healing per tick for every point of Toughness modifier above zero.</summary>
        public int HealingPerToughnessMod = 1;

        /// <summary>Fraction of the Willpower modifier added to the healing roll, in percent.</summary>
        public int HealingPerWillpowerModPercent = 25;

        /// <summary>Chance per turn that a healing tick happens at all, in percent.</summary>
        public int HealingTickChance = 50;

        // =====================================================================================
        // END OF THE SERIALIZED FIELD LIST -- append new fields BELOW this line, never above.
        // =====================================================================================

        /// <summary>
        /// Diagnostic counter for the projectile-deflection hook. Appended at the very end on
        /// purpose: a save written before this field existed simply has no bytes for it.
        /// </summary>
        public int ProjectileHitsSeen = 0;

        private void ProcessChargeScaledHealing()
        {
            if (ParentObject == null || !ParentObject.HasHitpoints() || !ParentObject.IsCreature)
            {
                return;
            }
            if (ParentObject.hitpoints >= ParentObject.baseHitpoints)
            {
                return;
            }

            if (Stat.Random(1, 100) > HealingTickChance)
            {
                return;
            }

            int roll = BaseHealingPerTick
                + Math.Max(0, ParentObject.StatMod("Toughness")) * HealingPerToughnessMod;
            roll += (int)Math.Round(
                roll * (ParentObject.StatMod("Willpower") * HealingPerWillpowerModPercent / 100.0),
                MidpointRounding.AwayFromZero);

            int chargePercent = ARaine_Charge.GetChargePercent(ParentObject);
            HealingPercent = MinHealingPercent
                + (MaxHealingPercent - MinHealingPercent)
                  * Math.Max(0, Math.Min(100, chargePercent)) / 100;

            int amount = (int)Math.Round(roll * (HealingPercent / 100.0), MidpointRounding.AwayFromZero);

            // REGENERATION, wired in by hand.
            //
            // Vanilla's Regeneration boosts healing through the "Regenerating" event that
            // Stomach.ProcessNaturalHealing fires. My notes record its mutation text as promising
            // three things: "Your full natural healing rate applies in combat", "N% faster natural
            // healing rate", and chances to shed debuffs and regrow limbs.
            //
            // DisabledNaturalHealing zeroes that event's amount, so Regeneration's healing half was
            // multiplying zero and doing nothing. Rather than re-enable vanilla natural healing --
            // which would run a second and larger healing stream alongside this one and drown out the
            // charge mechanic -- its bonus is applied here, to the number we actually heal for, so the
            // mutation's own wording stays true:
            //
            //   "N% faster natural healing rate"      -> this multiplier. GetRegenerationBonus(level)
            //                                            is 0.1 + 0.1*level, so +20% at level 1 rising
            //                                            to +110% at level 10; 1.0 + that = 120%..210%.
            //   "full natural healing rate in combat" -> EndTurnEvent carries no combat gate, so this
            //                                            heal already runs every round while fighting.
            //   limb regrowth / debuff removal        -> separate machinery inside the mutation. It
            //                                            never depended on the healing event and is
            //                                            untouched by any of this.
            Regeneration regeneration = ParentObject.GetPart<Regeneration>();
            if (regeneration != null)
            {
                amount = (int)Math.Round(
                    amount * (1.0 + regeneration.GetRegenerationBonus(regeneration.Level)),
                    MidpointRounding.AwayFromZero);
            }

            if (amount < 1)
            {
                amount = 1;
            }

            ParentObject.Heal(amount, Message: false);
        }
    }
}
