using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.Parts.Skill
{
    /// <summary>
    /// The fourth link's ability: opens and closes the face implant's gifts together.
    ///
    /// WHAT IT DRIVES
    /// ---------------
    /// * The penetrate radar that the implant itself carries. CyberneticsPenetratingRadar
    ///   registers "Penetrating Radar" as its own toggleable ability on the implantee
    ///   (CyberneticsPenetratingRadar.cs:69), so this ability does not reimplement it -- it drives
    ///   that one, by looking the ability up by command and toggling only when the current state
    ///   differs from the wanted one. Toggling blindly would flip the radar the wrong way whenever
    ///   the two ever disagreed.
    ///
    /// * Telepathy, lent and withdrawn. It goes through AddMutationMod/RemoveMutationMod with a
    ///   MutationModifierTracker rather than AddMutation/RemoveMutation, which is the same
    ///   source-tracked mechanism vanilla's MutationOnEquip uses (MutationOnEquip.cs:157, :175).
    ///   That is what makes it safe alongside another source: closing this ability removes only
    ///   THIS tracker, so telepathy from elsewhere -- another implant, a mutation, a cooking
    ///   effect -- stays. AddMutation would have stripped the shared entry outright.
    /// </summary>
    [Serializable]
    public class A2Raine_Toncihana_SpiritResonance : A2Raine_Toncihana_InnateAbility
    {
        public const string COMMAND_NAME = "CommandA2Raine_Toncihana_SpiritResonance";

        /// <summary>The mutation this ability lends its owner.</summary>
        public const string LENT_MUTATION = "Telepathy";

        /// <summary>Shown as the source of the lent mutation in the mutation tracker.</summary>
        public const string SOURCE_NAME = "spirit resonance";

        public override string AbilityName
        {
            get { return "Spirit Resonance"; }
        }

        public override string AbilityCommand
        {
            get { return COMMAND_NAME; }
        }

        public override string AbilityDescription
        {
            get
            {
                return "While toggled on, the link at the face lets a voice that is not yours speak "
                    + "through you, and shows you the shape of the ground without needing eyes. "
                    + "The telepathy it lends is its own, and is withdrawn when you close it -- "
                    + "without disturbing that same gift from any other source.";
            }
        }

        /// <summary>
        /// Handle of the telepathy this ability lends. Guid.Empty while closed.
        ///
        /// APPEND-ONLY, below everything A2Raine_Toncihana_InnateAbility declares: removing or
        /// reordering a serialized field breaks existing saves (see the note on AbilityID in the
        /// base class for why [NonSerialized] is not an escape from this).
        /// </summary>
        public Guid TelepathyTracker = Guid.Empty;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == PooledEvent<CommandEvent>.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_NAME)
            {
                bool opening = !ParentObject.IsActivatedAbilityToggledOn(AbilityID);
                ParentObject.ToggleActivatedAbility(AbilityID);

                if (opening)
                {
                    Open();
                }
                else
                {
                    Close();
                }
            }
            return base.HandleEvent(E);
        }

        private void Open()
        {
            SetRadar(true);
            LendTelepathy();
            ARaine_Charge.Message(ParentObject,
                "{{C|Something very far away finishes saying your name.}}");
        }

        private void Close()
        {
            SetRadar(false);
            WithdrawTelepathy();
            ARaine_Charge.Message(ParentObject,
                "{{K|The far voice stops mid-word. The ground goes back to being opaque.}}");
        }

        /// <summary>
        /// Drives the implant's own radar to the wanted state. Toggling only when the states
        /// disagree keeps this ability and the radar from drifting apart.
        /// </summary>
        private void SetRadar(bool On)
        {
            ActivatedAbilityEntry radar = ParentObject.GetActivatedAbilityByCommand(
                XRL.World.Parts.CyberneticsPenetratingRadar.COMMAND_NAME);

            if (radar == null)
            {
                return;
            }

            if (ParentObject.IsActivatedAbilityToggledOn(radar.ID) != On)
            {
                ParentObject.ToggleActivatedAbility(radar.ID);
            }
        }

        /// <summary>Lends telepathy under our own tracker, so closing cannot touch other sources.</summary>
        private void LendTelepathy()
        {
            if (TelepathyTracker != Guid.Empty)
            {
                return;
            }

            TelepathyTracker = ParentObject.RequirePart<Mutations>().AddMutationMod(
                LENT_MUTATION,
                null,
                1,
                Mutations.MutationModifierTracker.SourceType.Equipment,
                SOURCE_NAME);
        }

        /// <summary>Withdraws only the tracker handed out above.</summary>
        private void WithdrawTelepathy()
        {
            if (TelepathyTracker == Guid.Empty)
            {
                return;
            }

            ParentObject.RequirePart<Mutations>().RemoveMutationMod(TelepathyTracker);
            TelepathyTracker = Guid.Empty;
        }
    }
}
