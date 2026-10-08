using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.Parts
{
    /// <summary>
    /// Everything spirit resonance does through its face implant, in one part:
    ///
    ///   * supplies compute power to the local lattice, in proportion to the bearer's level
    ///     (2 per level, so 100 at level 50);
    ///   * lights the ground around the bearer the way a penetrating radar does -- but only while
    ///     the spirit resonance ability itself is toggled on.
    ///
    /// WHY THIS DOES NOT USE CyberneticsPenetratingRadar
    /// -------------------------------------------------
    /// That part does the illumination correctly, but it also registers its OWN toggleable
    /// ability ("Penetrating Radar", CyberneticsPenetratingRadar.cs:69) on the implantee, which
    /// puts a second radar switch in the ability menu next to spirit resonance. Hiding that entry
    /// afterwards did not take effect, so this part does the illumination itself and the vanilla
    /// part is simply never attached (see 2Raine_Toncihana_Links.xml). Nothing to hide.
    ///
    /// The illumination logic is modelled on CyberneticsPenetratingRadar.cs:48-63:
    ///   * only for the player, and not on the world map;
    ///   * radius = Radius, raised by whatever compute power is available (that is the
    ///     "compute power increases this implant's range" line);
    ///   * written with LightLevel.Radar, which is what lets it show terrain through walls.
    ///
    /// It deliberately does NOT inherit IPoweredPart: no ChargeUse, no IsReady gate, so it never
    /// spends the bearer's charge. Spirit resonance's cost is stated elsewhere -- the tenth of
    /// maximum charge the link holds occupied.
    /// </summary>
    public class A2Raine_Toncihana_ResonanceCompute : IPart
    {
        /// <summary>Compute power granted per character level. 2 x 50 = 100.</summary>
        public int PowerPerLevel = 2;

        /// <summary>Base radar radius, in cells. Same default as the vanilla implant.</summary>
        public int Radius = 10;

        /// <summary>Percentage of the bearer's level added to the radius as a floor, unused for now.</summary>
        public int RadiusPerLevel = 0;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == SingletonEvent<GetAvailableComputePowerEvent>.ID
                || ID == BeforeRenderEvent.ID;
        }

        /// <summary>Supplies compute power, scaled by the bearer's level.</summary>
        public override bool HandleEvent(GetAvailableComputePowerEvent E)
        {
            GameObject bearer = Bearer();
            if (bearer != null && bearer.HasStat("Level"))
            {
                E.Amount += bearer.Stat("Level") * PowerPerLevel;
            }
            return base.HandleEvent(E);
        }

        /// <summary>
        /// Lights the area around the bearer while spirit resonance is open. Reading the ability's
        /// own toggle state is what makes one switch control both the radar and the telepathy.
        /// </summary>
        public override bool HandleEvent(BeforeRenderEvent E)
        {
            GameObject bearer = Bearer();
            if (bearer == null || !bearer.IsPlayer())
            {
                return base.HandleEvent(E);
            }

            if (!IsResonanceOpen(bearer))
            {
                return base.HandleEvent(E);
            }

            Cell cell = bearer.CurrentCell;
            if (cell == null || cell.OnWorldMap())
            {
                return base.HandleEvent(E);
            }

            int radius = GetAvailableComputePowerEvent.AdjustUp(bearer, Radius);

            if (radius > 0 && cell.ParentZone != null)
            {
                cell.ParentZone.AddLight(cell.X, cell.Y, radius, LightLevel.Radar);
            }

            return base.HandleEvent(E);
        }

        /// <summary>The creature this implant sits in.</summary>
        private GameObject Bearer()
        {
            return ParentObject == null ? null : ParentObject.Implantee;
        }

        /// <summary>Is the spirit resonance ability toggled on for this creature?</summary>
        private static bool IsResonanceOpen(GameObject Bearer)
        {
            ActivatedAbilityEntry ability = Bearer.GetActivatedAbilityByCommand(
                XRL.World.Parts.Skill.A2Raine_Toncihana_SpiritResonance.COMMAND_NAME);

            return ability != null && Bearer.IsActivatedAbilityToggledOn(ability.ID);
        }
    }
}
