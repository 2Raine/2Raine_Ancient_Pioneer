using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World
{
    /// <summary>
    /// Puts the bait fabricator into Joppa, beside the mill.
    ///
    /// STRUCTURE COPIED FROM THIS WORKSPACE'S OWN MOD: A2Raine_Toncihana_VillagePlacement.cs places
    /// the Harvest priest the same way, and the reasoning there applies unchanged, so it is repeated
    /// only in outline:
    ///
    ///   * The watcher is a part on the PLAYER, because the object has to be created in a zone the
    ///     player may not have visited yet -- until he arrives there is nothing in it to carry a part.
    ///   * It listens to EnteredCellEvent rather than ZoneActivatedEvent. ZoneActivatedEvent is
    ///     delivered to Zone.Parts, and every vanilla part listening for it sits on an object placed
    ///     in the zone; no vanilla precedent has a player part receiving it. EnteredCellEvent does
    ///     reach player parts.
    ///   * A [PlayerMutator] attaches it to a new character and a [HasCallAfterGameLoaded] handler
    ///     attaches it to a loaded save, because a new game never fires CallAfterGameLoaded.
    ///   * Placement is recorded in game state so it survives saving without this class holding
    ///     instance state, and so it never happens twice.
    ///
    /// WHERE, AND WHY THERE
    /// --------------------
    /// (4,9) in Joppa's own cell coordinates. Joppa.rpm is plain XML and can be read directly; it
    /// shows the mill at "(6,7) Wooden Water Wheel" and "(3,9) JoppaMillSign", with (4,9) sitting
    /// empty between them. So this is a square vanilla leaves EMPTY inside its own mill yard -- not a
    /// spot added by another mod -- which means it works with or without CoQ Extended installed, and
    /// a forge next to a mill reads correctly.
    ///
    /// Coordinates are plain zone cells: a zone is 80x25 with the top-left at (0,0), the same grid
    /// Joppa.rpm is written in, so the wanted square is literally GetCell(4, 9).
    /// </summary>
    public static class A2Raine_FishingPlacement
    {
        /// <summary>
        /// The bench blueprint. Named for what it is, matching its display name "angler's bench".
        ///
        /// It was called 2Raine_BaitFabricator while it only made bait; it now also dresses fish, so
        /// the old name had stopped describing it. Three names are in play and they are meant to line
        /// up: the BLUEPRINT 2Raine_AnglersBench (XML), the PART A2Raine_FishingFabricator (the code
        /// below, prefixed with A because C# identifiers cannot start with a digit), and the DISPLAY
        /// name "angler's bench". The part's file is still called A2Raine_FishingFabricator.cs.
        /// </summary>
        public const string MACHINE_BLUEPRINT = "2Raine_AnglersBench";

        /// <summary>Set once the machine exists, so it is never placed twice.</summary>
        private const string DONE_KEY = "2Raine_FishingFabricatorPlaced";

        /// <summary>The square to aim for, in Joppa's own coordinates.</summary>
        private const int CELL_X = 4;
        private const int CELL_Y = 9;

        /// <summary>Joppa's parasang on the world map.</summary>
        private const int JOPPA_WX = 11;
        private const int JOPPA_WY = 22;

        /// <summary>Surface strata; Joppa's town is on it.</summary>
        private const int SURFACE_Z = 10;

        public static void TryPlaceIn(Zone Z)
        {
            if (Z == null)
            {
                return;
            }
            try
            {
                XRLGame game = The.Game;
                if (game == null || game.GetStringGameState(DONE_KEY, "") == "1")
                {
                    return;
                }
                if (Z.wX != JOPPA_WX || Z.wY != JOPPA_WY || Z.Z != SURFACE_Z)
                {
                    return;
                }
                // A reload of the same zone: the machine is already here.
                if (Z.FindObject(MACHINE_BLUEPRINT) != null)
                {
                    game.SetStringGameState(DONE_KEY, "1");
                    return;
                }

                // The wanted square, or the nearest usable one. The ring search exists because
                // "exactly this square" is fragile -- the player may be standing on it, or another mod
                // may have put something there. Searching outward costs a step instead of the placement.
                Cell where = FindUsableNear(Z, CELL_X, CELL_Y);
                if (where == null)
                {
                    // Nothing recorded, so the next zone activation tries again.
                    return;
                }

                GameObject machine = GameObjectFactory.Factory.CreateObject(MACHINE_BLUEPRINT);
                if (machine == null)
                {
                    UnityEngine.Debug.LogWarning("[Fishing] could not create '" + MACHINE_BLUEPRINT
                        + "'; is its blueprint loading?");
                    return;
                }
                where.AddObject(machine);

                game.SetStringGameState(DONE_KEY, "1");
                UnityEngine.Debug.Log("[Fishing] bait fabricator placed in '" + Z.ZoneID + "' at "
                    + where.X + "," + where.Y + " (wanted " + CELL_X + "," + CELL_Y + ")");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Fishing] placement failed: " + e);
            }
        }

        /// <summary>
        /// The wanted cell if it can take a piece of furniture, otherwise the nearest cell that can.
        /// Walks outward ring by ring so the machine stays as close to the mill as possible.
        /// </summary>
        private static Cell FindUsableNear(Zone Z, int X, int Y)
        {
            int maxRadius = Math.Max(Z.Width, Z.Height);
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // Only the ring at this radius; the inside was covered by earlier passes.
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        {
                            continue;
                        }
                        int cx = X + dx;
                        int cy = Y + dy;
                        if (cx < 0 || cy < 0 || cx >= Z.Width || cy >= Z.Height)
                        {
                            continue;
                        }
                        Cell cell = Z.GetCell(cx, cy);
                        if (IsFree(cell))
                        {
                            return cell;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Can furniture go here? Not solid terrain, no creatures, and not under water. Kept
        /// deliberately strict: a forge dropped into the river or on top of a villager would be worse
        /// than no forge.
        ///
        /// Cell.IsSolid() is the terrain test the base game uses for this (AjiConch.cs:137). For the
        /// water test this calls the pole's own FindLiquid rather than Cell.HasAquaticSupportFor,
        /// because the latter takes a creature and would be handed null here.
        /// </summary>
        private static bool IsFree(Cell C)
        {
            if (C == null || C.IsSolid())
            {
                return false;
            }
            foreach (GameObject obj in C.Objects)
            {
                if (obj.IsCreature)
                {
                    return false;
                }
            }
            return A2Raine_FishingPole.FindLiquid(C) == null;
        }
    }

    /// <summary>
    /// Watches for Joppa becoming live and places the machine then. Carried by the player -- see the
    /// class comment above for why this rather than a part on the zone.
    /// </summary>
    public class A2Raine_FishingPlacementPart : IPart
    {
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (E.Cell != null && E.Cell.ParentZone != null)
            {
                A2Raine_FishingPlacement.TryPlaceIn(E.Cell.ParentZone);
            }
            return base.HandleEvent(E);
        }
    }

    /// <summary>Attaches the watcher to a NEW character.</summary>
    [PlayerMutator]
    public class A2Raine_FishingPlacementMutator : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            player.RequirePart<A2Raine_FishingPlacementPart>();
        }
    }

    /// <summary>
    /// Attaches the watcher when a save is loaded. A new game never fires CallAfterGameLoaded, which
    /// is why both this and the mutator above are needed.
    /// </summary>
    [HasCallAfterGameLoaded]
    public class A2Raine_FishingPlacementLoadHandler
    {
        [CallAfterGameLoaded]
        public static void AfterGameLoadedCallback()
        {
            try
            {
                GameObject player = The.Player;
                if (player != null)
                {
                    player.RequirePart<A2Raine_FishingPlacementPart>();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Fishing] could not attach the placement watcher: " + e);
            }
        }
    }
}
