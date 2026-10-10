using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace XRL.World.Parts
{
    /// <summary>
    /// Devour: swallow a creature whole, digest it a little each turn, and grow from what dies
    /// inside you.
    ///
    /// BUILT ON VANILLA'S SWALLOWING, NOT A COPY OF IT
    /// ----------------------------------------------
    /// The bearer also carries the vanilla Engulfing part (XRL/World/Parts/Engulfing.cs), which is
    /// what actually holds a creature: its Engulf(GameObject, Event) :465 does the size, movement
    /// mode and phase checks and applies the Engulfed effect, and Engulfed / CheckEngulfed :656
    /// expose the current occupant. Initialize below even lets vanilla keep the ability -- it is
    /// re-registered under this mod's name, not duplicated.
    ///
    /// This part supplies what vanilla does not:
    ///
    ///   1. the per-turn damage, scaled by the bearer's own maximum hit points,
    ///   2. the tally of what has been swallowed and killed, and
    ///   3. the growth that comes of it.
    ///
    /// THE DAMAGE
    /// ----------
    /// Vanilla's EngulfingDamage (EngulfingDamage.cs:24-41) registers "EndTurnEngulfing" and rolls a
    /// fixed string Amount ("1-6" by default) at whatever object the event carries. The damage here
    /// hangs on the same event and only the number changes: it derives from the bearer's maximum hit
    /// points, so growing tougher makes Devour bite harder.
    ///
    /// The curve is Playable Slime's (3388408799/LegacyClasses/Sym_ConsumeTracked.cs:187-206), which
    /// caps engulf damage with a base-2 logarithm:
    ///
    ///     x = log2(value) * 3        dice = x / 4        damage = dice d4
    ///
    /// A logarithm is deliberate: the bearer's hit points grow fast, and a linear term would outrun
    /// every other number in the mod.
    ///
    /// WHICH COUNT IS WHICH
    /// --------------------
    /// Two separate tallies per kind, the way Becoming keeps LearnedKinds and KillCounts apart
    /// (MachineLearning.cs:32/39) and prints them side by side (:181):
    ///
    ///   SWALLOWED  a creature that died WHILE inside the bearer -- one still carrying the Engulfed
    ///              effect at the moment of death. Playable Slime makes the same test the same way
    ///              (Sym_ConsumeTracked.cs:75). Worth PROGRESS_SWALLOWED percent.
    ///   KILLED     anything else the bearer killed. Worth PROGRESS_KILLED percent.
    ///
    /// A creature that dies inside counts as swallowed ONLY -- it does not also score as a kill.
    ///
    /// KilledEvent does NOT cascade (this is called out in Becoming's MachineLearningTracker.cs:10):
    /// it fires only the killer's own FireEvent/HandleEvent, so a part that wants it must opt into
    /// static registration, which AllowStaticRegistration below does.
    ///
    /// GROWTH
    /// ------
    /// Each kind tracks its own progress, 0 to PROGRESS_FULL percent. When a kind reaches full it is
    /// marked complete and grants ONE point of rapid-advance progress -- and because it is now
    /// complete it can never grant another, so each kind is worth at most one point no matter how
    /// many of them are eaten. Completing PROGRESS_PER_ADVANCE kinds triggers one rapid advance, by
    /// calling vanilla's own Leveler.RapidAdvancement(3, ParentObject) (Leveler.cs:315-354) -- the
    /// same static the game itself calls when a mutant levels past 5, 15, 25. It already handles
    /// choosing which mutation to advance and the RapidLevel call, so there is no reason to
    /// reimplement it.
    /// </summary>
    public class A2Raine_Horndycibar_Devour : IPart
    {
        public const string TASTE_COMMAND = "CommandA2Raine_Horndycibar_AcquiredTaste";

        /// <summary>Progress a kind gains when one of it dies inside the bearer.</summary>
        public const int PROGRESS_SWALLOWED = 20;

        /// <summary>Progress a kind gains when one of it is killed any other way.</summary>
        public const int PROGRESS_KILLED = 5;

        /// <summary>Progress at which a kind is complete and grants a point.</summary>
        public const int PROGRESS_FULL = 100;

        /// <summary>Completed kinds that trigger one rapid advance.</summary>
        public const int PROGRESS_PER_ADVANCE = 4;

        /// <summary>Completed kinds per point of Toughness gained.</summary>
        public const int COMPLETIONS_PER_TOUGHNESS = 2;

        /// <summary>Ranks added when a rapid advance fires; vanilla's own figure (Leveler.cs:270).</summary>
        public const int ADVANCE_RANKS = 3;

        public Guid TasteAbilityID = Guid.Empty;

        /// <summary>Kind -> how many of it died inside the bearer.</summary>
        public Dictionary<string, int> Swallowed = new Dictionary<string, int>();

        /// <summary>Kind -> how many of it the bearer killed from the outside.</summary>
        public Dictionary<string, int> Killed = new Dictionary<string, int>();

        /// <summary>Kind -> its growth, 0 to PROGRESS_FULL.</summary>
        public Dictionary<string, int> KindProgress = new Dictionary<string, int>();

        /// <summary>Kinds that reached PROGRESS_FULL; they grant their point only once.</summary>
        public List<string> Completed = new List<string>();

        /// <summary>Completed kinds banked toward the next rapid advance.</summary>
        public int AdvanceProgress;

        public override void Initialize()
        {
            base.Initialize();

            // Vanilla's Engulfing adds an ability of its own, named "Engulf". This mod calls that
            // act Devour, so the ability it created is taken back and one carrying the SAME command
            // put in its place -- reusing Engulfing's own COMMAND_NAME means the key still runs
            // Engulfing's target picker and its Engulf() checks, only the wording is ours. There is
            // deliberately no second, separate swallow ability.
            Engulfing engulfing = ParentObject.GetPart<Engulfing>();
            if (engulfing != null)
            {
                RemoveMyActivatedAbility(ref engulfing.ActivatedAbilityID);
                engulfing.ActivatedAbilityID = AddMyActivatedAbility(
                    "Devour", Engulfing.COMMAND_NAME, "Skill",
                    "Take a living thing into yourself, and let it feed what you are becoming.");
            }

            TasteAbilityID = AddMyActivatedAbility("An Acquired Taste", TASTE_COMMAND, "Skill",
                "Look inward at every life you have taken in.");
        }

        public override void Remove()
        {
            RemoveMyActivatedAbility(ref TasteAbilityID);
            base.Remove();
        }

        /// <summary>
        /// KilledEvent does not cascade, so it reaches us only if we register statically -- see the
        /// class comment and Becoming's MachineLearningTracker.cs:104-119.
        /// </summary>
        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("EndTurnEngulfing");
            base.Register(Object, Registrar);
        }

        /// <summary>
        /// CommandEvent.ID has to be listed: HandleEvent(CommandEvent) is only delivered for events
        /// this part asked for, so without it the ability does nothing when pressed.
        /// </summary>
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == PooledEvent<CommandEvent>.ID
                || ID == KilledEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == TASTE_COMMAND)
            {
                ShowTally();
                return false;
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(KilledEvent E)
        {
            if (E.Dying != null)
            {
                Record(E.Dying);
            }

            return base.HandleEvent(E);
        }

        /// <summary>
        /// Per-turn digestion. Mirrors EngulfingDamage's event (EngulfingDamage.cs:24-41) but derives
        /// the dice from the bearer's maximum hit points instead of a fixed string.
        /// </summary>
        public override bool FireEvent(Event E)
        {
            if (E.ID == "EndTurnEngulfing")
            {
                GameObject prey = E.GetParameter<GameObject>("Object");
                if (prey != null)
                {
                    int dice = DiceFor(BearerHitPoints());
                    Damage damage = new Damage(Stat.Roll(dice + "d4"));
                    Event hurt = Event.New("TakeDamage");
                    hurt.AddParameter("Damage", damage);
                    hurt.AddParameter("Owner", ParentObject);
                    hurt.AddParameter("Attacker", ParentObject);
                    hurt.AddParameter("Message", "as your body makes it part of you!");
                    prey.FireEvent(hurt);
                }
            }

            return base.FireEvent(E);
        }

        private int BearerHitPoints()
        {
            if (ParentObject == null || !ParentObject.HasHitpoints())
            {
                return 1;
            }

            return Math.Max(1, ParentObject.baseHitpoints);
        }

        /// <summary>
        /// Playable Slime's capped curve (Sym_ConsumeTracked.cs:187-206) turned into dice:
        /// x = log2(hit points) * 3, and one d4 per four points of x.
        /// </summary>
        public static int DiceFor(int HitPoints)
        {
            if (HitPoints < 1)
            {
                HitPoints = 1;
            }

            double x = Math.Log(HitPoints, 2.0) * 3.0;
            int dice = (int)Math.Round(x / 4.0, MidpointRounding.AwayFromZero);

            return Math.Max(1, dice);
        }

        /// <summary>
        /// Books one death: into the swallowed tally or the killed tally, never both, and then into
        /// that kind's growth.
        ///
        /// A kind that is already complete keeps being counted -- the tally is a record, not a
        /// scoreboard -- but contributes no further growth, which is what caps each kind at one
        /// point of rapid-advance progress.
        /// </summary>
        private void Record(GameObject Prey)
        {
            // Only creatures count. KilledEvent also fires for destroyable furniture and terrain --
            // walls, doors, workbenches, plants -- and they carry Hitpoints, so without this gate a
            // session spent smashing walls fills the tally with architecture.
            //
            // IsCombatObject() is the right test: GameObject.cs:1570 reads a flag whose own
            // obsolete overload documents it as "combat flagged objects always have Brain part",
            // which is exactly "a thing that lives and can fight". A wall has no Brain, so it fails
            // here; robots and plants that DO have a Brain still pass.
            if (!Prey.IsCombatObject())
            {
                return;
            }

            string kind = KindOf(Prey);

            bool swallowed = Prey.GetEffect<Engulfed>() != null;
            if (swallowed)
            {
                Swallowed[kind] = Count(Swallowed, kind) + 1;
            }
            else
            {
                Killed[kind] = Count(Killed, kind) + 1;
            }

            UnityEngine.Debug.Log("[Toncihana] devour: " + (swallowed ? "swallowed" : "killed")
                + " " + kind + " (" + SwallowedCount(kind) + " swallowed / "
                + KilledCount(kind) + " killed).");

            if (Completed.Contains(kind))
            {
                return;
            }

            int gained = swallowed ? PROGRESS_SWALLOWED : PROGRESS_KILLED;
            int now = Count(KindProgress, kind) + gained;
            if (now > PROGRESS_FULL)
            {
                now = PROGRESS_FULL;
            }
            KindProgress[kind] = now;

            if (now < PROGRESS_FULL)
            {
                return;
            }

            Completed.Add(kind);
            AdvanceProgress += 1;

            // Every COMPLETIONS_PER_TOUGHNESS completed kinds add a point of Toughness. Setting the
            // stat's BaseValue is what the engine watches: Leveler.cs:39-64 handles StatChangeEvent
            // and carries the hit-point change through on its own, including the per-level term, so
            // nothing else has to be adjusted here.
            if (Completed.Count % COMPLETIONS_PER_TOUGHNESS == 0)
            {
                ParentObject.GetStat("Toughness").BaseValue += 1;

                if (ParentObject.IsPlayer())
                {
                    IComponent<GameObject>.AddPlayerMessage("{{W|Two more lives taken in. "
                        + "Your body answers and grows harder to end. ({{C|+1 Toughness}})}}");
                }

                UnityEngine.Debug.Log("[Toncihana] devour: " + Completed.Count
                    + " kinds complete, Toughness +1.");
            }

            if (ParentObject.IsPlayer())
            {
                IComponent<GameObject>.AddPlayerMessage("{{W|" + kind
                    + " has become part of you.}}");
            }

            while (AdvanceProgress >= PROGRESS_PER_ADVANCE)
            {
                AdvanceProgress -= PROGRESS_PER_ADVANCE;
                UnityEngine.Debug.Log("[Toncihana] devour: " + PROGRESS_PER_ADVANCE
                    + " kinds complete, calling Leveler.RapidAdvancement(" + ADVANCE_RANKS + ").");
                Leveler.RapidAdvancement(ADVANCE_RANKS, ParentObject);
            }
        }

        /// <summary>
        /// The KIND a creature is counted as.
        ///
        /// The blueprint name is the unit: "Snapjaw Hunter" and "Snapjaw Scavenger" are two kinds,
        /// which is the granularity wanted here. But vanilla also spawns kit variants under a
        /// numbered name -- "Snapjaw Hunter 0", "... 1", "... 2" -- and 34 blueprints in
        /// Base/Creatures.xml are named that way. Counting those separately would split one kind into
        /// three, so a trailing pure-number word is dropped and the variants share their parent's
        /// kind. Only a SEPARATE final word that is all digits is stripped, so a name with digits in
        /// it survives intact.
        ///
        /// This is deliberately NOT Becoming's rule: its GetLearnableKind (MachineLearning.cs:231-247)
        /// walks up Inherits until it leaves the Base* tier, which would fold every Snapjaw -- Hunter,
        /// Scavenger, Brute and all -- into a single kind, coarser than what is wanted here.
        /// </summary>
        private static string KindOf(GameObject Prey)
        {
            string kind = Prey.Blueprint;
            if (string.IsNullOrEmpty(kind))
            {
                kind = Prey.ShortDisplayName;
            }

            if (string.IsNullOrEmpty(kind))
            {
                return kind;
            }

            int space = kind.LastIndexOf(' ');
            if (space <= 0 || space == kind.Length - 1)
            {
                return kind;
            }

            string tail = kind.Substring(space + 1);
            for (int i = 0; i < tail.Length; i++)
            {
                if (!char.IsDigit(tail[i]))
                {
                    return kind;
                }
            }

            return kind.Substring(0, space);
        }

        private static int Count(Dictionary<string, int> Table, string Kind)
        {
            int value;
            return Table.TryGetValue(Kind, out value) ? value : 0;
        }

        private int SwallowedCount(string Kind)
        {
            return Count(Swallowed, Kind);
        }

        private int KilledCount(string Kind)
        {
            return Count(Killed, Kind);
        }

        /// <summary>
        /// The tally screen. Every kind is listed with its two counts kept apart and its growth as a
        /// percentage -- the same "kind, count, how far along" shape Becoming prints
        /// (MachineLearning.cs:181), applied to this mod's numbers.
        /// </summary>
        private void ShowTally()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{{W|An Acquired Taste}}\n\n");

            List<string> kinds = new List<string>();
            foreach (string kind in Swallowed.Keys)
            {
                if (!kinds.Contains(kind))
                {
                    kinds.Add(kind);
                }
            }
            foreach (string kind in Killed.Keys)
            {
                if (!kinds.Contains(kind))
                {
                    kinds.Add(kind);
                }
            }
            kinds.Sort();

            if (kinds.Count == 0)
            {
                sb.Append("You have taken nothing into yourself yet.\n");
            }
            else
            {
                foreach (string kind in kinds)
                {
                    int percent = Count(KindProgress, kind);
                    sb.Append(kind)
                      .Append("  {{C|taken in ").Append(SwallowedCount(kind))
                      .Append("}}  {{R|felled ").Append(KilledCount(kind)).Append("}}  ");

                    if (Completed.Contains(kind))
                    {
                        sb.Append("{{G|wholly yours}}");
                    }
                    else
                    {
                        sb.Append("{{W|").Append(percent).Append("%}}");
                    }

                    sb.Append("\n");
                }
            }

            sb.Append("\n{{W|Growth}} ").Append(AdvanceProgress)
              .Append(" / ").Append(PROGRESS_PER_ADVANCE)
              .Append("   {{K|lives wholly yours: ").Append(Completed.Count).Append("}}");

            Popup.Show(sb.ToString());
        }
    }
}
