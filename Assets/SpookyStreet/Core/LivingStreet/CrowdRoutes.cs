using System;
using System.Collections.Generic;
using SpookyStreet.Core.Crowd;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>What happens when a walker group reaches a node of its loop.</summary>
    public enum RouteNodeKind
    {
        /// <summary>A bend: keep walking (bends are rounded into curves, <see cref="CrowdPath"/>).</summary>
        Walk,

        /// <summary>Trick-or-treat at a neighbour's door (<see cref="RouteNode.DoorId"/>): gather, ring, a treat each.</summary>
        Door,

        /// <summary>Ring Pip's bell at her porch (only on laps taken while her lantern is lit).</summary>
        PipPorch,

        /// <summary>Stop and admire a decoration for <see cref="RouteNode.Seconds"/>, facing it.</summary>
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

        /// <summary>The body's model (<see cref="CrowdKit.ChildModel"/> or a grown-up's).</summary>
        public string Model => KitRecipe != null ? KitRecipe.Model : CrowdKit.ChildModel;

        /// <summary>The Walk clip's own ground speed for this body (move at it, or scale the clip, so feet never slide).</summary>
        public float WalkSpeed => CrowdKit.WalkSpeed(Model);

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

    /// <summary>What a group walks a leg of its route for (owner, 2026-11-28: every walker visibly has a purpose).</summary>
    public enum RouteGoal
    {
        /// <summary>Trick-or-treat at a neighbour's door (<see cref="RouteDetour.DoorId"/>).</summary>
        Door,

        /// <summary>Ring Pip's bell once her lantern hangs lit.</summary>
        PipPorch,
    }

    /// <summary>
    /// A side trip off the plain lap at one of its nodes (the branch): up a garden path to a goal and back to the branch.
    /// A group decides a few metres before the branch whether to take it: a door it may visit now (or wait its turn for
    /// a few metres back), Pip's porch while it is open.
    /// </summary>
    public sealed class RouteDetour
    {
        public RouteDetour(int at, RouteGoal goal, string doorId, RouteNode[] nodes)
        {
            At = at;
            Goal = goal;
            DoorId = doorId;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }

        /// <summary>The plain lap's node the trip leaves from and comes back to.</summary>
        public int At { get; }

        public RouteGoal Goal { get; }

        public string DoorId { get; }

        /// <summary>From the branch (excluded) out to the goal's stop and back to the sidewalk (at or past the branch).</summary>
        public RouteNode[] Nodes { get; }

        /// <summary>The stop at the end of the way out.</summary>
        public RouteNode GoalNode
        {
            get
            {
                foreach (var n in Nodes)
                {
                    if (n.IsStop)
                    {
                        return n;
                    }
                }

                return Nodes[Nodes.Length - 1];
            }
        }
    }

    /// <summary>
    /// One authored loop: the plain lap (a closed polyline that starts and ends at its first node) along the street's
    /// sidewalks and paths, with side trips (<see cref="Detours"/>) to the doors and Pip's porch that a group takes as it
    /// comes to them. Every leg leads to a goal: the next door, Pip's porch, a decoration to admire.
    /// </summary>
    public sealed class CrowdRoute
    {
        public CrowdRoute(string id, float speed, RouteNode[] plain, RouteDetour[] detours, MemberSlot[] members,
            float startMetres)
        {
            Id = id;
            Speed = speed;
            Plain = plain ?? throw new ArgumentNullException(nameof(plain));
            Detours = detours ?? Array.Empty<RouteDetour>();
            Members = members ?? throw new ArgumentNullException(nameof(members));
            StartMetres = startMetres;
        }

        public string Id { get; }

        /// <summary>Walking speed (m/s): an evening stroll, children's pace.</summary>
        public float Speed { get; }

        /// <summary>The plain lap: first node and last node are the same point.</summary>
        public RouteNode[] Plain { get; }

        public IReadOnlyList<RouteDetour> Detours { get; }

        public MemberSlot[] Members { get; }

        /// <summary>Where on the plain lap the group starts (metres from its first node).</summary>
        public float StartMetres { get; }

        public int Size => Members.Length;

        /// <summary>The trip to Pip's porch (null: this route never visits).</summary>
        public RouteNode[] PorchExcursion
        {
            get
            {
                foreach (var d in Detours)
                {
                    if (d.Goal == RouteGoal.PipPorch)
                    {
                        return d.Nodes;
                    }
                }

                return null;
            }
        }

        /// <summary>The detour leaving from plain node <paramref name="index"/>, or null.</summary>
        public RouteDetour DetourAt(int index)
        {
            foreach (var d in Detours)
            {
                if (d.At == index)
                {
                    return d;
                }
            }

            return null;
        }

        /// <summary>
        /// Every node of a full lap with all its door trips (and Pip's porch when <paramref name="withPorch"/>): what a
        /// group walks when every door is free. Tests sample it.
        /// </summary>
        public RouteNode[] Lap(bool withPorch)
        {
            var lap = new List<RouteNode>();
            for (int i = 0; i < Plain.Length; i++)
            {
                lap.Add(Plain[i]);
                if (i == Plain.Length - 1)
                {
                    break;
                }

                var d = DetourAt(i);
                if (d != null && (d.Goal != RouteGoal.PipPorch || withPorch))
                {
                    lap.AddRange(d.Nodes);
                }
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
    /// grown-up going door to door on the east lane, and a strolling grandpa and grandchild who admire the decorations
    /// and walk into the heart and back. Groups visit Pip's porch through the heart on laps taken while her lantern is
    /// lit. Every heart leg is authored south of the plaza, away from the clues, the hint areas, the bench's view of the
    /// door and the side yard (proved by the Core route tests). Walkers follow these paths, never a navigation mesh.
    /// </summary>
    public static class CrowdRoutes
    {
        /// <summary>Groups walk at the kit's Walk clip pace (the child's and the grown-up's match), so feet never slide.</summary>
        public static readonly float GroupSpeed = CrowdKit.WalkSpeed(CrowdKit.ChildModel);

        /// <summary>The strolling pair keeps the grown-up's Walk pace.</summary>
        public static readonly float StrollSpeed = CrowdKit.WalkSpeed(CrowdKit.AdultModel);

        private const float N = LivingStreetLayout.NorthLineZ;
        private const float S = LivingStreetLayout.SouthLineZ;

        /// <summary>The first node of the groups' laps: the lane's south line just east of the hedge gap.</summary>
        public const float BranchX = 24f;

        /// <summary>Plain-lap indexes of the door branches in the door circuit.</summary>
        public const int FoggBranch = 2;
        public const int PearlBranch = 3;
        public const int AbbottsBranch = 6;

        /// <summary>Where the door circuit crosses the lane at its east end (a wide, gentle turn short of the Green's corner).</summary>
        public const float EastTurnX = 43.4f;

        /// <summary>
        /// The east lane's door circuit along the sidewalks: north at the hedge gap, east past Mr. Fogg's and Nana Pearl's
        /// garden paths, round the east end, west past the Abbotts' path and back to the gap. Each door is a side trip up
        /// its path (<see cref="DoorTrip"/>); Pip's porch is one from the first node.
        /// </summary>
        private static RouteNode[] DoorCircuit()
        {
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            var pearl = LivingStreetLayout.Door(LivingStreetLayout.Pearl);
            var abbotts = LivingStreetLayout.Door(LivingStreetLayout.Abbotts);
            return new[]
            {
                new RouteNode(BranchX, S),
                new RouteNode(BranchX, N),
                new RouteNode(fogg.StandX - TripSpread, N),
                new RouteNode(pearl.StandX - TripSpread, N),
                new RouteNode(EastTurnX, N),
                new RouteNode(EastTurnX, S),
                new RouteNode(abbotts.StandX + TripSpread, S),
                new RouteNode(BranchX, S),
            };
        }

        /// <summary>Where a family gathers at a door: just behind its waiting arc, on the door's axis, facing it.</summary>
        public static void Gathering(NeighbourDoor door, out float x, out float z)
        {
            DoorStoops.Point(door, DoorStoops.ReceiveOut(door) + DoorStoops.ArcRadius + 0.25f, 0f, out x, out z);
        }

        /// <summary>
        /// A door trip leaves the sidewalk this far before the door's path and joins it again this far after, so a
        /// family curves in to the door and out again in the way it was going: nobody turns round on the sidewalk.
        /// </summary>
        public const float TripSpread = 1.5f;

        /// <summary>
        /// Up to a door from the sidewalk (leaving it <see cref="TripSpread"/> before the door, along its way
        /// <paramref name="along"/>) to where the family gathers facing the door, and on to the sidewalk again past it.
        /// </summary>
        private static RouteDetour DoorTrip(int at, string doorId, float laneZ, float along)
        {
            var door = LivingStreetLayout.Door(doorId);
            Gathering(door, out float gx, out float gz);
            return new RouteDetour(at, RouteGoal.Door, doorId, new[]
            {
                new RouteNode(gx, gz, RouteNodeKind.Door, doorId, door.StandFacingYaw),
                new RouteNode(door.StandX + along * TripSpread, laneZ),
            });
        }

        private static RouteDetour[] DoorTrips() => new[]
        {
            DoorTrip(FoggBranch, LivingStreetLayout.Fogg, N, 1f),
            DoorTrip(PearlBranch, LivingStreetLayout.Pearl, N, 1f),
            DoorTrip(AbbottsBranch, LivingStreetLayout.Abbotts, S, -1f),
            new RouteDetour(0, RouteGoal.PipPorch, null, PorchExcursion()),
        };

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
            new RouteNode(BranchX, S),
        };

        /// <summary>
        /// The strollers' long loop: along the lane's middle with a stop to admire Mr. Fogg's stacked jack-o'-lanterns, a
        /// turn through the heart south of the plaza, a stop to admire the Abbotts' jack-o'-lanterns, and one to admire the
        /// lantern table and its lit samples on the Green.
        /// </summary>
        private static RouteNode[] StrollLoop() => new[]
        {
            new RouteNode(43.0f, 0.4f),
            new RouteNode(31.2f, 0.4f, RouteNodeKind.Look, null, 0f, 3.5f),
            new RouteNode(24.6f, 0.4f),
            new RouteNode(23.4f, -2.2f, RouteNodeKind.HeartGate),
            new RouteNode(18.0f, -3.2f),
            new RouteNode(11.2f, -4.0f),
            new RouteNode(11.6f, -6.4f),
            new RouteNode(17.2f, -6.0f),
            new RouteNode(21.0f, -3.0f),
            new RouteNode(23.6f, -2.8f, RouteNodeKind.HeartExit),
            new RouteNode(24.8f, -0.4f),
            new RouteNode(31.4f, -0.4f, RouteNodeKind.Look, null, 180f, 4f),
            new RouteNode(38.4f, -0.4f),
            new RouteNode(40.4f, -3.2f, RouteNodeKind.Look, null, 170f, 4f),
            new RouteNode(43.0f, -1.6f),
            new RouteNode(43.0f, 0.4f),
        };

        private static WalkerLook Look(string recipe) => new WalkerLook(recipe);

        /// <summary>
        /// The slice's three groups, eight walkers, all from the crowd kit: a cat mum with a ghost and a cat child, a
        /// skeleton dad with a robot and a little queen, and a paper-crown grandpa strolling with a candy-corn grandchild.
        /// </summary>
        public static readonly IReadOnlyList<CrowdRoute> All = new[]
        {
            new CrowdRoute("group_a", GroupSpeed, DoorCircuit(), DoorTrips(), new[]
            {
                new MemberSlot(0f, -0.35f, Look("GhostKid")),
                new MemberSlot(0.45f, 0.35f, Look("CatKid")),
                new MemberSlot(1.3f, 0f, Look("CatGrownUp")),
            }, 3f),
            new CrowdRoute("group_b", GroupSpeed, DoorCircuit(), DoorTrips(), new[]
            {
                new MemberSlot(0f, 0.35f, Look("RobotKid")),
                new MemberSlot(0.45f, -0.35f, Look("QueenKid")),
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
