using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// Names and measures of the east lane's three trick-or-treat houses (docs/living-street.md §4.1, §7 "Houses", §8;
    /// docs/living-street-houses-art.md): Mr. Fogg's tower house under its mansard, Nana Pearl's cottage with the long
    /// porch, and the Abbotts' twin-gable house, each with its own door leaf (hinged by the game for its gag) and porch
    /// lamp. Art only: the doors' gags, bells and treats live in <c>NeighbourDoors</c> and the slice's runtime. The
    /// numbers mirror the Blender builders (tools/blender/env/lane_houses.py) and their manifest (lane_manifest.json).
    /// <para>Houses are keyed by the slice's door ids (<c>LivingStreetLayout.Fogg</c>, <c>Pearl</c>, <c>Abbotts</c>). Each
    /// house fits the placement, door, wall and footprint the slice already uses for it, so only the model names change.</para>
    /// <para>Frames: Blender builder space (x, y, z up) is Unity (x, z, y) in a model's own frame; models face -Z.</para>
    /// </summary>
    public static class LaneHouseArt
    {
        /// <summary>Resources folder of the models and their atlases.</summary>
        public const string Folder = "Models/Environment";

        public const string Fogg = "fogg";
        public const string Pearl = "pearl";
        public const string Abbotts = "abbotts";

        /// <summary>The three houses in the slice's order.</summary>
        public static readonly IReadOnlyList<string> Houses = new[] { Fogg, Pearl, Abbotts };

        /// <summary>
        /// The house model (<c>fogg</c> → <c>LaneHouseFogg</c>): an empty root at the ground under the placement point with
        /// <see cref="Lod0Suffix"/> and <see cref="Lod1Suffix"/> children (Unity's LOD group) and the lit parts both
        /// levels share (<c>RoomCard_Glow</c>: the lit rooms and the hall behind the door; <c>Face_Glow</c>: the
        /// jack-o'-lanterns). Its atlas carries its own name, so <c>ModelKit</c> finds it without a fallback.
        /// </summary>
        public static string House(string id) => "LaneHouse" + Title(id);

        /// <summary>The door leaf: origin on its hinge edge at the sill, closed along +X, front toward -Z.</summary>
        public static string DoorLeaf(string id) => "LaneDoor" + Title(id);

        /// <summary>The porch lamp: origin in the middle of its glass; the glass is <see cref="LampGlow"/>.</summary>
        public static string PorchLamp(string id) => "LanePorchLamp" + Title(id);

        /// <summary>
        /// The texture set a house shares with its leaf and its lamp (<c>&lt;Atlas&gt;_BaseColor</c> 2048, <c>_Normal</c>
        /// 1024, <c>_Emission</c> 512): load the leaf and the lamp with <c>new ModelKit(materials, Folder) { FallbackAtlas =
        /// Atlas(id) }</c>.
        /// </summary>
        public static string Atlas(string id) => House(id);

        public const int AtlasSize = 2048;

        public const string Lod0Suffix = "_LOD0";
        public const string Lod1Suffix = "_LOD1";

        /// <summary>LOD0 shows while the house is taller than this share of the screen (about 24 m away at 50 degrees).</summary>
        public const float Lod0ScreenHeight = 0.35f;

        /// <summary>LOD1 shows down to this (in practice always: the sky dome ends first).</summary>
        public const float Lod1ScreenHeight = 0.005f;

        /// <summary>The porch lamp's glass (<see cref="GlowLevels.Lamp"/> by its name; the slice drives it at 0.9, warmer for Nana Pearl).</summary>
        public const string LampGlow = "Lamp" + GlowLevels.Suffix;

        /// <summary>The lit rooms behind the windows and the lit hall behind the door (<see cref="GlowLevels.RoomCard"/>).</summary>
        public const string RoomGlow = "RoomCard" + GlowLevels.Suffix;

        // ---- where the pieces go, from the door's centre on the front wall's face (the slice's DoorLocalX, WallLocalZ) ----

        /// <summary>The sill (the top of the landing or porch deck at the door).</summary>
        public const float Sill = 0.4f;

        /// <summary>The leaf (the slice's DoorWidth, DoorHeight) and the opening behind it.</summary>
        public const float LeafWidth = 1.14f;
        public const float LeafHeight = 2.12f;
        public const float OpeningWidth = 1.1f;
        public const float OpeningHeight = 2.1f;

        /// <summary>The leaf's hinge stands this far in front of the wall's face, on the door's left edge (seen from the street).</summary>
        public const float HingeInset = 0.08f;

        /// <summary>The porch lamp's centre: across from the door's centre, up from the ground, out from the wall (−Z).</summary>
        public const float LampAcross = 0.89f;
        public const float LampHeight = 2.25f;
        public const float LampOut = 0.12f;

        /// <summary>The doorbell's push button (part of the house): across from the door's centre and up from the ground.</summary>
        public const float BellAcross = 0.75f;
        public const float BellHeight = 1.5f;

        /// <summary>Depth of the lit hall behind the opening (the house's own; the slice's flat doorway card is not needed).</summary>
        public const float HallDepth = 0.95f;

        /// <summary>The door's centre across the front and the front wall's face, in the house's own frame (Unity x, z).</summary>
        public static void Door(string id, out float across, out float wall)
        {
            switch (Check(id))
            {
                case Fogg: across = 0.5f; wall = -3.0f; break;
                case Pearl: across = 0f; wall = -1.9f; break;
                default: across = -1.85f; wall = -3.0f; break;
            }
        }

        /// <summary>Footprint the house stays inside (width along x, depth along z), the slice's bounds-check size.</summary>
        public static void Footprint(string id, out float width, out float depth)
        {
            switch (Check(id))
            {
                case Fogg: width = 8f; depth = 7.7f; break;
                case Pearl: width = 10.5f; depth = 9f; break;
                default: width = 7.8f; depth = 7.7f; break;
            }
        }

        /// <summary>The visitor's stand at the foot of the steps (the slice's standLocalZ): every piece stays behind it.</summary>
        public static float Stand(string id) => Check(id) == Pearl ? -5.3f : -4.6f;

        /// <summary>Triangle budget of a house (the heart's houses: the skyline houses about 5k, the owl house about 26k).</summary>
        public const int MaxLod0Triangles = 20000;
        public const int MaxLod1Triangles = 5000;

        private static string Title(string id)
        {
            string s = Check(id);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string Check(string id)
        {
            foreach (string h in Houses)
            {
                if (string.Equals(h, id, StringComparison.Ordinal))
                {
                    return h;
                }
            }

            throw new ArgumentException("not a lane house: " + id, nameof(id));
        }
    }
}
