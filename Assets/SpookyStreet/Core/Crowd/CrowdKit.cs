using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Crowd
{
    /// <summary>A swappable part of a crowd body: the slot it fills and its mesh name in the body's FBX.</summary>
    public readonly struct CrowdPart
    {
        public CrowdPart(string slot, string name)
        {
            Slot = slot;
            Name = name;
        }

        public string Slot { get; }
        public string Name { get; }
    }

    /// <summary>One walker of the living street: a body model and one part per slot, merged into one mesh.</summary>
    public sealed class CrowdWalkerRecipe
    {
        public CrowdWalkerRecipe(string id, string model, params string[] parts)
        {
            Id = id;
            Model = model;
            Parts = parts;
        }

        /// <summary>Stable id (never shown to the player).</summary>
        public string Id { get; }

        /// <summary><see cref="CrowdKit.ChildModel"/> or <see cref="CrowdKit.AdultModel"/>.</summary>
        public string Model { get; }

        /// <summary>LOD0 mesh names in the model; LOD1 adds <see cref="CrowdKit.Lod1Suffix"/>.</summary>
        public IReadOnlyList<string> Parts { get; }

        public bool IsGrownUp => Model == CrowdKit.AdultModel;
    }

    /// <summary>
    /// The art contract of the crowd costume kit (docs/living-street.md §7 and §8, D34; made by
    /// tools/blender/crowd/crowd_export.py, documented in docs/living-street-crowd-art.md). Two bodies on Pip's
    /// skeleton, each one FBX under <see cref="Folder"/> holding the skeleton, the clips and every part as its own
    /// skinned mesh (plus a <see cref="Lod1Suffix"/> copy), all on the one shared atlas <see cref="Atlas"/>
    /// (<c>Crowd_BaseColor</c>, <c>_Normal</c>, <c>_Emission</c>). A walker shows only its recipe's parts, merged at
    /// spawn into one skinned mesh: one draw call. The same table is in tools/blender/crowd/crowd_kit.py.
    /// No part uses a reserved look (living-street §3.4 rule 6): no silver, foil, cape, pumpkin, witch, mummy,
    /// fortune teller, future residents' themes or Players' motifs.
    /// </summary>
    public static class CrowdKit
    {
        public const string Folder = "Models/Characters/Crowd";
        public const string Atlas = "Crowd";
        public const string ChildModel = "CrowdChild";
        public const string AdultModel = "CrowdAdult";
        public const string Lod1Suffix = "_Lo";

        public const string SlotBody = "Body";
        public const string SlotHands = "Hands";
        public const string SlotShoes = "Shoes";
        public const string SlotHead = "Head";
        public const string SlotHair = "Hair";
        public const string SlotHeadPiece = "HeadPiece";
        public const string SlotCostume = "Costume";
        public const string SlotTail = "Tail";
        public const string SlotProp = "Prop";

        /// <summary>Costume bones on top of Pip's skeleton: the prop hangs plumb from <c>Pail</c> (child of Hand_R);
        /// <c>Blink</c> (child of Head) brings the lids out of the skull; <c>Tail_1</c>/<c>Tail_2</c> swing a tail.</summary>
        public static readonly string[] CostumeBones = { "Pail", "Blink", "Tail_1", "Tail_2" };

        /// <summary>Clips in both FBX files (names from <c>CharacterClips</c>): Idle, Walk and Talk loop, Wave plays
        /// once and starts and ends on Idle frame 0.</summary>
        public static readonly string[] Clips = { "Idle", "Walk", "Wave", "Talk" };

        /// <summary>LOD budget per merged walker (living-street §8).</summary>
        public const int MaxLod0Triangles = 7000;
        public const int MaxLod1Triangles = 2500;
        public const int AtlasSize = 2048;

        /// <summary>Standing heights in metres (top of the skull), for spacing and the paper standees.</summary>
        public const float ChildHeight = 1.05f;
        public const float AdultHeight = 1.70f;

        /// <summary>Ground speed (m/s) the Walk clip was animated for at playback speed 1: the child walks 24 frames
        /// per cycle and the grown-up 41, so a parent and a child stroll side by side at the same pace. Move a
        /// walker at this speed (or scale the clip's speed by actual / this) so the feet do not slide.</summary>
        public static float WalkSpeed(string model) => model == AdultModel ? 1.173f : 1.167f;

        public static readonly CrowdPart[] ChildParts =
        {
            new CrowdPart(SlotBody, "Body_Indigo"), new CrowdPart(SlotBody, "Body_Skeleton"),
            new CrowdPart(SlotBody, "Body_Cat"), new CrowdPart(SlotBody, "Body_Purple"),
            new CrowdPart(SlotBody, "Body_Night"), new CrowdPart(SlotBody, "Body_Plum"),
            new CrowdPart(SlotHands, "Hands_T1"), new CrowdPart(SlotHands, "Hands_T2"),
            new CrowdPart(SlotHands, "Hands_T3"), new CrowdPart(SlotHands, "Hands_T5"),
            new CrowdPart(SlotHands, "Hands_Bone"), new CrowdPart(SlotHands, "Hands_Ink"),
            new CrowdPart(SlotShoes, "Shoes_Orange"), new CrowdPart(SlotShoes, "Shoes_Ink"),
            new CrowdPart(SlotShoes, "Shoes_Plum"), new CrowdPart(SlotShoes, "Shoes_Candy"),
            new CrowdPart(SlotShoes, "Shoes_Green"),
            new CrowdPart(SlotHead, "Head_Skull"), new CrowdPart(SlotHead, "Head_Cat"),
            new CrowdPart(SlotHead, "Head_Robot"), new CrowdPart(SlotHead, "Head_Corn"),
            new CrowdPart(SlotHead, "Head_Queen"),
            new CrowdPart(SlotHair, "Hair_Puffs"), new CrowdPart(SlotHair, "Hair_Long"),
            new CrowdPart(SlotCostume, "Ghost_Sheet"), new CrowdPart(SlotHeadPiece, "Skeleton_Hood"),
            new CrowdPart(SlotHeadPiece, "Cat_Hood"), new CrowdPart(SlotTail, "Cat_Tail"),
            new CrowdPart(SlotCostume, "Robot_Box"), new CrowdPart(SlotHeadPiece, "Robot_Helmet"),
            new CrowdPart(SlotCostume, "CandyCorn_Tunic"), new CrowdPart(SlotHeadPiece, "Crown_Paper"),
            new CrowdPart(SlotCostume, "Queen_Dress"),
            new CrowdPart(SlotProp, "Pail_Plum"), new CrowdPart(SlotProp, "Pail_Indigo"),
            new CrowdPart(SlotProp, "Bag_Paper"), new CrowdPart(SlotProp, "Lantern_Paper"),
        };

        public static readonly CrowdPart[] AdultParts =
        {
            new CrowdPart(SlotBody, "Body_King"), new CrowdPart(SlotBody, "Body_CatMum"),
            new CrowdPart(SlotBody, "Body_Skeleton"),
            new CrowdPart(SlotHands, "Hands_T2"), new CrowdPart(SlotHands, "Hands_T4"),
            new CrowdPart(SlotHands, "Hands_T6"),
            new CrowdPart(SlotShoes, "Shoes_Ink"), new CrowdPart(SlotShoes, "Shoes_Orange"),
            new CrowdPart(SlotShoes, "Shoes_Bone"),
            new CrowdPart(SlotHead, "Head_King"), new CrowdPart(SlotHead, "Head_CatMum"),
            new CrowdPart(SlotHead, "Head_Skull"),
            new CrowdPart(SlotHair, "Hair_Short"), new CrowdPart(SlotHair, "Hair_Bun"),
            new CrowdPart(SlotHair, "Hair_Crop"),
            new CrowdPart(SlotHeadPiece, "Crown_Paper"), new CrowdPart(SlotHeadPiece, "Cat_Ears"),
            new CrowdPart(SlotTail, "Cat_Tail"), new CrowdPart(SlotCostume, "King_Tunic"),
            new CrowdPart(SlotProp, "Pail_Indigo"), new CrowdPart(SlotProp, "Bag_Paper"),
            new CrowdPart(SlotProp, "Lantern_Paper"),
        };

        /// <summary>The slice's nine walkers: six costume themes (ghost sheet, skeleton suit, black cat, cardboard
        /// robot, candy corn, paper-crown king and queen) on two bodies.</summary>
        public static readonly CrowdWalkerRecipe[] Walkers =
        {
            new CrowdWalkerRecipe("GhostKid", ChildModel, "Body_Indigo", "Hands_T2", "Shoes_Orange", "Ghost_Sheet",
                "Pail_Plum"),
            new CrowdWalkerRecipe("SkeletonKid", ChildModel, "Body_Skeleton", "Hands_Bone", "Shoes_Ink", "Head_Skull",
                "Skeleton_Hood", "Pail_Indigo"),
            new CrowdWalkerRecipe("CatKid", ChildModel, "Body_Cat", "Hands_Ink", "Shoes_Plum", "Head_Cat", "Cat_Hood",
                "Cat_Tail", "Bag_Paper"),
            new CrowdWalkerRecipe("RobotKid", ChildModel, "Body_Purple", "Hands_T5", "Shoes_Candy", "Head_Robot",
                "Robot_Helmet", "Robot_Box", "Pail_Plum"),
            new CrowdWalkerRecipe("CandyCornKid", ChildModel, "Body_Night", "Hands_T3", "Shoes_Green", "Head_Corn",
                "Hair_Puffs", "CandyCorn_Tunic", "Bag_Paper"),
            new CrowdWalkerRecipe("QueenKid", ChildModel, "Body_Plum", "Hands_T1", "Shoes_Plum", "Head_Queen",
                "Hair_Long", "Crown_Paper", "Queen_Dress", "Lantern_Paper"),
            new CrowdWalkerRecipe("KingGrownUp", AdultModel, "Body_King", "Hands_T4", "Shoes_Ink", "Head_King",
                "Hair_Short", "Crown_Paper", "King_Tunic", "Lantern_Paper"),
            new CrowdWalkerRecipe("CatGrownUp", AdultModel, "Body_CatMum", "Hands_T2", "Shoes_Orange", "Head_CatMum",
                "Hair_Bun", "Cat_Ears", "Cat_Tail", "Bag_Paper"),
            new CrowdWalkerRecipe("SkeletonGrownUp", AdultModel, "Body_Skeleton", "Hands_T6", "Shoes_Bone", "Head_Skull",
                "Hair_Crop", "Pail_Indigo"),
        };

        /// <summary>Words no crowd part may carry: the cast's and future residents' themes and the Players' motifs
        /// (living-street §3.4 rule 6).</summary>
        public static readonly string[] ReservedWords =
        {
            "Silver", "Foil", "Cape", "Bat", "Pumpkin", "Witch", "Star", "Mummy", "Bandage", "Fortune", "Scarecrow",
            "Dragon", "Pirate", "Gingerbread", "Wizard", "Overalls", "Owl", "Fox", "Moth", "Moon", "Bell",
        };

        public static IReadOnlyList<CrowdPart> PartsOf(string model) => model == AdultModel ? AdultParts : ChildParts;

        /// <summary>The slot of <paramref name="part"/> in <paramref name="model"/>, or null when the model has no
        /// such part.</summary>
        public static string SlotOf(string model, string part)
        {
            foreach (var p in PartsOf(model))
            {
                if (p.Name == part)
                {
                    return p.Slot;
                }
            }

            return null;
        }

        /// <summary>The recipe with <paramref name="id"/>, or null.</summary>
        public static CrowdWalkerRecipe Find(string id) => Array.Find(Walkers, w => w.Id == id);

        /// <summary>Mesh names to merge for a walker at a level of detail (0 or 1).</summary>
        public static string[] MeshNames(CrowdWalkerRecipe walker, int lod)
        {
            var names = new string[walker.Parts.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = lod == 0 ? walker.Parts[i] : walker.Parts[i] + Lod1Suffix;
            }

            return names;
        }
    }
}
