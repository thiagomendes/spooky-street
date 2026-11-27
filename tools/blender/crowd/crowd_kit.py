"""The crowd costume kit for the living street (docs/living-street.md §7, slice of Phase 1; D34): the data.

Pure data, no Blender: the palette, the parts each body carries and the walker recipes. crowd_export.py builds
the parts, crowd_review.py renders the walkers, and `Assets/SpookyStreet/Core/Crowd/CrowdKit.cs` holds the same
recipes for the game (CrowdArtImportTests checks that the two agree with the committed FBX files).

Two bodies on Pip's skeleton (same bone names and axes) with their own bind proportions:
  CrowdChild   ~1.05 m, 4.5 heads (Pip's and Bea's proportions, a little younger)
  CrowdAdult   ~1.70 m, 6 heads (Mr. Vale's and Aunt Kit's proportions)
Each FBX holds the skeleton, the clips (Idle, Walk, Wave, Talk) and every part of that body as its own skinned
mesh, all on one shared atlas (Crowd_BaseColor / _Normal / _Emission). A walker is one part per slot, merged into
one skinned mesh at spawn: one draw call. Every part also has a `<Part>_Lo` mesh (LOD1).

Reserved looks (living-street §3.4 rule 6) are kept out: no cloth silver, no lavender foil, no bat cape, no
pumpkin, no star witch, no mummy, no fortune teller, no scarecrow, dragon, pirate, gingerbread witch, wizard or
ghost-in-overalls, no owl, fox, moth, moon or bell motif, and no capes at all. Witches are left out of the slice on
purpose: a pointed hat is June's silhouette at phone size.
"""

H = lambda v: (((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0)  # noqa: E731

# Art bible §2 families, a step calmer than the cast's vivid costume colours so the crowd stays background.
PAL = {
    "night": H(0x2A2550), "purple": H(0x5B3A7A), "plum": H(0x7A3E6E), "orange": H(0xEE7F2D), "candy": H(0xF6D365),
    "green": H(0x8DBF3F), "bone": H(0xF4E6C8), "ink": H(0x1E1A26), "indigo": H(0x394461),
    "kraft": H(0xB98A5A), "kraft_dark": H(0x8E6440), "berry": H(0xA83A5C), "rose": H(0xE89AA6),
    "white": H(0xFBF4E6), "sole": H(0xEDE3D0),
}
SKIN = {  # tone: (skin, blush)
    "t1": (H(0xF8D9C0), H(0xF2959A)), "t2": (H(0xF0C29E), H(0xE88A80)), "t3": (H(0xD9A27A), H(0xD9776A)),
    "t4": (H(0xC98C5E), H(0xCF6A55)), "t5": (H(0x9A6440), H(0xB85645)), "t6": (H(0x6E4129), H(0x9A4638)),
}
HAIR = {"dark": H(0x2A1712), "black": H(0x1E1220), "auburn": H(0xA9441F), "brown": H(0x6A3F24),
        "fair": H(0xD9B26A)}
IRIS = {"brown": H(0x7A4520), "hazel": H(0x8A6A2A), "green": H(0x4E7A3A), "blue": H(0x3F6A9A),
        "dark": H(0x3A2416)}

BODIES = ("CrowdChild", "CrowdAdult")

# Slots: a walker takes at most one part per slot. `Head` is empty under the ghost sheet (its hood is the head).
SLOTS = ("Body", "Hands", "Shoes", "Head", "Hair", "HeadPiece", "Costume", "Tail", "Prop")

# Parts per body: name -> (slot, builder, options). Names are the mesh names in the FBX (LOD1: name + "_Lo").
CHILD_PARTS = {
    # bodies: top (torso and sleeves) and legs; `print` paints a pattern over them
    "Body_Indigo": ("Body", "body", dict(top="indigo", legs="night")),
    "Body_Skeleton": ("Body", "body", dict(top="ink", legs="ink", print="skeleton")),
    "Body_Cat": ("Body", "body", dict(top="ink", legs="ink", print="cat_belly")),
    "Body_Purple": ("Body", "body", dict(top="purple", legs="ink")),
    "Body_Night": ("Body", "body", dict(top="night", legs="bone_stripe")),
    "Body_Plum": ("Body", "body", dict(top="plum", legs="purple")),
    "Hands_T1": ("Hands", "hands", dict(tone="t1")),
    "Hands_T2": ("Hands", "hands", dict(tone="t2")),
    "Hands_T3": ("Hands", "hands", dict(tone="t3")),
    "Hands_T5": ("Hands", "hands", dict(tone="t5")),
    "Hands_Bone": ("Hands", "hands", dict(glove="bone")),
    "Hands_Ink": ("Hands", "hands", dict(glove="ink")),
    "Shoes_Orange": ("Shoes", "shoes", dict(upper="orange", accent="purple")),
    "Shoes_Ink": ("Shoes", "shoes", dict(upper="ink", accent="bone")),
    "Shoes_Plum": ("Shoes", "shoes", dict(upper="plum", accent="candy")),
    "Shoes_Candy": ("Shoes", "shoes", dict(upper="candy", accent="indigo")),
    "Shoes_Green": ("Shoes", "shoes", dict(upper="green", accent="night")),
    "Head_Skull": ("Head", "head", dict(tone="t2", iris="blue", paint="skull")),
    "Head_Cat": ("Head", "head", dict(tone="t3", iris="green", paint="cat")),
    "Head_Robot": ("Head", "head", dict(tone="t5", iris="dark", paint="plain", mouth="grin")),
    "Head_Corn": ("Head", "head", dict(tone="t4", iris="dark", paint="plain", freckles=False)),
    "Head_Queen": ("Head", "head", dict(tone="t1", iris="hazel", paint="plain", freckles=True)),
    "Hair_Puffs": ("Hair", "hair", dict(style="puffs", colour="black")),
    "Hair_Long": ("Hair", "hair", dict(style="long", colour="auburn")),
    "Ghost_Sheet": ("Costume", "ghost_sheet", {}),
    "Skeleton_Hood": ("HeadPiece", "hood", dict(colour="ink", ears=False)),
    "Cat_Hood": ("HeadPiece", "hood", dict(colour="ink", ears=True)),
    "Cat_Tail": ("Tail", "tail", dict(colour="ink", tip="bone")),
    "Robot_Box": ("Costume", "robot_box", {}),
    "Robot_Helmet": ("HeadPiece", "robot_helmet", {}),
    "CandyCorn_Tunic": ("Costume", "candy_corn", {}),
    "Crown_Paper": ("HeadPiece", "crown", {}),
    "Queen_Dress": ("Costume", "tunic", dict(colour="purple", trim="candy", length=0.62, flare=1.5)),
    "Pail_Plum": ("Prop", "pail", dict(colour="plum")),
    "Pail_Indigo": ("Prop", "pail", dict(colour="indigo")),
    "Bag_Paper": ("Prop", "bag", {}),
    "Lantern_Paper": ("Prop", "lantern", dict(colour="candy")),
}
ADULT_PARTS = {
    "Body_King": ("Body", "body", dict(top="bone", legs="ink")),
    "Body_CatMum": ("Body", "body", dict(top="purple_knit", legs="ink")),
    "Body_Skeleton": ("Body", "body", dict(top="ink", legs="ink", print="skeleton")),
    "Hands_T2": ("Hands", "hands", dict(tone="t2")),
    "Hands_T4": ("Hands", "hands", dict(tone="t4")),
    "Hands_T6": ("Hands", "hands", dict(tone="t6")),
    "Shoes_Ink": ("Shoes", "shoes", dict(upper="ink", accent="bone")),
    "Shoes_Orange": ("Shoes", "shoes", dict(upper="orange", accent="night")),
    "Shoes_Bone": ("Shoes", "shoes", dict(upper="bone", accent="plum")),
    "Head_King": ("Head", "head", dict(tone="t4", iris="dark", paint="plain", beard=True)),
    "Head_CatMum": ("Head", "head", dict(tone="t2", iris="brown", paint="cat")),
    "Head_Skull": ("Head", "head", dict(tone="t6", iris="dark", paint="skull")),
    "Hair_Short": ("Hair", "hair", dict(style="short", colour="dark")),
    "Hair_Bun": ("Hair", "hair", dict(style="bun", colour="brown")),
    "Hair_Crop": ("Hair", "hair", dict(style="crop", colour="black")),
    "Crown_Paper": ("HeadPiece", "crown", {}),
    "Cat_Ears": ("HeadPiece", "ears_band", dict(colour="ink")),
    "Cat_Tail": ("Tail", "tail", dict(colour="ink", tip="bone")),
    "King_Tunic": ("Costume", "tunic", dict(colour="plum", trim="bone", length=0.5, flare=1.18, belt="orange",
                                           ermine=True)),
    "Pail_Indigo": ("Prop", "pail", dict(colour="indigo")),
    "Bag_Paper": ("Prop", "bag", {}),
    "Lantern_Paper": ("Prop", "lantern", dict(colour="bone")),
}
PARTS = {"CrowdChild": CHILD_PARTS, "CrowdAdult": ADULT_PARTS}

# The slice's walkers: id -> (body, [parts]). Nine walkers, six costume themes (ghost sheet, skeleton suit,
# black cat, cardboard robot, candy corn, paper-crown king and queen), two bodies.
WALKERS = {
    "GhostKid": ("CrowdChild", ["Body_Indigo", "Hands_T2", "Shoes_Orange", "Ghost_Sheet", "Pail_Plum"]),
    "SkeletonKid": ("CrowdChild", ["Body_Skeleton", "Hands_Bone", "Shoes_Ink", "Head_Skull", "Skeleton_Hood",
                                   "Pail_Indigo"]),
    "CatKid": ("CrowdChild", ["Body_Cat", "Hands_Ink", "Shoes_Plum", "Head_Cat", "Cat_Hood", "Cat_Tail",
                              "Bag_Paper"]),
    "RobotKid": ("CrowdChild", ["Body_Purple", "Hands_T5", "Shoes_Candy", "Head_Robot", "Robot_Helmet",
                                "Robot_Box", "Pail_Plum"]),
    "CandyCornKid": ("CrowdChild", ["Body_Night", "Hands_T3", "Shoes_Green", "Head_Corn", "Hair_Puffs",
                                    "CandyCorn_Tunic", "Bag_Paper"]),
    "QueenKid": ("CrowdChild", ["Body_Plum", "Hands_T1", "Shoes_Plum", "Head_Queen", "Hair_Long", "Crown_Paper",
                                "Queen_Dress", "Lantern_Paper"]),
    "KingGrownUp": ("CrowdAdult", ["Body_King", "Hands_T4", "Shoes_Ink", "Head_King", "Hair_Short", "Crown_Paper",
                                   "King_Tunic", "Lantern_Paper"]),
    "CatGrownUp": ("CrowdAdult", ["Body_CatMum", "Hands_T2", "Shoes_Orange", "Head_CatMum", "Hair_Bun", "Cat_Ears",
                                  "Cat_Tail", "Bag_Paper"]),
    "SkeletonGrownUp": ("CrowdAdult", ["Body_Skeleton", "Hands_T6", "Shoes_Bone", "Head_Skull", "Hair_Crop",
                                       "Pail_Indigo"]),
}

CLIPS = ("Idle", "Walk", "Wave", "Talk")
LOD1_SUFFIX = "_Lo"
BUDGET = {"lod0": 7000, "lod1": 2500, "atlas": 2048}
