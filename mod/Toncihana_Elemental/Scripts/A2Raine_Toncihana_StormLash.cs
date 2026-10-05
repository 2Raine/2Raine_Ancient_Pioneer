using System;
using XRL;
using XRL.World;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// The standing storm's loader: an EnergyAmmoLoader that spends a percentage of the SHOOTER's own
    /// charge and writes the projectile's numbers itself.
    ///
    /// COST AND DAMAGE ARE SEPARATE ON PURPOSE
    /// ---------------------------------------
    /// Cost is a percentage of the wielder's maximum charge, so the firing rhythm stays roughly even
    /// as ElectricalGeneration's numbers grow -- vanilla's GetMaxCharge is 2000 + Level*2000 and
    /// GetBaseChargePerTurn is Level*100*Percent/100 (our Stormcharge runs at 200%), so at 10% a
    /// level-1 character fires about every other action and a level-16 one about every action.
    ///
    /// Damage is NOT derived from that cost. Feeding the spent charge into vanilla's
    /// ElectricalGeneration.GetDischargeDamageRoll would make the dice scale as (200 + Level*200) * 15
    /// / 1000 -- about 3 extra d4 every level, i.e. 33d4 at level 10. The two are both linear in
    /// level, so "percentage cost" and "damage from cost" compound into a square. Damage therefore
    /// grows on its own, gentler curve.
    ///
    /// Voltage IS pinned, because Physics.ApplyDischarge walks it down one step per hop
    /// (Physics.cs:1974 "if (Voltage > 1)", :2073 "int voltage = Voltage - 1") -- so voltage is how
    /// far the arc chains, and letting it grow with level would chain it across the whole screen.
    /// 3 sits next to vanilla's Arc Winder (whose 300 charge yields 4).
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_StormLash : EnergyAmmoLoader
    {
        /// <summary>Percent of the shooter's MAXIMUM charge spent per shot.</summary>
        public int ChargePercent = 10;

        /// <summary>Arc chain length. Fixed: see the class comment.</summary>
        public int BaseVoltage = 3;

        /// <summary>Damage dice at level 0, and how many levels add one more d4.</summary>
        public int BaseDice = 2;
        public int LevelsPerDie = 5;

        public string NotEnoughMessage = "{{W|The storm in you is too thin to answer.}}";

        public override bool HandleEvent(LoadAmmoEvent E)
        {
            bool ours = E.Object == ParentObject && E.Actor != null && ChargePercent > 0;
            int spent = 0;

            if (ours)
            {
                // A percentage of max, with a floor so a low-level character can still fire at all.
                spent = ARaine_Charge.GetMaxCharge(E.Actor) * ChargePercent / 100;
                if (spent < 1000)
                {
                    spent = 1000;
                }
                if (!ARaine_Charge.TryUseCharge(E.Actor, spent))
                {
                    E.Message = NotEnoughMessage;
                    return false;
                }
            }

            // The base creates the projectile; ChargeUse is 0 in the blueprint, so it neither looks
            // for a cell nor writes the projectile's numbers.
            if (!base.HandleEvent(E))
            {
                return false;
            }

            if (ours && E.Projectile != null)
            {
                DischargeOnHit discharge = E.Projectile.GetPart<DischargeOnHit>();
                if (discharge != null)
                {
                    int level = E.Actor.Stat("Level");
                    discharge.DamageRange = (BaseDice + level / LevelsPerDie) + "d4";
                    discharge.Voltage = BaseVoltage.ToString();
                    UnityEngine.Debug.Log("[Toncihana] storm lash fired: spent=" + spent
                        + " of " + ARaine_Charge.GetMaxCharge(E.Actor)
                        + ", level=" + level
                        + ", damage=" + discharge.DamageRange
                        + ", voltage=" + discharge.Voltage);
                }
            }

            return true;
        }
    }
}
