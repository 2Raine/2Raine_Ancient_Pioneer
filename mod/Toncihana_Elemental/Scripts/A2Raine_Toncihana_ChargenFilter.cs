using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.CharacterBuilds.Qud.UI;
using XRL.UI.Framework;

namespace XRL.World.Parts
{
    /// <summary>
    /// Keeps vanilla's "Electrical Generation" out of the mutation picker when the character being
    /// built is a Toncihana.
    ///
    /// WHY THIS IS AN INTERFACE PATCH
    /// ------------------------------
    /// The picker is built by QudMutationsModuleWindow.ClearNodes()
    /// (qud_src/XRL/CharacterBuilds/Qud/UI/QudMutationsModuleWindow.cs:277-312). Its filter is
    /// `!entry.Hidden` (:299, :310) -- Exclusions plays no part there, it only governs whether two
    /// mutations may coexist. A genotype can only whitelist whole CATEGORIES
    /// (AllowedMutationCategories, consumed at QudGenotypeModule.cs:86), and Electrical Generation
    /// sits in Physical alongside dozens of mutations we want to keep.
    ///
    /// So the only way to hide that ONE entry is to drop it from the lists this method just built.
    /// That is deliberately an interface-level change: nothing global is flagged, nothing is stored
    /// on the character, and MutationEntry.Hidden is left alone. It follows that the rule is bound
    /// to the genotype being built, not to a save -- if a Toncihana is later dominated into another
    /// body, GetGenotype() reports that body's genotype, this filter stops matching, and the new
    /// body may learn vanilla Electrical Generation normally.
    ///
    /// WHY HARMONY
    /// -----------
    /// ClearNodes is public but its two lists (mutationNodes :88, categoryMenus :82) are private,
    /// and there is no event at this point. Same situation as the warm-static guard, so the same
    /// rules apply (AGENTS.md rule 12): no [HarmonyPatch] attribute, explicit instance, try/catch.
    /// </summary>
    public static class A2Raine_Toncihana_ChargenFilter
    {
        /// <summary>
        /// The XML Name of the vanilla mutation to hide -- and it is "Electrical Generation" WITH a
        /// space. Its Class is ElectricalGeneration, but MutationEntry.Name is the Name attribute
        /// (Base/Mutations.xml:16), and that is the value compared everywhere else.
        /// </summary>
        private const string HIDDEN_MUTATION = "Electrical Generation";

        /// <summary>
        /// The body blueprint that identifies our genotype. Matching on this rather than on a
        /// substring of the genotype NAME is deliberate: our genotype is called
        /// "2Raine_AncientPioneer_Elemental" (Genotypes.xml:4) and contains no "Toncihana" at all,
        /// so a name marker silently never matched. The body is what actually defines the race.
        /// </summary>
        private const string OUR_BODY = "2Raine_AncientPioneer_Body";

        private static bool installed;

        /// <summary>
        /// Runs once when mods finish loading -- EARLIER than any other entry point.
        ///
        /// This is the one that matters here: the mutation picker is shown during character
        /// creation, which happens before PlayerMutator.mutate() and before the load callback, so
        /// installing from those two left the picker unpatched. Verified in the log: with only
        /// those two, a session that sat in chargen produced no [Toncihana] output at all.
        /// </summary>
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
                    typeof(QudMutationsModuleWindow), "ClearNodes");

                if (target == null)
                {
                    UnityEngine.Debug.LogError("[Toncihana] chargen filter: ClearNodes not found;"
                        + " vanilla Electrical Generation stays selectable for Toncihana.");
                    return;
                }

                new Harmony("2Raine.Toncihana.ChargenFilter").Patch(
                    target,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(A2Raine_Toncihana_ChargenFilter), "Postfix")));

                Patches attached = Harmony.GetPatchInfo(target);
                int postfixCount = attached == null ? -1 : attached.Postfixes.Count;

                UnityEngine.Debug.Log("[Toncihana] chargen filter installed: '" + HIDDEN_MUTATION
                    + "' is hidden for genotypes using body '" + OUR_BODY
                    + "'. patch info on ClearNodes: postfix=" + postfixCount);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] chargen filter patch failed: " + e);
            }
        }

        /// <summary>Drops the borrowed mutation from the picker, but only for our genotype.</summary>
        public static void Postfix(QudMutationsModuleWindow __instance)
        {
            try
            {
                Traverse traverse = Traverse.Create(__instance);

                QudMutationsModule module = traverse.Field("module").GetValue<QudMutationsModule>();
                if (module == null || module.builder == null)
                {
                    return;
                }

                // The genotype THIS character is being built with -- not anything stored on a body.
                QudGenotypeModule genotypeModule = module.builder.GetModule<QudGenotypeModule>();
                string selected = genotypeModule == null ? null : genotypeModule.getSelected();
                if (selected == null)
                {
                    return;
                }

                // Ask the genotype entry what body it uses; that is the race identity.
                bool ours = false;
                string body = "(unknown)";
                Dictionary<string, GenotypeEntry> byName = genotypeModule.genotypesByName;
                if (byName != null)
                {
                    GenotypeEntry entry;
                    if (byName.TryGetValue(selected, out entry) && entry != null)
                    {
                        body = entry.BodyObject;
                        ours = entry.BodyObject == OUR_BODY;
                    }
                }

                UnityEngine.Debug.Log("[Toncihana] chargen filter: genotype='" + selected
                    + "', body='" + body + "', ours=" + ours + ".");

                if (!ours)
                {
                    return;
                }

                int removed = 0;

                List<QudMutationsModuleWindow.MLNode> nodes =
                    traverse.Field("mutationNodes").GetValue<List<QudMutationsModuleWindow.MLNode>>();
                if (nodes != null)
                {
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        QudMutationsModuleWindow.MLNode node = nodes[i];
                        if (node != null && node.Entry != null && node.Entry.Name == HIDDEN_MUTATION)
                        {
                            nodes.RemoveAt(i);
                            removed++;
                        }
                    }
                }

                // The visible menu entries carry the same value in their Id (makeMenuOption :269).
                List<CategoryMenuData> menus = traverse.Field("categoryMenus").GetValue<List<CategoryMenuData>>();
                if (menus != null)
                {
                    for (int m = 0; m < menus.Count; m++)
                    {
                        CategoryMenuData menu = menus[m];
                        if (menu == null || menu.menuOptions == null)
                        {
                            continue;
                        }
                        for (int i = menu.menuOptions.Count - 1; i >= 0; i--)
                        {
                            if (menu.menuOptions[i] != null && menu.menuOptions[i].Id == HIDDEN_MUTATION)
                            {
                                menu.menuOptions.RemoveAt(i);
                                removed++;
                            }
                        }
                    }
                }

                if (removed > 0)
                {
                    UnityEngine.Debug.Log("[Toncihana] chargen filter: removed " + removed
                        + " entry/entries for '" + HIDDEN_MUTATION + "' from genotype '"
                        + selected + "'.");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Toncihana] chargen filter postfix failed: " + e);
            }
        }
    }
}
