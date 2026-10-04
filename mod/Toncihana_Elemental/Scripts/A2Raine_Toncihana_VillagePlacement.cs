using System;
using System.Collections.Generic;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;

namespace Toncihana
{
    /// <summary>
    /// Puts Osheb, the Harvest priest, into Joppa -- at (66,9) on Joppa's map.
    ///
    /// SCOPE
    /// -----
    /// Joppa only, by decision. Character creation offers four procedurally generated starting
    /// villages besides Joppa, and supporting those needs a different anchor (there is no fixed map
    /// to read a square off). This file does not attempt it.
    ///
    /// Coordinates are plain zone cells. A zone is 80x25 and its top-left is (0,0), the same grid as
    /// Joppa.rpm -- so the wanted square is literally GetCell(66, 9). Vanilla places objects by
    /// coordinate 573 times and never converts; the "/ 3" arithmetic that does exist in vanilla
    /// belongs to addressing the WORLD MAP, which is a different thing entirely. Do not add a
    /// conversion here.
    ///
    /// HOW THE SQUARE IS REACHED
    /// -------------------------
    /// A part on the player notices the cell it enters and hands the zone to TryPlaceIn. The zone is
    /// checked to be Joppa's ground level, the priest is placed once, and the fact is recorded in the
    /// game state so it survives saving without this class holding any instance state.
    ///
    /// If the exact square happens to be taken -- by the player, by a quest object, by another mod --
    /// the search walks outward ring by ring and takes the nearest usable square, so a busy square
    /// costs a step or two rather than the whole placement. Only if that fails does it fall back to
    /// the warden, then a villager, then any empty square.
    /// </summary>
    public static class A2Raine_Toncihana_VillagePlacement
    {
        public const string NPC_BLUEPRINT = "2Raine_Toncihana_Keeper";

        /// <summary>Set once the priest has been put somewhere, so he is never placed twice.</summary>
        private const string DONE_KEY = "2Raine_Toncihana_KeeperPlaced";

        /// <summary>
        /// The square he stands on, in Joppa's own cell coordinates.
        /// </summary>
        private const int PRIEST_CELL_X = 66;
        private const int PRIEST_CELL_Y = 9;

        /// <summary>Joppa's parasang on the world map.</summary>
        private const int JOPPA_WX = 11;
        private const int JOPPA_WY = 22;

        /// <summary>Surface strata level; Joppa's town is on it.</summary>
        private const int SURFACE_Z = 10;

        /// <summary>
        /// True while the priest is expected to be placed by this code.
        ///
        /// The old Joppa.rpm map patch has been retired precisely so that there is only one mechanism.
        /// Leaving both in place would put two priests in Joppa.
        /// </summary>
        public static bool Enabled = true;

        public static void TryPlaceIn(Zone Z)
        {
            if (!Enabled || Z == null)
            {
                return;
            }

            try
            {
                XRLGame game = The.Game;
                if (game == null)
                {
                    return;
                }

                if (game.GetStringGameState(DONE_KEY, "") == "1")
                {
                    return;
                }

                if (!IsJoppaGroundLevel(Z))
                {
                    return;
                }

                // A reload of the same zone, or the player walked him in from elsewhere.
                if (Z.FindObject(NPC_BLUEPRINT) != null)
                {
                    game.SetStringGameState(DONE_KEY, "1");
                    return;
                }

                Cell where = null;
                string anchor = "";

                // The requested square, or the nearest usable square to it.
                where = FindNearestUsableTo(Z, PRIEST_CELL_X, PRIEST_CELL_Y);
                if (where != null)
                {
                    anchor = "the path south of the shrine";
                }

                if (where == null)
                {
                    where = FindSpotBesideTheWarden(Z);
                    anchor = "a warden";
                }
                if (where == null)
                {
                    where = FindSpotNextToAResident(Z);
                    anchor = "a villager";
                }
                if (where == null)
                {
                    where = FindAnyEmptyCell(Z);
                    anchor = "an empty cell";
                }
                if (where == null)
                {
                    // Nothing was recorded, so the next zone activation tries again.
                    return;
                }

                GameObject priest = GameObjectFactory.Factory.CreateObject(NPC_BLUEPRINT);
                if (priest == null)
                {
                    UnityEngine.Debug.LogWarning("[Toncihana] could not create '" + NPC_BLUEPRINT
                        + "'; is its blueprint loading?");
                    return;
                }
                where.AddObject(priest);

                game.SetStringGameState(DONE_KEY, "1");
                UnityEngine.Debug.LogWarning("[Toncihana] Harvest priest placed in '" + Z.ZoneID
                    + "' at " + where.X + "," + where.Y + ", by " + anchor
                    + " (wanted " + PRIEST_CELL_X + "," + PRIEST_CELL_Y + ").");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] village placement failed: " + ex);
            }
        }

        /// <summary>Joppa's ground level, by parasang and strata only.</summary>
        private static bool IsJoppaGroundLevel(Zone Z)
        {
            return Z.wX == JOPPA_WX && Z.wY == JOPPA_WY && Z.Z == SURFACE_Z;
        }

        /// <summary>
        /// The wanted square if it is usable, otherwise the nearest usable square to it.
        ///
        /// The ring search exists because "exactly this square" is a fragile request: it may hold the
        /// player, a quest object, or something another mod placed. Searching outward keeps the priest
        /// on the requested patch of path rather than discarding the whole idea.
        /// </summary>
        private static Cell FindNearestUsableTo(Zone Z, int X, int Y)
        {
            int maxRadius = Math.Max(Z.Width, Z.Height);
            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        // Only the ring at distance r; the interior was covered by earlier passes.
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        {
                            continue;
                        }
                        int x = X + dx;
                        int y = Y + dy;
                        if (x < 0 || y < 0 || x >= Z.Width || y >= Z.Height)
                        {
                            continue;
                        }
                        Cell cell = Z.GetCell(x, y);
                        if (IsUsable(cell))
                        {
                            return cell;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>A free cell beside the village warden, or null.</summary>
        private static Cell FindSpotBesideTheWarden(Zone Z)
        {
            foreach (GameObject obj in Z.GetObjects())
            {
                if (obj.GetIntProperty("VillageWarden") > 0 && obj.CurrentCell != null)
                {
                    return FirstFreeNeighbour(obj.CurrentCell);
                }
            }
            return null;
        }

        /// <summary>A free cell beside any villager, or null.</summary>
        private static Cell FindSpotNextToAResident(Zone Z)
        {
            List<Cell> candidates = new List<Cell>();
            foreach (GameObject obj in Z.GetObjects())
            {
                if (obj.CurrentCell == null || obj == The.Player)
                {
                    continue;
                }
                if (!obj.HasPart<Brain>() || obj.IsHostileTowards(The.Player))
                {
                    continue;
                }
                candidates.Add(obj.CurrentCell);
            }
            Shuffle(candidates);
            foreach (Cell cell in candidates)
            {
                Cell free = FirstFreeNeighbour(cell);
                if (free != null)
                {
                    return free;
                }
            }
            return null;
        }

        /// <summary>The first free neighbour of a cell, diagonal steps included, or null.</summary>
        private static Cell FirstFreeNeighbour(Cell From)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    Cell cell = From.ParentZone.GetCell(From.X + dx, From.Y + dy);
                    if (IsUsable(cell))
                    {
                        return cell;
                    }
                }
            }
            return null;
        }

        /// <summary>Any free cell in the zone, or null.</summary>
        private static Cell FindAnyEmptyCell(Zone Z)
        {
            List<Cell> empty = Z.GetEmptyCells();
            if (empty == null || empty.Count == 0)
            {
                return null;
            }
            Shuffle(empty);
            foreach (Cell cell in empty)
            {
                if (IsUsable(cell))
                {
                    return cell;
                }
            }
            return null;
        }

        /// <summary>
        /// A cell the priest can be put on: passable, unoccupied, and not open liquid.
        ///
        /// IsEmpty already rejects a cell holding anything solid, which covers brine stalk and
        /// watervine. The LiquidVolume test is what additionally rejects open water, whose cell has
        /// no object in it to reject.
        /// </summary>
        private static bool IsUsable(Cell Cell)
        {
            if (Cell == null || !Cell.IsPassable() || !Cell.IsEmpty())
            {
                return false;
            }
            return !Cell.HasObjectWithPart("LiquidVolume");
        }

        /// <summary>
        /// Shuffle with the engine's seeded RNG.
        ///
        /// Hand-rolled rather than reaching for an XRL helper whose namespace could not be confirmed.
        /// XRL.Rules.Stat.Random is the generator the rest of this mod already uses.
        /// </summary>
        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = XRL.Rules.Stat.Random(0, i);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>
    /// Watches for Joppa becoming live and places the priest then.
    ///
    /// WHY THIS LISTENS TO EnteredCellEvent
    /// ------------------------------------
    /// The part is carried by the PLAYER, because the priest has to be created in a zone the player
    /// may not have visited yet and nothing exists there to carry a part until he is.
    ///
    /// ZoneActivatedEvent looks like the natural fit -- it is sent from Zone.Activated() -- but it is
    /// delivered to Zone.Parts, and every vanilla IPart that listens for it is mounted on an object
    /// sitting IN the zone, not on the player. AfterBrightsheolCourtSpawner, the clearest example,
    /// reaches its zone through base.currentCell.ParentZone, which only works because it is itself
    /// placed in a cell. There is no vanilla precedent for a player part receiving ZoneActivatedEvent.
    ///
    /// EnteredCellEvent, by contrast, definitely reaches player parts -- that is how vanilla
    /// GlowsphereProperties works -- so the zone is reached through E.Cell.ParentZone. It fires as the
    /// player moves, so TryPlaceIn is entered often; it answers from a single game state string in the
    /// common case and returns immediately.
    /// </summary>
    public class A2Raine_Toncihana_PlacementPart : IPart
    {
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (E.Cell != null && E.Cell.ParentZone != null)
            {
                A2Raine_Toncihana_VillagePlacement.TryPlaceIn(E.Cell.ParentZone);
            }
            return base.HandleEvent(E);
        }
    }

    /// <summary>Attaches the watcher to a NEW character.</summary>
    [PlayerMutator]
    public class A2Raine_Toncihana_PlacementMutator : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            player.RequirePart<A2Raine_Toncihana_PlacementPart>();
        }
    }

    /// <summary>
    /// Attaches the watcher when a save is loaded. A new game never fires CallAfterGameLoaded, which
    /// is why both this and the mutator above are needed.
    /// </summary>
    [HasCallAfterGameLoaded]
    public class A2Raine_Toncihana_PlacementLoadHandler
    {
        [CallAfterGameLoaded]
        public static void AfterGameLoadedCallback()
        {
            try
            {
                GameObject player = The.Player;
                if (player != null)
                {
                    player.RequirePart<A2Raine_Toncihana_PlacementPart>();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] could not attach the placement watcher: " + ex);
            }
        }
    }
}
