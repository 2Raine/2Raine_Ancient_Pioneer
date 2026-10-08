using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// Keeps vanilla's "Electrical Generation" out of the mutation pool for Toncihana.
    ///
    /// ONE GATE, EVERY ROUTE
    /// ---------------------
    /// Every way a character is handed a new mutation goes through
    /// Mutations.GetMutatePool (qud_src/XRL/World/Parts/Mutations.cs:887):
    ///   StatusScreen.cs:655                buying mutations from the status screen
    ///   WaterRitualRandomMutation.cs:50    water rituals
    ///   EndGame.cs:674                     the endgame
    ///   PsychicHunterSystem.cs (x4)        psychic hunters
    /// Filtering this one method therefore covers all of them, instead of patching each UI.
    ///
    /// BOUND TO THE BODY, NOT TO A SAVE
    /// --------------------------------
    /// The test is who.HasPart&lt;A2Raine_Toncihana_Physiology&gt;() -- a part that lives on the body.
    /// A Toncihana has it. A character that has been dominated into another body does not, so the
    /// filter stops matching and that body may take vanilla Electrical Generation normally.
    /// Nothing is stored on the character and nothing is written to the save.
    ///
    /// NOT MutationLevel. That property is vanilla's slot for the Chimera/Esper paths
    /// (QudMutationsModule.cs:99, :103); taking it would make both of those impossible here.
    ///
    /// TWO LEVELS OF DIAGNOSTIC, because earlier attempts failed without evidence to work from:
    ///   "installed ... postfix=N"  proves the patch attached
    ///   "body='...', removed=N"    proves it ran and matched
    /// </summary>
    public static class A2Raine_Toncihana_MutationPoolFilter
    {
        /// <summary>
        /// The vanilla mutation to keep out -- and its name has a SPACE in it.
        /// Base/Mutations.xml:16 declares Name="Electrical Generation" while Class is
        /// "ElectricalGeneration". Every comparison against a mutation entry uses
        /// MutationEntry.Name, which is the spaced one (MutationEntry.cs:224,
        /// QudMutationsModuleWindow.cs:269).
        /// </summary>
        private const string HIDDEN_MUTATION = "Electrical Generation";

        private static bool installed;

        /// <summary>Runs once when mods finish loading -- earlier than any other entry point.</summary>
        [ModSensitiveCacheInit]
        public static void CachedInit()
        {
            Install();
        }

        public static void Install()
        {
            if (installed)
            {
                return;
            }
            installed = true;

            try
            {
                System.Reflection.MethodInfo target = AccessTools.Method(
                    typeof(Mutations),
                    "GetMutatePool",
                    new Type[]
                    {
                        typeof(GameObject),
                        typeof(List<BaseMutation>),
                        typeof(Predicate<MutationEntry>),
                        typeof(bool)
                    });

                if (target == null)
                {
                    UnityEngine.Debug.LogError("[Toncihana] mutation pool filter: GetMutatePool not"
                        + " found; vanilla Electrical Generation stays in the pool.");
                    return;
                }

                new Harmony("2Raine.Toncihana.MutationPool").Patch(
                    target,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(A2Raine_Toncihana_MutationPoolFilter), "Postfix")));

                Patches attached = Harmony.GetPatchInfo(target);
                int postfixCount = attached == null ? -1 : attached.Postfixes.Count;

                UnityEngine.Debug.Log("[Toncihana] mutation pool filter installed: '"
                    + HIDDEN_MUTATION + "' is kept out of the pool for bodies carrying"
                    + " A2Raine_Toncihana_Physiology. patch info on GetMutatePool: postfix="
                    + postfixCount);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation pool filter patch failed: " + e);
            }
        }

        public static void Postfix(GameObject who, List<MutationEntry> __result)
        {
            try
            {
                if (who == null || __result == null || __result.Count == 0)
                {
                    return;
                }

                if (!who.HasPart<A2Raine_Toncihana_Physiology>())
                {
                    return;
                }

                int before = __result.Count;
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    if (__result[i] != null && __result[i].Name == HIDDEN_MUTATION)
                    {
                        __result.RemoveAt(i);
                    }
                }

                UnityEngine.Debug.Log("[Toncihana] mutation pool filter: body='" + who.Blueprint
                    + "', pool " + before + " -> " + __result.Count + " (removed "
                    + (before - __result.Count) + " x '" + HIDDEN_MUTATION + "').");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation pool postfix failed: " + e);
            }
        }
    }
}
