using System;
using HarmonyLib;
using XRL.Liquids;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
    /// <summary>
    /// Keeps Toncihana's Electrical Generation through warm static.
    ///
    /// WHY THIS IS A PATCH AND NOT A PART
    /// ----------------------------------
    /// Drinking warm static rerolls every mutation through
    /// LiquidWarmStatic.GlitchMutations(GameObject) (qud_src/XRL/Liquids/LiquidWarmStatic.cs:273):
    /// it copies MutationList, drops everything at BaseLevel &lt;= 0 (:281), skips Morphotypes
    /// (:294), then for each survivor picks a random replacement, RemoveMutation's the old one
    /// (:363) and AddMutation's the new one (:375).
    ///
    /// There is nothing to hang a part on: Mutations raises "BeforeMutationAdded" / "MutationAdded"
    /// (Mutations.cs:511, :520) but nothing for removal, and GlitchMutations is private static.
    /// So this is the case AGENTS.md rule 12 reserves Harmony for, done that rule's way: no
    /// [HarmonyPatch] attribute, an explicit Harmony instance, try/catch around the patch.
    ///
    /// HOW IT PROTECTS -- TAKE IT OFF THE TABLE, NOT PUT IT BACK AFTERWARDS
    /// --------------------------------------------------------------------
    /// The first version let the reroll take the mutation and then re-added it with AddMutation.
    /// That was wrong twice over: the player still "loses" the mutation for the duration, and if
    /// the reroll happened to pick ElectricalGeneration, the character ended up holding BOTH and
    /// the mutation count grew.
    ///
    /// Instead the prefix lifts the mutation straight out of MutationList and the postfix puts
    /// the SAME instance back. GlitchMutations copies MutationList at :280, so a mutation removed
    /// before it runs is simply not in the list it shuffles: it cannot be rerolled, cannot be
    /// replaced, and cannot be chosen as a replacement. Nothing calls RemoveMutation/AddMutation,
    /// so no events fire and no state is touched -- the instance carries its own BaseLevel back.
    ///
    /// A second, independent guard lives in 2Raine_Toncihana_Mutations.xml: Stormcharge declares
    /// Exclusions="ElectricalGeneration", so the reroll's own OkWith filter (MutationEntry.cs:207,
    /// applied at LiquidWarmStatic.cs:328) refuses to pair the two.
    /// </summary>
    public static class A2Raine_Toncihana_MutationGuard
    {
        /// <summary>
        /// Must equal the C# class name: the engine resolves the type through it.
        ///
        /// This is the SUBCLASS, not the vanilla parent. Toncihana's charge store is
        /// A2Raine_Toncihana_Stormcharge : ElectricalGeneration, and it is the subclass that sits
        /// on the body -- ARaine_Charge.GetGeneration looks for this name first and only falls
        /// back to ElectricalGeneration (ARaine_Charge.cs:49, :56).
        /// </summary>
        private const string PROTECTED_MUTATION = "A2Raine_Toncihana_Stormcharge";

        private static bool installed;

        /// <summary>Same early hook as the chargen filter -- installs as soon as mods load.</summary>
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
                    typeof(LiquidWarmStatic),
                    "GlitchMutations",
                    new Type[] { typeof(GameObject) });

                if (target == null)
                {
                    UnityEngine.Debug.LogError("[Toncihana] mutation guard: GlitchMutations not found,"
                        + " so " + PROTECTED_MUTATION + " is NOT protected from warm static.");
                    return;
                }

                new Harmony("2Raine.Toncihana.MutationGuard").Patch(
                    target,
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(A2Raine_Toncihana_MutationGuard), "Prefix")),
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(A2Raine_Toncihana_MutationGuard), "Postfix")));

                // Ask Harmony what it actually attached. Patch() returning without throwing is NOT
                // proof the patch is live, and this is the only way to tell from the log.
                Patches attached = Harmony.GetPatchInfo(target);
                int prefixCount = attached == null ? -1 : attached.Prefixes.Count;
                int postfixCount = attached == null ? -1 : attached.Postfixes.Count;

                UnityEngine.Debug.Log("[Toncihana] mutation guard installed: " + PROTECTED_MUTATION
                    + " survives warm static. patch info on GlitchMutations: prefix="
                    + prefixCount + ", postfix=" + postfixCount);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation guard patch failed: " + e);
            }
        }

        /// <summary>
        /// Lifts the mutation out of MutationList before the reroll copies it, and hands the
        /// instance to the postfix through __state.
        /// </summary>
        public static void Prefix(GameObject Object, ref BaseMutation __state)
        {
            __state = null;

            if (!GameObject.Validate(Object))
            {
                return;
            }

            Mutations mutations = Object.GetPart<Mutations>();
            if (mutations == null || mutations.MutationList == null)
            {
                return;
            }

            BaseMutation mutation = mutations.GetMutation(PROTECTED_MUTATION);
            if (mutation == null)
            {
                UnityEngine.Debug.Log("[Toncihana] mutation guard prefix: " + PROTECTED_MUTATION
                    + " is NOT on " + Object.Blueprint + "; nothing to protect. Present: "
                    + DescribeMutations(mutations));
                return;
            }

            __state = mutation;
            mutations.MutationList.Remove(mutation);

            UnityEngine.Debug.Log("[Toncihana] mutation guard prefix: took " + PROTECTED_MUTATION
                + " (level " + mutation.BaseLevel + ") off " + Object.Blueprint
                + " for the reroll.");
        }

        /// <summary>Puts the very same instance back, level and all.</summary>
        public static void Postfix(GameObject Object, BaseMutation __state)
        {
            if (__state == null)
            {
                return;
            }

            if (!GameObject.Validate(Object))
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation guard: " + PROTECTED_MUTATION
                    + " was removed for the reroll but the object is gone; it is LOST.");
                return;
            }

            Mutations mutations = Object.GetPart<Mutations>();
            if (mutations == null || mutations.MutationList == null)
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation guard: " + PROTECTED_MUTATION
                    + " was removed for the reroll but there is no Mutations part to restore it to.");
                return;
            }

            if (mutations.GetMutation(PROTECTED_MUTATION) != null)
            {
                // The reroll re-added an equivalent under the same name. Keep the original and
                // drop the newcomer rather than ending up with two.
                mutations.MutationList.Remove(mutations.GetMutation(PROTECTED_MUTATION));
            }

            mutations.MutationList.Add(__state);
            Object.SyncMutationLevelAndGlimmer();

            UnityEngine.Debug.Log("[Toncihana] mutation guard postfix: restored "
                + PROTECTED_MUTATION + " at level " + __state.BaseLevel + " on " + Object.Blueprint);
        }

        /// <summary>Names of the mutations present, for the diagnostic above.</summary>
        private static string DescribeMutations(Mutations Mutations)
        {
            if (Mutations.MutationList == null)
            {
                return "(none)";
            }

            string text = "";
            for (int i = 0; i < Mutations.MutationList.Count; i++)
            {
                if (i > 0)
                {
                    text += ", ";
                }
                text += Mutations.MutationList[i].Name;
            }
            return text.Length == 0 ? "(none)" : text;
        }
    }
}
