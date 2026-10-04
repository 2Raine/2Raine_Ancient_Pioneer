using System;
using System.Collections.Generic;
using XRL.Rules;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// Plays the game's own electrical arc.
    ///
    /// HISTORY -- WHAT WAS TRIED AND WHY IT FAILED
    /// -------------------------------------------
    /// 1. Hand-painted particles along a line (Cell.ParticleText). Wrong: particles are decoration,
    ///    so there was no travel, no conduction and nothing for terrain to interrupt. It also turned
    ///    out that the particle was frequently invisible.
    ///
    /// 2. A real missile, fired through a purpose-built invisible launcher
    ///    (Combat.FireMissileWeapon). This was closer to right, but it produced a spurious
    ///    "you unequipped a ranged weapon" message on every use and, in practice, did not visibly
    ///    fire or damage anything at all.
    ///
    /// 3. WHAT THIS FILE DOES NOW: call the engine's own GameObject.Discharge. This is the single
    ///    routine vanilla uses for electrical discharge -- ElectricalGeneration.Discharge and
    ///    PerformDischarge go through it, and DischargeOnHit/DischargeOnDeath/DischargeOnStep all
    ///    end up there. Calling it directly means the arc is produced by the game itself, so its
    ///    appearance, its conduction, its saves and its damage are vanilla by construction rather
    ///    than by imitation.
    ///
    /// The named-argument set below is copied from a published, working mod (ChargeBomb.cs) and was
    /// verified to compile against this exact build:
    ///     Source.Discharge(Voltage: ..., DamageRange: ..., Owner: ..., Target: ..., Accidental: ...)
    /// Note that "Cell", "Source", "Weapon", "Skip" and "Indirect" are NOT parameter names on this
    /// build -- they were each rejected by the compiler, so do not add them back.
    /// </summary>
    public static class ARaine_Arc
    {
        /// <summary>
        /// How many chunks of charge a given outlay represents, using vanilla's own chunk size.
        ///
        /// DISCHARGE_CHUNK is 1000 (read straight from the assembly's Constant table), and vanilla
        /// expresses every discharge figure in those chunks: "1d4 damage per DischargeChunk charge,
        /// up to 1 target per DischargeChunk charge". Published mods do the same arithmetic
        /// (ChargeBomb: GetDischargeDamageRoll((int)(charge / 2.5)), i.e. charge / 1000 chunks).
        /// </summary>
        public const int DischargeChunk = 1000;

        public static int ChunksFor(int ChargeSpent)
        {
            return Math.Max(0, ChargeSpent / DischargeChunk);
        }

        /// <summary>
        /// Ordinary Qud dice notation for a count of dice, e.g. "3d4". Kept here so every ability
        /// builds its damage string the same way.
        /// </summary>
        public static string DiceRoll(int Dice, int DieSize)
        {
            return Math.Max(1, Dice) + "d" + Math.Max(1, DieSize);
        }

        /// <summary>
        /// Damage dice for an electrical ability, combining character level and charge outlay:
        ///     dice = level / LevelsPerLevel + charge spent / DischargeChunk + FlatDice
        /// This is the shape the abilities were asked for: damage rises with the character's level
        /// and with how much charge is put into the strike.
        /// </summary>
        public static string ScaledDamageRoll(int CharLevel, int ChargeSpent, int LevelsPerLevel,
            int FlatDice, int DieSize)
        {
            int dice = Math.Max(1, CharLevel) / Math.Max(1, LevelsPerLevel)
                     + ChunksFor(ChargeSpent)
                     + FlatDice;
            return DiceRoll(dice, DieSize);
        }

        /// <summary>
        /// Discharge electricity into one target, exactly the way the game does it itself.
        ///
        /// Voltage drives how hard the arc hits; DamageRange is the damage dice, as a string,
        /// matching the engine's own signature. Returns true if the discharge was performed.
        /// </summary>
        public static bool Discharge(GameObject Source, GameObject Target, int Voltage, string DamageRange)
        {
            // The multi-arc twin returns the arc count; here one arc is what was asked for.
            return DischargeMany(Source, Target, Voltage, DamageRange, 1) > 0;
        }

        /// <summary>
        /// Discharge several arcs into the target, the way vanilla's own discharge scales with the
        /// amount of charge released.
        ///
        /// Vanilla's rule (ActivatedAbilities.xml:1019-1020, verbatim): "Discharges all held
        /// electrical charge for 1d4 damage per DischargeChunk charge. Discharge can arc to adjacent
        /// targets dealing reduced damage, up to 1 target per DischargeChunk charge." With
        /// DISCHARGE_CHUNK = 1000, releasing 3000 charge means 3 chunks, i.e. 3 arcs.
        ///
        /// Each arc rolls its own damage, so N arcs give N independent rolls before the target's
        /// resistances apply -- the same shape as vanilla rather than one pre-multiplied number.
        /// Returns how many arcs were actually loosed.
        /// </summary>
        public static int DischargeMany(GameObject Source, GameObject Target, int Voltage, string DamageRange, int Count)
        {
            if (Source == null || Target == null || Count <= 0)
            {
                return 0;
            }

            int loosed = 0;
            for (int i = 0; i < Count; i++)
            {
                // If the target dies part-way through, stop rather than discharging into a corpse.
                if (i > 0 && Target.IsInvalid())
                {
                    break;
                }
                if (DischargeOne(Source, Target, Voltage, DamageRange))
                {
                    loosed++;
                }
            }
            return loosed;
        }

        private static bool DischargeOne(GameObject Source, GameObject Target, int Voltage, string DamageRange)
        {
            if (Source == null || Target == null)
            {
                return false;
            }
            try
            {
                Source.Discharge(
                    Voltage: Math.Max(1, Voltage),
                    DamageRange: string.IsNullOrEmpty(DamageRange) ? "1d4" : DamageRange,
                    Owner: Source,
                    Target: Target,
                    Accidental: false);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Toncihana] discharge failed: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Spread arcs outward, one per extra arc, the way vanilla's own discharge does it.
        ///
        /// The vanilla rule, read from ElectricalGeneration's own ability text
        /// (ActivatedAbilities.xml:1020): "Discharge can arc to adjacent targets dealing reduced
        /// damage, up to 1 target per DischargeChunk charge."
        ///
        /// Vanilla picks those targets with Cell.GetCombatTarget, which is the engine's own "who in
        /// this square is a legitimate combat target for me" test -- the IL of PerformDischarge calls
        /// both GetCombatTarget and PickDirection, and the published ChargeBomb mod does the same
        /// thing in the same order:
        ///
        ///     List&lt;Cell&gt; AdjacentCells = parameter.GetLocalAdjacentCells();
        ///     AdjacentCells.Add(parameter);
        ///     foreach (Cell cell in AdjacentCells) ShockTargets.Add(cell.GetCombatTarget());
        ///
        /// Reusing GetCombatTarget rather than testing hostility by hand means the arc follows the
        /// game's own idea of a valid target (it will not zap the player's own allies, and it
        /// respects the same filters every other electrical effect uses).
        ///
        /// This differs from vanilla in ONE deliberate way: the extra arcs are NOT damage-divided.
        /// ChargeBomb divides voltage by the target count; here each arc rolls its own damage, which
        /// is what was asked for -- more arcs means more total damage, not the same damage spread out.
        ///
        /// The SEARCH RADIUS is derived from the arc count rather than being a fixed constant, so a
        /// stronger character's arcs genuinely reach further instead of all crowding into the same
        /// few squares:
        ///     radius = Arcs + RadiusBonusPerArc, capped at MaxSpreadRadius
        /// so 1 arc looks only at the squares touching the target, 2 arcs reach two rings out, and so
        /// on. The cap exists because GetLocalAdjacentCells builds a list for the whole ring, and an
        /// uncapped radius could iterate a very large area on a high-level character.
        ///
        /// Returns the number of targets actually struck.
        /// </summary>
        public static int DischargeSpread(GameObject Source, Cell Origin, GameObject Primary,
            int Arcs, int Voltage, string DamageRange)
        {
            return DischargeSpread(Source, Origin, Primary, Arcs, Voltage, DamageRange,
                RadiusBonusPerArc, MaxSpreadRadius);
        }

        /// <summary>Extra rings of reach granted per arc beyond the first.</summary>
        public const int RadiusBonusPerArc = 1;

        /// <summary>Hard ceiling on the spread search so a huge arc count cannot scan the zone.</summary>
        public const int MaxSpreadRadius = 12;

        public static int DischargeSpread(GameObject Source, Cell Origin, GameObject Primary,
            int Arcs, int Voltage, string DamageRange, int RadiusBonusPerArc, int MaxRadius)
        {
            if (Source == null || Origin == null || Arcs <= 0)
            {
                return 0;
            }

            int struck = 0;
            HashSet<GameObject> already = new HashSet<GameObject>();

            // --- arc 1: the primary target, exactly where the ability was aimed.
            if (Primary != null && !Primary.IsInvalid())
            {
                if (DischargeOne(Source, Primary, Voltage, DamageRange))
                {
                    struck++;
                }
                already.Add(Primary);
            }

            // --- remaining arcs: one new target each, searching outward ring by ring from the
            //     target square. Ring 1 is the eight squares around it, ring 2 the next shell out,
            //     and so on, so an arc always takes the nearest thing not already hit.
            int remaining = Arcs - struck;

            // Radius scales with the arc count: 1 arc reaches one ring out, 2 arcs two rings, ...
            // The cap keeps GetLocalAdjacentCells from building an enormous ring on a high-level
            // character with a big arc count.
            int searchLimit = Math.Max(1, Math.Min(MaxRadius, Arcs + Math.Max(0, RadiusBonusPerArc)));
            int radius = 1;

            while (remaining > 0 && radius <= searchLimit)
            {
                foreach (Cell cell in Origin.GetLocalAdjacentCells(radius, false))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    if (cell == null)
                    {
                        continue;
                    }

                    GameObject candidate = cell.GetCombatTarget(Source);
                    if (candidate == null || candidate == Source || already.Contains(candidate))
                    {
                        continue;
                    }
                    if (candidate.IsInvalid())
                    {
                        continue;
                    }

                    if (DischargeOne(Source, candidate, Voltage, DamageRange))
                    {
                        struck++;
                        remaining--;
                    }
                    already.Add(candidate);
                }
                radius++;
            }

            return struck;
        }

        /// <summary>
        /// Decorative arc for the melee proc only.
        ///
        /// This is a ParticleBlip on whatever occupies each square along the line, which is the
        /// approach a published mod (WaterDischarge.cs) uses for its electrical zaps and therefore
        /// the one that is known to actually be visible. The melee proc does NOT discharge here:
        /// the melee hit has already dealt its damage, and discharging would stack a second hit on
        /// top of it.
        /// </summary>
        public static void ZapLine(GameObject From, GameObject To)
        {
            if (From == null || To == null)
            {
                return;
            }
            Cell a = From.CurrentCell;
            Cell b = To.CurrentCell;
            if (a == null || b == null || b.ParentZone == null)
            {
                return;
            }

            try
            {
                int steps = Math.Max(1, Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));
                for (int i = 0; i <= steps; i++)
                {
                    int x = a.X + (b.X - a.X) * i / steps;
                    int y = a.Y + (b.Y - a.Y) * i / steps;

                    Cell cell = b.ParentZone.GetCell(x, y);
                    if (cell == null || !cell.IsVisible())
                    {
                        continue;
                    }

                    GameObject occupant = cell.GetFirstObject();
                    if (occupant == null)
                    {
                        continue;
                    }

                    // Bright white/gold box-drawing glyphs: the same zap vocabulary WaterDischarge
                    // uses. Cosmetic randomness only, so the seeded RNG is untouched.
                    string glyph = ((char)Stat.RandomCosmetic(191, 198)).ToString();
                    occupant.ParticleBlip(
                        Stat.RandomCosmetic(0, 1) == 0 ? "&W" + glyph : "&Y" + glyph, 30);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] zap line failed: " + ex.Message);
            }
        }

        /// <summary>
        /// The same bolt, aimed at a CELL rather than at an object.
        ///
        /// ZapLine(GameObject, GameObject) needs an endpoint that exists, so it draws nothing into an
        /// empty square. A cone covers plenty of empty squares, and leaving those unlit made the
        /// ability look like it had misfired -- which is why this overload exists. Vanilla's own
        /// SapChargeOnHit.DrawZap walks the line the same way (Zone.Line) and blips whatever occupies
        /// each cell, so this is the same idea with the cell itself as the last stop.
        /// </summary>
        public static void ZapLine(GameObject From, Cell Target)
        {
            if (From == null || Target == null)
            {
                return;
            }
            Cell a = From.CurrentCell;
            if (a == null || Target.ParentZone == null)
            {
                return;
            }

            try
            {
                int steps = Math.Max(1, Math.Max(Math.Abs(Target.X - a.X), Math.Abs(Target.Y - a.Y)));
                for (int i = 0; i <= steps; i++)
                {
                    int x = a.X + (Target.X - a.X) * i / steps;
                    int y = a.Y + (Target.Y - a.Y) * i / steps;

                    Cell cell = Target.ParentZone.GetCell(x, y);
                    if (cell == null || !cell.IsVisible())
                    {
                        continue;
                    }

                    GameObject occupant = cell.GetFirstObject();
                    if (occupant == null)
                    {
                        continue;
                    }

                    string glyph = ((char)Stat.RandomCosmetic(191, 198)).ToString();
                    occupant.ParticleBlip(
                        Stat.RandomCosmetic(0, 1) == 0 ? "&W" + glyph : "&Y" + glyph, 30);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] zap line failed: " + ex.Message);
            }
        }
    }
}
