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
    /// There is nothing to hang a part on. Mutations raises "BeforeMutationAdded" and
    /// "MutationAdded" (XRL/World/Parts/Mutations.cs:511, :520) but nothing at all for removal,
    /// and GlitchMutations is itself private static. A part can neither observe nor refuse the
    /// swap.
    ///
    /// So this is the case AGENTS.md rule 12 reserves Harmony for, and it follows that rule's
    /// requirements: no [HarmonyPatch] attribute (so the loader's PatchAll never touches it), an
    /// explicit Harmony instance patched by hand, and a try/catch so that if the signature ever
    /// drifts, the failure is one logged line rather than the rest of the mod.
    ///
    /// Prefix remembers the level; Postfix puts the mutation back if the reroll took it.
    /// </summary>
    public static class A2Raine_Toncihana_MutationGuard
    {
        /// <summary>
        /// Must equal the C# class name: the engine resolves the type through it.
        ///
        /// This is the SUBCLASS, not the vanilla parent. Toncihana's charge store is
        /// A2Raine_Toncihana_Stormcharge : ElectricalGeneration, and it is the subclass that sits
        /// on the body -- ARaine_Charge.GetGeneration looks for this name first and only falls
        /// back to ElectricalGeneration (ARaine_Charge.cs:49, :56). Protecting the parent would
        /// have guarded a part the character does not have.
        /// </summary>
        private const string PROTECTED_MUTATION = "A2Raine_Toncihana_Stormcharge";

        private static bool installed;

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

                UnityEngine.Debug.Log("[Toncihana] mutation guard installed: " + PROTECTED_MUTATION
                    + " survives warm static.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] mutation guard patch failed: " + e);
            }
        }

        /// <summary>Runs before the reroll and hands the level to the postfix through __state.</summary>
        public static void Prefix(GameObject Object, ref int __state)
        {
            __state = 0;
            if (!GameObject.Validate(Object))
            {
                return;
            }

            Mutations mutations = Object.GetPart<Mutations>();
            if (mutations == null)
            {
                return;
            }

            BaseMutation mutation = mutations.GetMutation(PROTECTED_MUTATION);
            if (mutation != null)
            {
                __state = mutation.BaseLevel;
            }
        }

        /// <summary>Runs after the reroll and restores the mutation if the reroll took it.</summary>
        public static void Postfix(GameObject Object, int __state)
        {
            if (__state <= 0 || !GameObject.Validate(Object))
            {
                return;
            }

            Mutations mutations = Object.GetPart<Mutations>();
            if (mutations == null || mutations.GetMutation(PROTECTED_MUTATION) != null)
            {
                return;
            }

            mutations.AddMutation(BaseMutation.Create(PROTECTED_MUTATION), __state);
            UnityEngine.Debug.Log("[Toncihana] warm static took " + PROTECTED_MUTATION
                + "; restored at level " + __state + " on " + Object.Blueprint);
        }
    }
}
