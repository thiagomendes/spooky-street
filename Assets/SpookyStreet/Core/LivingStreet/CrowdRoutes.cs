using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>What happens when a walker group reaches a node of its loop.</summary>
    public enum RouteNodeKind
    {
        /// <summary>A bend: keep walking.</summary>
        Walk,

        /// <summary>Ring a neighbour's bell and wait at the foot of the steps (<see cref="RouteNode.DoorId"/>).</summary>
        Door,

        /// <summary>Ring Pip's bell at her porch (only on laps taken while her lantern is lit).</summary>
        PipPorch,

        /// <summary>Stop and look at a porch's decorations for <see cref="RouteNode.Seconds"/>.</summary>
        Look,

        /// <summary>The case block starts past here: enter only while the heart's cap allows (D34: at most 3 walkers).</summary>
        HeartGate,

        /// <summary>The case block is behind the group again.</summary>
        HeartExit,
    }

    /// <summary>One authored node of a loop (world plan metres).</summary>
    public readonly struct RouteNode
    {
        public RouteNode(float x, float z, RouteNodeKind kind = RouteNodeKind.Walk, string doorId = null,
            float faceYaw = float.NaN, float seconds = 0f)
        {
            X = x;
            Z = z;
            Kind = kind;
            DoorId = doorId;
            FaceYaw = faceYaw;
            Seconds = seconds;
        }

        public float X { get; }
        public float Z { get; }
        public RouteNodeKind Kind { get; }
        public string DoorId { get; }

        /// <summary>Unity yaw the group faces while stopped here (NaN: keep the walking heading).</summary>
        public float FaceYaw { get; }

        /// <summary>How long a <see cref="RouteNodeKind.Look"/> stop lasts.</summary>
        public float Seconds { get; }

        public bool IsStop => Kind == RouteNodeKind.Door || Kind == RouteNodeKind.PipPorch || Kind == RouteNodeKind.Look;

        public override string ToString() => Kind + " (" + X.ToString("F1") + ", " + Z.ToString("F1") + ")";
    }

    /// <summary>Body of a crowd walker: the crowd kit's base bodies (living-street §7).</summary>
    public enum CrowdBodyKind
    {
        SmallChild,
        GrownUp,
    }

    /// <summary>
    /// One walker's look: a base body and a costume from the kit's unreserved themes (§3.4 rule 6, §7), a palette
    /// variant and a hand prop. The runtime turns this into a body: placeholders now, the crowd kit's merged skinned
    /// meshes later (the swap point is the runtime's <c>ICrowdBodyFactory</c>).
    /// </summary>
    public readonly struct WalkerLook
    {
        public WalkerLook(CrowdBodyKind body, string costume, int palette, string prop)
        {
            Body = body;
            Costume = costume;
            Palette = palette;
            Prop = prop;
        }

        public CrowdBodyKind Body { get; }

        /// <summary>Costume theme ID (<see cref="CrowdLooks.Costumes"/>).</summary>
        public string Costume { get; }

        public int Palette { get; }

        /// <summary>Hand prop ID ("pail", "bag", "lantern" or null).</summary>
        public string Prop { get; }

        public override string ToString() => Body + "/" + Costume + "/" + Palette + "/" + (Prop ?? "-");
    }

    /// <summary>A member's place in its group: how far behind the group's point on the loop and how far to its side.</summary>
    public readonly struct MemberSlot
    {
        public MemberSlot(float back, float lateral, WalkerLook look)
        {
            Back = back;
            Lateral = lateral;
            Look = look;
        }

        /// <summary>Metres behind the group's point along the loop.</summary>
        public float Back { get; }

        /// <summary>Metres to the group's right (negative: left).</summary>
        public float Lateral { get; }

        public WalkerLook Look { get; }
    }

    /// <summary>
    /// One authored loop: the plain lap (a closed polyline that starts and ends at its branch node) and, for
    /// trick-or-treat groups, the excursion to Pip's porch that the group adds to a lap while her lantern is lit.
    /// </summary>
    public sealed class CrowdRoute
    {
        public CrowdRoute(string id, float speed, RouteNode[] plain, RouteNode[] porchExcursion, MemberSlot[] members,
            float startMetres)
        {
            Id = id;
            Speed = speed;
            Plain = plain ?? throw new ArgumentNullException(nameof(plain));
            PorchExcursion = porchExcursion;
            Members = members ?? throw new ArgumentNullException(nameof(members));
            StartMetres = startMetres;
        }

        public string Id { get; }

        /// <summary>Walking speed (m/s): an evening stroll, children's pace.</summary>
        public float Speed { get; }

        /// <summary>The plain lap: first node and last node are the same point (the branch).</summary>
        public RouteNode[] Plain { get; }

        /// <summary>Nodes between two visits of the branch: out to Pip's porch and back (null: never visits).</summary>
        public RouteNode[] PorchExcursion { get; }

        public MemberSlot[] Members { get; }

        /// <summary>Where on the plain lap the group starts (metres from the branch).</summary>
        public float StartMetres { get; }

        public int Size => Members.Length;

        /// <summary>The nodes of a lap, with or without the excursion to Pip's porch.</summary>
        public RouteNode[] Lap(bool withPorch)
        {
            if (!withPorch || PorchExcursion == null || PorchExcursion.Length == 0)
            {
                return Plain;
            }

            var lap = new List<RouteNode>(Plain.Length + PorchExcursion.Length + 1) { Plain[0] };
            lap.AddRange(PorchExcursion);
            lap.Add(Plain[0]);
            for (int i = 1; i < Plain.Length; i++)
            {
                lap.Add(Plain[i]);
            }

            return lap.ToArray();
        }
    }

    /// <summary>The costume themes the crowd may wear, and the looks the game reserves (living-street §3.4 rule 6).</summary>
    public static class CrowdLooks
    {
        public const string GhostSheet = "ghost_sheet";
        public const string Skeleton = "skeleton";
        public const string BlackCat = "black_cat";
        public const string WitchGreen = "witch_green";
        public const string WitchPlum = "witch_plum";
        public const string Spider = "spider";
        public const string CandyCorn = "candy_corn";
        public const string Robot = "cardboard_robot";
        public const string Bumblebee = "bumblebee";
        public const string FrogPrince = "frog_prince";
        public const string PaperCrown = "paper_crown";
        public const string PatchworkGiant = "patchwork_giant";

        /// <summary>The kit's unreserved themes (§7).</summary>
        public static readonly IReadOnlyList<string> Costumes = new[]
        {
            GhostSheet, Skeleton, BlackCat, WitchGreen, WitchPlum, Spider, CandyCorn, Robot, Bumblebee, FrogPrince, PaperCrown,
            PatchworkGiant,
        };

        /// <summary>
        /// Looks no crowd costume may use: the case's clue materials, the heroes' and future residents' themes, and the
        /// Players' puppet motifs before their cases (Pip stays the only bat cape on the street).
        /// </summary>
        public static readonly IReadOnlyList<string> Reserved = new[]
        {
            "cloth_silver", "lavender_foil", "bat_cape", "pumpkin", "star_witch", "mummy", "fortune_teller", "scarecrow",
            "dragon", "pirate", "gingerbread_witch", "wizard", "ghost_in_overalls", "owl", "fox", "moth", "moon", "bell",
        };

        public static bool IsAllowed(string costume)
        {
            if (string.IsNullOrEmpty(costume))
            {
                return false;
            }

            foreach (string reserved in Reserved)
            {
                if (costume.IndexOf(reserved, StringComparison.Ordinal) >= 0)
                {
                    return false;
                }
            }

            foreach (string allowed in Costumes)
            {
                if (string.Equals(allowed, costume, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The slice's crowd (living-street §9 phase 1: 6–8 walkers): two trick-or-treat groups of two children and a
    /// grown-up going door to door on the east lane, and a strolling couple who walk into the heart and back. Groups
    /// visit Pip's porch through the heart on laps taken while her lantern is lit. Every heart leg is authored south of
    /// the plaza, away from the clues, the hint areas, the bench's view of the door and the side yard (proved by
    /// <c>CrowdRouteTests</c>). Walkers follow these loops, never a navigation mesh.
    /// </summary>
    public static class CrowdRoutes
    {
        public const float GroupSpeed = 1.05f;
        public const float StrollSpeed = 1.15f;

        private const float N = LivingStreetLayout.NorthLineZ;
        private const float S = LivingStreetLayout.SouthLineZ;

        /// <summary>The branch of the groups' laps: the lane's south line just east of the hedge gap.</summary>
        public const float BranchX = 24f;

        /// <summary>The east lane's door circuit: Fogg, Nana Pearl, round the east end, the Abbotts, back to the branch.</summary>
        private static RouteNode[] DoorCircuit()
        {
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            var pearl = LivingStreetLayout.Door(LivingStreetLayout.Pearl);
            var abbotts = LivingStreetLayout.Door(LivingStreetLayout.Abbotts);
            return new[]
            {
                new RouteNode(BranchX, S),
                new RouteNode(BranchX, N),
                new RouteNode(fogg.StandX, N),
                new RouteNode(fogg.StandX, fogg.StandZ, RouteNodeKind.Door, fogg.Id, fogg.StandFacingYaw),
                new RouteNode(fogg.StandX, N),
                new RouteNode(pearl.StandX, N),
                new RouteNode(pearl.StandX, pearl.StandZ, RouteNodeKind.Door, pearl.Id, pearl.StandFacingYaw),
                new RouteNode(pearl.StandX, N),
                new RouteNode(42.6f, N),
                new RouteNode(42.6f, S),
                new RouteNode(abbotts.StandX, S),
                new RouteNode(abbotts.StandX, abbotts.StandZ, RouteNodeKind.Door, abbotts.Id, abbotts.StandFacingYaw),
                new RouteNode(abbotts.StandX, S),
                new RouteNode(BranchX, S),
            };
        }

        /// <summary>Out through the hedge gap, along the south of the plaza, down Pip's path to her steps, and back.</summary>
        private static RouteNode[] PorchExcursion() => new[]
        {
            new RouteNode(23.4f, S, RouteNodeKind.HeartGate),
            new RouteNode(21.0f, S),
            new RouteNode(16.0f, -4.2f),
            new RouteNode(2.2f, -5.3f),
            new RouteNode(LivingStreetLayout.PorchStandX, -6.4f),
            new RouteNode(LivingStreetLayout.PorchStandX, LivingStreetLayout.PorchStandZ, RouteNodeKind.PipPorch, null,
                LivingStreetLayout.PorchStandYaw),
            new RouteNode(LivingStreetLayout.PorchStandX, -6.4f),
            new RouteNode(2.2f, -5.3f),
            new RouteNode(16.0f, -4.2f),
            new RouteNode(21.0f, S),
            new RouteNode(23.6f, S, RouteNodeKind.HeartExit),
        };

        /// <summary>The strollers' long loop: the lane's middle, a look at the Abbotts' porch, a turn in the heart.</summary>
        private static RouteNode[] StrollLoop() => new[]
        {
            new RouteNode(43.0f, 0.4f),
            new RouteNode(23.4f, 0.4f, RouteNodeKind.HeartGate),
            new RouteNode(18.0f, -2.6f),
            new RouteNode(11.2f, -4.0f),
            new RouteNode(11.6f, -6.4f),
            new RouteNode(17.2f, -6.0f),
            new RouteNode(21.0f, -0.6f),
            new RouteNode(23.6f, -0.4f, RouteNodeKind.HeartExit),
            new RouteNode(30.8f, -0.4f),
            new RouteNode(31.0f, -0.4f, RouteNodeKind.Look, null, 180f, 4f),
            new RouteNode(43.0f, -0.4f),
            new RouteNode(43.0f, 0.4f),
        };

        private static WalkerLook Kid(string costume, int palette, string prop) =>
            new WalkerLook(CrowdBodyKind.SmallChild, costume, palette, prop);

        private static WalkerLook Adult(string costume, int palette) => new WalkerLook(CrowdBodyKind.GrownUp, costume, palette, null);

        /// <summary>The slice's three groups, eight walkers.</summary>
        public static readonly IReadOnlyList<CrowdRoute> All = new[]
        {
            new CrowdRoute("group_a", GroupSpeed, DoorCircuit(), PorchExcursion(), new[]
            {
                new MemberSlot(0f, -0.35f, Kid(CrowdLooks.GhostSheet, 0, "pail")),
                new MemberSlot(0.45f, 0.35f, Kid(CrowdLooks.BlackCat, 1, "bag")),
                new MemberSlot(1.3f, 0f, Adult(CrowdLooks.WitchGreen, 0)),
            }, 3f),
            new CrowdRoute("group_b", GroupSpeed, DoorCircuit(), PorchExcursion(), new[]
            {
                new MemberSlot(0f, 0.35f, Kid(CrowdLooks.Robot, 0, "pail")),
                new MemberSlot(0.4f, -0.35f, Kid(CrowdLooks.Bumblebee, 0, "lantern")),
                new MemberSlot(1.3f, 0f, Adult(CrowdLooks.Skeleton, 1)),
            }, 33f),
            new CrowdRoute("strollers", StrollSpeed, StrollLoop(), null, new[]
            {
                new MemberSlot(0f, -0.38f, Adult(CrowdLooks.PaperCrown, 0)),
                new MemberSlot(0.15f, 0.38f, Adult(CrowdLooks.Spider, 1)),
            }, 8f),
        };

        /// <summary>Walkers in the slice.</summary>
        public static int WalkerCount
        {
            get
            {
                int n = 0;
                foreach (var route in All)
                {
                    n += route.Size;
                }

                return n;
            }
        }
    }
}
