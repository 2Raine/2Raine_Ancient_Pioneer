using System;
using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;

namespace Toncihana
{
    /// <summary>
    /// Route 2 of 3 for handing the Elemental its innate mutations: the embark boot sequence.
    ///
    /// This module is deliberately the same minimal shape as the published "Resurrecting Pets"
    /// mod's EmbarkModule -- a class deriving AbstractEmbarkBuilderModule, an empty
    /// &lt;module Class="..."/&gt; registration in Toncihana_EmbarkModules.xml, no &lt;window&gt;,
    /// and no other overrides. At BOOTEVENT_BOOTPLAYEROBJECT the 'element' argument IS the player
    /// GameObject.
    ///
    /// The same class is also declared in A2Raine_Toncihana_Mutator (via [PlayerMutator] and
    /// [CallAfterGameLoaded]); every route is guarded by HasMutation so they cannot double up.
    /// </summary>
    public class A2Raine_Toncihana_GenotypeModule : AbstractEmbarkBuilderModule
    {
        public const string GENOTYPE = "2Raine_AncientPioneer_Elemental";

        public override object handleBootEvent(
            string id,
            XRLGame game,
            EmbarkInfo info,
            object element = null)
        {
            if (id == QudGameBootModule.BOOTEVENT_BOOTPLAYEROBJECT)
            {
                GameObject player = element as GameObject;
                string genotype = player == null ? "<null player>" : player.GetGenotype();

                UnityEngine.Debug.LogWarning("[Toncihana] BOOTPLAYEROBJECT reached. genotype='"
                    + genotype + "' expected='" + GENOTYPE + "'");

                if (player != null && genotype == GENOTYPE)
                {
                    A2Raine_Toncihana_Mutator.GrantTo(player);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Toncihana] genotype mismatch, mutations NOT granted here.");
                }
            }
            return base.handleBootEvent(id, game, info, element: element);
        }
    }
}
