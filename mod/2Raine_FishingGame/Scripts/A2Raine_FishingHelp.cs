using System;
using System.Reflection;
using ConsoleLib.Console;
using HarmonyLib;
using XRL;
using XRL.UI;

namespace XRL.World.Parts
{
    /// <summary>
    /// Points the Sifrah help key at this mod's own book instead of the game's missing one.
    ///
    /// WHY THIS NEEDS A PATCH AT ALL: SifrahGame.ShowHelp is
    ///
    ///     public static void ShowHelp()                 // SifrahGame.cs:622 -- static, NOT virtual
    ///     {
    ///         BookUI.ShowBookByID("Sifrah");            // a literal, and no book with that ID exists
    ///     }
    ///
    /// and it is called from inside Play() (SifrahGame.cs:726-731), which is not virtual either. So a
    /// subclass cannot redirect it, and the only way for "?" to open something useful is to patch the
    /// method or to squat the shared global ID "Sifrah". Patching lets this mod own a private book id
    /// (2Raine_Fishing_Help) and leave the global namespace alone.
    ///
    /// A PREFIX, NOT A POSTFIX: ShowHelp's whole body is a blocking call -- ShowBookByID runs its own
    /// read loop and does not return until the reader presses Escape -- so a postfix would fire only
    /// after the book had closed, which is useless for choosing which book opens. The Harmony research
    /// note in the repo (qud_csharp_harmony_research_note.md:1106) ranks postfixes first and warns
    /// that "prefix patches that prevent the main function from running ... should be avoided unless
    /// they are the only option". This is that case.
    ///
    /// HOW IT IS INSTALLED, deliberately: by hand, with new Harmony(id).Patch(...) rather than with a
    /// [HarmonyPatch] attribute, because ModInfo.ApplyHarmonyPatches (ModInfo.cs:845-857) only fires
    /// Harmony.PatchAll when some type carries a HarmonyAttribute -- and PatchAll is all-or-nothing, so
    /// one bad patch would take every other patch in this assembly down with it. Installing by hand
    /// keeps a failure here local, and the whole install sits in a try/catch besides.
    ///
    /// THE TIMING HOOK: [HasCallAfterGameLoaded] on the class plus [CallAfterGameLoaded] on the method.
    /// ModManager.GetMethodsWithAttribute (ModManager.cs:1220-1250) uses the class-level attribute as a
    /// type filter and then selects only methods carrying the method-level attribute, so the Prefix
    /// below is never invoked as a callback.
    /// </summary>
    [HasCallAfterGameLoaded]
    public static class A2Raine_FishingHelp
    {
        /// <summary>This mod's private book id. Deliberately not "Sifrah".</summary>
        public const string BOOK_ID = "2Raine_Fishing_Help";

        private const string HARMONY_ID = "2Raine_FishingGame.SifrahHelp";

        private static bool Installed;

        [CallAfterGameLoaded]
        public static void Install()
        {
            if (Installed)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony(HARMONY_ID);

                MethodInfo showHelp = typeof(SifrahGame).GetMethod("ShowHelp", BindingFlags.Public | BindingFlags.Static);
                MethodInfo helpPrefix = typeof(A2Raine_FishingHelp).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                if (showHelp != null && helpPrefix != null)
                {
                    harmony.Patch(showHelp, prefix: new HarmonyMethod(helpPrefix));
                    Installed = true;
                    UnityEngine.Debug.Log("[Fishing] Harmony installed: help key -> book '" + BOOK_ID + "'");
                }
                else
                {
                    UnityEngine.Debug.Log("[Fishing] SifrahGame.ShowHelp not found; help key left alone");
                }

                // A second patch used to live here, on ConsoleLib.Console.Keyboard.getvk. It existed
                // only to unstick SifrahGame.Play's exit loop, which waits on Space / Enter / Escape --
                // two of which are command-bound and never reach the keyboard queue (KeyMap.cs:260-261
                // + GameManager.cs:1894). Fishing no longer runs inside SifrahGame at all, so that
                // patch is gone: it touched every keypress in the game to serve a screen we no longer
                // open.
            }
            catch (Exception e)
            {
                // A failed patch must stay local. The help key simply keeps its vanilla behaviour,
                // which is to open a book that does not exist.
                UnityEngine.Debug.Log("[Fishing] help redirect failed, vanilla behaviour kept: " + e);
            }
        }

        /// <summary>
        /// Replaces the body of ShowHelp. Returning false skips the original, so the literal
        /// "Sifrah" is never looked up. If our own book is somehow unavailable, fall back by
        /// returning true and letting the base game do what it always did.
        /// </summary>
        public static bool Prefix()
        {
            try
            {
                BookUI.ShowBookByID(BOOK_ID);
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("[Fishing] help book '" + BOOK_ID + "' failed, using vanilla: " + e);
                return true;
            }
        }
    }
}
