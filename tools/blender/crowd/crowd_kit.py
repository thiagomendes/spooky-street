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
    # the black cat's felt: a soft charcoal plum, a step lighter than ink so the cat keeps its shape at night
    "soot": H(0x3D3644),
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
    "Body_Cat": ("Body", "body", dict(top="soot", legs="soot", print="cat_belly", cuff="bone")),
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
    "Skeleton_Hood": ("HeadPiece", "hood", dict(colour="ink", ears=False, skull=True)),
    "Cat_Hood": ("HeadPiece", "hood", dict(colour="soot", ears=True, collar="candy")),
    "Cat_Tail": ("Tail", "tail", dict(colour="soot", tip="bone", tip_scale=1.4)),
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
ATLAS = {"CrowdChild": "Crowd", "CrowdAdult": "Crowd"}

# --- The door neighbours of the east lane (docs/living-street-houses-art.md; living-street §4.1) -----------------
# Mr. Fogg, Nana Pearl and the two Abbotts answer their doors in house clothes. They are the grown-up body (same
# proportions, skeleton, bind pose and clips as CrowdAdult) exported as its own FBX with its own atlas,
# `CrowdNeighbours_*`, so the shipped walkers' file and atlas stay as they are; a neighbour merges into one skinned
# mesh and animates exactly like a walker. No reserved look: no silver (the grey hair is a warm cream grey), no
# costume theme at all, no owl, fox, moth, moon, star or bell motif.
NEIGHBOUR_BODY = "CrowdNeighbours"
ATLAS[NEIGHBOUR_BODY] = NEIGHBOUR_BODY
HAIR.update({"grey": H(0xCFC3B0), "white": H(0xE6DCCB)})
PAL.update({"green_dark": H(0x3D6B40), "rose": H(0xB86E8E)})
NEIGHBOUR_PARTS = {
    "Body_Fogg": ("Body", "body", dict(top="bone_cable", legs="indigo")),
    "Body_Pearl": ("Body", "body", dict(top="rose_knit", legs="night", print="cardigan")),
    "Body_AbbottA": ("Body", "body", dict(top="orange_stripe", legs="ink")),
    "Body_AbbottB": ("Body", "body", dict(top="plum_stripe", legs="kraft_dark")),
    "Hands_T1": ("Hands", "hands", dict(tone="t1")),
    "Hands_T2": ("Hands", "hands", dict(tone="t2")),
    "Hands_T4": ("Hands", "hands", dict(tone="t4")),
    "Hands_T6": ("Hands", "hands", dict(tone="t6")),
    "Slippers_Plum": ("Shoes", "slippers", dict(colour="plum", cuff="white")),
    "Slippers_Berry": ("Shoes", "slippers", dict(colour="berry", cuff="white")),
    "Slippers_Green": ("Shoes", "slippers", dict(colour="green_dark", cuff="bone")),
    "Head_Fogg": ("Head", "head", dict(tone="t1", iris="blue", paint="plain", beard=True, moustache="big", hair="brown")),
    "Head_Pearl": ("Head", "head", dict(tone="t4", iris="brown", paint="plain", glasses=True, hair="grey")),
    "Head_AbbottA": ("Head", "head", dict(tone="t6", iris="dark", paint="plain", beard=True, hair="black")),
    "Head_AbbottB": ("Head", "head", dict(tone="t2", iris="green", paint="plain", glasses=True, freckles=True,
                                          hair="auburn")),
    "Hair_Brown": ("Hair", "hair", dict(style="crop", colour="brown")),
    "Hair_GreyBun": ("Hair", "hair", dict(style="bun", colour="grey")),
    "Hair_Short": ("Hair", "hair", dict(style="short", colour="black")),
    "Hair_Auburn": ("Hair", "hair", dict(style="short", colour="auburn")),
    "Beanie_Orange": ("HeadPiece", "beanie", dict(colour="orange", band="plum")),
    "Apron_Bone": ("Costume", "apron", dict(colour="bone", trim="berry", pocket="berry")),
    "Apron_Candy": ("Costume", "apron", dict(colour="candy", trim="plum", pocket="orange")),
    "Tin_Treats": ("Prop", "tin", dict(colour="plum", band="bone")),
    "Basket_Treats": ("Prop", "basket", {}),
}
PARTS[NEIGHBOUR_BODY] = NEIGHBOUR_PARTS

# id -> (body, [parts]); the ids are the slice's (LivingStreetLayout.NeighboursAt), the same table as
# CrowdKit.Neighbours in Assets/SpookyStreet/Core/Crowd/CrowdKit.cs
NEIGHBOURS = {
    # booming and silly: a big cream cable jumper, an orange bobble beanie, a big brown beard and moustache, a tin of sweets
    "Neighbour_Fogg": (NEIGHBOUR_BODY, ["Body_Fogg", "Hands_T1", "Slippers_Plum", "Head_Fogg", "Hair_Brown",
                                        "Beanie_Orange", "Tin_Treats"]),
    # slow and warm: a rose cardigan, her bone apron, glasses, a grey bun, a basket of sweets
    "Neighbour_Pearl": (NEIGHBOUR_BODY, ["Body_Pearl", "Hands_T4", "Slippers_Berry", "Head_Pearl", "Hair_GreyBun",
                                         "Apron_Bone", "Basket_Treats"]),
    # the two dads in matching striped jumpers (orange with plum, plum with orange) and matching slippers
    "Neighbour_AbbottA": (NEIGHBOUR_BODY, ["Body_AbbottA", "Hands_T6", "Slippers_Green", "Head_AbbottA", "Hair_Short",
                                           "Tin_Treats"]),
    "Neighbour_AbbottB": (NEIGHBOUR_BODY, ["Body_AbbottB", "Hands_T2", "Slippers_Green", "Head_AbbottB", "Hair_Auburn",
                                           "Apron_Candy"]),
}
