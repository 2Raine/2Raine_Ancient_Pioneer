using System;
using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Skill;

namespace Toncihana
{
    /// <summary>
    /// Grants (and repairs) the Elemental race's two innate mutations.
    ///
    /// WHY THIS EXISTS AT ALL (and why the XML alone does not work)
    /// -----------------------------------------------------------
    /// <mutation> is NOT a valid child of <genotype>. Verified directly against the shipped
    /// Assembly-CSharp.dll: XRL.GenotypeEntry, the class the genotype parser fills in, declares
    /// exactly these fields -- Name, DisplayName, MutationPoints, CyberneticsLicensePoints,
    /// StatPoints, RandomWeight, CharacterBuilderModules, BodyTypes, RestrictedGender, Subtypes,
    /// Class, Tile, Gear, DetailColor, BodyObject, BaseHPGain, BaseSPGain, BaseMPGain,
    /// StartingLocation, Species, IsMutant, IsTrueKin, _AllowedMutationCategories,
    /// _AllowedMutationCategoriesList, Constructor, Stats, Skills, RemoveSkills, Reputations,
    /// SaveModifiers, ExtraInfo, RemoveExtraInfo -- and there is NO mutation field anywhere in it.
    /// A <mutation> element under <genotype> is therefore parsed by nobody and silently dropped.
    ///
    /// THREE grants are registered on purpose. Each is independently guarded by HasMutation, so
    /// running any or all of them is safe, and between them every route into the race is covered:
    ///
    ///   1. [PlayerMutator] mutate()            -- runs when a NEW game starts.
    ///   2. A2Raine_Toncihana_GenotypeModule (embark)    -- runs at BOOTEVENT_BOOTPLAYEROBJECT for a new game.
    ///   3. [CallAfterGameLoaded]               -- runs on EVERY save load, which is the only way to
    ///                                             hand the mutations to a character that was created
    ///                                             before this mod existed. Without it, testing on an
    ///                                             existing save could never succeed.
    ///
    /// Routes 1 and 3 are the canonical shape used by published mods (Clever Girl pairs
    /// [PlayerMutator] with [HasCallAfterGameLoaded] and a static [CallAfterGameLoaded] reading the
    /// player via XRLCore.Core.Game.Player.Body).
    /// </summary>
    [PlayerMutator]
    [HasCallAfterGameLoaded]
    public class A2Raine_Toncihana_Mutator : IPlayerMutator
    {
        public const string GENOTYPE = "2Raine_AncientPioneer_Elemental";

        /// <summary>
        /// Mutations granted to every Toncihana. Each name MUST equal its Name= in
        /// Toncihana_Mutations.xml, which in turn MUST equal its Class=. The engine resolves the C#
        /// type through the Name, so a pretty name with spaces resolves to no type at all and
        /// AddMutation returns -1. Proven in game: 'Overcharged Electrical Generation' -> -1, versus
        /// vanilla 'Regeneration' -> 0, whose Name and Class are identical.
        /// </summary>
        private static readonly string[] InnateMutations =
        {
            "A2Raine_Toncihana_Stormcharge",
            "Regeneration",                       // vanilla mutation, reused as-is
        };

        /// <summary>
        /// Empty on purpose.
        ///
        /// This used to hold A2Raine_Toncihana_StormCalling, a BaseSkill -- which put a tree in the
        /// Skills screen that read "already learned" even while the abilities were withheld, because
        /// a skill tree is shown whenever the part exists. The abilities are unlocked by implanting
        /// links now and registered by A2Raine_Toncihana_StormLinks, a plain part, so there is
        /// nothing left for this list to grant. TryGrantSkill is kept because the repair path may
        /// want it again; it simply has nothing to walk.
        /// </summary>
        private static readonly string[] InnateSkills = new string[0];

        /// <summary>
        /// The four ability parts the skill attaches. Verified present in the load diagnostic so a
        /// silently-missing ability is visible in the log.
        /// </summary>
        private static readonly string[] InnateAbilities =
        {
            "A2Raine_Toncihana_ThunderLordDecree",   // 雷霆领主的法令
            "A2Raine_Toncihana_ThunderFire",         // 雷火
            "A2Raine_Toncihana_ThunderStep",         // 雷动
            "A2Raine_Toncihana_LightningSnake",      // 雷蛇
            "A2Raine_Toncihana_ThunderBreath",       // 雷扇
        };

        private const string Overcharged = "A2Raine_Toncihana_Stormcharge";

        private const string Regen = "Regeneration";

        // ------------------------------------------------------------------ route 1: new game
        public void mutate(GameObject player)
        {
            if (player == null)
            {
                return;
            }
            UnityEngine.Debug.LogWarning("[Toncihana] PlayerMutator fired. genotype='"
                + SafeGenotype(player) + "'");
            Grant(player, false);
        }

        // ------------------------------------------------------------------ route 3: save load
        [CallAfterGameLoaded]
        public static void GameLoadedCallback()
        {
            try
            {
                A2Raine_Toncihana_MutationGuard.Install();

                GameObject player = PlayerBody();
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Toncihana] load callback: no player body.");
                    return;
                }

                bool entryPresent = MutationFactory.GetMutationEntryByName(Overcharged) != null;

                int entryCount = 0;
                foreach (MutationEntry unused in MutationFactory.AllMutationEntries())
                {
                    entryCount++;
                }

                Mutations before = player.GetPart<Mutations>();

                // Any character that is not ours reports every granted mutation as missing --
                // the LOAD PROBLEM below said genotype='' on exactly such a save. Ask first.
                bool ours = IsToncihana(player);

                string report = "[Toncihana] LOAD DIAGNOSTIC"
                    + " | genotype='" + SafeGenotype(player) + "'"
                    + " | ours=" + ours
                    + " | physiology=" + player.HasPart<A2Raine_Toncihana_Physiology>()
                    + " | total-mutation-entries=" + entryCount
                    + " | mutations-part=" + (before != null);

                // Report every granted MUTATION by entry + presence.
                foreach (string name in InnateMutations)
                {
                    bool entry = MutationFactory.GetMutationEntryByName(name) != null;
                    bool has = before != null && before.HasMutation(name);
                    report += " | mut:" + name + "{entry=" + entry + ",has=" + has + "}";
                }

                // ...and every granted SKILL by part presence, which is the equivalent check.
                foreach (string name in InnateSkills)
                {
                    report += " | skill:" + name + "{has=" + player.HasPart(name) + "}";
                }

                // ...and the four ability parts the skill is responsible for attaching, so a
                // silently-missing ability shows up here rather than as "the ability does nothing".
                foreach (string name in InnateAbilities)
                {
                    report += " | ability:" + name + "{has=" + player.HasPart(name) + "}";
                }

                report += " | activatedAbilities=" + AbilityCount(player);
                report += " | ours:" + OurAbilityStatus(player);

                // Only speak when something is actually WRONG -- a healthy load stays silent, so the
                // log stays readable and a real fault stands out. Everything verified here was used
                // to find bugs that has since been fixed, so this is now a regression net, not a
                // progress report.
                bool problem = !entryPresent || before == null;
                foreach (string name in InnateMutations)
                {
                    if (before == null || !before.HasMutation(name))
                    {
                        problem = true;
                    }
                }
                // Both of these describe the UNLOCKED state only. While the storm abilities are
                // withheld, none registered and no carriers attached is exactly right, and checking
                // them anyway printed a LOAD PROBLEM on every single load.
                if (A2Raine_Toncihana_StormCalling.AbilitiesUnlocked)
                {
                    if (!AllOurAbilitiesRegistered(player))
                    {
                        problem = true;
                    }
                    foreach (string name in InnateAbilities)
                    {
                        if (!player.HasPart(name))
                        {
                            problem = true;
                        }
                    }
                }

                if (problem && ours)
                {
                    UnityEngine.Debug.LogWarning("[Toncihana] LOAD PROBLEM" + report);
                }

                // Repair route. Grant already refuses anyone who is not a Toncihana, so this can
                // never hand the subclass's mutations to another character.
                Grant(PlayerBody(), true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Toncihana] load callback failed: " + ex);
            }
        }

        // ------------------------------------------------------------------ shared
        /// <summary>Public entry point used by the embark module (route 2).</summary>
        public static void GrantTo(GameObject Player)
        {
            Grant(Player, false);
        }

        private static void Grant(GameObject Player, bool LogOnlyCompletions)
        {
            bool toncihana = IsToncihana(Player);

            // --- mutations first, so the charge store exists before the abilities that read it.
            //
            // These are SUBCLASS traits, not genotype traits: Stormcharge and Regeneration belong to
            // Toncihana alone, so they are gated on the Toncihana body part rather than on the
            // genotype. Without this gate any future Ancient Pioneer subclass would inherit them.
            if (toncihana)
            {
                try
                {
                    Mutations mutations = Player.RequirePart<Mutations>();
                    if (mutations == null)
                    {
                        UnityEngine.Debug.LogError("[Toncihana] player has no Mutations part!");
                    }
                    else
                    {
                        foreach (string name in InnateMutations)
                        {
                            TryGrantMutation(mutations, name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("[Toncihana] could not grant innate mutations: " + ex);
                }

                // --- then the four abilities, as SKILLS.
                foreach (string name in InnateSkills)
                {
                    TryGrantSkill(Player, name);
                }

                // --- the links: the register that holds the ability handles, and the inward eye
                // that opens the link screen. Both are plain parts, so neither adds a skill tree.
                // EnsureAbilities is also the load-time repair path -- it re-registers whatever the
                // implanted links say should exist.
                A2Raine_Toncihana_StormLinks links = Player.RequirePart<A2Raine_Toncihana_StormLinks>();
                if (links != null)
                {
                    links.EnsureAbilities(Player);
                }
                Player.RequirePart<A2Raine_Toncihana_InwardEye>();
                Player.RequirePart<A2Raine_Toncihana_Devour>();
            }
        }

        /// <summary>
        /// True only for a Toncihana character, so a FUTURE Ancient Pioneer subclass cannot inherit
        /// Toncihana's mutations and skill.
        ///
        /// Two independent tests, deliberately, because they can disagree in a way that silently
        /// costs the character its abilities:
        ///   1. the physiology part, which is what actually makes the mechanics work. It lives on the
        ///      Toncihana body blueprint, so it is present only once the subtype's BodyObject has
        ///      been applied -- and a subtype overriding the genotype's BodyObject is the one part of
        ///      this design that has never been confirmed in game.
        ///   2. the genotype name, which is set from the moment the character exists. This is the
        ///      fallback for the case where the body is not built yet, or where the subtype's
        ///      BodyObject override did not take.
        ///
        /// Requiring BOTH would be fragile for the reason above; accepting EITHER is safe, because
        /// the genotype test alone already excludes every other Ancient Pioneer.
        /// </summary>
        private static bool IsToncihana(GameObject Player)
        {
            if (Player == null)
            {
                return false;
            }
            if (Player.HasPart<A2Raine_Toncihana_Physiology>())
            {
                return true;
            }
            try
            {
                string genotype = Player.GetGenotype();
                return !string.IsNullOrEmpty(genotype)
                    && genotype.IndexOf("Toncihana", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Make sure the innate skill is attached AND that its four abilities are actually
        /// registered.
        ///
        /// WHY THIS DOES NOT JUST CHECK HasPart
        /// -----------------------------------
        /// It used to: "if (Player.HasPart(ClassName)) return;" -- and that guard made the bug
        /// permanent instead of self-healing. A save can hold the skill PART without the abilities
        /// having been registered, which is exactly what happened here (the part was attached with
        /// RequirePart, whose AddSkill never ran). HasPart was then true forever after, so every
        /// subsequent load took the "already has it, skipping" branch and the abilities could never
        /// appear. The lesson: the guard has to test the thing that was actually missing.
        ///
        /// So this checks the registered abilities. If any of the four commands is unregistered it
        /// re-runs AddSkill(string); AddSkill is idempotent for an already-attached part, and
        /// registering an ability that already exists is a no-op, so re-running is safe.
        /// </summary>
        private static void TryGrantSkill(GameObject Player, string ClassName)
        {
            // With the abilities withheld there is nothing to repair, and running the checks anyway
            // would print "repairing skill ..." on every single load -- the ability count can never
            // be satisfied while the switch is off. The skill part itself is still wanted, because
            // the skill tree reads it.
            if (!A2Raine_Toncihana_StormCalling.AbilitiesUnlocked)
            {
                if (!Player.HasPart(ClassName))
                {
                    Player.AddSkill(ClassName);
                }
                return;
            }

            try
            {
                bool hasPart = Player.HasPart(ClassName);
                bool allRegistered = AllOurAbilitiesRegistered(Player);

                if (hasPart && allRegistered)
                {
                    return;
                }

                UnityEngine.Debug.LogWarning("[Toncihana] repairing skill '" + ClassName
                    + "' (hasPart=" + hasPart + ", allAbilitiesRegistered=" + allRegistered + ")");

                // Call the skill's own registration routine DIRECTLY instead of
                // Player.AddSkill(ClassName).
                //
                // AddSkill(string) looks like the right call -- it is how mods attach a skill by name
                // (Object.AddSkill("Survival_Camp")) and it is what runs the skill part's AddSkill().
                // But Qud's Skills.AddSkill bails out when the part is ALREADY attached, so on any
                // save that has the part it does nothing at all. The override never runs, and a newly
                // added ability can never appear. The log proved it: this "repairing skill" line was
                // printed while the skill's own "registered N abilities" line never was.
                //
                // EnsureAbilities is idempotent, so calling it on a healthy character is harmless.
                A2Raine_Toncihana_StormCalling skill = Player.GetPart<A2Raine_Toncihana_StormCalling>();
                if (skill != null)
                {
                    skill.EnsureAbilities(Player);
                }
                else
                {
                    Player.AddSkill(ClassName);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Toncihana] could not add skill '" + ClassName + "': " + ex);
            }
        }

        /// <summary>How many activated abilities the object currently has, for the diagnostic log.</summary>
        private static int AbilityCount(GameObject Player)
        {
            ActivatedAbilities abilities = Player.GetPart<ActivatedAbilities>();
            return abilities == null ? -1 : abilities.GetAbilityCount();
        }

        /// <summary>
        /// The NAMES of every activated ability actually registered on the object.
        ///
        /// A count is not evidence on its own: abilities can be registered and still be missing from
        /// the ability menu, and the count cannot distinguish those cases. Listing the names shows
        /// what the engine believes it has, and whether ours are among them.
        /// </summary>
        private static string AbilityNames(GameObject Player)
        {
            try
            {
                ActivatedAbilities abilities = Player.GetPart<ActivatedAbilities>();
                if (abilities == null)
                {
                    return "<no ActivatedAbilities part>";
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (object ability in abilities.GetAbilityListOrderedByPreference())
                {
                    ActivatedAbilityEntry entry = ability as ActivatedAbilityEntry;
                    if (entry == null)
                    {
                        sb.Append(" [").Append(ability == null ? "null" : ability.GetType().Name)
                          .Append("?]");
                        continue;
                    }
                    // FLAG_VISIBLE is what decides whether the ability shows up in the ability menu at
                    // all, so report it explicitly rather than just the raw bitfield.
                    bool visible = (entry.Flags & ActivatedAbilityEntry.FLAG_VISIBLE) != 0;
                    bool enabled = (entry.Flags & ActivatedAbilityEntry.FLAG_ENABLED) != 0;
                    sb.Append(" [").Append(entry.DisplayName)
                      .Append(" cmd=").Append(entry.Command)
                      .Append(" class=").Append(entry.Class)
                      .Append(" flags=").Append(entry.Flags)
                      .Append(" visible=").Append(visible)
                      .Append(" enabled=").Append(enabled)
                      .Append(']');
                }
                return sb.Length == 0 ? "<none registered>" : sb.ToString();
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private static void TryGrantMutation(Mutations Mutations, string Name)
        {
            // The entry must exist before AddMutation can do anything with it, and a missing entry
            // fails silently, so check it explicitly and say so.
            MutationEntry entry = MutationFactory.GetMutationEntryByName(Name);
            if (entry == null)
            {
                UnityEngine.Debug.LogError("[Toncihana] no mutation entry named '" + Name
                    + "' -- the mutations xml did not load, or the name does not match.");
                return;
            }

            if (Mutations.HasMutation(Name))
            {
                return;
            }

            // AddMutation returns -1 on failure and a non-negative id on success, and failures are
            // otherwise silent -- so a bad return is worth a line even on a healthy load.
            int result = Mutations.AddMutation(Name, 1);
            if (result < 0 || !Mutations.HasMutation(Name))
            {
                UnityEngine.Debug.LogError("[Toncihana] AddMutation('" + Name + "', 1) -> " + result
                    + "; now HasMutation=" + Mutations.HasMutation(Name));
            }
        }

        /// <summary>The five commands our five abilities register under, in order.</summary>
        private static readonly string[] OurCommands =
        {
            "CommandA2Raine_Toncihana_ThunderLordDecree",
            "CommandA2Raine_Toncihana_ThunderFire",
            "CommandA2Raine_Toncihana_ThunderStep",
            "CommandA2Raine_Toncihana_LightningSnake",
            "CommandA2Raine_Toncihana_ThunderBreath",
        };

        /// <summary>
        /// True only when all of our activated abilities are registered.
        ///
        /// Only meaningful while the abilities are unlocked. With them withheld this is never
        /// consulted for a verdict -- see the call site -- because "none registered" is the correct
        /// state and treating it as a fault would print a false LOAD PROBLEM on every load.
        /// </summary>
        private static bool AllOurAbilitiesRegistered(GameObject Player)
        {
            ActivatedAbilities abilities = Player.GetPart<ActivatedAbilities>();
            if (abilities == null)
            {
                return false;
            }
            foreach (string command in OurCommands)
            {
                if (!(abilities.GetAbilityByCommand(command) is ActivatedAbilityEntry))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Report whether each of our four abilities is actually present in the owner's
        /// ActivatedAbilities list.
        ///
        /// This is the check that was missing before: the parts were all attached and HasPart was
        /// true for every one of them, yet none of the abilities were registered, so the count of
        /// abilities looked plausible while the menu was empty. HasPart proves the CARRIER is
        /// attached; only this proves the ABILITY exists.
        /// </summary>
        private static string OurAbilityStatus(GameObject Player)
        {
            try
            {
                ActivatedAbilities abilities = Player.GetPart<ActivatedAbilities>();
                if (abilities == null)
                {
                    return "<no ActivatedAbilities part>";
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string command in OurCommands)
                {
                    string label = command.Replace("CommandToncihana", "");
                    ActivatedAbilityEntry entry =
                        abilities.GetAbilityByCommand(command) as ActivatedAbilityEntry;
                    if (entry == null)
                    {
                        sb.Append(" | ").Append(label).Append("=MISSING");
                        continue;
                    }
                    sb.Append(" | ").Append(label)
                      .Append("=registered(toggle=")
                      .Append(entry.Flags & ActivatedAbilityEntry.FLAG_TOGGLE)
                      .Append(", active=")
                      .Append(entry.Flags & ActivatedAbilityEntry.FLAG_TOGGLE_ACTIVE)
                      .Append(')');
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "<error: " + ex.Message + ">";
            }
        }

        private static GameObject PlayerBody()
        {
            if (XRLCore.Core == null || XRLCore.Core.Game == null || XRLCore.Core.Game.Player == null)
            {
                return null;
            }
            return XRLCore.Core.Game.Player.Body;
        }

        private static string SafeGenotype(GameObject Player)
        {
            try
            {
                return Player.GetGenotype();
            }
            catch
            {
                return "<error>";
            }
        }
    }
}
