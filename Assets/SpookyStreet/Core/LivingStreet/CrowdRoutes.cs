using System;
using System.Collections.Generic;
using SpookyStreet.Core.Crowd;

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
    /// One walker's look: a crowd kit recipe (<see cref="CrowdKit.Walkers"/>, the art contract of
    /// docs/living-street-crowd-art.md), whose parts the runtime merges into one skinned mesh. The swap point for other
    /// bodies is the runtime's <c>ICrowdBodyFactory</c>.
    /// </summary>
    public readonly struct WalkerLook
    {
        public WalkerLook(string recipe)
        {
            Recipe = recipe;
        }

        /// <summary>The kit recipe id ("GhostKid").</summary>
        public string Recipe { get; }

        /// <summary>The kit recipe (null for an unknown id).</summary>
        public CrowdWalkerRecipe KitRecipe => CrowdKit.Find(Recipe) ?? LivingStreetLayout.NeighbourRecipe(Recipe);

        public CrowdBodyKind Body => KitRecipe != null && KitRecipe.IsGrownUp ? CrowdBodyKind.GrownUp : CrowdBodyKind.SmallChild;

        /// <summary>The Walk clip's own ground speed for this body (move at it, or scale the clip, so feet never slide).</summary>
        public float WalkSpeed => CrowdKit.WalkSpeed(KitRecipe != null ? KitRecipe.Model : CrowdKit.ChildModel);

        public override string ToString() => Recipe;
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

    /// <summary>The looks the crowd may wear: the kit's recipes, none of them reserved (living-street §3.4 rule 6).</summary>
    public static class CrowdLooks
    {
        /// <summary>The kit's recipe ids.</summary>
        public static IReadOnlyList<string> Costumes
        {
            get
            {
                var ids = new List<string>();
                foreach (var walker in CrowdKit.Walkers)
                {
                    ids.Add(walker.Id);
                }

                return ids;
            }
        }

        /// <summary>
        /// Looks no crowd costume may use: the case's clue materials, the heroes' and future residents' themes, and the
        /// Players' puppet motifs before their cases (Pip stays the only bat cape on the street).
        /// </summary>
        public static readonly IReadOnlyList<string> Reserved = new[]
        {
            "cloth_silver", "lavender_foil", "bat_cape", "pumpkin", "star_witch", "mummy", "fortune_teller", "scarecrow",
            "dragon", "pirate", "gingerbread_witch", "wizard", "ghost_in_overalls", "owl", "fox", "moth", "moon", "bell",
        };

        /// <summary>True for a kit recipe none of whose parts carries a reserved word.</summary>
        public static bool IsAllowed(string recipe)
        {
            if (string.IsNullOrEmpty(recipe))
            {
                return false;
            }

            var walker = CrowdKit.Find(recipe) ?? LivingStreetLayout.NeighbourRecipe(recipe);
            if (walker == null)
            {
                return false;
            }

            foreach (string reserved in Reserved)
            {
                if (recipe.IndexOf(reserved, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            foreach (string part in walker.Parts)
            {
                foreach (string word in CrowdKit.ReservedWords)
                {
                    if (part.IndexOf(word, StringComparison.Ordinal) >= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
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
        /// <summary>Groups walk at the kit's Walk clip pace (the child's and the grown-up's match), so feet never slide.</summary>
        public static readonly float GroupSpeed = CrowdKit.WalkSpeed(CrowdKit.ChildModel);

        /// <summary>The strolling pair keeps the grown-up's Walk pace.</summary>
        public static readonly float StrollSpeed = CrowdKit.WalkSpeed(CrowdKit.AdultModel);

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
            new RouteNode(24.6f, 0.4f),
            new RouteNode(23.4f, -2.2f, RouteNodeKind.HeartGate),
            new RouteNode(18.0f, -3.2f),
            new RouteNode(11.2f, -4.0f),
            new RouteNode(11.6f, -6.4f),
            new RouteNode(17.2f, -6.0f),
            new RouteNode(21.0f, -3.0f),
            new RouteNode(23.6f, -2.8f, RouteNodeKind.HeartExit),
            new RouteNode(24.8f, -0.4f),
            new RouteNode(30.8f, -0.4f),
            new RouteNode(31.0f, -0.4f, RouteNodeKind.Look, null, 180f, 4f),
            new RouteNode(43.0f, -0.4f),
            new RouteNode(43.0f, 0.4f),
        };

        private static WalkerLook Look(string recipe) => new WalkerLook(recipe);

        /// <summary>
        /// The slice's three groups, eight walkers, all from the crowd kit: a cat mum with a ghost and a cat child, a
        /// skeleton dad with a robot and a little queen, and a paper-crown grandpa strolling with a candy-corn grandchild.
        /// </summary>
        public static readonly IReadOnlyList<CrowdRoute> All = new[]
        {
            new CrowdRoute("group_a", GroupSpeed, DoorCircuit(), PorchExcursion(), new[]
            {
                new MemberSlot(0f, -0.35f, Look("GhostKid")),
                new MemberSlot(0.45f, 0.35f, Look("CatKid")),
                new MemberSlot(1.3f, 0f, Look("CatGrownUp")),
            }, 3f),
            new CrowdRoute("group_b", GroupSpeed, DoorCircuit(), PorchExcursion(), new[]
            {
                new MemberSlot(0f, 0.35f, Look("RobotKid")),
                new MemberSlot(0.4f, -0.35f, Look("QueenKid")),
                new MemberSlot(1.3f, 0f, Look("SkeletonGrownUp")),
            }, 33f),
            new CrowdRoute("strollers", StrollSpeed, StrollLoop(), null, new[]
            {
                new MemberSlot(0f, -0.38f, Look("KingGrownUp")),
                new MemberSlot(0.15f, 0.38f, Look("CandyCornKid")),
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
