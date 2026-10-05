using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
    /// <summary>
    /// Eating a spirit stone feeds the storm inside the eater.
    ///
    /// Hooks the string event "OnEat", which Food.HandleEvent(InventoryActionEvent) fires on the
    /// eaten OBJECT (parameters Actor / Eater / Subject / Food / Object) right before the "Eating"
    /// event goes to the eater -- so no Harmony, and no need to watch every meal everyone eats.
    ///
    /// The counter is a plain GameObject IntProperty, the same shape Becoming uses for its Insight
    /// and AetherEssence currencies: we are the only reader and writer, nothing else in the game
    /// needs to see it as a statistic.
    /// </summary>
    [Serializable]
    public class A2Raine_SpiritStoneMeal : IPart
    {
        public const string CHARGE_PROPERTY = "2Raine_SpiritCharge";

        public int Amount = 1;

        public string Message = "{{B|Something in you takes the strike and keeps it, and does not give it back.}}";

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("OnEat");
            base.Register(Object, Registrar);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "OnEat")
            {
                GameObject eater = E.GetGameObjectParameter("Eater");
                if (eater == null)
                {
                    eater = E.GetGameObjectParameter("Actor");
                }
                if (eater != null)
                {
                    eater.ModIntProperty(CHARGE_PROPERTY, Amount);
                    // Says so in the log, so a silent miss is distinguishable from a stone that
                    // simply never carried this part (objects do not re-read their blueprint).
                    UnityEngine.Debug.Log("[Toncihana] spirit stone eaten by " + eater.Blueprint
                        + "; " + CHARGE_PROPERTY + "=" + eater.GetIntProperty(CHARGE_PROPERTY));
                    if (eater.IsPlayer())
                    {
                        IComponent<GameObject>.AddPlayerMessage(Message);
                    }
                }
            }
            return base.FireEvent(E);
        }
    }
}
