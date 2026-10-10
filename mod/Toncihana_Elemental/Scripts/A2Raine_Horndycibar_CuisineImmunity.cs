using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.Parts
{
    /// <summary>
    /// Stop food that would make the eater ill from doing so, for horndycibar alone.
    ///
    /// WHY A PATCH
    /// -----------
    /// IllnessChance is decided inside Food itself:
    ///
    ///     Food.cs:220-238  public virtual int IllnessChance(GameObject Actor)
    ///                      :234  if (IllOnEat) return 100;
    ///     Food.cs:203      if (IllnessChance(E.Actor).in100())
    ///     Food.cs:209          E.Actor.ApplyEffect(new Ill(100));
    ///
    /// IllOnEat (Food.cs:19) belongs to the FOOD, not to the eater, so there is no trait to set on
    /// the character and no event the character can answer -- the eater is not consulted at all.
    /// That leaves a patch, which AGENTS.md rule 12 allows, done that rule's way: no
    /// [HarmonyPatch] attribute, an explicit Harmony instance, try/catch, and a Postfix.
    ///
    /// WHY NOT BLOCK THE Ill EFFECT INSTEAD
    /// ------------------------------------
    /// Answering CanApplyEffectEvent would also excuse the eater from Ill from every other source
    /// -- poison, spores, corpses -- which is far more than "ill on eat". This narrows the change
    /// to the number this one roll produces.
    ///
    /// WHY THE PATCH IS INSTALLED GLOBALLY AND NOT PER CHARACTER
    /// ---------------------------------------------------------
    /// The Postfix asks whether the Actor carries A2Raine_Horndycibar_Traits, so it changes
    /// nothing for anyone else; installing it once at mod load is simpler and cheaper than
    /// installing it when a particular character appears, and avoids the part having to worry
    /// about being added and removed repeatedly.
    ///
    /// [ModSensitiveCacheInit] runs when the mod finishes loading (AGENTS.md rule 15), which is
    /// early enough for anything that can eat.
    /// </summary>
    public static class A2Raine_Horndycibar_CuisineImmunity
    {
        private static bool installed;

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
                    typeof(Food),
                    "IllnessChance",
                    new Type[] { typeof(GameObject) });

                if (target == null)
                {
                    UnityEngine.Debug.LogError("[Toncihana] cuisine immunity: Food.IllnessChance"
                        + " not found, so horndycibar will still fall ill from food.");
                    return;
                }

                new Harmony("2Raine.Horndycibar.CuisineImmunity").Patch(
                    target,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(A2Raine_Horndycibar_CuisineImmunity), "Postfix")));

                // Patch() returning without throwing is not proof the patch is live; this is.
                Patches attached = Harmony.GetPatchInfo(target);
                int postfixes = (attached == null || attached.Postfixes == null)
                    ? 0
                    : attached.Postfixes.Count;

                UnityEngine.Debug.Log("[Toncihana] cuisine immunity installed: food cannot make"
                    + " horndycibar ill. patch info on IllnessChance: postfix=" + postfixes);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] cuisine immunity: patch failed: " + e);
            }
        }

        /// <summary>
        /// Zeroes the illness chance, but only for an eater carrying our marker part, and only when
        /// there was a chance to begin with -- so nothing else about the roll changes.
        /// </summary>
        public static void Postfix(GameObject Actor, ref int __result)
        {
            if (__result <= 0 || Actor == null)
            {
                return;
            }

            if (Actor.HasPart<A2Raine_Horndycibar_Traits>())
            {
                __result = 0;
            }
        }
    }
}
