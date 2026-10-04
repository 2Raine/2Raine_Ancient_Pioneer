using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace XRL.World.Parts.Skill
{
    /// <summary>
    /// 雷霆领主的法令, 雷火, 雷动 and 雷蛇 -- the Elemental's four innate abilities, as one skill.
    ///
    /// WHY THEY LIVE UNDER ONE SKILL
    /// ----------------------------
    /// Four separate top-level skills cluttered the skill screen with four unrelated entries. Qud's
    /// skill system already models "a group with members", so these are declared in
    /// Toncihana_Skills.xml as one &lt;skill&gt; with four child &lt;power&gt; nodes.
    ///
    /// THE BUG THIS CLASS EXISTS TO AVOID (read before moving the registration code)
    /// ---------------------------------------------------------------------------
    /// An earlier version registered each ability from that ability part's own AddSkill(), and then
    /// attached the parts with GO.RequirePart&lt;T&gt;(). The abilities never appeared in the ability
    /// menu. The diagnostic in A2Raine_Toncihana_Mutator printed the whole registered list:
    ///
    ///     abilities: [Make Camp ...] [Sprint ...] [Evolve ...] [Power Devices ...] [Discharge ...]
    ///
    /// -- five vanilla abilities, and not one of ours. The parts WERE attached (HasPart was true for
    /// all four), but RequirePart only puts a part on the object; it does NOT run the skill
    /// lifecycle. AddSkill() was therefore never called, and AddMyActivatedAbility lives inside it,
    /// so nothing was ever registered. GameObject.AddSkill(string) is the path that goes through the
    /// skill registry and DOES call the lifecycle.
    ///
    /// So registration happens HERE, in the skill that is actually added with AddSkill(string), and
    /// the four ability classes are only attached as carriers for their own tuning and command
    /// handling.
    ///
    /// Nothing here levels: abilities have no upgrade path, so per-level tuning fields evaluate at
    /// level 1 and character-level scaling is read from the character instead.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_StormCalling : BaseSkill
    {
        /// <summary>Offered in the skill screen under this name.</summary>
        public const string SKILL_NAME = "Storm Calling";

        /// <summary>Handles of the four abilities in the owner's ActivatedAbilities part.</summary>
        public Guid DecreeAbilityID = Guid.Empty;
        public Guid ThunderFireAbilityID = Guid.Empty;
        public Guid ThunderStepAbilityID = Guid.Empty;
        public Guid LightningSnakeAbilityID = Guid.Empty;

        /// <summary>
        /// 雷扇 / Thunder Breath. APPENDED LAST on purpose: serialization walks fields in
        /// declaration order, so a new field may only ever be added below the existing ones.
        /// A save written before this existed simply has no bytes for it and reads back as
        /// Guid.Empty, which AddSkill then refills.
        /// </summary>
        public Guid ThunderBreathAbilityID = Guid.Empty;

        /// <summary>
        /// MASTER SWITCH for whether the five storm abilities are granted at all.
        ///
        /// FALSE for now, on request: the abilities are meant to be UNLOCKED later by some mechanism
        /// that does not exist yet, so a new character should not start with them. Flip this to true
        /// (or call EnsureAbilities) when that unlock path is built.
        ///
        /// A static readonly rather than a const: with a const the compiler folds the condition and
        /// reports the unreachable half as CS0162. A static field is still never serialized (only
        /// instance fields are), so this is equally safe to flip and costs no save compatibility.
        /// </summary>
        public static readonly bool AbilitiesUnlocked = false;

        public override bool AddSkill(GameObject GO)
        {
            EnsureAbilities(GO);
            return base.AddSkill(GO);
        }

        /// <summary>
        /// Bring the character's storm abilities in line with AbilitiesUnlocked.
        ///
        /// While AbilitiesUnlocked is false this REMOVES the abilities rather than merely declining to
        /// add them. That matters: a save made earlier already has all five registered, and
        /// "do not register" would leave them sitting in the ability menu forever. Withholding is
        /// therefore an active step, and it runs on every load, so an old save heals itself.
        ///
        /// The skill itself stays learned -- the skill tree still lists its five powers, and an
        /// unlearned power is exactly the state wanted: visible in the tree, absent from the ability
        /// menu, ready to be granted later.
        /// </summary>
        public void EnsureAbilities(GameObject GO)
        {
            if (GO == null)
            {
                return;
            }

            // Nothing is granted, and nothing is taken away either.
            //
            // An earlier version ACTIVELY REVOKED the abilities here when the switch was off, so that
            // saves made before the switch existed would clean themselves up. That was wrong: this
            // runs on EVERY load, so it would also have stripped the abilities from a character who
            // had legitimately unlocked them. Withholding has to be a decision made once, at grant
            // time, not a recurring sweep.
            //
            // Returning here means a new character never learns the abilities in the first place,
            // which is all that was wanted. Existing saves keep whatever they have.
            if (!AbilitiesUnlocked)
            {
                return;
            }

            // Attach the ability carriers first, so their fields and command handlers exist
            // before anything can fire. This is purely so their config is available; it is NOT what
            // registers the abilities -- see the class comment.
            A2Raine_Toncihana_ThunderLordDecree decree = GO.RequirePart<A2Raine_Toncihana_ThunderLordDecree>();
            A2Raine_Toncihana_ThunderFire thunderFire = GO.RequirePart<A2Raine_Toncihana_ThunderFire>();
            A2Raine_Toncihana_ThunderStep thunderStep = GO.RequirePart<A2Raine_Toncihana_ThunderStep>();
            A2Raine_Toncihana_LightningSnake lightningSnake = GO.RequirePart<A2Raine_Toncihana_LightningSnake>();
            A2Raine_Toncihana_ThunderBreath thunderBreath = GO.RequirePart<A2Raine_Toncihana_ThunderBreath>();

            // Register all five, each behind a guard so re-running cannot duplicate anything.
            if (!IsAbilityRegistered(GO, decree.AbilityCommand))
            {
                DecreeAbilityID = AddMyActivatedAbility(
                    Name: decree.AbilityName,
                    Command: decree.AbilityCommand,
                    Class: "Skill",
                    Description: decree.AbilityDescription,
                    Toggleable: true,
                    DefaultToggleState: false);
            }

            if (!IsAbilityRegistered(GO, thunderFire.AbilityCommand))
            {
                ThunderFireAbilityID = AddMyActivatedAbility(
                    Name: thunderFire.AbilityName,
                    Command: thunderFire.AbilityCommand,
                    Class: "Skill",
                    Description: thunderFire.AbilityDescription,
                    UITileDefault: thunderFire.AbilityIcon);
            }

            if (!IsAbilityRegistered(GO, thunderStep.AbilityCommand))
            {
                ThunderStepAbilityID = AddMyActivatedAbility(
                    Name: thunderStep.AbilityName,
                    Command: thunderStep.AbilityCommand,
                    Class: "Skill",
                    Description: thunderStep.AbilityDescription,
                    UITileDefault: thunderStep.AbilityIcon);
            }

            if (!IsAbilityRegistered(GO, lightningSnake.AbilityCommand))
            {
                LightningSnakeAbilityID = AddMyActivatedAbility(
                    Name: lightningSnake.AbilityName,
                    Command: lightningSnake.AbilityCommand,
                    Class: "Skill",
                    Description: lightningSnake.AbilityDescription,
                    UITileDefault: lightningSnake.AbilityIcon);
            }

            if (!IsAbilityRegistered(GO, thunderBreath.AbilityCommand))
            {
                ThunderBreathAbilityID = AddMyActivatedAbility(
                    Name: thunderBreath.AbilityName,
                    Command: thunderBreath.AbilityCommand,
                    Class: "Skill",
                    Description: thunderBreath.AbilityDescription,
                    UITileDefault: thunderBreath.AbilityIcon);
            }

            // Hand the handles to the carriers, which need them to run their own cooldowns.
            decree.AbilityID = DecreeAbilityID;
            thunderFire.AbilityID = ThunderFireAbilityID;
            thunderStep.AbilityID = ThunderStepAbilityID;
            lightningSnake.AbilityID = LightningSnakeAbilityID;
            thunderBreath.AbilityID = ThunderBreathAbilityID;

            UnityEngine.Debug.LogWarning("[Toncihana] StormCalling registered 5 abilities"
                + " | decree=" + (DecreeAbilityID != Guid.Empty)
                + " fire=" + (ThunderFireAbilityID != Guid.Empty)
                + " step=" + (ThunderStepAbilityID != Guid.Empty)
                + " snake=" + (LightningSnakeAbilityID != Guid.Empty)
                + " breath=" + (ThunderBreathAbilityID != Guid.Empty));
        }

        /// <summary>
        /// Is this command already registered as an activated ability on GO?
        ///
        /// Used to make AddSkill idempotent. GetAbilityByCommand is the same lookup the mutator's
        /// own registration check uses, so the two agree on what "registered" means.
        /// </summary>
        private static bool IsAbilityRegistered(GameObject GO, string Command)
        {
            if (GO == null || string.IsNullOrEmpty(Command))
            {
                return false;
            }
            ActivatedAbilities abilities = GO.GetPart<ActivatedAbilities>();
            if (abilities == null)
            {
                return false;
            }
            return abilities.GetAbilityByCommand(Command) is ActivatedAbilityEntry;
        }

        public override bool RemoveSkill(GameObject GO)
        {
            RemoveMyActivatedAbility(ref DecreeAbilityID);
            RemoveMyActivatedAbility(ref ThunderFireAbilityID);
            RemoveMyActivatedAbility(ref ThunderStepAbilityID);
            RemoveMyActivatedAbility(ref LightningSnakeAbilityID);
            RemoveMyActivatedAbility(ref ThunderBreathAbilityID);

            GO.RemovePart<A2Raine_Toncihana_ThunderLordDecree>();
            GO.RemovePart<A2Raine_Toncihana_ThunderFire>();
            GO.RemovePart<A2Raine_Toncihana_ThunderStep>();
            GO.RemovePart<A2Raine_Toncihana_LightningSnake>();
            GO.RemovePart<A2Raine_Toncihana_ThunderBreath>();
            return base.RemoveSkill(GO);
        }
    }

    /// <summary>
    /// Shared plumbing for the four abilities.
    ///
    /// These classes are CARRIERS, not registrars. They hold their own tuning fields, their display
    /// strings and their command handling; the activated ability itself is registered by
    /// A2Raine_Toncihana_StormCalling.AddSkill, because that is the only AddSkill that is reliably invoked
    /// (see the comment on that class for the diagnostic that proved it). Do NOT move registration
    /// back in here unless the parts stop being attached with RequirePart.
    /// </summary>
    [Serializable]
    public abstract class A2Raine_Toncihana_InnateAbility : BaseSkill
    {
        /// <summary>
        /// Handle of this ability in the owner's ActivatedAbilities part. Assigned by
        /// A2Raine_Toncihana_StormCalling.AddSkill, and needed here so the ability can start its own cooldown.
        ///
        /// DO NOT mark this [NonSerialized] and do not move it. It was tried, on the theory that a
        /// runtime handle has no business in a save file -- the theory is fine, the mechanism is not:
        /// [NonSerialized] removes the field's bytes from the reader's layout, which is exactly as
        /// destructive as deleting the field, and it corrupted saves. This class's field layout is
        /// frozen; append only, and only below any existing fields.
        /// </summary>
        public Guid AbilityID = Guid.Empty;

        /// <summary>Label shown in the ability menu.</summary>
        public abstract string AbilityName { get; }

        /// <summary>Command event this ability fires.</summary>
        public abstract string AbilityCommand { get; }

        /// <summary>Description shown in the ability menu.</summary>
        public abstract string AbilityDescription { get; }

        /// <summary>Optional icon; null means the engine default.</summary>
        public virtual Renderable AbilityIcon
        {
            get { return null; }
        }

        /// <summary>The character's level. Abilities do not level; their scaling reads this.</summary>
        public int CharLevel
        {
            get { return Math.Max(1, ParentObject == null ? 1 : ParentObject.Stat("Level")); }
        }
    }

    // =====================================================================================
    // 天生技能 1 -- 雷霆领主的法令
    // =====================================================================================
    [Serializable]
    public class A2Raine_Toncihana_ThunderLordDecree : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_ThunderLordDecree";

        public override string AbilityName
        {
            get { return "Thunder Lord's Decree"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "While toggled on, every melee attack carries extra electric damage and may "
                    + "paralyze what it strikes. Damage and paralysis chance both grow with your "
                    + "character's level.";
            }
        }

        /// <summary>
        /// Electric damage is (character level / LevelsPerDamageDie + 1) d DieSize, added to EVERY
        /// melee hit -- the chance is deliberately 100%.
        ///   level 1-3 -> 1d2, level 4-7 -> 2d2, level 8-11 -> 3d2, ...
        /// </summary>
        public int LevelsPerDamageDie = 4;
        public int ElectricDieSize = 2;

        /// <summary>Chance to paralyze, percent, per CHARACTER level (2% per level), capped.</summary>
        public int ParalyzeChancePerLevel = 2;
        public int ParalyzeChanceCap = 100;

        /// <summary>Paralysis duration roll: ParalyzeDurationDice + "d" + ParalyzeDieSize.</summary>
        public int ParalyzeDurationDice = 1;
        public int ParalyzeDieSize = 3;
        public int ParalyzeDurationCap = 12;

        public string GetElectricDamageRoll()
        {
            return ARaine_Arc.ScaledDamageRoll(
                CharLevel, 0, LevelsPerDamageDie, 1, ElectricDieSize);
        }

        public int GetParalyzeChance()
        {
            return Math.Min(ParalyzeChanceCap, CharLevel * ParalyzeChancePerLevel);
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("DealDamage");
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                if (ParentObject.IsActivatedAbilityToggledOn(AbilityID))
                {
                    ParentObject.ToggleActivatedAbility(AbilityID);
                    ARaine_Charge.Message(ParentObject, "{{K|The decree falls silent.}}");
                }
                else
                {
                    ParentObject.ToggleActivatedAbility(AbilityID);
                    ParentObject.PlayWorldSound("Sounds/Abilities/sfx_ability_mutation_electrical_generation");
                    ARaine_Charge.Message(ParentObject,
                        "{{C|You speak the decree, and the air around your hands begins to crackle.}}");
                }
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "DealDamage" && ParentObject.IsActivatedAbilityToggledOn(AbilityID))
            {
                Damage damage = E.GetParameter("Damage") as Damage;

                // Parameter names confirmed from a live dump of this event:
                //   Damage=XRL.World.Damage; Defender=<target>; Attacker=<striker>; Weapon=<weapon>
                GameObject target = E.GetGameObjectParameter("Defender")
                    ?? E.GetGameObjectParameter("Target")
                    ?? E.GetGameObjectParameter("Object");

                if (damage != null && target != null && target != ParentObject)
                {
                    ApplyDecree(damage, target);
                }
            }
            return base.FireEvent(E);
        }

        private void ApplyDecree(Damage Damage, GameObject Target)
        {
            // --- electric damage: EVERY hit, no chance roll.
            string roll = GetElectricDamageRoll();
            int bonus = Math.Max(1, Stat.Roll(roll));

            // Folded into the melee strike rather than applied as a second hit: because the blow now
            // carries the Electric attribute, the game's own combat log and death handling describe
            // it as electrical damage rather than an anonymous extra wound.
            Damage.AddAttribute("Electric");
            Damage.Amount += bonus;

            ARaine_Arc.ZapLine(ParentObject, Target);

            ARaine_Charge.Message(ParentObject,
                "{{C|An arc leaps from your hand into " + Target.DisplayNameOnlyStripped
                + " (+" + bonus + " electric, " + roll + ").}}");

            // --- paralysis: (2 x character level) percent, capped.
            if (Stat.Random(1, 100) <= GetParalyzeChance())
            {
                int duration = Math.Min(
                    ParalyzeDurationCap,
                    Stat.Roll(Math.Max(1, ParalyzeDurationDice) + "d" + Math.Max(1, ParalyzeDieSize)));
                duration = Math.Max(1, duration);
                Target.ApplyEffect(new Paralyzed(duration, -1));
                ARaine_Charge.Message(ParentObject,
                    "{{C|" + Target.DisplayNameOnlyStripped + " locks up, paralyzed.}}");
            }
        }
    }

    // =====================================================================================
    // 天生技能 2 -- 雷火
    // =====================================================================================
    [Serializable]
    public class A2Raine_Toncihana_ThunderFire : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_ThunderFire";

        public override string AbilityName
        {
            get { return "Thunder-Fire"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "Burn a chosen amount of stored charge to instantly heat one adjacent object. "
                    + ChargePerFiveDegrees + " charge buys " + DegreesPerFiveDegrees + " degrees.";
            }
        }

        public override Renderable AbilityIcon
        {
            get
            {
                return new Renderable("Mutations/flaming_ray.bmp", ColorString: "R", DetailColor: 'R');
            }
        }

        /// <summary>Charge spent per that many degrees of temperature rise: 5 degrees per 100 charge.</summary>
        public int ChargePerFiveDegrees = 100;
        public int DegreesPerFiveDegrees = 5;

        /// <summary>
        /// Cooldown, and the value IS the number of rounds the game reports.
        ///
        /// Measured, not inferred: with this set to 300 the in-game ability tooltip read "300
        /// rounds". So 30 here gives 30 rounds. Willpower still shortens it, because the value goes
        /// to CooldownMyActivatedAbility rather than being ticked by hand.
        ///
        /// History, so nobody re-derives the wrong rule: this was first 10 (one round), then 300
        /// after I misread vanilla Axe_Dismember. Its ability text says "cooldown 30" and it does
        /// pass 300 to the engine, but the engine's own readout for that is what matters and it
        /// counts this argument as rounds. Trust the tooltip over arithmetic on vanilla constants.
        /// </summary>
        public int Cooldown = 30;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                PerformThunderFire();
            }
            return base.HandleEvent(E);
        }

        private void PerformThunderFire()
        {
            GameObject actor = ParentObject;

            int max = ARaine_Charge.GetMaxCharge(actor);
            int current = ARaine_Charge.GetCharge(actor);

            if (max <= 0)
            {
                actor.Fail("You have no charge to burn.");
                return;
            }
            if (current <= 0)
            {
                actor.Fail("Your charge is spent.");
                return;
            }

            Cell here = actor.CurrentCell;
            if (here == null)
            {
                return;
            }

            // PickDirection returns the adjacent CELL in the chosen direction. That is the right
            // primitive for a one-square ability: there are only eight directions, so the reticle
            // cannot reach a distant cell, and it confirms on a single keypress.
            //
            // Published mods use it for exactly this job -- BRMLifeDrain, Grab, BRMTeleportOther,
            // PsychoplethoricDeterioration and SpraybottleCompanions all call PickDirection, several
            // of them with a label argument like the one passed here.
            //
            // Four earlier attempts, and why each failed:
            //   * PickLine(range, ...)       -- parameters guessed from a signature dump; the guess
            //                                   was wrong, so distant cells stayed reachable.
            //   * hand-rolled neighbour list -- right range, but it popped a dialog instead of using
            //                                   the game's own reticle.
            //   * PickFieldAdjacent(1, ...)  -- the game's own adjacent helper, but it drives
            //                                   ShowFieldPicker, the multi-square field tool: one
            //                                   click per square plus a confirmation.
            //   * PickDestinationCell(1, ...) -- compiled, but its first parameter is NOT the range,
            //                                   so it imposed no limit at all.
            Cell picked = PickDirection("Thunder-Fire");

            if (picked == null)
            {
                return;
            }

            // The picker hands back a CELL. Heat the most relevant thing standing in it: a combat
            // target if there is one, otherwise the first rendered object, which is how the player
            // reads the tile anyway.
            GameObject target = picked.GetCombatTarget();
            if (target == null || target == actor)
            {
                target = picked.GetFirstObjectWithPart("Render");
            }

            if (target == null || target == actor)
            {
                actor.Fail("There is nothing there to set alight.");
                return;
            }

            // No cap any more: the only limit is the charge actually held right now. The
            // MaxSpendPercent field and its 30% ceiling were removed on request.
            int ceiling = current;
            if (ceiling <= 0)
            {
                actor.Fail("You cannot gather enough charge to kindle the fire.");
                return;
            }

            int spend = AskChargeToSpend(current, ceiling);
            if (spend <= 0)
            {
                return;
            }

            if (!ARaine_Charge.TryUseCharge(actor, spend))
            {
                actor.Fail("Your charge slips away before you can shape it.");
                return;
            }

            int degrees = spend * DegreesPerFiveDegrees / Math.Max(1, ChargePerFiveDegrees);

            actor.UseEnergy(1000, "Skill Thunder-Fire");
            actor.PlayWorldSound("Sounds/Abilities/sfx_ability_mutation_flaming_ray");

            ARaine_Charge.Message(actor,
                "{{R|You pour " + spend + " charge into " + target.DisplayNameOnlyStripped
                + ", and its temperature climbs " + degrees + " degrees.}}");

            // Diagnostic, because the reported numbers (30 charge -> 100 degrees) do not match this
            // formula (100 charge -> 5 degrees, so 30 charge -> 1 degree). The log records the real
            // spend, the computed degrees, and the target's temperature on both sides of the call, so
            // the disagreement can be settled with data instead of arithmetic.
            int tempBefore = target.Temperature;
            target.TemperatureChange(degrees, actor);
            UnityEngine.Debug.LogWarning("[Toncihana] thunder-fire: spend=" + spend
                + " chargePer" + DegreesPerFiveDegrees + "deg=" + ChargePerFiveDegrees
                + " -> degrees=" + degrees
                + " | temp " + tempBefore + " -> " + target.Temperature
                + " | rate=" + ChargePerFiveDegrees + "charge/" + DegreesPerFiveDegrees + "deg");

            ARaine_Arc.Discharge(actor, target, Math.Max(1, degrees / 10), "1d4");

            CooldownMyActivatedAbility(AbilityID, Cooldown);
        }

        /// <summary>
        /// Ask how much charge to burn, via the game's own number-entry dialog. The prompt states the
        /// current charge and the exchange rate, because the amount is typed rather than picked from
        /// fixed options.
        /// </summary>
        private int AskChargeToSpend(int Current, int Ceiling)
        {
            string message = "Expend how much charge?\n\n"
                + "Current charge: {{C|" + Current + "}}\n"
                + "Rate: {{C|" + ChargePerFiveDegrees + "}} charge = "
                + "{{R|" + DegreesPerFiveDegrees + "}} degrees\n"
                + "Most you can spend now: {{C|" + Ceiling + "}}";

            int initial = Math.Min(Ceiling, ChargePerFiveDegrees);

            // Signature confirmed by reflection against the shipped assembly:
            //   int? AskNumber(string Message, string Sound = "Sounds/UI/ui_notification",
            //                  string RestrictChars = "", int Start = 0, int Min = 0,
            //                  int Max = int.MaxValue)
            // The default-value parameter is called "Start", not "Default" -- guessing that name
            // produces a misleading "no overload" error.
            int? asked = Popup.AskNumber(
                Message: message,
                RestrictChars: "0123456789",
                Start: initial,
                Min: 1,
                Max: Ceiling);

            return asked.HasValue ? asked.Value : 0;
        }
    }

    // =====================================================================================
    // 天生技能 3 -- 雷动
    // =====================================================================================
    [Serializable]
    public class A2Raine_Toncihana_ThunderStep : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_ThunderStep";

        public override string AbilityName
        {
            get { return "Thunder Step"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "Spend " + ChargeSpendPercent + "% of your maximum charge to strike any square "
                    + "you can see and then trade places with whatever stands there. The discharge "
                    + "deals 1d" + DamageDieSize + " per " + ARaine_Arc.DischargeChunk
                    + " charge spent. Cooldown 30 rounds, reduced by Willpower like any other ability.";
            }
        }

        public override Renderable AbilityIcon
        {
            get
            {
                return new Renderable("Mutations/electrical_generation.bmp", ColorString: "c", DetailColor: 'C');
            }
        }

        /// <summary>Percentage of MAXIMUM charge spent per use.</summary>
        public int ChargeSpendPercent = 20;

        /// <summary>
        /// Aiming range in squares, set large deliberately: the ability reaches anywhere the player
        /// can SEE, and line of sight is what actually limits it.
        /// </summary>
        public int MaxTargetRange = 999;

        /// <summary>
        /// DEAD FIELD -- kept purely to hold this class's serialized layout still.
        ///
        /// It used to divide the character level into damage dice. That term is gone, but the field
        /// cannot be deleted and cannot be moved: serialization walks the fields in DECLARATION
        /// ORDER, so removing it or reordering it would shift DamageDieSize and Cooldown into the
        /// wrong slots and make every existing save read the wrong bytes. Fields in a released part
        /// are frozen; only appending below them is safe. Do not "clean this up".
        /// </summary>
        public int LevelsPerDamageDie = 5;

        /// <summary>
        /// Damage dice, from SPENT CHARGE ONLY:
        ///     dice = charge spent / DischargeChunk, minimum 1, rolled as d DamageDieSize.
        ///
        /// The character-level term was removed on request. It made the ability scale on two axes at
        /// once, and because the charge term is a percentage of a maximum that itself grows with
        /// level and mutation level, the two compounded into quadratic growth.
        ///
        /// ARaine_Arc.ScaledDamageRoll is deliberately NOT used here: it always adds a level term
        /// (its LevelsPerLevel argument floors at 1), so a charge-only roll has to be built by hand
        /// from the same shared DischargeChunk so the two cannot drift apart.
        /// </summary>
        public int DamageDieSize = 4;

        public string GetDamageRoll(int ChargeSpent)
        {
            int dice = Math.Max(1, ARaine_Arc.ChunksFor(ChargeSpent));
            return ARaine_Arc.DiceRoll(dice, DamageDieSize);
        }

        /// <summary>
        /// Voltage handed to the engine's discharge when arriving, and voltage is what drives the
        /// number of visible arcs. Fixed at 2 rather than scaled: the old formula
        /// (character level / LevelsPerDamageDie + charge chunks) reached the mid-twenties at high
        /// level and buried the screen in arcs. Damage is unaffected -- it comes from GetDamageRoll,
        /// which is passed as a separate argument.
        ///
        /// A CONST, not a field, and it must stay one. This class's layout is shared by all four
        /// ability parts, so adding an instance field re-lays-out every one of them at once: an
        /// earlier attempt at exactly that produced four deserialization errors in a single load.
        /// A const is compiled into the call sites and never serialized, so it costs nothing.
        /// </summary>
        public const int LandingArcs = 2;

        /// <summary>
        /// Cooldown, and the value IS the number of rounds the game reports (measured, see the note
        /// on A2Raine_Toncihana_ThunderFire.Cooldown). 30 here gives the 30 rounds this ability advertises
        /// in its description. Willpower still reduces it.
        /// </summary>
        public int Cooldown = 30;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                PerformThunderStep();
            }
            return base.HandleEvent(E);
        }

        private void PerformThunderStep()
        {
            GameObject actor = ParentObject;

            int max = ARaine_Charge.GetMaxCharge(actor);
            if (max <= 0)
            {
                actor.Fail("You have no charge to step with.");
                return;
            }

            int cost = Math.Max(1, max * ChargeSpendPercent / 100);
            if (ARaine_Charge.GetCharge(actor) < cost)
            {
                actor.Fail("You need {{C|" + cost + "}} charge to move that way.");
                return;
            }

            Cell here = actor.CurrentCell;
            if (here == null)
            {
                return;
            }

            // (MaxRange, AllowVis, Filter, IgnoreSolid, IgnoreLOS, RequireCombat, BlackoutStops,
            //  Actor, User, Label, Snap, AutoTarget)
            List<Cell> line = PickLine(
                MaxTargetRange + 1,
                AllowVis.OnlyVisible,
                (Predicate<GameObject>)null,
                false,
                false,
                false,
                false,
                ParentObject,
                null,
                "Thunder Step",
                true,
                false);

            if (line == null || line.Count < 2)
            {
                return;
            }

            // First entry is the character's own square; last is the chosen destination.
            Cell destination = line[line.Count - 1];
            if (destination == null || destination == here)
            {
                return;
            }

            if (!ARaine_Charge.TryUseCharge(actor, cost))
            {
                return;
            }

            string damageRoll = GetDamageRoll(cost);

            actor.UseEnergy(1000, "Skill Thunder Step");

            // Vanilla's own phase/teleport sound. "sfx_ability_mutation_teleport" does NOT exist in
            // this build; "sfx_ability_mutation_phase" does and is what the game's own phasing and
            // teleport effects use.
            actor.PlayWorldSound("Sounds/Abilities/sfx_ability_mutation_phase");

            // The engine's own discharge is the ONLY damage source here: it plays the real electrical
            // arc and applies its damage, so calling TakeDamage as well would hit twice.
            List<GameObject> occupants = destination.GetObjectsInCell()
                .FindAll(o => o != null && o != actor);

            foreach (GameObject occupant in occupants)
            {
                // Voltage drives the ENGINE's own discharge, which is where the visible arcs come
                // from. It is now a flat LandingArcs (2) instead of scaling with level and charge --
                // vanilla scales arcs with the charge released ("up to 1 target per DischargeChunk
                // charge", DischargeChunk = 1000), and the scaled figure reached the mid-twenties and
                // covered the screen. Damage is untouched: it comes from damageRoll below.
                ARaine_Arc.Discharge(actor, occupant, LandingArcs, damageRoll);
            }

            ARaine_Charge.Message(actor,
                "{{C|You become the current and arrive in a crack of thunder, trailing "
                + damageRoll + " discharge.}}");

            // Then trade places: TeleportTo moves us, and anything still alive there is pushed back
            // to the square we just left.
            Cell origin = here;
            actor.TeleportTo(destination, 0);

            foreach (GameObject occupant in occupants)
            {
                if (occupant != null && !occupant.IsInvalid() && occupant.CurrentCell == destination)
                {
                    occupant.TeleportTo(origin, 0);
                }
            }

            CooldownMyActivatedAbility(AbilityID, Cooldown);
        }
    }

    // =====================================================================================
    // 天生技能 4 -- 雷蛇
    // =====================================================================================
    [Serializable]
    public class A2Raine_Toncihana_LightningSnake : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_LightningSnake";

        public override string AbilityName
        {
            get { return "Lightning Snake"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "Spend " + ChargeCost + " charge to loose serpents of electricity into the "
                    + "adjacent square in a chosen direction, each dealing "
                    + GetArcCount() + "d" + DamageDieSize + " electric damage. Both the number of "
                    + "serpents and their damage grow with your character level, and the extra "
                    + "serpents leap onward to nearby targets.";
            }
        }

        public override Renderable AbilityIcon
        {
            get
            {
                return new Renderable("Mutations/electrical_generation.bmp", ColorString: "c", DetailColor: 'c');
            }
        }

        /// <summary>Flat charge cost per use. One use looses GetArcCount() arcs.</summary>
        public int ChargeCost = 1000;

        /// <summary>
        /// Hard ceiling on the number of serpents. The level formula kept climbing (11 arcs at
        /// character level 50), which is a lot of simultaneous arcs to draw, resolve and read on
        /// screen for an ability whose whole damage output is 1 point per arc. Capped at 6 on request.
        /// </summary>
        public int MaxArcs = 6;

        /// <summary>
        /// Damage is (character level / LevelsPerDamageDie + 1) d DieSize, and the number of arcs is
        /// the same figure clamped to MaxArcs, so the ability scales on one axis: level.
        ///   level 1 -> 1d1, x1 arc;  level 5 -> 2d1, x2 arcs;  level 10 -> 3d1, x3 arcs
        ///   level 25 -> 6d1, x6 arcs;  level 50 -> 6d1, x6 arcs (arcs capped, damage not)
        /// </summary>
        public int LevelsPerDamageDie = 5;
        public int DamageDieSize = 1;

        public string GetDamageRoll()
        {
            // No charge term: the flat cost buys the arcs, and the level buys the dice.
            return ARaine_Arc.ScaledDamageRoll(CharLevel, 0, LevelsPerDamageDie, 1, DamageDieSize);
        }

        public int GetArcCount()
        {
            int raw = Math.Max(1, CharLevel / Math.Max(1, LevelsPerDamageDie) + 1);
            return Math.Min(Math.Max(1, MaxArcs), raw);
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                PerformLightningSnake();
            }
            return base.HandleEvent(E);
        }

        private void PerformLightningSnake()
        {
            GameObject actor = ParentObject;
            Cell here = actor.CurrentCell;
            if (here == null)
            {
                return;
            }

            if (ARaine_Charge.GetCharge(actor) < ChargeCost)
            {
                actor.Fail("You need {{C|" + ChargeCost + "}} charge to loose the arc.");
                return;
            }

            // PickDirection(string) returns the chosen adjacent CELL, not a direction string.
            Cell directionCell = PickDirection("Loose the arc in which direction?") as Cell;
            if (directionCell == null)
            {
                return;
            }

            Zone zone = here.ParentZone as Zone;
            if (zone == null)
            {
                return;
            }

            int dx = directionCell.X - here.X;
            int dy = directionCell.Y - here.Y;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            Cell target = zone.GetCell(here.X + dx, here.Y + dy);
            if (target == null)
            {
                actor.Fail("There is nothing that way.");
                return;
            }

            string direction = DirectionName(dx, dy);

            if (!ARaine_Charge.TryUseCharge(actor, ChargeCost))
            {
                return;
            }

            actor.UseEnergy(1000, "Skill Lightning Snake");
            actor.PlayWorldSound("Sounds/Abilities/sfx_ability_mutation_electrical_generation");

            List<GameObject> occupants = target.GetObjectsInCell()
                .FindAll(o => o != null && o != actor);

            string damageRoll = GetDamageRoll();
            int arcs = GetArcCount();
            int voltage = Math.Max(1, arcs);
            int struck;

            if (occupants.Count == 0)
            {
                // An arc loosed at empty air still seeks outward, so it can catch something nearby.
                struck = ARaine_Arc.DischargeSpread(actor, target, null, arcs, voltage, damageRoll);
            }
            else
            {
                struck = ARaine_Arc.DischargeSpread(actor, target, occupants[0], arcs, voltage, damageRoll);

                // Anything else sharing that square is hit too, so a pile of creatures cannot hide
                // behind the first one.
                for (int i = 1; i < occupants.Count; i++)
                {
                    if (ARaine_Arc.Discharge(actor, occupants[i], voltage, damageRoll))
                    {
                        struck++;
                    }
                }
            }

            if (struck == 0)
            {
                ARaine_Charge.Message(actor,
                    "{{C|A serpent of current lashes " + direction
                    + " and grounds itself in the empty dark.}}");
            }
            else
            {
                ARaine_Charge.Message(actor,
                    "{{C|" + arcs + " serpent" + (arcs == 1 ? "" : "s") + " of current lash "
                    + direction + ", each for " + damageRoll + "; "
                    + struck + " target" + (struck == 1 ? "" : "s") + " struck.}}");
            }
        }

        /// <summary>Compass name for a one-square step, for the message text only.</summary>
        private static string DirectionName(int dx, int dy)
        {
            string name = string.Empty;
            if (dy > 0)
            {
                name += "N";
            }
            else if (dy < 0)
            {
                name += "S";
            }
            if (dx > 0)
            {
                name += "E";
            }
            else if (dx < 0)
            {
                name += "W";
            }
            return name.Length == 0 ? "outward" : name;
        }
    }

    // =====================================================================================
    // 天生技能 5 -- 雷扇 / Thunder Breath
    //
    // Built to behave like vanilla's breath mutations (PoisonBreather, FlameBreather, ...) in every
    // way that shows on screen, except that what comes out is current rather than gas.
    //
    // VANILLA'S BREATH FLOW, read out of BreatherBase.Cast, which this copies step for step:
    //
    //     XRLCore.Core.RenderMapToBuffer();                 // paints the map
    //     PickCone(GetConeLength(-1), GetConeAngle(-1),
    //              AllowVis.OnlyVisible, GetCommandDisplayName());   // <-- the cone PREVIEW
    //     Popup.ShowYesNoCancel("Breathe ...?");            // confirm
    //     UseEnergy(1000);
    //     CooldownMyActivatedAbility(..., GetCooldownTurns(Level));
    //     ... then BreatheInCell once per cell of the cone
    //
    // The preview is PickCone's own reticle -- that is where breath's fan-shaped highlight comes
    // from, and it is why this ability uses PickCone rather than any other picker.
    //
    // WHAT IS DELIBERATELY NOT COPIED: BreatherBase spawns a Gas object per cell and lets the gas do
    // the work. Electricity is not a gas, so this applies typed damage per cell instead. That also
    // means the damage does NOT spread: nothing here touches Physics.ApplyDischarge, which is the
    // only routine vanilla uses to make a discharge arc outward.
    // =====================================================================================
    [Serializable]
    public class A2Raine_Toncihana_ThunderBreath : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_ThunderBreath";

        public override string AbilityName
        {
            get { return "Thunder Breath"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "Exhale a fan of current " + ConeLength + " squares long and " + ConeAngle
                    + " degrees wide. Costs " + ChargeSpendPercent
                    + "% of your maximum charge and deals 1d" + DamageDieSize + " electric damage per "
                    + ChargePerDie + " charge spent, to everything the fan touches. The current does "
                    + "not arc onward.";
            }
        }

        public override Renderable AbilityIcon
        {
            get
            {
                return new Renderable("Mutations/electrical_generation.bmp", ColorString: "c", DetailColor: 'C');
            }
        }

        /// <summary>
        /// Fan length in squares, matching how BreatherBase.GetConeLength works.
        /// Raised from 3 to 5 on request: at length 3 the fan read as a stubby wedge rather than a
        /// cone, because the width is measured in degrees and 90 degrees over only 3 squares barely
        /// spreads at all.
        /// </summary>
        public const int ConeLength = 5;

        /// <summary>
        /// Fan width in degrees, matching how BreatherBase.GetConeAngle works.
        /// Narrowed from 90 to 60 on request, to trade width for reach and make the shape read as a
        /// cone instead of a wedge.
        /// </summary>
        public const int ConeAngle = 60;

        /// <summary>Percentage of MAXIMUM charge spent per use.</summary>
        public const int ChargeSpendPercent = 20;

        /// <summary>Charge per damage die: 500 charge buys one d4.</summary>
        public const int ChargePerDie = 500;

        public const int DamageDieSize = 4;

        /// <summary>
        /// Rounds of cooldown. ZERO, on request: the ability is meant to be usable as often as the
        /// charge allows, so the charge cost is the only limiter. Passing 0 to
        /// CooldownMyActivatedAbility is skipped entirely rather than called with 0, so the ability
        /// never enters cooldown at all.
        /// </summary>
        public const int Cooldown = 0;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                Cast();
            }
            return base.HandleEvent(E);
        }

        private void Cast()
        {
            GameObject actor = ParentObject;
            if (actor == null || actor.CurrentCell == null)
            {
                return;
            }

            int max = ARaine_Charge.GetMaxCharge(actor);
            if (max <= 0)
            {
                actor.Fail("You have no charge to exhale.");
                return;
            }

            int cost = Math.Max(1, max * ChargeSpendPercent / 100);
            if (ARaine_Charge.GetCharge(actor) < cost)
            {
                actor.Fail("Your charge is too thin to shape a thunderhead.");
                return;
            }

            // Show the fan. PickCone is where breath's cone-shaped PREVIEW comes from, which is the
            // whole reason to use it rather than any other picker.
            //
            // Its real signature came from the compiler: PickCone(int Length, int Angle, AllowVis,
            // Predicate<GameObject> Filter, string Label). The signature dump lists only four
            // parameters and leaves out the predicate, so it cannot be used to write this call.
            //
            // Vanilla's Cast opens with XRLCore.Core.RenderMapToBuffer(GetScrapBuffer1(...)) to paint
            // the map under the cone. That is left out here deliberately: the picker renders what it
            // needs, so pre-painting only adds a way to get arguments wrong. If the preview ever comes
            // up blank, add it back as
            //     XRL.Core.XRLCore.Core.RenderMapToBuffer(ConsoleLib.Console.ScreenBuffer.GetScrapBuffer1(false));
            List<Cell> cells = PickCone(
                ConeLength,
                ConeAngle,
                AllowVis.OnlyVisible,
                (Predicate<GameObject>)null,
                AbilityName);
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            // NO second confirmation here, deliberately. PickCone already ends with its own
            // yes/no prompt -- that is the cone tool the breath mutations use, and it confirms on
            // its own. Adding ShowYesNoCancel after it made the player answer twice for one
            // activation. (Vanilla's BreatherBase.Cast does show one, but it does NOT use the
            // prompting form of PickCone first; the two together are what double up.)

            if (!ARaine_Charge.TryUseCharge(actor, cost))
            {
                actor.Fail("Your charge slips away before you can exhale it.");
                return;
            }

            // Spend the turn. No cooldown: Cooldown is 0, and CooldownMyActivatedAbility is skipped
            // rather than called with 0 so the ability never enters cooldown at all. The charge cost
            // is the only limiter.
            actor.UseEnergy(1000, "Skill Thunder Breath");
            actor.PlayWorldSound("Sounds/Abilities/sfx_ability_gas_breathe");

            // One damage roll for the whole fan, applied to each thing it catches, mirroring how
            // vanilla's own area effects roll once and let each target's resistances apply.
            int dice = Math.Max(1, cost / Math.Max(1, ChargePerDie));
            int damage = Stat.Roll(ARaine_Arc.DiceRoll(dice, DamageDieSize));
            HashSet<GameObject> already = new HashSet<GameObject>();

            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }
                // The fan's apex is the actor's own square; never hit yourself with your own breath.
                if (cell == actor.CurrentCell)
                {
                    continue;
                }

                // Draw the arc BEFORE the damage, so the bolt is visible even on a cell whose only
                // occupant is about to be destroyed.
                ARaine_Arc.ZapLine(actor, cell);

                foreach (GameObject obj in cell.GetObjectsInCell())
                {
                    if (obj == null || obj == actor || already.Contains(obj))
                    {
                        continue;
                    }
                    // ANYTHING that can be hurt, not just creatures. Electricity does not care
                    // whether its target has a mind: plants, walls and other destructibles all take
                    // it, which is what was asked for and what a breath of current should do.
                    // HasHitpoints is the engine's own test, and TakeDamage itself no-ops on
                    // anything without hitpoints, so this is safe on inert scenery.
                    if (!obj.HasHitpoints())
                    {
                        continue;
                    }
                    already.Add(obj);
                    // "Electric" is the attribute string the damage pipeline reads, so the target's
                    // own ElectricResistance and any ElectricImmunity apply normally.
                    obj.TakeDamage(damage, "from a fan of current!", "Electric",
                        Owner: actor, Phase: actor.GetPhase());
                }
            }

            // Report the breath and its damage. Deliberately says nothing about WHAT was hit: the
            // fan catches walls and plants as readily as creatures, so counting "creatures" was both
            // wrong and beside the point. It states the roll and the damage instead.
            ARaine_Charge.Message(actor,
                "{{C|The air splits with a rolling peal of thunder, and the fan bites for "
                + damage + ".}}");
        }
    }
}
