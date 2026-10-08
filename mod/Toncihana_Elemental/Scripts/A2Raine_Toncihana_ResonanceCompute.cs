using System;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.Parts
{
    /// <summary>
    /// Spirit resonance feeds the local compute lattice in proportion to its bearer's level:
    /// 2 units per level, which is 100 units at level 50.
    ///
    /// WHY NOT ComputeNode
    /// -------------------
    /// ComputeNode (XRL/World/Parts/ComputeNode.cs) is the vanilla way to supply compute power,
    /// but its amount is a static value: it answers GetAvailableComputePowerEvent with
    /// GetEffectivePower(), which is just the Power field with a power-load bonus folded in
    /// (:44, :87). There is no per-level curve in it, so this part answers the same event and does
    /// its own arithmetic.
    ///
    /// It also deliberately does NOT inherit IPoweredPart: no ChargeUse, no IsReady gate, so it
    /// never spends the bearer's charge. Spirit resonance's only cost is stated elsewhere -- the
    /// tenth of maximum charge that the link holds occupied.
    /// </summary>
    public class A2Raine_Toncihana_ResonanceCompute : IPart
    {
        /// <summary>Compute power granted per character level. 2 x 50 = 100.</summary>
        public int PowerPerLevel = 2;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == SingletonEvent<GetAvailableComputePowerEvent>.ID;
        }

        public override bool HandleEvent(GetAvailableComputePowerEvent E)
        {
            // The part sits on the implant; the thing that has the level is whatever it is
            // implanted in.
            GameObject bearer = null;
            if (ParentObject != null)
            {
                bearer = ParentObject.Implantee;
            }

            if (bearer != null && bearer.HasStat("Level"))
            {
                E.Amount += bearer.Stat("Level") * PowerPerLevel;
            }

            return base.HandleEvent(E);
        }
    }
}
