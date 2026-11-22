using System;

namespace SpookyStreet.Core.Case
{
    public enum DoorState
    {
        Latched,
        Open,
    }

    /// <summary>Where June stands. Her walk down the stairs is presentation; only the mark persists.</summary>
    public enum JunePlacement
    {
        /// <summary>By the lamp in the attic, until Pip talks to her.</summary>
        Attic,

        /// <summary>At her mark by the latch in the foyer (spec §3: entering the foyer also places her there).</summary>
        Foyer,

        /// <summary>On the porch for the reopening, and from then on.</summary>
        Porch,
    }

    public enum OwlState
    {
        /// <summary>The linkage has slipped: knocks sound, the owl never bows.</summary>
        Slipped,

        /// <summary>Arm reconnected; the owl bows the next time the cam turns.</summary>
        Linked,

        /// <summary>Tested: the owl bows with every three knocks.</summary>
        Bowing,
    }

    /// <summary>
    /// Every prop and character state the world needs, derived only from facts. On resume the world applies this
    /// before enabling controls (spec §7), so a reset during a reveal rebuilds the same world (SAVE-01).
    /// </summary>
    public readonly struct CaseWorldView : IEquatable<CaseWorldView>
    {
        private CaseWorldView(bool paperBatFallen, DoorState frontDoor, JunePlacement june, bool servicePanelOpen,
            OwlState owl, bool boothOpen, bool sweetTaken)
        {
            PaperBatFallen = paperBatFallen;
            FrontDoor = frontDoor;
            June = june;
            ServicePanelOpen = servicePanelOpen;
            Owl = owl;
            BoothOpen = boothOpen;
            SweetTaken = sweetTaken;
        }

        /// <summary>The arrival knock dislodged the porch's paper bat.</summary>
        public bool PaperBatFallen { get; }

        public DoorState FrontDoor { get; }

        public JunePlacement June { get; }

        /// <summary>The workshop grille's child-height service panel is open (arm and crank reachable).</summary>
        public bool ServicePanelOpen { get; }

        public bool ArmLinked => Owl != OwlState.Slipped;

        public OwlState Owl { get; }

        /// <summary>Kit's puppet booth is open and Kit is on the porch.</summary>
        public bool BoothOpen { get; }

        public bool KitPresent => BoothOpen;

        public bool SweetTaken { get; }

        public static CaseWorldView From(CaseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var june = state.Has(CaseFact.CaseClosed) ? JunePlacement.Porch
                : state.Has(CaseFact.FoundJune) ? JunePlacement.Foyer
                : JunePlacement.Attic;
            var owl = state.Has(CaseFact.TestedOwl) ? OwlState.Bowing
                : state.Has(CaseFact.LinkedArm) ? OwlState.Linked
                : OwlState.Slipped;
            return new CaseWorldView(
                state.Has(CaseFact.HeardKnocks),
                state.Has(CaseFact.FrontOpen) ? DoorState.Open : DoorState.Latched,
                june,
                state.Has(CaseFact.SawCam),
                owl,
                state.Has(CaseFact.CaseClosed),
                state.Has(CaseFact.FoundSweet));
        }

        public bool Equals(CaseWorldView other) =>
            PaperBatFallen == other.PaperBatFallen && FrontDoor == other.FrontDoor && June == other.June &&
            ServicePanelOpen == other.ServicePanelOpen && Owl == other.Owl && BoothOpen == other.BoothOpen &&
            SweetTaken == other.SweetTaken;

        public override bool Equals(object obj) => obj is CaseWorldView other && Equals(other);

        public override int GetHashCode() =>
            ((int)FrontDoor << 1) ^ ((int)June << 3) ^ ((int)Owl << 5) ^ (PaperBatFallen ? 1 : 0) ^
            (ServicePanelOpen ? 128 : 0) ^ (BoothOpen ? 256 : 0) ^ (SweetTaken ? 512 : 0);

        public override string ToString() =>
            $"door={FrontDoor} june={June} panel={ServicePanelOpen} owl={Owl} booth={BoothOpen} bat={PaperBatFallen} sweet={SweetTaken}";
    }
}
