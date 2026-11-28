"""The three trick-or-treat porch houses of Lantern Lane's east lane (docs/living-street.md §4.1 and §7 "Houses",
§3.4 rule 6, §8; docs/living-street-houses-art.md): Mr. Fogg, Nana Pearl and the Abbotts.

Each house is assembled from the porch kit's modules (`porch_kit.py`: clapboard wall, sash window, kit shingles,
corner trim, posts) in the owl house's craft, varied by silhouette, roof, rail rhythm, paint and decor, so the lane
reads as the same street as the heart and every house reads as a different family from the gameplay camera:

  LaneHouseFogg      Mr. Fogg (booming, silly): a tall bottle-green house under a mansard roof with iron cresting,
                     a square entrance tower over his door with a pyramid cap, loud orange-and-plum bunting, a
                     felt spider on a paper web under the door hood and a stack of grinning jack-o'-lanterns.
  LaneHousePearl     Nana Pearl (slow, warm): a low, wide rose cottage whose big roof sweeps down over a long porch,
                     one gabled dormer, a fieldstone chimney, flower boxes, a rocking chair with a knitted blanket and
                     a basket of apples, her big brass porch lantern (it warms while she answers).
  LaneHouseAbbotts   The Abbotts (two dads, a duet): the twin-gable house, a mustard gable and a taller plum gable
                     side by side, a striped awning over the lit bay window with paper ghosts and a cat in it, a
                     paper chain and two matching jack-o'-lanterns.

Door leaves (separate models, hinged by the game): LaneDoorFogg (orange, four raised panels, a silly paper
skeleton pinned on it), LaneDoorPearl (candy yellow, a little curtained window and a wreath of paper maple leaves),
LaneDoorAbbotts (round-topped plum planks with iron straps and two little paper ghosts). Porch lamps (separate
models; the glass is `Lamp_Glow`): LanePorchLampFogg, LanePorchLampPearl (bigger, brass), LanePorchLampAbbotts.

The game's contract (`Assets/SpookyStreet/Core/World/LaneHouseArt.cs`, from `LivingStreetLayout.Doors`): each house
is centred on its placement, front toward -Y (Unity -Z), its door centre at `DOORS[house][0]` across the front, the
front wall's face at `DOORS[house][1]`, the sill at 0.4 m; the visitor's stand is clear of every piece. A leaf's
origin is its hinge edge at the sill, the leaf runs along +X (1.14 x 2.12 m) and its front faces -Y; the game
places that hinge at (door - 0.57, wall - 0.08). A lamp's origin is the middle of its glass, placed by the game at
(door + 0.89, 2.25, wall - 0.12). The doorbell (a push button, never a knocker: knocks are case 01's clue) is part
of the house at (door + 0.75, 1.5) on the wall.

Rules held (§3.4 rule 6, §6 rule 1): no cloth silver, no lavender foil, no hero costume theme, no moon, star, owl,
fox, moth or bell motif, no bats (the keepsakes are paper bats), no lit attic or gable window and no round window
(the owl house's attic window stays the only one and the brightest point); porch pumpkins only.

`lod=1` builds the far mesh of the same house (broad boards and shingle tabs, no bevels, no small dressing); its lit
parts are shared with LOD0 (glow parts are skipped there).

Conventions: metres, Z up, front toward -Y, origin at the ground under the house's placement point.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import env_lib as el
import owl_house as oh
import porch_kit as pk
import props as pr
import street as st
from env_lib import E, H, R, S, T, Builder, lc, shade

HOUSES = ("Fogg", "Pearl", "Abbotts")
MODEL = {h: "LaneHouse" + h for h in HOUSES}
LEAF = {h: "LaneDoor" + h for h in HOUSES}
LAMP = {h: "LanePorchLamp" + h for h in HOUSES}
LOD_SUFFIX = ("_LOD0", "_LOD1")

# (door centre x, front wall face y) in each house's own frame, from LivingStreetLayout.Doors
DOORS = {"Fogg": (0.5, -3.0), "Pearl": (0.0, -1.9), "Abbotts": (-1.85, -3.0)}
# the footprints LivingStreetLayout checks the lane against (EnvironmentLayout.Size of the houses they replace)
FOOTPRINT = {"Fogg": (8.0, 7.7), "Pearl": (10.5, 9.0), "Abbotts": (7.8, 7.7)}
# where the visitor stands to ring, local y (LivingStreetLayout standLocalZ)
STAND = {"Fogg": -4.6, "Pearl": -5.3, "Abbotts": -4.6}

SILL = 0.4
DOOR_W, DOOR_H = 1.14, 2.12  # the leaf (LivingStreetLayout.DoorWidth / DoorHeight)
OPEN_W, OPEN_H = 1.1, 2.1  # the opening behind it
HINGE_IN = 0.08  # the hinge stands this far in front of the wall face
LAMP_AT = (0.89, 2.25, -0.12)  # from the door centre and the wall face
BELL_AT = (0.75, 1.5)
HALL = 0.95  # depth of the lit hall seen through the open door

LOD = [0]  # the level being built (set by the export)

GLOW_WINDOW = (0.98, 0.70, 0.36)


def lod():
    return LOD[0]


# --- paint --------------------------------------------------------------------------------------------------

def paints(craft, body, roof, door, trim=None, accent=None, roof_tints=None):
    """The kit's paint roles (`porch_kit.paints`) in a family's colours."""
    P = dict(pk.paints(craft))
    trim = trim or E["bone_dim"]
    accent = accent or E["purple"]
    P.update({
        "clap": craft.timber(body, "X", wear=0.25),
        "wall_v": craft.timber(shade(body, 0.8), "Z", wear=0.2),
        "back": craft.timber(shade(body, 0.62), "X", wear=0.0),
        "trim_x": craft.timber(trim, "X", wear=0.55, bare=E["timber_dark"]),
        "trim_y": craft.timber(trim, "Y", wear=0.55, bare=E["timber_dark"]),
        "trim_z": craft.timber(trim, "Z", wear=0.55, bare=E["timber_dark"]),
        "door": craft.timber(door, "Z", wear=0.35, bare=E["timber_dark"]),
        "door_x": craft.timber(door, "X", wear=0.35, bare=E["timber_dark"]),
        "shutter": craft.timber(accent, "X", wear=0.4),
        "shutter_z": craft.timber(accent, "Z", wear=0.4),
        "shingle": craft.tinted(roof, 0.85, 0.25, 5.0),
        "roofdeck": craft.timber(shade(roof, 0.55), "X", wear=0.0),
        "stone": craft.tinted(E["stone"], 0.9, 0.3, 4.0),
        "fieldstone": craft.tinted(H(0x8A7C8E), 0.9, 0.35, 5.0),
        "brick": craft.tinted(shade(E["plum"], 0.85), 0.9, 0.3, 9.0),
        "paper": craft.paper_white(), "paint": craft.paint(),
        "felt": craft.tinted((1.0, 1.0, 1.0), 0.95, 0.18, 30.0, sheen=0.35),
    })
    P["_roof_tints"] = roof_tints or pk.ROOF_TINTS
    return P


# --- kit pieces at two levels of detail ------------------------------------------------------------------------

def wall(b, P, width, height, openings=(), base=True, frieze=True):
    if lod():
        st.far_wall(b, P, width, height, openings, base, frieze)
    else:
        pk.wall(b, P, width, height, openings, base, frieze)


def window(b, craft, P, cx, z0, w=0.8, h=1.2, lit=False, shutters=True, room="window", cutouts=()):
    """A sash window in a wall opening (cx - w/2 .. cx + w/2, z0 .. z0 + h). Lit: a warm painted room card over the
    glass (`RoomCard_Glow`, shared by both levels of detail), with paper cut-outs standing in it."""
    if lod():
        st.far_window(b, P, cx, z0, w, h, False, shutters)
    else:
        pk.window(b, P, cx, z0, w, h, shutters)
    if lit and not lod():
        oh.room_card(b, craft, w, h, T(cx, -0.0102, z0), room, flip=1 if cx < 0 else -1)
        for outline, rgb in cutouts:
            b.paper(P["paper"], outline, T(cx, -0.0125, z0 + 0.04), lc(rgb), fold=0.02, curl=0.04, cuts=0)


def shingle_panel(b, P, width, length, pitch, M, keep=None, deck_outline=None, fascia=True, mat=None):
    """A roof plane: deck, fascia and shingles; origin at the eave centre, x along the eave, rising toward +Y.
    `keep(cx, y0, y1, hw)` trims tabs; `deck_outline` (x, y) shapes the deck (default the full rectangle)."""
    Mp = M @ R("X", pitch)
    with b.at(Mp):
        if deck_outline:
            b.prism(P["roofdeck"], [(x, -y) for x, y in deck_outline], 0.07, T(0, 0.0, 0) @ R("X", -90), tint=0.8)
        else:
            b.box(P["roofdeck"], (width, length, 0.07), (0, length / 2, -0.035), bevel=0, tint=0.8)
        if fascia:
            b.box(P["trim_x"], (width + 0.02, 0.05, 0.16), (0, -0.02, -0.06), bevel=0 if lod() else 0.014)
    if lod():
        arc = pk.ARC
        pk.ARC = 2
        try:
            pk.shingles(b, mat or P["shingle"], width, length, Mp, tab=st.TAB, exposure=st.EXPOSURE, thick=0.075, keep=keep,
                        palette=P["_roof_tints"], skirt=0.08)
        finally:
            pk.ARC = arc
    else:
        pk.shingles(b, mat or P["shingle"], width, length, Mp, keep=keep, palette=P["_roof_tints"], skirt=0.06)


def trapezoid_keep(half_bottom, half_top, length, extra=None):
    def keep(cx, y0, y1, hw):
        lim = half_bottom - (half_bottom - half_top) * min(1.0, y1 / length)
        if abs(cx) + hw * 0.5 > lim:
            return False
        return extra(cx, y0, y1, hw) if extra else True
    return keep


def corner(b, P, x, y, rz, height):
    with b.at(T(x, y, SILL) @ R("Z", rz)):
        if lod():
            b.box(P["trim_z"], (0.2, 0.06, height), (0.07, -0.03, height / 2), bevel=0, drop=("+y",))
            b.box(P["trim_z"], (0.06, 0.2, height), (-0.03, 0.07, height / 2), bevel=0, drop=("+x",))
        else:
            pk.corner_trim(b, P, height)


def plinth(b, P, x0, x1, y0, y1):
    b.box(P["stone"], (x1 - x0 + 0.08, y1 - y0 + 0.08, SILL), ((x0 + x1) / 2, (y0 + y1) / 2, SILL / 2), bevel=0, tint=0.72,
          drop=("-z",))


def block(b, craft, P, x0, x1, y0, y1, height, front=(), left=(), right=(), back=(), door=None, corners="flfrblbr"):
    """Four walls on a stone plinth. Windows are dicts for `window` plus "x" (along the wall, from its centre seen from
    outside) and "z" (sill above the floor); `door` is the door's x on the front (its opening; the frame is
    `door_frame`)."""
    plinth(b, P, x0, x1, y0, y1)
    sides = (("front", T((x0 + x1) / 2, y0, SILL), x1 - x0, front, door),
             ("left", T(x0, (y0 + y1) / 2, SILL) @ R("Z", -90), y1 - y0, left, None),
             ("right", T(x1, (y0 + y1) / 2, SILL) @ R("Z", 90), y1 - y0, right, None),
             ("back", T((x0 + x1) / 2, y1, SILL) @ R("Z", 180), x1 - x0, back, None))
    for _name, M, width, wins, dx in sides:
        ops = [(wn["x"] - wn.get("w", 0.8) / 2, wn["x"] + wn.get("w", 0.8) / 2, wn["z"], wn["z"] + wn.get("h", 1.2)) for wn in wins]
        if dx is not None:
            local = dx - (x0 + x1) / 2
            ops.append((local - OPEN_W / 2 - 0.02, local + OPEN_W / 2 + 0.02, 0.0, OPEN_H + 0.02))
        with b.at(M):
            wall(b, P, width, height, ops)
            for wn in wins:
                window(b, craft, P, wn["x"], wn["z"], wn.get("w", 0.8), wn.get("h", 1.2), wn.get("lit", False),
                       wn.get("shutters", True), wn.get("room", "window"), wn.get("cutouts", ()))
    spots = {"fl": (x0, y0, 0), "fr": (x1, y0, 90), "br": (x1, y1, 180), "bl": (x0, y1, 270)}
    for i in range(0, len(corners), 2):
        x, y, rz = spots[corners[i:i + 2]]
        corner(b, P, x, y, rz, height)


def door_frame(b, craft, P, cx, y, depth=0.11, arch=False, hall="hall"):
    """The door's casing standing proud of the wall, the threshold, the bell's push button and, behind the
    opening, a lit hall (a closed view box and a painted room card: `RoomCard_Glow`)."""
    hw = OPEN_W / 2
    bev = 0 if lod() else 0.016
    with b.at(T(cx, y, SILL)):
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.13, depth, OPEN_H + 0.12), (sx * (hw + 0.075), -depth / 2 + 0.01, (OPEN_H + 0.12) / 2), bevel=bev)
            b.box(P["trim_z"], (0.06, 0.12, OPEN_H), (sx * (hw + 0.01), 0.06, OPEN_H / 2), bevel=0, drop=("+y",))  # the jamb
        b.box(P["trim_x"], (OPEN_W + 0.44, depth + 0.02, 0.12), (0, -depth / 2, OPEN_H + 0.12), bevel=bev)
        b.box(P["trim_x"], (OPEN_W + 0.52, depth + 0.06, 0.05), (0, -depth / 2 - 0.01, OPEN_H + 0.2), bevel=bev * 0.7, tint=1.06)
        b.box(P["trim_x"], (OPEN_W + 0.16, 0.24, 0.05), (0, -0.04, -0.02), bevel=bev * 0.6, tint=0.9)  # threshold
        if arch:  # round-topped door: two quarter spandrels fill the opening's top corners, in the casing's depth
            r = hw + 0.02
            spring = DOOR_H - r
            for sx in (-1, 1):
                # the corner outside the arc: down the jamb to the spring, round the arc to the top, back along the head
                arc_pts = [(sx * r * math.cos(math.radians(a)), spring + r * math.sin(math.radians(a))) for a in range(0, 91, 15)]
                pts = [(sx * (r + 0.01), spring)] + arc_pts[1:] + [(0.0, OPEN_H + 0.04), (sx * (r + 0.01), OPEN_H + 0.04)]
                if sx > 0:
                    pts = pts[::-1]
                b.prism(P["trim_x"], pts, depth * 0.8, T(0, -depth + 0.02, 0), tint=1.02)
        # the bell: a round orange plate with a bone rim and a bone push button, a candy top (as Pip's porch hook)
        bx, bz = BELL_AT[0], BELL_AT[1] - SILL
        plate = -0.035
        b.disc(P["orange"], 0.065, 0.02, T(bx, plate, bz), segs=16, tint=1.0, bevel=0 if lod() else 0.004)
        if not lod():
            b.ring(P["paint"], 0.054, 0.067, 0.004, T(bx, plate - 0.002, bz), segs=16, tint=lc(E["bone"]))
            with b.at(T(bx, plate, bz) @ R("X", 90)):
                b.lathe(P["paint"], [(0.0, -0.002), (0.03, -0.002), (0.03, 0.007), (0.024, 0.013), (0.0, 0.014)], 12, tint=lc(E["bone"]))
                b.lathe(P["paint"], [(0.018, 0.012), (0.019, 0.019), (0.012, 0.024), (0.0, 0.025)], 10, tint=lc(E["candy"]))
        if not lod():
            oh.view_box(b, craft, -hw - 0.02, hw + 0.02, 0.12, HALL, 0.0, OPEN_H + 0.02)
            oh.room_card(b, craft, OPEN_W + 0.04, OPEN_H + 0.02, T(0, HALL, 0), hall)


def steps(b, P, cx, y, width, top=SILL, tread=0.34, rise=0.2, cheeks=True):
    """Plank steps down from a deck or landing at `top` whose front edge is at `y`."""
    n = round(top / rise)
    bev = 0 if lod() else 0.01
    for i in range(1, n):
        z = top - rise * i
        y0 = y - tread * (i - 1)
        b.box(P["deck_x"], (width, tread + 0.03, 0.05), (cx, y0 - tread / 2, z - 0.025), bevel=bev, tint=b.vary(0.9, 1.08))
        b.box(P["wall_v"], (width - 0.08, 0.03, rise), (cx, y0 + 0.0, z - rise / 2 + rise), bevel=0, tint=0.8, drop=("+y",))
        b.box(P["wall_v"], (width - 0.08, 0.03, z), (cx, y0 - tread + 0.02, z / 2), bevel=0, tint=0.8, drop=("+y", "-z"))
    if cheeks:
        run = tread * (n - 1)
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.12, 0.12, top + 0.18), (cx + sx * (width / 2 + 0.06), y - run + 0.06, (top + 0.18) / 2), bevel=bev * 1.5)
            if not lod():
                b.lathe(P["trim_z"], [(0.0, 0.0), (0.07, 0.0), (0.08, 0.05), (0.045, 0.1), (0.0, 0.12)], 10,
                        T(cx + sx * (width / 2 + 0.06), y - run + 0.06, top + 0.18))


def landing(b, P, cx, y, width, depth):
    """A plank landing at the sill in front of a door (front edge at y - depth)."""
    n = max(2, round(width / 0.2))
    w = width / n
    for i in range(n):
        x = cx - width / 2 + (i + 0.5) * w
        b.box(P["deck"], (w - 0.008, depth, 0.05), (x, y - depth / 2, SILL - 0.025), bevel=0 if lod() else 0.008, drop=("-z",))
    b.box(P["trim_x"], (width + 0.04, 0.06, 0.12), (cx, y - depth + 0.02, SILL - 0.1), bevel=0 if lod() else 0.012)
    b.box(P["wall_v"], (width, depth, SILL - 0.1), (cx, y - depth / 2, (SILL - 0.1) / 2), bevel=0, tint=0.7, drop=("-z", "+y"))


def jack(b, craft, x, y, z, r, style=0, rz=0.0, rgb=None):
    if lod():
        if r > 0.25:
            b.lathe(craft.tinted(rgb or E["orange"], 0.6, 0.2, 6.0), [(0.0, 0.0), (r * 0.8, r * 0.1), (r, r * 0.7), (r * 0.7, r * 1.35), (0.0, r * 1.4)],
                    8, T(x, y, z))
        return
    pr.pumpkin(b, craft, r, 0.8, T(x, y, z) @ R("Z", rz), style=style, rgb=rgb, lobes=8, per_lobe=2, glow=1.6)


def bunting(b, craft, p0, p1, sag, count, start=0, w=0.2, h=0.27):
    if lod():
        return
    pr.bunting(b, craft, p0, p1, sag, count, w, h, 12, start)


def chimney(b, P, x, y, z0, z1, size=0.62, lean=2.0, pots=2, stone=False):
    if lod():
        b.box(P["brick"] if not stone else P["fieldstone"], (size, size, z1 - z0), (x, y, (z0 + z1) / 2), bevel=0, rot=R("Y", lean), tint=0.9)
        b.box(P["trim_x"], (size + 0.16, size + 0.16, 0.12), (x, y, z1 + 0.04), bevel=0)
        return
    st.chimney(b, dict(P, brick=P["fieldstone"]) if stone else P, x, y, z0, z1, size, lean, pots, course=0.26 if not stone else 0.22)


def cutout(kind, h):
    return st.cat_cutout(h) if kind == "cat" else st.ghost_cutout(h)


# --- Mr. Fogg: the tower house with the mansard ------------------------------------------------------------------

FOGG_GREEN = H(0x3F7255)
FOGG_ROOF = H(0x3B3462)
FOGG_DOOR = H(0xD9642A)


def build_fogg(craft):
    b = Builder(71 + 10 * lod())
    P = paints(craft, FOGG_GREEN, FOGG_ROOF, FOGG_DOOR, accent=H(0x7A3E6E), trim=E["bone_dim"],
               roof_tints=[(1, 1, 1)] * 5 + [(1.2, 0.9, 1.1), (0.8, 0.85, 1.2), (0.85, 0.8, 0.9)])
    P["brick"] = craft.tinted(shade(E["plum"], 0.85), 0.9, 0.3, 9.0)
    dx, wy = DOORS["Fogg"]
    x0, x1, y0, y1 = -3.25, 3.35, -2.75, 3.15
    s1, s2 = 2.75, 2.45  # storeys
    eave = SILL + s1 + s2
    tx0, tx1, ty1 = dx - 0.95, dx + 0.95, -1.3  # the tower (front at the door wall, wy)
    # main block: the tower covers the middle of its front
    lit_win = dict(lit=True, room="window")
    front = [dict(x=-2.15 - (x0 + x1) / 2, z=0.7, w=0.85, h=1.35, cutouts=((cutout("ghost", 0.62), E["ink"]),), **lit_win),
             dict(x=2.55 - (x0 + x1) / 2, z=0.7, w=0.85, h=1.35),
             dict(x=-2.15 - (x0 + x1) / 2, z=s1 + 0.45, w=0.8, h=1.25, shutters=False),
             dict(x=2.55 - (x0 + x1) / 2, z=s1 + 0.45, w=0.8, h=1.25, shutters=False, lit=True, room="window")]
    side = [dict(x=-1.0, z=0.7, w=0.8, h=1.3), dict(x=1.3, z=s1 + 0.45, w=0.8, h=1.25, shutters=False)]
    block(b, craft, P, x0, x1, y0, y1, s1 + s2, front=front, left=side, right=side, back=())
    for z in (SILL + s1,):  # belt course between the storeys
        b.box(P["trim_x"], (x1 - x0 + 0.12, 0.08, 0.14), ((x0 + x1) / 2, y0 - 0.04, z), bevel=0 if lod() else 0.014)
    b.box(P["trim_x"], (x1 - x0 + 0.3, y1 - y0 + 0.3, 0.16), ((x0 + x1) / 2, (y0 + y1) / 2, eave + 0.05), bevel=0 if lod() else 0.02)
    # the mansard: steep lower slopes with round-shouldered dormers, a low top with iron cresting
    over, rise, inset = 0.15, 1.9, 0.62
    pitch = math.degrees(math.atan2(rise, inset + over))
    slope = math.hypot(rise, inset + over)
    top = eave + 0.1 + rise
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 + over, (y1 - y0) / 2 + over
    dormers = (-2.15, 2.55)

    def front_keep(cx_, y0_, y1_, hw):
        x = cx_ + cx
        if tx0 - 0.15 < x + hw * 0.5 and x - hw * 0.5 < tx1 + 0.15:
            return False  # behind the tower
        return not any(abs(x - d) < 0.55 + hw * 0.5 and y0_ < 1.75 for d in dormers)
    for rz, half, dist, extra in ((0, hx, hy, front_keep), (180, hx, hy, None), (-90, hy, hx, None), (90, hy, hx, None)):
        M = T(cx, cy, eave + 0.1) @ R("Z", rz) @ T(0, -dist, 0)
        tophalf = half - inset - over
        keep = trapezoid_keep(half, tophalf, slope, extra)
        outline = [(-half, 0.0), (half, 0.0), (tophalf, slope), (-tophalf, slope)]
        shingle_panel(b, P, 2 * half, slope, pitch, M, keep=keep, deck_outline=outline, fascia=False)
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):  # hip rolls
        a = Vector((cx + sx * hx, cy + sy * hy, eave + 0.1))
        c = Vector((cx + sx * (hx - inset - over), cy + sy * (hy - inset - over), top))
        b.rod(P["trim_x"], a, c, 0.06, 6, 1.0)
    tw, td = 2 * (hx - inset - over), 2 * (hy - inset - over)
    b.box(P["roofdeck"], (tw + 0.1, td + 0.1, 0.18), (cx, cy, top + 0.02), bevel=0, tint=0.7)
    b.box(P["trim_x"], (tw + 0.2, td + 0.2, 0.08), (cx, cy, top + 0.12), bevel=0 if lod() else 0.012)
    ink = P["paint"]
    if not lod():  # iron cresting along the front and back of the top
        for sy in (-1, 1):
            yy = cy + sy * (td / 2 + 0.05)
            b.rod(ink, (cx - tw / 2, yy, top + 0.18), (cx + tw / 2, yy, top + 0.18), 0.012, 5, lc(E["ink"]))
            n = int(tw / 0.22)
            for i in range(n + 1):
                x = cx - tw / 2 + i * tw / n
                b.rod(ink, (x, yy, top + 0.15), (x, yy, top + (0.42 if i % 2 else 0.3)), 0.01, 4, lc(E["ink"]))
                if i % 2:
                    b.lathe(ink, [(0.0, 0.0), (0.03, 0.02), (0.0, 0.08)], 6, T(x, yy, top + 0.42), tint=lc(E["ink"]))
    for d in dormers:  # round-shouldered dormers (dark glass: never a lit attic window)
        with b.at(T(d, y0 - over + 0.25, eave + 0.32)):
            dw, dh = 1.1, 1.25
            b.box(P["clap"], (dw, 0.6, dh), (0, 0.3, dh / 2), bevel=0, tint=0.9, drop=("+y",))
            arch = [(-dw / 2, 0.0), (dw / 2, 0.0)] + el.arc(0.0, dh, dw / 2, 0, 180, 6 if lod() else 10)
            b.prism(P["shingle"], [(x * 1.12, z * 1.04 + 0.02) for x, z in arch], 0.75, T(0, -0.08, 0), tint=(0.9, 0.9, 1.05))
            b.prism(P["trim_x"], [(x * 0.82, z * 0.9 + 0.08) for x, z in el.arch_outline(0.66, 0.62, 6 if lod() else 10)], 0.03, T(0, -0.095, 0.05))
            b.prism(P["glass"], [(x * 0.66, z * 0.82 + 0.13) for x, z in el.arch_outline(0.66, 0.62, 6 if lod() else 10)], 0.02, T(0, -0.11, 0.05), tint=1.0)
            b.box(P["trim_z"], (0.035, 0.03, 0.75), (0, -0.12, 0.55), bevel=0)
            b.box(P["trim_x"], (0.75, 0.16, 0.06), (0, -0.14, 0.1), bevel=0 if lod() else 0.012)
    # the tower: from the ground to above the mansard, the door in its base
    th = top + 0.55
    plinth(b, P, tx0, tx1, wy, ty1)
    with b.at(T(dx, wy, SILL)):
        wall(b, P, tx1 - tx0, th - SILL, ((-OPEN_W / 2 - 0.02, OPEN_W / 2 + 0.02, 0.0, OPEN_H + 0.02), (-0.36, 0.36, s1 + 0.4, s1 + 1.55),
                                          (-0.3, 0.3, th - SILL - 1.3, th - SILL - 0.45)))
        window(b, craft, P, 0.0, s1 + 0.4, 0.72, 1.15, lit=False, shutters=False)
        window(b, craft, P, 0.0, th - SILL - 1.3, 0.6, 0.85, lit=False, shutters=False)
    for sx in (-1, 1):
        with b.at(T(dx + sx * (tx1 - tx0) / 2, (wy + y0) / 2, SILL) @ R("Z", sx * 90)):
            wall(b, P, y0 - wy, th - SILL, base=True)
        with b.at(T(dx + sx * (tx1 - tx0) / 2, (y0 + ty1) / 2, eave) @ R("Z", sx * 90)):
            wall(b, P, ty1 - y0, th - eave, base=False)
        corner(b, P, dx + sx * (tx1 - tx0) / 2, wy, 0 if sx < 0 else 90, th - SILL)
    with b.at(T(dx, ty1, eave) @ R("Z", 180)):
        wall(b, P, tx1 - tx0, th - eave, base=False)
    for z in (SILL + s1, eave + 0.05):
        b.box(P["trim_x"], (tx1 - tx0 + 0.16, 0.1, 0.14), (dx, wy - 0.05, z), bevel=0 if lod() else 0.014)
    # pyramid cap with flared eaves, an orange band and a finial
    cap_over, cap_pitch = 0.28, 52.0
    chalf = (tx1 - tx0) / 2 + cap_over
    crise = chalf * math.tan(math.radians(cap_pitch))
    clen = chalf / math.cos(math.radians(cap_pitch))
    b.box(P["trim_x"], (tx1 - tx0 + 0.24, (ty1 - wy) + 0.24, 0.16), (dx, (wy + ty1) / 2, th + 0.06), bevel=0 if lod() else 0.016)
    b.box(P["orange"], (tx1 - tx0 + 0.18, (ty1 - wy) + 0.18, 0.06), (dx, (wy + ty1) / 2, th - 0.06), bevel=0, tint=1.0)
    tcx, tcy = dx, (wy + ty1) / 2
    cd = (ty1 - wy) / 2 + cap_over
    for rz in (0, 90, 180, -90):
        half = chalf if rz in (0, 180) else cd
        dist = cd if rz in (0, 180) else chalf
        length = dist / math.cos(math.radians(cap_pitch))
        tip = max(0.0, half - dist)
        M = T(tcx, tcy, th + 0.14) @ R("Z", rz) @ T(0, -dist, 0)
        shingle_panel(b, P, 2 * half, length, cap_pitch, M, keep=trapezoid_keep(half, tip, length),
                      deck_outline=[(-half, 0.0), (half, 0.0), (tip, length), (-tip, length)], fascia=False)
    apex = th + 0.14 + min(chalf, cd) * math.tan(math.radians(cap_pitch))
    b.lathe(P["paint"], [(0.0, -0.1), (0.09, -0.05), (0.06, 0.2), (0.12, 0.34), (0.07, 0.5), (0.025, 0.55), (0.025, 0.85), (0.0, 0.9)],
            6 if lod() else 10, T(tcx, tcy, apex), tint=lc(E["orange"]))
    # door, its hood on fretwork brackets, the landing and steps
    door_frame(b, craft, P, dx, wy)
    hood = [(-1.0, 0.0), (1.0, 0.0), (0.0, 0.62)]
    hz = SILL + OPEN_H + 0.32
    b.prism(P["shingle"], hood, 0.82, T(dx, wy - 0.82, hz), tint=(1.1, 0.95, 1.15))
    b.prism(P["trim_x"], [(-1.08, -0.12), (1.08, -0.12), (0.0, 0.66)], 0.07, T(dx, wy - 0.87, hz), tint=1.0)
    b.prism(P["orange"], [(-0.5, 0.06), (0.5, 0.06), (0.0, 0.36)], 0.02, T(dx, wy - 0.885, hz), tint=1.0)
    for sx in (-1, 1):
        r = 0.55
        br = [(0.0, 0.0), (r, 0.0), (r, -0.07)] + el.arc(r, -r, r - 0.07, 90, 180, 4 if lod() else 6)[1:] + [(0.0, -r)]
        with b.at(T(dx + sx * 0.86, wy - 0.0, hz - 0.02) @ R("Z", -90) @ S(1, 1, 1)):
            b.prism(P["trim_x"], br, 0.07, T(0, -0.035, 0), bevel=0 if lod() else 0.012)
    landing(b, P, dx, wy, 1.9, 0.5)
    steps(b, P, dx, wy - 0.5, 1.9)
    chimney(b, P, x1 - 0.7, y1 - 1.0, top - 0.4, top + 1.6, size=0.66, lean=-1.5)
    # loud dressing: bunting under the belt course, three jack-o'-lanterns stacked by the steps, the spider
    bunting(b, craft, (x0 + 0.1, y0 - 0.1, SILL + s1 - 0.05), (tx0 - 0.08, y0 - 0.1, SILL + s1 - 0.05), 0.3, 6)
    bunting(b, craft, (tx1 + 0.08, y0 - 0.1, SILL + s1 - 0.05), (x1 - 0.1, y0 - 0.1, SILL + s1 - 0.05), 0.3, 5, start=2)
    jack(b, craft, dx + 1.45, wy - 0.62, 0.0, 0.36, 0, -14)
    jack(b, craft, dx + 1.45, wy - 0.58, 0.47, 0.24, 2, 10, H(0xF59A23))
    jack(b, craft, dx - 1.5, wy - 0.95, 0.0, 0.27, 1, 20)
    jack(b, craft, dx + 2.05, wy - 0.8, 0.0, 0.2, None, 40, H(0xF0E3C4))
    if not lod():
        spider(b, craft, P, dx - 0.82, wy - 0.62, hz - 0.1, 0.55)
    finish(b, "Fogg")


def spider(b, craft, P, x, y, z_top, drop):
    """A round felt spider on a silk thread under the hood, with a paper web in the hood's corner."""
    felt = P["felt"]
    ink = lc(E["ink"])
    c = Vector((x, y, z_top - drop))
    b.rod(P["paint"], (x, y, z_top), c + Vector((0, 0, 0.06)), 0.003, 4, lc(E["bone"]))
    b.lathe(felt, [(0.0, -0.07), (0.055, -0.05), (0.07, 0.0), (0.05, 0.05), (0.0, 0.065)], 10, T(*c), tint=ink)
    b.lathe(felt, [(0.0, -0.035), (0.035, -0.02), (0.035, 0.015), (0.0, 0.035)], 8, T(c.x, c.y - 0.075, c.z - 0.02), tint=ink)
    for s in (-1, 1):
        b.disc(P["paint"], 0.014, 0.004, T(c.x + s * 0.016, c.y - 0.108, c.z - 0.01) @ R("X", 0), segs=8, tint=lc(E["bone"]))
        for k in range(4):
            a = math.radians(-50 + k * 33)
            knee = c + Vector((s * 0.11, math.sin(a) * 0.07, 0.06 - 0.01 * k))
            foot = c + Vector((s * 0.17, math.sin(a) * 0.11, -0.06))
            b.rod(P["paint"], c + Vector((s * 0.04, math.sin(a) * 0.02, 0.0)), knee, 0.006, 4, ink)
            b.rod(P["paint"], knee, foot, 0.005, 4, ink)
    # web: radial threads and two rings of bone paint in the hood's corner
    hub = Vector((x - 0.05, y + 0.02, z_top - 0.05))
    ends = []
    for k in range(5):  # spokes fanning down and out from the hood's corner
        a = math.radians(22.5 * k)
        ends.append(hub + Vector((-math.sin(a) * 0.42, 0.0, -math.cos(a) * 0.42)))
        b.rod(P["paint"], hub, ends[-1], 0.003, 3, lc(E["bone"]))
    for f in (0.45, 0.8):  # two rings, sagging a little between the spokes
        for p0, p1 in zip(ends, ends[1:]):
            a0, a1 = hub.lerp(p0, f), hub.lerp(p1, f)
            mid = (a0 + a1) / 2 + (hub - (a0 + a1) / 2) * 0.12
            b.rod(P["paint"], a0, mid, 0.0025, 3, lc(E["bone"]))
            b.rod(P["paint"], mid, a1, 0.0025, 3, lc(E["bone"]))


# --- Nana Pearl: the cottage under one sweeping roof ---------------------------------------------------------------

PEARL_ROSE = H(0x9A5A7E)
PEARL_ROOF = H(0x4E3170)
PEARL_DOOR = H(0xE6BE5A)


def build_pearl(craft):
    b = Builder(72 + 10 * lod())
    P = paints(craft, PEARL_ROSE, PEARL_ROOF, PEARL_DOOR, accent=H(0x5B3A7A), trim=E["bone_dim"])
    dx, wy = DOORS["Pearl"]
    x0, x1, y0, y1 = -4.55, 4.55, wy, 3.75
    wall_h = 2.8
    eave = SILL + wall_h
    lit = dict(lit=True)
    front = [dict(x=-2.55, z=0.65, w=1.0, h=1.3, cutouts=((cutout("cat", 0.55), E["ink"]),), **lit),
             dict(x=2.55, z=0.65, w=1.0, h=1.3, **lit)]
    sidew = [dict(x=0.0, z=0.7, w=0.8, h=1.2)]
    block(b, craft, P, x0, x1, y0, y1, wall_h, front=front, left=sidew, right=sidew, door=dx)
    # gables at both ends (dark little attic windows), the main roof with the porch's cat-slide
    pitch = 40.0
    ridge_y = (y0 + y1) / 2
    hd = (y1 - y0) / 2
    rise = hd * math.tan(math.radians(pitch))
    for sx in (-1, 1):
        with b.at(T(sx * x1, ridge_y, eave) @ R("Z", sx * 90)):
            tri = [(-hd, 0.0), (hd, 0.0), (0.0, rise)]
            b.prism(P["back"], tri, 0.1, T(0, 0.02, 0), tint=0.85)
            if lod():
                st.far_gable(b, P, tri)
            else:
                for i in range(int(rise / 0.2)):
                    z = (i + 0.5) * 0.2
                    half = hd * (1 - z / rise) - 0.02
                    if half > 0.15:
                        b.box(P["clap"], (2 * half, 0.035, 0.214), (0, 0.005, z), drop=("+y", "+z"), rot=R("X", -6), tint=b.vary(0.8, 1.12))
            b.box(P["trim_x"], (2 * hd + 0.1, 0.07, 0.14), (0, -0.03, 0.02), bevel=0 if lod() else 0.014)
            window(b, craft, P, 0.0, 0.45, 0.55, 0.8, lit=False, shutters=False)
    over = 0.4
    length = (hd + over) / math.cos(math.radians(pitch))
    ridge = eave + rise
    shingle_panel(b, P, 2 * x1 + 2 * over, length, pitch, T(0, y1 + over, eave - over * math.tan(math.radians(pitch))) @ R("Z", 180))
    # front: from the ridge down to the wall line, then on over the porch at a gentler pitch
    porch_d, pp = 1.95, 17.0
    dorm_x, dorm_w = 0.0, 1.5

    def keep_front(cx, y0_, y1_, hw):
        return not (abs(cx - dorm_x) < dorm_w / 2 + hw * 0.5 + 0.05 and y0_ < 2.6 and y1_ > 0.35)
    shingle_panel(b, P, 2 * x1 + 2 * over, hd / math.cos(math.radians(pitch)) + 0.02, pitch, T(0, y0, eave), keep=keep_front, fascia=False)
    plen = (porch_d + 0.3) / math.cos(math.radians(pp))
    peave = eave - (porch_d + 0.3) * math.tan(math.radians(pp))
    shingle_panel(b, P, 2 * x1 + 2 * over, plen + 0.05, pp, T(0, y0 - porch_d - 0.3, peave))
    b.cyl(P["trim_y"], 0.1, 2 * x1 + 2 * over + 0.08, T(0, ridge_y, ridge + 0.03) @ R("Y", 90), segments=6 if lod() else 8)
    for sx in (-1, 1):  # bargeboards on both gables, and the porch roof's ends
        for sy in (-1, 1):
            b.box(P["trim_x"], (0.07, length + 0.08, 0.2), (sx * (x1 + over), ridge_y + sy * (hd + over) / 2, eave + rise / 2 - 0.05),
                  bevel=0 if lod() else 0.014, rot=R("X", -sy * pitch))
        b.prism(P["wall_v"], [(0.0, 0.0), (porch_d + 0.3, 0.0), (0.0, (porch_d + 0.3) * math.tan(math.radians(pp)))], 0.08,
                T(sx * (x1 + 0.02) - (0.08 if sx > 0 else 0.0), y0, peave + 0.02) @ R("Z", -90) @ S(1, 1, 1), tint=0.85)
    # the dormer: a little gable on the front slope, its window dark (never a lit attic window)
    tan = math.tan(math.radians(pitch))
    with b.at(T(dorm_x, y0 + 0.55, eave + 0.45)):
        dh = 1.3
        wall(b, P, dorm_w, dh, ((-0.38, 0.38, 0.22, 1.12),), base=False, frieze=False)
        window(b, craft, P, 0.0, 0.22, 0.76, 0.9, lit=False, shutters=False)
        for sx in (-1, 1):
            b.box(P["clap"], (0.1, 1.6, dh), (sx * (dorm_w / 2 - 0.05), 0.8, dh / 2), bevel=0, tint=0.85)
        dr = 0.85
        b.prism(P["clap"], [(-dorm_w / 2, 0.0), (dorm_w / 2, 0.0), (0.0, dr * 0.9)], 0.08, T(0, -0.02, dh), tint=0.95)
        b.prism(P["shingle"], [(-dorm_w / 2 - 0.22, -0.06), (dorm_w / 2 + 0.22, -0.06), (0.0, dr)], 2.2, T(0, -0.2, dh + 0.04), tint=(1.05, 1.0, 1.1))
        b.prism(P["trim_x"], [(-dorm_w / 2 - 0.28, -0.1), (dorm_w / 2 + 0.28, -0.1), (0.0, dr + 0.06)], 0.06, T(0, -0.27, dh + 0.02))
    chimney(b, P, x1 + 0.34, ridge_y + 0.6, 0.0, ridge + 1.1, size=0.85, lean=1.0, pots=1, stone=True)
    # the porch: deck, posts with brackets, picket rails either side of the steps
    pz = SILL
    py = y0 - porch_d
    n = round((2 * x1 - 0.2) / 0.2)
    w = (2 * x1 - 0.2) / n
    for i in range(n):
        x = -x1 + 0.1 + (i + 0.5) * w
        b.box(P["deck"], (w - 0.008, porch_d, 0.05), (x, y0 - porch_d / 2, pz - 0.025), bevel=0 if lod() else 0.008, drop=("-z",),
              tint=b.vary(0.88, 1.1))
    b.box(P["trim_x"], (2 * x1, 0.06, 0.14), (0, py + 0.02, pz - 0.1), bevel=0 if lod() else 0.012)
    b.box(P["wall_v"], (2 * x1 - 0.2, porch_d, pz - 0.1), (0, y0 - porch_d / 2, (pz - 0.1) / 2), bevel=0, tint=0.7, drop=("-z", "+y"))
    for i in range(int((2 * x1) / 0.18)):  # slatted skirt
        x = -x1 + 0.09 + i * 0.18
        if abs(x) > 0.95:
            b.box(P["wall_v"], (0.1, 0.025, pz - 0.15), (x, py + 0.01, (pz - 0.15) / 2 + 0.02), bevel=0, drop=("+y", "-z", "+z"))
    post_x = (-4.25, -1.3, 1.3, 4.25)
    post_h = peave + (porch_d + 0.3 - 0.18) * math.tan(math.radians(pp)) - pz - 0.08
    post_h = peave - pz + 0.04
    for x in post_x:
        with b.at(T(x, py + 0.14, pz)):
            if lod():
                b.box(P["trim_z"], (0.16, 0.16, post_h), (0, 0, post_h / 2), bevel=0)
            else:
                pk.post(b, P, post_h, (-1, 1) if abs(x) < 4 else ((1,) if x < 0 else (-1,)))
    b.box(P["trim_x"], (2 * x1 + 0.1, 0.16, 0.16), (0, py + 0.14, pz + post_h + 0.02), bevel=0 if lod() else 0.016)
    for sx in (-1, 1):
        picket_rail(b, P, sx * (1.3 + 4.25) / 2, py + 0.14, 4.25 - 1.3 - 0.2, pz)
    steps(b, P, 0.0, py, 2.2)
    # a big brass porch lantern hangs from the porch beam too (decor), the door frame and the hall
    door_frame(b, craft, P, dx, wy)
    # flower boxes, a rocking chair with a knitted blanket, a basket of apples, two little pumpkins, a mat
    for x in (-2.55, 2.55):
        flower_box(b, craft, P, x, y0, SILL + 0.62, 1.15)
    rocking_chair(b, craft, P, 2.6, y0 - 1.05, pz, 200)
    apples(b, craft, P, 1.55, y0 - 0.6, pz)
    jack(b, craft, -1.0, y0 - 1.55, pz, 0.27, 1, 18)
    jack(b, craft, -1.55, y0 - 1.5, pz, 0.18, None, -20, H(0xF0E3C4))
    jack(b, craft, 1.45, py - 0.45, 0.0, 0.3, 0, -10)
    if not lod():
        mat = craft.striped_cloth(H(0x7A3E6E), H(0xE6BE5A), 0.12, "X")
        b.box(mat, (0.95, 0.6, 0.02), (dx, wy - 0.42, pz + 0.01), bevel=0, drop=("-z",), tint=1.0)
    finish(b, "Pearl")


def picket_rail(b, P, cx, y, length, base):
    b.box(P["trim_x"], (length, 0.07, 0.06), (cx, y, base + 0.14), bevel=0 if lod() else 0.012)
    b.box(P["trim_x"], (length + 0.02, 0.12, 0.05), (cx, y, base + 0.8), bevel=0 if lod() else 0.014, tint=1.05)
    if lod():
        b.box(P["trim_z"], (length, 0.03, 0.6), (cx, y, base + 0.47), bevel=0, tint=0.85, drop=("+y",))
        return
    n = round(length / 0.16)
    for i in range(n):
        x = cx - length / 2 + (i + 0.5) * length / n
        pts = [(-0.04, 0.0), (0.04, 0.0), (0.04, 0.56), (0.0, 0.63), (-0.04, 0.56)]
        b.prism(P["trim_z"], pts, 0.025, T(x, y - 0.0125, base + 0.17), tint=b.vary(0.88, 1.05))


def flower_box(b, craft, P, x, wy, z, w):
    b.box(P["shutter"], (w, 0.24, 0.2), (x, wy - 0.16, z - 0.1), bevel=0 if lod() else 0.014)
    for sx in (-1, 1):
        b.prism(P["trim_x"], [(0.0, 0.0), (0.2, 0.0), (0.0, -0.18)], 0.04, T(x + sx * (w / 2 - 0.12), wy - 0.04, z - 0.2) @ R("Z", -90))
    if lod():
        b.box(craft.tinted(H(0x3D6B40), 0.9, 0.3), (w - 0.1, 0.18, 0.12), (x, wy - 0.16, z + 0.04), bevel=0, drop=("-z",))
        return
    felt = P["felt"]
    rng = b.rng
    for i in range(9):
        fx = x - w / 2 + 0.1 + i * (w - 0.2) / 8
        fy = wy - 0.16 + rng.uniform(-0.05, 0.05)
        top = z + rng.uniform(0.1, 0.2)
        b.rod(P["paint"], (fx, fy, z - 0.02), (fx, fy, top), 0.008, 4, lc(H(0x4E7A3A)))
        col = (E["orange"], H(0xA83A5C), E["candy"], H(0x8A5AA8))[i % 4]
        with b.at(T(fx, fy - 0.02, top) @ R("X", rng.uniform(55, 80))):
            for k in range(5):
                a = 2 * math.pi * k / 5
                b.lathe(felt, [(0.0, 0.0), (0.025, 0.004), (0.0, 0.008)], 5, T(math.cos(a) * 0.028, math.sin(a) * 0.028, 0), tint=lc(col))
            b.lathe(felt, [(0.0, 0.0), (0.016, 0.006), (0.0, 0.014)], 6, T(0, 0, 0.004), tint=lc(E["candy"] if i % 4 else E["ink"]))
        for k in range(2):
            pr.leaf(b, craft, T(fx + rng.uniform(-0.04, 0.04), fy - 0.06, z + 0.02) @ R("Z", rng.uniform(-40, 40)) @ R("X", 70), 0.07,
                    H(0x4E7A3A), low=True)


def rocking_chair(b, craft, P, x, y, z, yaw):
    wood = craft.timber(H(0x6E4A6A), "Z", wear=0.4, bare=E["timber_dark"])
    wood_x = craft.timber(H(0x6E4A6A), "X", wear=0.4, bare=E["timber_dark"])
    with b.at(T(x, y, z) @ R("Z", yaw)):
        for sx in (-1, 1):
            rocker = [(-0.42 + 0.84 * i / 8, 0.05 * ((i - 4) / 4) ** 2 * 2) for i in range(9)]
            pts = rocker + [(0.42, 0.11), ] + [(-0.42 + 0.84 * i / 8, 0.05 + 0.05 * ((i - 4) / 4) ** 2 * 2) for i in range(8, -1, -1)][1:]
            b.prism(wood, pts, 0.04, T(sx * 0.24, 0.0, 0) @ R("Z", 90), tint=1.0)
            for yy, h in ((-0.2, 0.5), (0.2, 1.05)):
                b.box(wood, (0.05, 0.05, h), (sx * 0.24, yy, 0.08 + h / 2), bevel=0 if lod() else 0.01)
            b.box(wood_x, (0.05, 0.46, 0.04), (sx * 0.24, 0.0, 0.66), bevel=0 if lod() else 0.008)
        b.box(wood_x, (0.52, 0.46, 0.05), (0, 0.0, 0.46), bevel=0 if lod() else 0.01)
        for k in range(4):
            b.box(wood, (0.05, 0.02, 0.5), (-0.18 + 0.12 * k, 0.21, 0.82), bevel=0, rot=R("X", -8))
        b.box(wood_x, (0.56, 0.05, 0.06), (0, 0.24, 1.1), bevel=0 if lod() else 0.01)
        if not lod():  # the knitted blanket over the seat and the back
            knit = craft.striped_cloth(H(0xEE7F2D), H(0xF4E6C8), 0.1, "Z")
            b.box(knit, (0.5, 0.03, 0.42), (0, 0.17, 0.74), bevel=0.01, rot=R("X", -8), tint=1.0)
            b.box(knit, (0.5, 0.34, 0.03), (0, 0.0, 0.5), bevel=0.01, tint=1.0)
            b.box(knit, (0.5, 0.03, 0.22), (0, -0.2, 0.4), bevel=0.01, rot=R("X", 12), tint=1.0)


def apples(b, craft, P, x, y, z):
    wick = craft.timber(H(0xB98A5A), "X", wear=0.1, bare=H(0x8E6440))
    b.lathe(wick, [(0.0, 0.0), (0.16, 0.0), (0.2, 0.18), (0.21, 0.2), (0.0, 0.2)], 8 if lod() else 14, T(x, y, z))
    if lod():
        return
    b.ring(P["paint"], 0.17, 0.19, 0.02, T(x, y + 0.01, z + 0.2) @ R("X", 0), a0=0, a1=180, segs=10, tint=lc(H(0x8E6440)))
    red = craft.tinted(H(0xB8352E), 0.45, 0.15, 6.0)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.lathe(red, [(0.0, 0.0), (0.06, 0.015), (0.065, 0.06), (0.03, 0.11), (0.0, 0.1)], 8,
                T(x + math.cos(a) * 0.09, y + math.sin(a) * 0.09, z + 0.15), tint=b.vary(0.85, 1.1))


# --- The Abbotts: the twin-gable house -----------------------------------------------------------------------------

ABBOTT_MUSTARD = H(0xC29A45)
ABBOTT_PLUM = H(0x7B5196)
ABBOTT_ROOF = H(0x3E2F5C)
ABBOTT_DOOR = H(0x6E2F5E)


def build_abbotts(craft):
    b = Builder(73 + 10 * lod())
    PL = paints(craft, ABBOTT_MUSTARD, ABBOTT_ROOF, ABBOTT_DOOR, accent=H(0x6E2F5E), trim=E["bone_dim"])
    PR = paints(craft, ABBOTT_PLUM, ABBOTT_ROOF, ABBOTT_DOOR, accent=H(0xC29A45), trim=E["bone_dim"])
    for P in (PL, PR):
        P["brick"] = craft.tinted(shade(E["plum"], 0.8), 0.9, 0.3, 9.0)
    dx, wy = DOORS["Abbotts"]
    y0, y1 = wy, 2.9
    lx0, lx1, rx0, rx1 = -3.7, -0.1, -0.1, 3.7
    lh, rh = 2.7, 5.2  # wall heights: one storey on the left, two on the right
    pitch = 52.0
    # left gable: the door under it, a lit window beside it, the ghost cut-outs
    block(b, craft, PL, lx0, lx1, y0, y1, lh, door=dx, corners="flbl",
          front=[dict(x=-3.05 - (lx0 + lx1) / 2, z=0.75, w=0.62, h=1.1, shutters=False, lit=True,
                      cutouts=((cutout("ghost", 0.42), E["bone"]),))],
          left=[dict(x=0.6, z=0.75, w=0.8, h=1.2)])
    lhalf = (lx1 - lx0) / 2
    lrise = lhalf * math.tan(math.radians(pitch))
    leave = SILL + lh
    lc_x = (lx0 + lx1) / 2
    with b.at(T(lc_x, y0, leave)):
        gable(b, PL, lhalf, lrise)
        window(b, craft, PL, 0.0, 0.55, 0.6, 0.85, lit=False, shutters=False)
    with b.at(T(lc_x, y1, leave) @ R("Z", 180)):
        b.prism(PL["back"], [(-lhalf, 0.0), (lhalf, 0.0), (0.0, lrise)], 0.1, T(0, 0.02, 0), tint=0.8)
    over = 0.35
    llen = (lhalf + over) / math.cos(math.radians(pitch))
    tanp = math.tan(math.radians(pitch))
    shingle_panel(b, PL, (y1 - y0) + 2 * over, llen, pitch, T(lx0 - over, (y0 + y1) / 2, leave - over * tanp) @ R("Z", -90))
    shingle_panel(b, PL, (y1 - y0) + 2 * over, lhalf / math.cos(math.radians(pitch)), pitch,
                  T(lx1, (y0 + y1) / 2, leave) @ R("Z", 90), fascia=False)
    b.cyl(PL["trim_y"], 0.09, (y1 - y0) + 2 * over + 0.06, T(lc_x, (y0 + y1) / 2, leave + lrise + 0.03) @ R("X", 90), segments=6 if lod() else 8)
    barge(b, PL, lc_x, y0 - over - 0.03, leave + lrise, llen, pitch)
    # right gable, taller: the bay window with the striped awning, a lit window above, a dark one in the gable
    block(b, craft, PR, rx0, rx1, y0, y1, rh, corners="frbr",
          front=[dict(x=0.0, z=0.6, w=1.7, h=1.45, shutters=False, lit=True,
                      cutouts=((cutout("ghost", 0.5), E["bone"]), (cutout("cat", 0.42), E["ink"]))),
                 dict(x=0.0, z=2.95, w=0.85, h=1.25, lit=True)],
          right=[dict(x=-0.8, z=0.7, w=0.8, h=1.25), dict(x=0.8, z=2.95, w=0.8, h=1.2, shutters=False)],
          left=[dict(x=0.9, z=2.95, w=0.7, h=1.1, shutters=False)])
    rhalf = (rx1 - rx0) / 2
    rrise = rhalf * math.tan(math.radians(pitch))
    reave = SILL + rh
    rc_x = (rx0 + rx1) / 2
    with b.at(T(rc_x, y0, reave)):
        gable(b, PR, rhalf, rrise)
        window(b, craft, PR, 0.0, 0.6, 0.55, 0.8, lit=False, shutters=False)
    with b.at(T(rc_x, y1, reave) @ R("Z", 180)):
        b.prism(PR["back"], [(-rhalf, 0.0), (rhalf, 0.0), (0.0, rrise)], 0.1, T(0, 0.02, 0), tint=0.8)
    rlen = (rhalf + over) / math.cos(math.radians(pitch))
    for rz, xx in ((-90, rx0 - over), (90, rx1 + over)):
        shingle_panel(b, PR, (y1 - y0) + 2 * over, rlen, pitch, T(xx, (y0 + y1) / 2, reave - over * tanp) @ R("Z", rz))
    b.cyl(PR["trim_y"], 0.09, (y1 - y0) + 2 * over + 0.06, T(rc_x, (y0 + y1) / 2, reave + rrise + 0.03) @ R("X", 90), segments=6 if lod() else 8)
    barge(b, PR, rc_x, y0 - over - 0.03, reave + rrise, rlen, pitch)
    b.box(PR["trim_x"], (rx1 - rx0 + 0.12, 0.08, 0.14), (rc_x, y0 - 0.04, SILL + 2.65), bevel=0 if lod() else 0.014)
    # the awning over the bay: orange and plum stripes, scalloped edge
    stripe = craft.striped_cloth(H(0xEE7F2D), H(0x6E2F5E), 0.3, "X")
    aw = 2.3
    with b.at(T(rc_x, y0, SILL + 2.25)):
        b.box(stripe, (aw, 0.7, 0.025), (0, -0.33, 0.0), bevel=0, rot=R("X", -24), tint=1.0)
        val = el.scallop_strip(aw, 0.12, 7, 0.07, 3 if lod() else 5)
        b.prism(stripe, val, 0.02, T(-aw / 2, -0.66, -0.13), tint=1.0)
        for sx in (-1, 1):
            b.rod(PR["paint"], (sx * aw / 2, -0.64, -0.15), (sx * aw / 2, 0.0, 0.12), 0.012, 5, lc(E["ink"]))
    # the bay's sill box and knee brackets
    b.box(PR["trim_x"], (1.95, 0.24, 0.08), (rc_x, y0 - 0.1, SILL + 0.56), bevel=0 if lod() else 0.014)
    chimney(b, PR, rx1 - 0.9, y1 - 0.7, reave + 0.6, reave + rrise + 1.3, size=0.6, lean=2.5)
    # the door (round-topped), a little hood on brackets, landing and steps
    door_frame(b, craft, PL, dx, wy, arch=True)
    hz = SILL + OPEN_H + 0.3
    b.box(PL["shingle"], (1.7, 0.7, 0.06), (dx, wy - 0.33, hz + 0.12), bevel=0, rot=R("X", -18), tint=(1.05, 0.95, 1.1))
    b.box(PL["trim_x"], (1.76, 0.06, 0.12), (dx, wy - 0.68, hz + 0.0), bevel=0 if lod() else 0.014)
    for sx in (-1, 1):
        r = 0.45
        br = [(0.0, 0.0), (r, 0.0), (r, -0.06)] + el.arc(r, -r, r - 0.06, 90, 180, 4 if lod() else 6)[1:] + [(0.0, -r)]
        with b.at(T(dx + sx * 0.78, wy, hz - 0.04) @ R("Z", -90)):
            b.prism(PL["trim_x"], br, 0.06, T(0, -0.03, 0), bevel=0 if lod() else 0.01)
    landing(b, PL, dx, wy, 1.9, 0.5)
    steps(b, PL, dx, wy - 0.5, 1.9)
    # a paper chain along the left eave, two matching jack-o'-lanterns, a cut-paper ghost pair by the steps
    if not lod():
        paper_chain(b, craft, (lx0 + 0.1, y0 - 0.12, leave - 0.15), (lx1 - 0.15, y0 - 0.12, leave - 0.15), 0.28)
        paper_chain(b, craft, (rx0 + 0.15, y0 - 0.12, SILL + 2.55), (rx1 - 0.1, y0 - 0.12, SILL + 2.55), 0.22)
    jack(b, craft, dx + 1.35, wy - 0.7, 0.0, 0.32, 0, -12)
    jack(b, craft, dx - 1.45, wy - 1.1, 0.0, 0.32, 0, 12)
    jack(b, craft, rc_x + 1.2, wy - 0.45, 0.0, 0.22, None, 30, H(0xF59A23))
    finish(b, "Abbotts")


def gable(b, P, half, rise):
    tri = [(-half, 0.0), (half, 0.0), (0.0, rise)]
    b.prism(P["back"], tri, 0.1, T(0, 0.02, 0), tint=0.85)
    if lod():
        st.far_gable(b, P, tri)
    else:
        for i in range(int(rise / 0.2)):
            z = (i + 0.5) * 0.2
            hw = half * (1 - z / rise) - 0.02
            if hw > 0.15:
                b.box(P["clap"], (2 * hw, 0.035, 0.214), (0, 0.005, z), drop=("+y", "+z"), rot=R("X", -6), tint=b.vary(0.8, 1.12))
    b.box(P["trim_x"], (2 * half + 0.1, 0.07, 0.14), (0, -0.03, 0.02), bevel=0 if lod() else 0.014)


def barge(b, P, cx, y, ridge, length, pitch):
    strip = el.scallop_strip(length + 0.1, 0.17, max(4, round(length / 0.5)), 0.09, 3 if lod() else 4)
    for sx in (1, -1):
        m = (S(-1, 1, 1) if sx < 0 else T()) @ R("Y", pitch)
        b.prism(P["trim_x"], strip, 0.05, T(cx, y, ridge + 0.08) @ m, bevel=0 if lod() else 0.012)
    b.box(P["orange"], (0.1, 0.06, 0.1), (cx, y - 0.01, ridge - 0.05), bevel=0, rot=R("Y", 45), tint=1.0)


def paper_chain(b, craft, p0, p1, sag):
    paper = craft.paper_white()
    pts = el.catenary(p0, p1, sag, 24)
    cols = (E["orange"], H(0x6E2F5E), E["candy"], H(0x4E7A3A))
    for i, (a, c) in enumerate(zip(pts, pts[1:])):
        a, c = Vector(a), Vector(c)
        m = (a + c) / 2
        d = (c - a).normalized()
        rot = d.to_track_quat("X", "Z").to_matrix().to_4x4() @ R("X", 90 * (i % 2))
        b.ring(paper, 0.03, 0.045, 0.02, T(*m) @ rot @ R("Z", 90), segs=8, tint=lc(cols[i % 4]))


# --- shared ----------------------------------------------------------------------------------------------------------

def finish(b, house):
    b.finish(MODEL[house] + ("_far" if lod() else ""))


BUILDERS = {"Fogg": build_fogg, "Pearl": build_pearl, "Abbotts": build_abbotts}


# --- door leaves (origin: the hinge edge at the sill; the leaf runs along +X; front toward -Y) ------------------------

def _leaf_core(b, mat, mat_x, outline=None):
    """The leaf: 4.5 cm thick behind the hinge's front plane (y 0..0.045), stile-and-rail faces on the front."""
    if outline:
        b.prism(mat, outline, 0.045, T(0, 0.0, 0), tint=0.95)
    else:
        b.box(mat, (DOOR_W - 0.02, 0.045, DOOR_H - 0.02), (DOOR_W / 2, 0.0225, DOOR_H / 2), bevel=0.006, tint=0.95)


def build_leaf_fogg(craft):
    b = Builder(91)
    door = craft.timber(FOGG_DOOR, "Z", wear=0.35, bare=E["timber_dark"])
    door_x = craft.timber(FOGG_DOOR, "X", wear=0.35, bare=E["timber_dark"])
    panel = craft.timber(H(0x7A3E6E), "Z", wear=0.3, bare=E["timber_dark"])
    _leaf_core(b, door, door_x)
    W, Hh = DOOR_W, DOOR_H
    for x in (0.07, W - 0.07):
        b.box(door, (0.13, 0.03, Hh - 0.04), (x, -0.012, Hh / 2), bevel=0.01)
    for z, h in ((0.1, 0.18), (1.0, 0.14), (Hh - 0.09, 0.16)):
        b.box(door_x, (W - 0.26, 0.03, h), (W / 2, -0.012, z), bevel=0.01)
    b.box(door, (0.1, 0.03, Hh - 0.36), (W / 2, -0.012, Hh / 2 + 0.02), bevel=0.01)
    for px in (W / 2 - 0.235, W / 2 + 0.235):  # four raised plum panels
        for z0, z1 in ((0.2, 0.92), (1.08, Hh - 0.18)):
            b.box(panel, (0.32, 0.025, z1 - z0 - 0.04), (px, -0.02, (z0 + z1) / 2), bevel=0.02, tint=1.05)
    paint = craft.paint()
    # a big brass knob and a brass kick plate (no knocker: the bell is the push button)
    b.lathe(paint, [(0.0, 0.0), (0.024, 0.0), (0.018, 0.03), (0.04, 0.055), (0.035, 0.085), (0.0, 0.095)], 10,
            T(W - 0.12, -0.03, 1.0) @ R("X", 90), tint=lc(E["brass"]))
    b.box(paint, (0.07, 0.012, 0.16), (W - 0.12, -0.03, 1.0), bevel=0.005, tint=lc(E["brass"]))
    b.box(paint, (W - 0.2, 0.01, 0.16), (W / 2, -0.032, 0.12), bevel=0.004, tint=lc(shade(E["brass"], 0.85)))
    # the silly paper skeleton pinned on it, dangling a little (bone paper with painted ink sockets)
    paper = craft.paper_white()
    sk = skeleton_outline(0.62)
    b.paper(paper, sk, T(W / 2, -0.035, 1.08) @ R("Y", 6), lc(E["bone"]), fold=0.04, curl=0.06, cuts=1)
    ink = lc(E["ink"])
    for s in (-1, 1):
        b.disc(paint, 0.017, 0.003, T(W / 2 + s * 0.024 + 0.006, -0.045, 1.08 + 0.53) @ R("X", 0), segs=8, tint=ink)
    b.box(paint, (0.03, 0.004, 0.01), (W / 2 + 0.006, -0.045, 1.08 + 0.485), bevel=0, tint=ink)
    b.lathe(paint, [(0.0, 0.0), (0.012, 0.0), (0.012, 0.01), (0.0, 0.016)], 8, T(W / 2 + 0.003, -0.04, 1.08 + 0.62) @ R("X", 90),
            tint=lc(E["orange"]))  # the tack
    for x in (0.0, ):  # three ink hinges on the hinge edge
        for z in (0.25, 1.05, 1.85):
            b.box(paint, (0.05, 0.02, 0.1), (0.02, -0.006, z), bevel=0.004, tint=ink)
    b.finish(LEAF["Fogg"])


def skeleton_outline(h):
    """A friendly cut-paper skeleton (round skull, rib cage, arms and legs out), feet at z = 0, centred on x."""
    u = h / 20.0
    half = [(0.0, 20.0), (1.4, 19.8), (2.2, 18.8), (2.2, 17.4), (1.5, 16.4), (0.8, 16.0), (0.8, 15.4), (2.6, 15.2), (5.0, 13.6),
            (6.6, 11.4), (6.0, 11.0), (4.6, 12.6), (3.0, 13.4), (3.0, 10.4), (2.2, 9.4), (2.0, 8.6), (2.9, 8.2), (3.6, 4.0), (4.6, 0.6),
            (3.4, 0.0), (2.4, 0.6), (1.6, 4.4), (0.6, 7.6), (0.0, 7.8)]
    pts = [(x * u, z * u) for x, z in half] + [(-x * u, z * u) for x, z in reversed(half[1:-1])]
    return pts


def build_leaf_pearl(craft):
    b = Builder(92)
    door = craft.timber(PEARL_DOOR, "Z", wear=0.3, bare=E["timber_dark"])
    door_x = craft.timber(PEARL_DOOR, "X", wear=0.3, bare=E["timber_dark"])
    trim = craft.timber(E["bone_dim"], "Z", wear=0.4, bare=E["timber_dark"])
    _leaf_core(b, door, door_x)
    W, Hh = DOOR_W, DOOR_H
    for x in (0.07, W - 0.07):
        b.box(door, (0.13, 0.03, Hh - 0.04), (x, -0.012, Hh / 2), bevel=0.01)
    for z, h in ((0.1, 0.18), (0.88, 0.12), (1.22, 0.06), (Hh - 0.09, 0.16)):
        b.box(door_x, (W - 0.26, 0.03, h), (W / 2, -0.012, z), bevel=0.01)
    for px in (W / 2 - 0.2, W / 2 + 0.2):
        b.box(door, (0.3, 0.025, 0.6), (px, -0.02, 0.52), bevel=0.02, tint=1.08)
    # the little window: four panes of warm-dark glass with a lace curtain behind (painted paper, unlit)
    glass = craft.tinted((0.16, 0.11, 0.12), 0.15, 0.1)
    lace = craft.paper_white()
    wz0, wz1 = 1.32, 1.9
    b.box(glass, (0.62, 0.01, wz1 - wz0), (W / 2, -0.004, (wz0 + wz1) / 2), bevel=0, tint=1.0)
    lace_pts = [(-0.29, 0.0), (0.29, 0.0), (0.29, 0.56)] + [(0.29 - 0.58 * i / 12, 0.5 + 0.03 * (i % 2)) for i in range(13)]
    b.poly(lace, [(W / 2 + x, -0.006, wz0 + z * 0.95) for x, z in lace_pts[::-1]], tint=lc(H(0xE9DCC4)))
    b.box(trim, (0.035, 0.025, wz1 - wz0), (W / 2, -0.018, (wz0 + wz1) / 2), bevel=0.006)
    b.box(trim, (0.62, 0.025, 0.035), (W / 2, -0.018, (wz0 + wz1) / 2), bevel=0.006)
    b.box(trim, (0.7, 0.04, 0.05), (W / 2, -0.024, wz0 - 0.02), bevel=0.01)
    # a wreath of paper maple leaves (no bells, no stars): orange, red, gold, around a willow ring
    paint = craft.paint()
    c = Vector((W / 2, -0.035, 1.05))
    b.ring(paint, 0.13, 0.16, 0.025, T(c.x, c.y + 0.012, c.z), segs=16, tint=lc(H(0x6B4A3A)))
    cols = (E["leaf_a"], E["leaf_b"], E["leaf_c"])
    for k in range(14):
        a = 2 * math.pi * k / 14
        p = c + Vector((math.cos(a) * 0.15, -0.004 * (k % 3), math.sin(a) * 0.15))
        pr.leaf(b, craft, T(*p) @ R("Y", -math.degrees(a) + 90 + b.rng.uniform(-25, 25)), 0.085, cols[k % 3], curl=0.2)
    b.prism(craft.paper_white(), [(-0.05, 0.0), (0.05, 0.0), (0.09, -0.07), (0.02, -0.03), (0.0, -0.11), (-0.02, -0.03), (-0.09, -0.07)],
            0.006, T(c.x, c.y - 0.012, c.z - 0.15), tint=lc(H(0x7A3E6E)))  # a plum bow
    b.lathe(paint, [(0.0, 0.0), (0.026, 0.0), (0.02, 0.03), (0.036, 0.05), (0.03, 0.08), (0.0, 0.09)], 10,
            T(W - 0.12, -0.03, 1.0) @ R("X", 90), tint=lc(E["brass"]))
    b.box(paint, (0.06, 0.012, 0.15), (W - 0.12, -0.03, 1.0), bevel=0.005, tint=lc(E["brass"]))
    for z in (0.25, 1.05, 1.85):
        b.box(paint, (0.05, 0.02, 0.1), (0.02, -0.006, z), bevel=0.004, tint=lc(E["brass"]))
    b.finish(LEAF["Pearl"])


def arch_leaf_outline(segs=10):
    r = DOOR_W / 2 - 0.01
    spring = DOOR_H - 0.01 - r
    return [(0.01, 0.01), (DOOR_W - 0.01, 0.01)] + [(DOOR_W / 2 + r * math.cos(math.radians(180 * i / segs)),
                                                    spring + r * math.sin(math.radians(180 * i / segs))) for i in range(segs + 1)]


def build_leaf_abbotts(craft):
    b = Builder(93)
    door = craft.timber(ABBOTT_DOOR, "Z", wear=0.35, bare=E["timber_dark"])
    paint = craft.paint()
    W, Hh = DOOR_W, DOOR_H
    r = W / 2 - 0.01
    spring = Hh - 0.01 - r
    # vertical planks under an arched top
    n = 6
    for i in range(n):
        xa, xb = 0.01 + i * (W - 0.02) / n + 0.004, 0.01 + (i + 1) * (W - 0.02) / n - 0.004
        top = []
        for xx in (xb, xa):
            d = (xx - W / 2) / r
            top.append((xx, spring + r * math.sqrt(max(0.0, 1 - d * d))))
        mid = ((xa + xb) / 2, spring + r * math.sqrt(max(0.0, 1 - ((xa + xb) / 2 - W / 2) ** 2 / (r * r))))
        pts = [(xa, 0.01), (xb, 0.01), top[0], mid, top[1]]
        b.prism(door, pts, 0.045, T(0, 0.0, 0), tint=b.vary(0.88, 1.08))
    ink = lc(E["ink"])
    for z in (0.35, 1.45):  # iron straps with round ends
        b.box(paint, (W - 0.12, 0.012, 0.07), (W / 2 - 0.02, -0.006, z), bevel=0.004, tint=ink)
        for k in range(4):
            b.disc(paint, 0.012, 0.006, T(0.12 + k * 0.26, -0.014, z) @ R("X", 0), segs=6, tint=lc(H(0x4A3F5A)))
    b.ring(paint, r - 0.03, r - 0.01, 0.01, T(W / 2, -0.008, spring), a0=0, a1=180, segs=12, tint=lc(E["bone_dim"]))
    # two little paper ghosts hand in hand (a duet), pinned at a child's eye height
    paper = craft.paper_white()
    for k, (gx, h) in enumerate(((W / 2 - 0.12, 0.3), (W / 2 + 0.12, 0.26))):
        b.paper(paper, st.ghost_cutout(h), T(gx, -0.02 - 0.002 * k, 1.0) @ R("Y", 8 - 16 * k), lc(E["bone"]), fold=0.03, curl=0.05, cuts=1)
        for s in (-1, 1):
            b.disc(paint, 0.013, 0.003, T(gx + s * 0.035, -0.032, 1.0 + h * 0.62), segs=8, tint=ink)
    b.lathe(paint, [(0.0, 0.0), (0.024, 0.0), (0.018, 0.03), (0.038, 0.05), (0.032, 0.08), (0.0, 0.09)], 10,
            T(W - 0.13, -0.03, 1.0) @ R("X", 90), tint=lc(E["brass"]))
    b.box(paint, (0.065, 0.012, 0.16), (W - 0.13, -0.03, 1.0), bevel=0.005, tint=lc(E["brass"]))
    for z in (0.35, 1.45):
        b.box(paint, (0.05, 0.02, 0.12), (0.02, -0.006, z), bevel=0.004, tint=ink)
    b.finish(LEAF["Abbotts"])


LEAF_BUILDERS = {"Fogg": build_leaf_fogg, "Pearl": build_leaf_pearl, "Abbotts": build_leaf_abbotts}


# --- porch lamps (origin: the middle of the glass; the bracket runs back to the wall along +Y) -------------------------

LAMP_GLASS = H(0xFFC27A)


def build_lamp(craft, house):
    b = Builder(95 + HOUSES.index(house))
    paint = craft.paint()
    glass = craft.glow(LAMP_GLASS, 2.0)
    frame = {"Fogg": E["ink"], "Pearl": E["brass"], "Abbotts": H(0x6E2F5E)}[house]
    cap = {"Fogg": E["orange"], "Pearl": shade(E["brass"], 0.8), "Abbotts": E["candy"]}[house]
    k = 1.25 if house == "Pearl" else 1.0
    wall_y = -LAMP_AT[2]
    prev = getattr(b, "glow_tag", None)
    if house == "Pearl":  # a six-sided carriage lantern
        sides, rr, hh = 6, 0.13 * k, 0.3 * k
    elif house == "Abbotts":  # a round tin lantern with a candy cap
        sides, rr, hh = 10, 0.1, 0.26
    else:  # a square iron lantern
        sides, rr, hh = 4, 0.11, 0.28
    turn = 45.0 if sides == 4 else 0.0
    b.glow_tag = "Lamp"
    b.lathe(glass, [(0.0, -hh / 2), (rr * 0.9, -hh / 2), (rr, -hh * 0.2), (rr, hh * 0.35), (rr * 0.85, hh / 2), (0.0, hh / 2)], sides,
            R("Z", turn))
    b.glow_tag = prev
    for i in range(sides):  # frame bars
        a = math.radians(turn + 360 * (i + 0.5) / sides) if sides != 10 else math.radians(36 * i)
        if sides == 10 and i % 2:
            continue
        p = Vector((math.cos(a) * rr * 1.02, math.sin(a) * rr * 1.02, 0))
        b.rod(paint, p + Vector((0, 0, -hh / 2)), p + Vector((0, 0, hh / 2)), 0.008, 4, lc(frame))
    b.lathe(paint, [(0.0, -hh / 2 - 0.05), (rr * 0.7, -hh / 2 - 0.05), (rr * 1.05, -hh / 2 - 0.01), (rr * 1.05, -hh / 2 + 0.005), (0.0, -hh / 2 + 0.005)],
            sides, R("Z", turn), tint=lc(frame))
    b.lathe(paint, [(0.0, hh / 2 - 0.005), (rr * 1.15, hh / 2 - 0.005), (rr * 1.15, hh / 2 + 0.02), (rr * 0.5, hh / 2 + 0.1 * k), (0.07 * k, hh / 2 + 0.12 * k),
                    (0.0, hh / 2 + 0.13 * k)], sides, R("Z", turn), tint=lc(cap))
    b.lathe(paint, [(0.0, 0.0), (0.03, 0.0), (0.0, 0.07)], 8, T(0, 0, hh / 2 + 0.13 * k), tint=lc(frame))
    # the bracket: a wall plate and a curled arm over the top
    top = hh / 2 + 0.16 * k
    b.box(paint, (0.12, 0.02, 0.26), (0, wall_y - 0.01, 0.05), bevel=0.006, tint=lc(frame))
    b.rod(paint, (0, wall_y - 0.02, top), (0, 0.0, top), 0.012, 6, lc(frame))
    b.rod(paint, (0, wall_y - 0.02, -0.04), (0, wall_y * 0.45, top), 0.01, 6, lc(frame))
    b.ring(paint, 0.035, 0.05, 0.012, T(0, wall_y * 0.55, top + 0.05) @ R("Z", 90), segs=10, tint=lc(frame))
    b.finish(LAMP[house])
