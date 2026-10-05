using System;
using XRL;
using XRL.World;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// The standing storm's loader: an EnergyAmmoLoader that spends the SHOOTER's own charge and
    /// then writes the projectile's numbers with vanilla's own discharge formulas.
    ///
    /// WHY EnergyAmmoLoader AND NOT ElectricalDischargeLoader
    /// ------------------------------------------------------
    /// The two loaders differ in exactly one place that matters here. Both create the projectile on
    /// LoadAmmoEvent; ElectricalDischargeLoader then OVERWRITES its numbers
    /// (ElectricalDischargeLoader.cs:134-136):
    ///
    ///     dischargeOnHit.Voltage     = GetVoltage(activeChargeUse, value).ToString();
    ///     dischargeOnHit.DamageRange = GetDamageRoll(activeChargeUse, value);
    ///
    /// with values derived from its own battery accounting -- and when there is no charge that
    /// yields null, i.e. no damage at all. EnergyAmmoLoader just creates the object
    /// (EnergyAmmoLoader.cs:109-125). Since this weapon is paid for out of the wielder's own
    /// ElectricalGeneration store rather than a cell, the battery-derived numbers are meaningless
    /// here, so the base creates the projectile and we write the numbers ourselves.
    ///
    /// VANILLA'S FORMULAS (ElectricalGeneration.cs:89-106), reused rather than reinvented:
    ///
    ///     GetDischargeDamageRoll(charge) => charge < 1000 ? null : (charge / 1000) + "d4"
    ///     GetDischargeVoltage(charge)   => charge / 1000
    ///
    /// and the charge fed to them is the shot's cost scaled by 15, which is
    /// ElectricalDischargeLoader's own ChargeFactor (ElectricalDischargeLoader.cs:26) -- so an Arc
    /// Winder's 300 charge becomes 4500, i.e. 4d4 at voltage 4.
    ///
    /// ChargePerShot is spent through ARaine_Charge, this mod's own reader/writer for the
    /// ElectricalGeneration store (ARaine_Charge.cs:126-139).
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_StormLash : EnergyAmmoLoader
    {
        /// <summary>Charge -> effective discharge charge. Same 15 as ElectricalDischargeLoader.</summary>
        public const int EffectiveChargePerCharge = 15;

        /// <summary>Charge spent per shot. Arc Winder spends 300; this is a weaker weapon.</summary>
        public int ChargePerShot = 200;

        public string NotEnoughMessage = "{{W|The storm in you is too thin to answer.}}";

        public override bool HandleEvent(LoadAmmoEvent E)
        {
            bool ours = E.Object == ParentObject && E.Actor != null && ChargePerShot > 0;

            if (ours && !ARaine_Charge.TryUseCharge(E.Actor, ChargePerShot))
            {
                E.Message = NotEnoughMessage;
                return false;
            }

            // The base creates the projectile; ChargeUse is 0 in the blueprint, so it neither looks
            // for a cell nor touches the projectile's numbers.
            if (!base.HandleEvent(E))
            {
                return false;
            }

            if (ours && E.Projectile != null)
            {
                DischargeOnHit discharge = E.Projectile.GetPart<DischargeOnHit>();
                if (discharge != null)
                {
                    int charge = ChargePerShot * EffectiveChargePerCharge;
                    discharge.Voltage = ElectricalGeneration.GetDischargeVoltage(charge).ToString();
                    discharge.DamageRange = ElectricalGeneration.GetDischargeDamageRoll(charge);
                }
            }

            return true;
        }
    }
}
