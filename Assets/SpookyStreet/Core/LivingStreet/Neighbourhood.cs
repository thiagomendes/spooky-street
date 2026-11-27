using System;
using System.Collections.Generic;
using SpookyStreet.Core.Save;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// The shapes of Pip's paper lantern (living-street §4.2, D35), chosen from pictures: bat, cat, ghost and leaf.
    /// Never moon, star or pumpkin: those are reserved looks (§3.4 rule 6: the Players' moon, June's star witch, Bea's
    /// pumpkin), and a lantern in one of them would read as a clue or a friend's sign.
    /// </summary>
    public enum LanternShape
    {
        Bat,
        Cat,
        Ghost,
        Leaf,
    }

    /// <summary>
    /// The lantern's paper colours: the v1.3 Halloween families only, never the case's reserved looks (no cloth silver,
    /// no lavender foil; §3.4 rule 6).
    /// </summary>
    public enum LanternColour
    {
        Orange,
        Plum,
        Green,
        Yellow,
    }

    /// <summary>Pip's folded lantern: a shape and a colour, both stable IDs in the save.</summary>
    public readonly struct PipLantern : IEquatable<PipLantern>
    {
        public PipLantern(LanternShape shape, LanternColour colour)
        {
            Shape = shape;
            Colour = colour;
        }

        public LanternShape Shape { get; }
        public LanternColour Colour { get; }

        public bool Equals(PipLantern other) => Shape == other.Shape && Colour == other.Colour;

        public override bool Equals(object obj) => obj is PipLantern other && Equals(other);

        public override int GetHashCode() => (int)Shape * 31 + (int)Colour;

        public override string ToString() => LanternCraft.ShapeId(Shape) + "/" + LanternCraft.ColourId(Colour);
    }

    /// <summary>The lantern table's choices: stable IDs, picture names, labels and the screen's focus layout.</summary>
    public static class LanternCraft
    {
        public static readonly IReadOnlyList<LanternShape> Shapes =
            new[] { LanternShape.Bat, LanternShape.Cat, LanternShape.Ghost, LanternShape.Leaf };

        public static readonly IReadOnlyList<LanternColour> Colours =
            new[] { LanternColour.Orange, LanternColour.Plum, LanternColour.Green, LanternColour.Yellow };

        /// <summary>What the screen starts on the first time: a bat like Pip's costume, in the street's lantern orange.</summary>
        public static readonly PipLantern Default = new PipLantern(LanternShape.Bat, LanternColour.Orange);

        /// <summary>Shapes no lantern may take (D35, §3.4 rule 6).</summary>
        public static readonly IReadOnlyList<string> ReservedShapes = new[] { "moon", "star", "pumpkin" };

        public static string ShapeId(LanternShape shape)
        {
            switch (shape)
            {
                case LanternShape.Bat: return "bat";
                case LanternShape.Cat: return "cat";
                case LanternShape.Ghost: return "ghost";
                default: return "leaf";
            }
        }

        public static string ColourId(LanternColour colour)
        {
            switch (colour)
            {
                case LanternColour.Orange: return "orange";
                case LanternColour.Plum: return "plum";
                case LanternColour.Green: return "green";
                default: return "yellow";
            }
        }

        public static bool TryShape(string id, out LanternShape shape)
        {
            foreach (var s in Shapes)
            {
                if (string.Equals(ShapeId(s), id, StringComparison.Ordinal))
                {
                    shape = s;
                    return true;
                }
            }

            shape = default;
            return false;
        }

        public static bool TryColour(string id, out LanternColour colour)
        {
            foreach (var c in Colours)
            {
                if (string.Equals(ColourId(c), id, StringComparison.Ordinal))
                {
                    colour = c;
                    return true;
                }
            }

            colour = default;
            return false;
        }

        public static string ShapeKey(LanternShape shape)
        {
            switch (shape)
            {
                case LanternShape.Bat: return Keys.ShapeBat;
                case LanternShape.Cat: return Keys.ShapeCat;
                case LanternShape.Ghost: return Keys.ShapeGhost;
                default: return Keys.ShapeLeaf;
            }
        }

        public static string ColourKey(LanternColour colour)
        {
            switch (colour)
            {
                case LanternColour.Orange: return Keys.ColourOrange;
                case LanternColour.Plum: return Keys.ColourPlum;
                case LanternColour.Green: return Keys.ColourGreen;
                default: return Keys.ColourYellow;
            }
        }

        /// <summary>The paper colour as 0xRRGGBB (palette v1.3: jack orange, plum, witch green, candy-corn yellow).</summary>
        public static int ColourRgb(LanternColour colour)
        {
            switch (colour)
            {
                case LanternColour.Orange: return 0xEE7F2D;
                case LanternColour.Plum: return 0x7A3E6E;
                case LanternColour.Green: return 0x5E9A3A;
                default: return 0xF6D365;
            }
        }

        // The screen's focus graph (S004 UI base): close on top, a row of shapes, a row of colours, the fold button.
        public const string FocusClose = "close";
        public const string FocusFold = "fold";

        public static string FocusShape(LanternShape shape) => "shape." + ShapeId(shape);

        public static string FocusColour(LanternColour colour) => "colour." + ColourId(colour);

        /// <summary>The lantern screen's focus graph: close (top right), shapes, colours, then the fold button.</summary>
        public static Flow.UiFocusGraph FocusGraph()
        {
            var graph = new Flow.UiFocusGraph("lantern_table");
            graph.Add(FocusClose, 0, Shapes.Count - 1);
            for (int i = 0; i < Shapes.Count; i++)
            {
                graph.Add(FocusShape(Shapes[i]), 1, i);
                graph.Add(FocusColour(Colours[i]), 2, i);
            }

            graph.Add(FocusFold, 3, 1);
            graph.Exit = FocusClose;
            graph.DefaultFocus = FocusShape(Shapes[0]);
            return graph;
        }
    }

    /// <summary>
    /// What the free world remembers (living-street §4, §10): Pip's lantern (once she has folded one it hangs lit on
    /// her porch) and her visits to each neighbour's door (capped at <see cref="MaxVisits"/>: the third and later visits
    /// share a gag). Immutable. Nothing here is a count shown to the child (engagement §2.3); only the grown-ups' page
    /// says how many neighbours were greeted.
    /// </summary>
    public sealed class NeighbourhoodState : IEquatable<NeighbourhoodState>
    {
        public const int MaxVisits = 3;

        public static readonly NeighbourhoodState Empty = new NeighbourhoodState(null, new Dictionary<string, int>());

        private readonly Dictionary<string, int> _visits;

        private NeighbourhoodState(PipLantern? lantern, Dictionary<string, int> visits)
        {
            Lantern = lantern;
            _visits = visits;
        }

        /// <summary>Pip's folded lantern (null until she makes one).</summary>
        public PipLantern? Lantern { get; }

        /// <summary>True once her lantern hangs lit on her porch (from then on groups ring her bell).</summary>
        public bool LanternLit => Lantern.HasValue;

        public bool IsEmpty => !Lantern.HasValue && _visits.Count == 0;

        /// <summary>Pip's visits to the door (0..<see cref="MaxVisits"/>).</summary>
        public int Visits(string doorId) => doorId != null && _visits.TryGetValue(doorId, out int n) ? n : 0;

        /// <summary>Neighbours whose bell Pip has rung (the grown-ups' page only).</summary>
        public int NeighboursMet
        {
            get
            {
                int n = 0;
                foreach (var door in LivingStreetLayout.Doors)
                {
                    n += Visits(door.Id) > 0 ? 1 : 0;
                }

                return n;
            }
        }

        public NeighbourhoodState WithLantern(PipLantern lantern) => new NeighbourhoodState(lantern, _visits);

        /// <summary>One more visit to <paramref name="doorId"/> (a known door; capped).</summary>
        public NeighbourhoodState WithVisit(string doorId)
        {
            if (LivingStreetLayout.Door(doorId) == null)
            {
                return this;
            }

            int n = Visits(doorId);
            if (n >= MaxVisits)
            {
                return this;
            }

            var visits = new Dictionary<string, int>(_visits, StringComparer.Ordinal) { [doorId] = n + 1 };
            return new NeighbourhoodState(Lantern, visits);
        }

        public static NeighbourhoodState From(PipLantern? lantern, IEnumerable<KeyValuePair<string, int>> visits)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (visits != null)
            {
                foreach (var pair in visits)
                {
                    if (LivingStreetLayout.Door(pair.Key) != null && pair.Value > 0)
                    {
                        map[pair.Key] = Math.Min(MaxVisits, pair.Value);
                    }
                }
            }

            return new NeighbourhoodState(lantern, map);
        }

        public bool Equals(NeighbourhoodState other)
        {
            if (other == null || !Nullable.Equals(Lantern, other.Lantern) || other._visits.Count != _visits.Count)
            {
                return false;
            }

            foreach (var pair in _visits)
            {
                if (other.Visits(pair.Key) != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as NeighbourhoodState);

        public override int GetHashCode() => (Lantern.HasValue ? Lantern.Value.GetHashCode() : -1) * 17 + _visits.Count;
    }

    /// <summary>
    /// JSON form of <see cref="NeighbourhoodState"/> ("neighbourhood.json", beside the street file, living-street §4):
    /// schema version, the lantern's shape and colour IDs (absent until folded) and one <c>street.free.door.*</c> entry
    /// per visit. Unknown IDs are dropped; a newer schema is preserved instead of read.
    /// </summary>
    public static class NeighbourhoodCodec
    {
        public const int CurrentSchemaVersion = 1;

        public static string Serialize(NeighbourhoodState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var visits = new List<string>();
            foreach (var door in LivingStreetLayout.Doors)
            {
                for (int i = 0; i < state.Visits(door.Id); i++)
                {
                    visits.Add(door.StableId);
                }
            }

            var writer = new SaveJsonWriter().Number("schemaVersion", CurrentSchemaVersion);
            if (state.Lantern.HasValue)
            {
                writer.String("lanternShape", LanternCraft.ShapeId(state.Lantern.Value.Shape))
                    .String("lanternColour", LanternCraft.ColourId(state.Lantern.Value.Colour));
            }

            return writer.Strings("doorVisits", visits).ToString();
        }

        public static bool TryRead(string json, out NeighbourhoodState state, out bool newerSchema)
        {
            state = NeighbourhoodState.Empty;
            newerSchema = false;
            var root = SaveJsonReader.ParseObject(json);
            if (root == null || !SaveJsonReader.TryInt(root, "schemaVersion", out int version) || version < 1)
            {
                return false;
            }

            if (version > CurrentSchemaVersion)
            {
                newerSchema = true;
                return false;
            }

            if (!SaveJsonReader.TryStrings(root, "doorVisits", out var ids))
            {
                return false;
            }

            PipLantern? lantern = null;
            if (SaveJsonReader.TryString(root, "lanternShape", out string shapeId) &&
                SaveJsonReader.TryString(root, "lanternColour", out string colourId) &&
                LanternCraft.TryShape(shapeId, out var shape) && LanternCraft.TryColour(colourId, out var colour))
            {
                lantern = new PipLantern(shape, colour);
            }

            var visits = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                foreach (var door in LivingStreetLayout.Doors)
                {
                    if (string.Equals(door.StableId, id, StringComparison.Ordinal))
                    {
                        visits.TryGetValue(door.Id, out int n);
                        visits[door.Id] = n + 1;
                    }
                }
            }

            state = NeighbourhoodState.From(lantern, visits);
            return true;
        }

        public static SaveCheck Check(string json)
        {
            if (TryRead(json, out _, out bool newer))
            {
                return SaveCheck.Valid;
            }

            return newer ? SaveCheck.Newer : SaveCheck.Invalid;
        }
    }

    /// <summary>
    /// The neighbourhood file with the atomic write, previous copy and recovery of every slot (<see cref="SaveSlotStore"/>),
    /// like the street file. A confirmed New game clears it; Replay case keeps it (D31: the free world is not case data).
    /// A newer build's file is kept untouched until a New game sets it aside.
    /// </summary>
    public sealed class NeighbourhoodStore
    {
        private readonly SaveSlotStore _slot;

        public NeighbourhoodStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), NeighbourhoodCodec.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) => new SaveSlotPaths(directory, "neighbourhood");

        public SaveSlotPaths Paths => _slot.Paths;

        public NeighbourhoodState State { get; private set; } = NeighbourhoodState.Empty;

        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.Empty;

        public bool WritesBlocked => _slot.WritesBlocked;

        public NeighbourhoodState Load()
        {
            var load = _slot.Load();
            LoadStatus = load.Status;
            State = load.HasContents && NeighbourhoodCodec.TryRead(load.Contents, out var read, out _)
                ? read
                : NeighbourhoodState.Empty;
            return State;
        }

        /// <summary>Pip folded a lantern (or a new one): it hangs lit on her porch. Returns true when it changed.</summary>
        public bool MakeLantern(PipLantern lantern) => Set(State.WithLantern(lantern));

        /// <summary>Pip rang <paramref name="doorId"/>'s bell and was answered. Returns true when the visits changed.</summary>
        public bool Visit(string doorId) => Set(State.WithVisit(doorId));

        /// <summary>Confirmed "Replay case": the free world is kept (nothing to write).</summary>
        public NeighbourhoodState Replay() => State;

        /// <summary>Confirmed New game: Pip arrives as the new kid again, so the street forgets her lantern and visits.</summary>
        public void Reset()
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            State = NeighbourhoodState.Empty;
            _slot.Write(NeighbourhoodCodec.Serialize(State));
        }

        private bool Set(NeighbourhoodState next)
        {
            if (next.Equals(State))
            {
                return false;
            }

            State = next;
            if (!_slot.WritesBlocked)
            {
                _slot.Write(NeighbourhoodCodec.Serialize(State));
            }

            return true;
        }
    }
}
