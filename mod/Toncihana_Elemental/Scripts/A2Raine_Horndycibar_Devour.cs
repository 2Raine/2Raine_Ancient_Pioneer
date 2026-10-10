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
    /// expose the current occupant. This part only supplies the three things vanilla does not:
    ///
    ///   1. a Devour command that picks a target and hands it to Engulfing.Engulf,
    ///   2. the per-turn damage, scaled by the bearer's own maximum hit points, and
    ///   3. the tally of what has died inside, and the growth that comes of it.
    ///
    /// THE DAMAGE
    /// ----------
    /// Vanilla's EngulfingDamage (EngulfingDamage.cs:24-41) registers "EndTurnEngulfing" and rolls a
    /// fixed string Amount ("1-6" by default) at whatever object the event carries. The DAMAGE HERE
    /// therefore hangs on the same event, and only the number changes: it is derived from the
    /// bearer's maximum hit points, so growing tougher makes Devour bite harder -- which is the
    /// point of the reward loop this is meant to feed.
    ///
    /// The curve is Playable Slime's (3388408799/LegacyClasses/Sym_ConsumeTracked.cs:187-206),
    /// which caps engulf damage with a base-2 logarithm:
    ///
    ///     x = log2(value) * 3        dice = x / 4        damage = dice d4
    ///
    /// A logarithm is deliberate: the bearer's hit points grow fast, and a linear damage term would
    /// outrun every other number in the mod. At 100 HP this is 5d4, at 400 HP 7d4 -- it keeps
    /// rising, but it never explodes.
    ///
    /// WHAT COUNTS AS A MEAL
    /// ---------------------
    /// A creature that dies WHILE inside the bearer, i.e. one that still carries the Engulfed
    /// effect at the moment of death. Playable Slime makes the same test the same way
    /// (Sym_ConsumeTracked.cs:75, consumeTarget.GetEffect&lt;Engulfed&gt;()).
    ///
    /// KilledEvent does NOT cascade (this is called out in Becoming's MachineLearningTracker.cs:10):
    /// it fires only the killer's own FireEvent/HandleEvent, so a part that wants it must opt into
    /// static registration, which AllowStaticRegistration :71 below does.
    ///
    /// GROWTH
    /// ------
    /// Every 10 kills OF THE SAME KIND add 1 point of progress (the tally is per blueprint, so a
    /// hundred snapjaws count as a hundred, ten snapjaws and ten crabs as one point each). Four
    /// points of progress trigger one rapid advance, by calling vanilla's own
    /// Leveler.RapidAdvancement(3, ParentObject) (Leveler.cs:315-354) -- the same static the game
    /// itself calls when a mutant levels past 5, 15, 25. It already handles choosing which mutation
    /// to advance and the RapidLevel call, so there is no reason to reimplement it.
    /// </summary>
    public class A2Raine_Horndycibar_Devour : IPart
    {
        public const string DEVOUR_COMMAND = "CommandA2Raine_Horndycibar_Devour";
        public const string TASTE_COMMAND = "CommandA2Raine_Horndycibar_AcquiredTaste";

        /// <summary>Kills of one kind that add a point of progress.</summary>
        public const int KILLS_PER_PROGRESS = 10;

        /// <summary>Points of progress that trigger one rapid advance.</summary>
        public const int PROGRESS_PER_ADVANCE = 4;

        /// <summary>Ranks added when a rapid advance fires; vanilla's own figure (Leveler.cs:270).</summary>
        public const int ADVANCE_RANKS = 3;

        public Guid DevourAbilityID = Guid.Empty;
        public Guid TasteAbilityID = Guid.Empty;

        /// <summary>Blueprint name of each kind swallowed -> how many of it have died inside.</summary>
        public Dictionary<string, int> Swallowed = new Dictionary<string, int>();

        public int Progress;

        public override void Initialize()
        {
            DevourAbilityID = AddMyActivatedAbility("Devour", DEVOUR_COMMAND, "Skill",
                "Swallow a nearby creature and digest it.");
            TasteAbilityID = AddMyActivatedAbility("An Acquired Taste", TASTE_COMMAND, "Skill",
                "Recall everything that has died inside you.");
            base.Initialize();
        }

        public override void Remove()
        {
            RemoveMyActivatedAbility(ref DevourAbilityID);
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

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == KilledEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == DEVOUR_COMMAND)
            {
                Devour();
                return false;
            }

            if (E.Command == TASTE_COMMAND)
            {
                ShowTally();
                return false;
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(KilledEvent E)
        {
            if (E.Dying != null && E.Dying.GetEffect<Engulfed>() != null)
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
                    hurt.AddParameter("Message", "from %t digestive enzymes!");
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

        private void Devour()
        {
            if (ParentObject == null || ParentObject.CurrentCell == null)
            {
                return;
            }

            Engulfing engulfing = ParentObject.GetPart<Engulfing>();
            if (engulfing == null)
            {
                Popup.ShowFail("You have nothing to swallow with.");
                return;
            }

            List<GameObject> candidates = new List<GameObject>();
            foreach (Cell cell in ParentObject.CurrentCell.GetAdjacentCells())
            {
                foreach (GameObject obj in cell.GetObjects())
                {
                    if (obj == null || obj == ParentObject || !obj.IsCombatObject())
                    {
                        continue;
                    }

                    candidates.Add(obj);
                }
            }

            if (candidates.Count == 0)
            {
                Popup.ShowFail("There is nothing beside you to swallow.");
                return;
            }

            string[] options = new string[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                options[i] = candidates[i].ShortDisplayName;
            }

            int pick = Popup.PickOption("Swallow what?", null, "", "Sounds/Misc/sfx_characterMod_mutation_windowPopup", options);
            if (pick < 0 || pick >= candidates.Count)
            {
                return;
            }

            GameObject target = candidates[pick];
            if (!engulfing.Engulf(target))
            {
                return;
            }

            ParentObject.UseEnergy(1000, "Physical Mutation");
            UnityEngine.Debug.Log("[Toncihana] devour: swallowed " + target.Blueprint
                + " (max hp " + BearerHitPoints() + " -> " + DiceFor(BearerHitPoints()) + "d4 per turn).");
        }

        /// <summary>Kills of one kind add a point of progress every KILLS_PER_PROGRESS of them.</summary>
        private void Record(GameObject Prey)
        {
            string kind = Prey.Blueprint;
            if (string.IsNullOrEmpty(kind))
            {
                kind = Prey.ShortDisplayName;
            }

            int seen;
            Swallowed.TryGetValue(kind, out seen);
            seen += 1;
            Swallowed[kind] = seen;

            UnityEngine.Debug.Log("[Toncihana] devour: digested " + kind + " (" + seen
                + " total).");

            if (seen % KILLS_PER_PROGRESS != 0)
            {
                return;
            }

            Progress += 1;
            if (ParentObject.IsPlayer())
            {
                IComponent<GameObject>.AddPlayerMessage("{{W|Something you swallowed settles into place.}}");
            }

            while (Progress >= PROGRESS_PER_ADVANCE)
            {
                Progress -= PROGRESS_PER_ADVANCE;
                UnityEngine.Debug.Log("[Toncihana] devour: " + Progress_PER_ADVANCE_LOG);
                Leveler.RapidAdvancement(ADVANCE_RANKS, ParentObject);
            }
        }

        private const string Progress_PER_ADVANCE_LOG =
            "growth complete, calling Leveler.RapidAdvancement.";

        /// <summary>
        /// The tally screen. Shows every kind swallowed, how many of it, and how far the next point
        /// of progress is -- expressed as a percentage the way Playable Slime does it
        /// (Sym_ConsumeTracked.cs:359-363).
        /// </summary>
        private void ShowTally()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{{W|What has died inside you}}\n\n");

            if (Swallowed.Count == 0)
            {
                sb.Append("Nothing yet.\n");
            }
            else
            {
                List<string> kinds = new List<string>(Swallowed.Keys);
                kinds.Sort();
                foreach (string kind in kinds)
                {
                    sb.Append(kind).Append("  {{C|x").Append(Swallowed[kind]).Append("}}\n");
                }
            }

            int total = 0;
            int best = 0;
            foreach (KeyValuePair<string, int> pair in Swallowed)
            {
                total += pair.Value;
                int within = pair.Value % KILLS_PER_PROGRESS;
                if (within > best)
                {
                    best = within;
                }
            }

            int percent = (int)Math.Round(best * 100.0 / KILLS_PER_PROGRESS, MidpointRounding.AwayFromZero);

            sb.Append("\n{{W|Growth}} ").Append(Progress).Append(" / ").Append(PROGRESS_PER_ADVANCE);
            sb.Append("   {{K|closest kind ").Append(percent).Append("%}}");
            sb.Append("\n{{K|total swallowed: ").Append(total).Append("}}");

            Popup.Show(sb.ToString());
        }
    }
}
