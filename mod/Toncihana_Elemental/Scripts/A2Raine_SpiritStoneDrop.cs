using System;
using XRL;
using XRL.Rules;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// A rare world drop: any creature has a small chance to be carrying a spirit stone, and it is
    /// left on the floor where the creature falls.
    ///
    /// WHY THE STONE GOES ON THE CELL, NOT ON THE BODY
    /// -----------------------------------------------
    /// Qud has two ways an item survives a death, and both are worse here:
    ///   * EQUIPPED items are dropped by Body.UnequipPartAndChildren(..., ForDeath: true), but that
    ///     needs a free body part -- only "Floating Nearby" is universal, via Anatomy.ApplyTo.
    ///   * INVENTORY items only transfer into a CORPSE, and Corpse.cs:129-131 needs CorpseChance
    ///     plus a non-null CorpseBlueprint; the stock Creature template has neither. Even then the
    ///     player has to butcher the body.
    /// Placing the stone on GetDropCell() avoids both paths: nothing is worn, no corpse is needed,
    /// and the stone lands where the creature died.
    ///
    /// GetDropCell (GameObject.cs:13686-13709) returns the creature's own cell, or a free adjacent
    /// one when its cell has turned solid, preferring whichever is nearest the player.
    ///
    /// This part reaches every creature by being merged onto the Creature base blueprint (see
    /// ObjectBlueprints/2Raine_Toncihana_SpiritStoneDrop.xml) -- the same shape Qud Expanded uses
    /// for Vixy_Reequip at 3785441196/ObjectBlueprints/Creatures.xml:492.
    /// </summary>
    [Serializable]
    public class A2Raine_SpiritStoneDrop : IPart
    {
        /// <summary>Percent chance per creature (1 = one in a hundred).</summary>
        public int Chance = 1;

        public string Blueprint = "2Raine_SpiritStone";

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID;
        }

        public override bool HandleEvent(BeforeDeathRemovalEvent E)
        {
            // The event goes to the dying creature's own parts (that is how Corpse uses it), so
            // ParentObject is the one dying. The player is excluded: they are a Creature too, and
            // would otherwise be born holding a stone.
            if (!ParentObject.IsPlayer() && Chance.in100())
            {
                Drop();
            }
            return base.HandleEvent(E);
        }

        private void Drop()
        {
            Cell cell = ParentObject.GetDropCell();
            if (cell == null)
            {
                return;
            }
            GameObject stone = GameObjectFactory.Factory.CreateObject(Blueprint);
            if (stone == null)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] spirit stone drop: no blueprint '"
                    + Blueprint + "'");
                return;
            }
            cell.AddObject(stone);
        }
    }
}
