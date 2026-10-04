using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

/// THE NAMESPACE IS LOAD-BEARING -- DO NOT MOVE THIS CLASS.
///
/// Qud resolves a blueprint's <part Name="X" /> by CONCATENATING a fixed prefix with X and nothing
/// else. The call sites all look like
///     ModManager.ResolveType("XRL.World.Parts." + Part)
/// in ActionForwarder, GameObject.AddPart(string), Zone.GetPart and MapFileObjectBlueprint. There is
/// no fallback to the bare type name that helps here, and writing the fully qualified name in the XML
/// does NOT work either -- the engine just prepends its prefix to that too, producing the nonsense
/// type "XRL.World.Parts.Toncihana.A2Raine_BornEquipped".
///
/// Both mistakes were made and both are visible in Player.log:
///     <part Name="A2Raine_BornEquipped" />            -> Could not find XRL.World.Parts.A2Raine_BornEquipped
///     <part Name="Toncihana.A2Raine_BornEquipped" />  -> Could not find XRL.World.Parts.Toncihana.A2Raine_BornEquipped
///
/// So a part of ours must live in XRL.World.Parts and be referenced by its bare name. That is exactly
/// what A2Raine_Toncihana_Physiology does, which is why that one has always worked.
namespace XRL.World.Parts
{
    /// <summary>
    /// Equips an item into a body slot the moment its owner is created.
    ///
    /// WHY THIS IS CODE AND NOT XML
    /// ---------------------------
    /// Qud's blueprint XML has no tag for pre-equipping anything -- there is no `<equipment>`,
    /// `<equip>` or `<worn>` element anywhere in the base game, and the creature blueprints that
    /// seem to ship with something equipped are actually carrying it in inventory. So a creature
    /// that should be WEARING an item has to be given one at runtime.
    ///
    /// It matters which of the two it is, because they drop differently on death:
    ///   * EQUIPPED items go through Body.UnequipPartAndChildren(ForDeath: true)
    ///     -> BodyPart drops them via GetDropInventory(), which for a free-standing creature
    ///        resolves to GetDropCell() -- i.e. they land ON THE GROUND.
    ///   * INVENTORY items are moved by the Corpse part's ProcessCorpseDrop, which only runs if a
    ///     corpse is actually created (CorpseChance, and a non-null CorpseBlueprint). The stock
    ///     Creature template has CorpseChance="0" and no blueprint, so inventory items simply do
    ///     not transfer.
    ///
    /// So wearing the item is both the more thematic option and the one that reliably reaches the
    /// player: kill the elemental, pick the stone off the ground.
    ///
    /// Configuration is by XML attribute, so one part serves every elemental type:
    ///     &lt;part Name="A2Raine_BornEquipped" Blueprint="2Raine_SpiritStone_Lightning" /&gt;
    /// </summary>
    [Serializable]
    public class A2Raine_BornEquipped : IPart
    {
        /// <summary>Blueprint of the item to wear. Required.</summary>
        public string Blueprint;

        /// <summary>
        /// Body part type to wear it on. Defaults to the floating slot, which every anatomy has
        /// whether or not its XML mentions it -- XRL.World.Anatomy hardcodes
        /// `FloatingNearby = "Floating Nearby"` and adds that part to every body it applies.
        /// </summary>
        public string Slot = "Floating Nearby";

        /// <summary>
        /// WHICH part of that type to use, counting from 0. Needed because a creature can have
        /// several parts of the same type and each one holds at most ONE item: BodyPart._Equipped is
        /// a single GameObject, so two spirit stones need two parts.
        ///
        /// The Elemental anatomy ends up with THREE floating slots -- two declared in Bodies.xml plus
        /// one XRL.World.Anatomy.ApplyTo adds to every anatomy unconditionally. Putting a stone on
        /// index 0 and another on index 1 fills the first two.
        ///
        /// GameObject.ForceEquipObject(Object, string Slot) cannot express this: it resolves the slot
        /// with GetFirstPart, so every call lands on the same part and the second stone would bounce.
        /// </summary>
        public int SlotIndex = 0;

        public string ExtraBlueprint;
        public string ExtraSlot = "Floating Nearby";
        public int ExtraSlotIndex = 0;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == AfterObjectCreatedEvent.ID;
        }

        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            // Only act for our own object. AfterObjectCreatedEvent reaches every registered part on
            // the creature, so without this check a two-part creature would try to equip twice.
            if (E.Object != ParentObject)
            {
                return base.HandleEvent(E);
            }
            EquipNow();
            return base.HandleEvent(E);
        }

        /// <summary>
        /// The Nth part of the configured type, or null if there is no such part.
        ///
        /// Returns null (rather than the first part) when the index is out of range, so the caller
        /// falls back to the engine's own slot lookup instead of silently dumping the item onto the
        /// wrong part.
        ///
        /// Note the shape of the engine call: BodyPart.GetPart takes the required type AND the list to
        /// fill (there is no one-argument overload returning a list), so the list is built here.
        /// </summary>
        private BodyPart FindSlot(string slot, int slotIndex)
        {
            if (ParentObject == null || ParentObject.Body == null)
            {
                return null;
            }
            BodyPart body = ParentObject.Body.GetBody();
            if (body == null)
            {
                return null;
            }

            List<BodyPart> parts = body.GetPart(slot, new List<BodyPart>());
            if (parts == null || slotIndex < 0 || slotIndex >= parts.Count)
            {
                return null;
            }
            return parts[slotIndex];
        }

        private void EquipNow()
        {
            EquipOne(Blueprint, Slot, SlotIndex);
            EquipOne(ExtraBlueprint, ExtraSlot, ExtraSlotIndex);
        }

        private void EquipOne(string blueprint, string slot, int slotIndex)
        {
            if (string.IsNullOrEmpty(blueprint) || ParentObject == null)
            {
                return;
            }

            try
            {
                GameObject item = GameObjectFactory.Factory.CreateObject(blueprint);
                if (item == null)
                {
                    UnityEngine.Debug.LogWarning("[Toncihana] BornEquipped: could not create '"
                        + blueprint + "'; is the blueprint name right?");
                    return;
                }

                // Resolve the slot ourselves so SlotIndex can pick a specific part. When the lookup
                // fails we still fall through to ForceEquipObject(string), which is the engine's own
                // path and covers any slot name we could not enumerate.
                bool worn = false;
                if (!string.IsNullOrEmpty(slot))
                {
                    BodyPart part = FindSlot(slot, slotIndex);
                    if (part != null)
                    {
                        worn = ParentObject.ForceEquipObject(item, part, Silent: true);
                    }
                    else
                    {
                        worn = ParentObject.ForceEquipObject(item, slot, Silent: true);
                    }
                }

                if (!worn)
                {
                    // Slot missing, occupied, or the item refuses it: fall back to inventory so the
                    // item is at least not destroyed. It will be reachable by other means.
                    UnityEngine.Debug.LogWarning("[Toncihana] BornEquipped: could not wear '"
                        + blueprint + "' on slot '" + slot + "' index " + slotIndex + " for "
                        + ParentObject.Blueprint + "; putting it in inventory instead.");
                    if (ParentObject.Inventory != null)
                    {
                        ParentObject.Inventory.AddObject(item);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Toncihana] BornEquipped failed for '"
                    + blueprint + "': " + ex);
            }
        }
    }
}
