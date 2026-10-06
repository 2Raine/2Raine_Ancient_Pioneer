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

        /// <summary>
        /// Floor on the charge spent per shot. 0 = none, i.e. purely the percentage.
        ///
        /// This used to be hardcoded to 1000, which badly distorted the early game: a level-1
        /// character's maximum is 4000, so 10% is 400 -- but the floor made every shot cost 1000,
        /// i.e. 25% of the bar, and with 200 charge regenerating per action that is five actions
        /// per shot instead of two. It was there to keep the old (vanilla) damage formula from
        /// returning null below 1000 charge, but damage no longer uses that formula, so the floor
        /// had outlived its reason. Left as a knob in case a floor is wanted later.
        /// </summary>
        public int MinChargePerShot = 0;

        /// <summary>Arc chain length. Fixed: see the class comment.</summary>
        public int BaseVoltage = 3;

        /// <summary>Damage dice at level 0.</summary>
        public int BaseDice = 1;

        /// <summary>
        /// LINEAR term: levels per extra die. Deliberately 0 by default -- a straight line is the
        /// wrong shape for this game. Levels arrive fastest through the low teens and slow down
        /// around 20, which is still mid-game, so a linear curve peaks far too early.
        /// </summary>
        public int LevelsPerDie = 0;

        /// <summary>
        /// SQUARED term: extra dice = level*level / this. Small early, accelerating later, which is
        /// the inverse of how the level curve behaves. At 150: L1 2d4, L10 2d4, L20 4d4, L30 8d4,
        /// L40 12d4. Raise the divisor to flatten it, lower it to steepen.
        /// </summary>
        public int SquaredDiceDivisor = 150;

        public string NotEnoughMessage = "{{W|The storm in you is too thin to answer.}}";

        public override bool HandleEvent(LoadAmmoEvent E)
        {
            bool ours = E.Object == ParentObject && E.Actor != null && ChargePercent > 0;
            int spent = 0;

            if (ours)
            {
                // A percentage of max, with a floor so a low-level character can still fire at all.
                spent = ARaine_Charge.GetMaxCharge(E.Actor) * ChargePercent / 100;
                if (spent < MinChargePerShot)
                {
                    spent = MinChargePerShot;
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
                    discharge.DamageRange = DiceFor(level) + "d4";
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

        /// <summary>
        /// The damage the shot rolls: a base number of d4 plus a squared term, because level
        /// arrivals are front-loaded and a purely linear curve peaks far too early (see the notes).
        /// </summary>
        public int DiceFor(int Level)
        {
            int dice = BaseDice;
            if (LevelsPerDie > 0)
            {
                dice += Level / LevelsPerDie;
            }
            if (SquaredDiceDivisor > 0)
            {
                dice += Level * Level / SquaredDiceDivisor;
            }
            return dice;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == GetDisplayNameEvent.ID;
        }

        /// <summary>
        /// Arc Winder reads as "yellow heart 4d4" because ElectricalDischargeLoader answers
        /// GetDisplayNameEvent (ElectricalDischargeLoader.cs:265-275) with its own damage roll.
        /// EnergyAmmoLoader has no such handler, so this weapon showed only its penetration --
        /// which is not what it deals. U+0003 is CP437's heart, the very character that loader
        /// uses; the roll is recomputed per level, matching what LoadAmmoEvent will fire.
        ///
        /// The penetration readout is switched off separately, with Attributes="... NonPenetrating"
        /// on the projectile (see 2Raine_Toncihana_StormLash.xml). GetDisplayNameEvent.cs:240 is
        /// what honours that word, and MissileWeapon.cs:1769 pins actual penetration to 1 when it
        /// is present -- correct here, since the shot deals its damage through DischargeOnHit.
        /// </summary>
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (E.Understood())
            {
                GameObject wielder = ParentObject.Equipped;
                int level = wielder != null ? wielder.Stat("Level") : 1;
                E.AddTag("{{W|" + '\u0003' + "}}" + DiceFor(level) + "d4", -20);
            }
            return base.HandleEvent(E);
        }
    }
}
