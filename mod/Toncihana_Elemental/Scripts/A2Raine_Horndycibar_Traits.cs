using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.Parts
{
    /// <summary>
    /// Makes horndycibar a non-mutant for the two questions that matter, without touching the
    /// genotype (which Toncihana shares).
    ///
    /// WHY NOT JUST SET IsMutant="false" ON THE GENOTYPE
    /// -------------------------------------------------
    /// That flag belongs to the genotype, and Toncihana lives under the same one, so changing it
    /// there would change both characters. A part answers per-creature instead, which is exactly
    /// what vanilla does for robots: Robot.cs:35-37 overrides the same event and answers false.
    ///
    /// IsMutantEvent.cs:24 resolves the answer as genotypeEntry.IsMutant, then :26-40 lets the
    /// object's own "IsMutant" event and IsMutantEvent handlers override it before anyone reads
    /// the result. So a part on this body is authoritative for this body only.
    ///
    /// WHAT IT BUYS
    /// ------------
    /// LiquidWarmStatic.cs:133 gates the mutation shuffle behind Target.IsMutant(), so warm static
    /// no longer rerolls this character's genome -- which is why the earlier Harmony exemption for
    /// the Stormcharge mutation is no longer what protects it.
    ///
    /// WHAT IT COSTS
    /// -------------
    /// Leveler.cs:264-270 reads IsMutant to decide three things on level-up:
    ///
    ///     :265  HitPoints        = RollHP(BaseHPGain)              -- unaffected by IsMutant
    ///     :267  MutationPoints   = num2 ? RollMP(BaseMPGain) : 0    -- LOST when not a mutant
    ///     :270  RapidAdvancement = num2 && (num+5)%10==0 ? 3 : 0    -- LOST, and wanted lost
    ///
    /// Hit points are untouched (BaseHPGain is 1-4 for mutant and True Kin alike), so "hit points
    /// like True Kin" needs no work. Mutation points would silently become 0, so :271's own
    /// GetLevelUpPointsEvent -- which the game raises for exactly this purpose and passes every one
    /// of those numbers by ref -- is used to put them back.
    ///
    /// The value added is 1, matching the genotype's BaseMPGain="1": RollMP over a gain of 1 is 1
    /// every time, so this reproduces the mutant's pace without re-deriving it.
    ///
    /// Rapid advancement is deliberately NOT restored: the request was that this character not be
    /// handed the automatic advances at 5/15/25 that a mutant gets.
    /// </summary>
    public class A2Raine_Horndycibar_Traits : IPart
    {
        /// <summary>Mutation points per level, matching the genotype's BaseMPGain.</summary>
        public int MutationPointsPerLevel = 1;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == IsMutantEvent.ID
                || ID == GetLevelUpPointsEvent.ID;
        }

        public override bool HandleEvent(IsMutantEvent E)
        {
            E.IsMutant = false;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetLevelUpPointsEvent E)
        {
            if (MutationPointsPerLevel > 0)
            {
                E.MutationPoints += MutationPointsPerLevel;
            }

            return base.HandleEvent(E);
        }
    }
}
