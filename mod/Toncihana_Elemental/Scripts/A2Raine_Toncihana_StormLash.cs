using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// The standing storm's firing cost: taken from the shooter's OWN charge store, not from an
    /// energy cell.
    ///
    /// WHY THIS IS A SEPARATE PART FROM THE LOADER
    /// -------------------------------------------
    /// The weapon's own EnergyAmmoLoader runs with ChargeUse="0" -- the same shape vanilla's
    /// Glowmoth_Gaze uses (Creatures.xml:2833) -- so IPoweredPart never goes looking for a battery
    /// (EnergyAmmoLoader.cs:87-90 skips its ammo accounting entirely when the charge use is 0).
    /// The cost is taken here instead, on LoadAmmoEvent, which is what a MissileWeapon raises while
    /// loading: LoadAmmoEvent.cs:6-14 carries Object (the weapon) and Actor (the shooter), and
    /// E.Message is the text shown when a loader refuses.
    ///
    /// Charge is read through ARaine_Charge, which already knows how to find this mod's
    /// ElectricalGeneration subclass (ARaine_Charge.cs:40-62) and can spend from it
    /// (ARaine_Charge.cs:126-139).
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_StormLash : IPart
    {
        /// <summary>Charge spent per shot. Arc Winder uses 300; this is a weaker weapon.</summary>
        public int ChargePerShot = 200;

        public string NotEnoughMessage = "{{W|The storm in you is too thin to answer.}}";

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<LoadAmmoEvent>.ID;
        }

        public override bool HandleEvent(LoadAmmoEvent E)
        {
            // Only our own weapon, and only when someone is actually doing the firing.
            if (E.Object == ParentObject && E.Actor != null && ChargePerShot > 0)
            {
                if (!ARaine_Charge.TryUseCharge(E.Actor, ChargePerShot))
                {
                    E.Message = NotEnoughMessage;
                    return false;
                }
            }
            return base.HandleEvent(E);
        }
    }
}
