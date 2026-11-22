"""Aunt Kit's owl house (spec §1, §2, §4): the dark indigo house assembled from the porch kit, with the round
attic window, the ticket-booth front door, June's round-topped side attic window, and the rooftop owl puppet
on its wind wheel, dressed for Halloween.

Layout: body x -3..3, y 0..6 (front wall at y = 0), porch y -2..0, ground z = 0.

Run: blender -b --python tools/blender/env/owl_house.py -- out.png [draft|final] [view ...]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import env_lib as el
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, arc, grouped, lc, owl_outline, scallop_strip, shade
from porch_kit import FLOOR, POST, WALL

PITCH = 50.0
HALF, DEPTH, OVER = 3.0, 6.0, 0.35
EAVE_Z = FLOOR + WALL
RISE = HALF * math.tan(math.radians(PITCH))
RIDGE_Z = EAVE_Z + RISE
WIN_Z, WIN_R = EAVE_Z + 1.5, 0.56  # the round attic window
SILL_Z = 2.5  # June's side window (spec §4 attic sill)
DORMER_Y, DORMER_TOP = 3.0, 3.98
SIDE_W, SIDE_SPRING = 1.0, 0.95  # the way in: Pip is 1.2 m tall with a 0.28 m capsule radius
SIDE_APEX = SIDE_SPRING + SIDE_W / 2  # 1.45 m above the sill
SIDE_WINDOW = T(-HALF, DORMER_Y, 0) @ R("Z", -90) @ T(0, -0.025, SILL_Z)  # sill centre, front toward local -Y
ALCOVE = (0.715, 4.0, 4.6)  # June's alcove inside the dormer (interior.ALC_*: 0.7 half width, eave 3.9, ridge 4.5), with margin
SIDE_PANE_HINGE = T(*(SIDE_WINDOW @ Vector((SIDE_W / 2, -0.02, 0.0))))  # the pane's jamb, opposite the open casement
OWL_Z = RIDGE_Z + 0.34
WHEEL = Vector((0.0, 0.5, RIDGE_Z + 2.45))


def owl_puppet(b, P, craft, height=0.95):
    """The wooden owl puppet, feet at the local origin, facing -Y: a jig-sawn body with layered face, hinged
    wings on brass bolts, and round ring-framed eyes that repeat the attic window."""
    u = height / 81.0
    body = craft.timber(H(0xC9762B), "Z", wear=0.35, bare=E["timber_dark"])
    wing = craft.timber(E["plum"], "Z", wear=0.35, bare=E["timber_dark"])
    paint = craft.paint()
    b.prism(body, owl_outline(height, 3), 0.22, T(0, -0.11, 0), bevel=0.012, tint=1.0)
    n = 6
    dome = [(math.sin(math.pi * i / n) if 0 < i < n else 0.0, -math.cos(math.pi * i / n)) for i in range(n + 1)]
    b.lathe(body, dome, 14, T(0, -0.11, 26 * u) @ S(25 * u, 0.085, 24 * u), tint=1.0)  # a round belly on the slab
    b.lathe(P["candy"], dome, 12, T(0, -0.15, 17 * u) @ S(15 * u, 0.06, 14 * u), tint=1.0)  # domed chest
    for row in range(3):  # painted feather chevrons
        w = (9 - 2.2 * row) * u
        z = (24 - 7 * row) * u
        b.prism(P["orange"], [(-w, z), (0, z - 4 * u), (w, z), (w, z + 2.4 * u), (0, z - 1.6 * u), (-w, z + 2.4 * u)], 0.03,
                T(0, -0.218 + 0.004 * row, 0), tint=1.0 - 0.08 * row)
    for s in (1, -1):
        shape = [(0.0, 0.0), (9 * u, -4 * u), (12 * u, -30 * u), (4 * u, -44 * u), (-5 * u, -30 * u), (-4 * u, -8 * u)]
        with b.at(T(s * 22 * u, -0.045, 58 * u) @ (S(-1, 1, 1) if s < 0 else T()) @ R("Y", -12) @ R("Z", -26)):  # panels cupped forward
            b.prism(wing, shape, 0.06, T(0, -0.09, 0), bevel=0.016)
            for k in range(3):
                b.prism(P["door_x"], scallop_strip(13 * u, 0.4 * u, 2, 2.6 * u, 3), 0.012, T(-2 * u, -0.1, (-12 - 9 * k) * u), tint=1.2)
            b.disc(paint, 2.4 * u, 0.02, T(2 * u, -0.105, -4 * u), segs=8, tint=lc(E["brass"]), bevel=0.004)
        ex, ez = s * 12 * u, 49 * u
        b.ring(P["trim_x"], 7.6 * u, 12.5 * u, 0.06, T(ex, -0.185, ez), segs=20, tint=1.0)
        b.disc(P["eye"], 8 * u, 0.02, T(ex, -0.145, ez), segs=20, tint=1.0)
        b.disc(P["ink"], 3.6 * u, 0.02, T(ex, -0.157, ez), segs=12, tint=1.0, bevel=0.004)
        for k in range(3):  # toes over the perch bar
            b.box(paint, (0.028, 0.11, 0.035), (s * 9 * u + (k - 1) * 0.034, -0.085, 0.012), bevel=0.01, tint=lc(E["orange"]))
    for s in (1, -1):  # the V brow
        b.box(wing, (21 * u, 0.03, 5.5 * u), (s * 12 * u, -0.205 - 0.004 * s, 63.5 * u), bevel=0.01, rot=R("Y", s * 30), tint=1.25)
    b.lathe(paint, [(0.0, 0.0), (5.5 * u, 0.0), (0.0, 0.085)], 4, T(0, -0.16, 40 * u) @ R("X", 112) @ R("Z", 45), tint=lc(E["orange"]))


def wind_wheel(b, P, craft, radius=0.72):
    """The small roof wheel: eight pitched paddles on a hub, a rim hoop, tail vane and mast. Hub at the origin,
    axis along Y; the mast runs down `WHEEL.z - RIDGE_Z`."""
    paint = craft.paint()
    wood = craft.timber(E["timber_dark"], "Z", wear=0.3)
    drop = WHEEL.z - RIDGE_Z
    b.box(wood, (0.09, 0.09, drop + 0.1), (0, 0.12, -drop / 2 + 0.05), bevel=0.016)
    b.box(wood, (0.07, 1.5, 0.06), (0, 0.5, -drop + 0.72), bevel=0.012, rot=R("X", -62))
    b.box(wood, (0.16, 0.16, 0.1), (0, 0.12, -drop + 0.12), bevel=0.02)
    b.cyl(paint, 0.075, 0.2, R("X", 90) @ T(0, 0, -0.03), segments=10, tint=lc(E["brass"]))
    b.lathe(paint, [(0.075, 0.0), (0.05, 0.05), (0.0, 0.07)], 10, T(0, -0.07, 0) @ R("X", 90), tint=lc(E["orange"]))
    cols = (E["orange"], E["bone"], E["purple"], E["bone"])
    blade = [(-0.035, 0.07), (0.035, 0.07), (0.115, radius - 0.02), (-0.115, radius - 0.02)]
    for k in range(8):
        with b.at(R("Y", 45 * k + 12)):
            b.prism(paint, blade, 0.02, R("Z", 24) @ T(0, -0.01, 0), tint=lc(shade(cols[k % 4], 0.92)), bevel=0.006)
            b.rod(wood, (0, 0.0, 0.05), (0, 0.0, radius - 0.03), 0.014, 5)
    b.ring(wood, radius - 0.07, radius - 0.03, 0.035, T(0, 0.01, 0), segs=24, tint=0.9)
    b.rod(wood, (0, 0.1, 0), (0, 0.95, 0), 0.02, 6)
    fin = [(0.0, 0.0), (0.3, 0.2), (0.42, 0.0), (0.3, -0.2)]
    b.prism(paint, fin, 0.02, T(0.01, 0.75, 0) @ R("Z", 90), tint=lc(E["orange"]), bevel=0.006)
    b.rod(paint, (0.06, 0.12, -0.1), (0.06, 0.12, -drop + 0.02), 0.012, 5, lc(E["bone_dim"]))


def build_house(craft, dress=True, lights=True, attic=3.4):
    """Builds the house meshes at the origin (call through env_lib.grouped to get one root)."""
    P = pk.paints(craft)
    P["paper_glow"] = craft.glow(E["glow"], attic)
    tan = math.tan(math.radians(PITCH))

    # --- walls, foundation, gables ---
    b = Builder(101)
    stone = craft.tinted(E["stone"], 0.9, 0.3, 4.0)
    b.box(stone, (2 * HALF - 0.04, DEPTH - 0.04, FLOOR), (0, DEPTH / 2, FLOOR / 2), bevel=0, tint=0.7)
    for sx in (-1, 1):
        n = 30
        for i in range(n):
            y = (i + 0.5) * DEPTH / n
            b.box(P["wall_v"], (0.03, DEPTH / n - 0.012, FLOOR - 0.03), (sx * (HALF + 0.0), y, FLOOR / 2 - 0.015), bevel=0, tint=b.vary(0.75, 1.0),
                  drop=("-z", "-x" if sx > 0 else "+x"))
    win, dr = (-0.4, 0.4, 0.75, 1.95), (-0.57, 0.57, 0.0, 2.02)
    for x, kind in ((-2, "w"), (0, "d"), (2, "w")):
        with b.at(T(x, 0, FLOOR)):
            pk.wall(b, P, openings=(win if kind == "w" else dr,))
            if kind == "w":
                pk.window(b, P)
            else:
                pk.door(b, P)
    for y, kind in ((1, ""), (3, "dormer"), (5, "w")):
        with b.at(T(-HALF, y, FLOOR) @ R("Z", -90)):
            pk.wall(b, P, openings=((win,) if kind == "w" else ((-SIDE_W / 2, SIDE_W / 2, SILL_Z - FLOOR, WALL),) if kind else ()))
            if kind == "w":
                pk.window(b, P)
    for y, kind in ((1, ""), (3, "w"), (5, "")):
        with b.at(T(HALF, y, FLOOR) @ R("Z", 90)):
            pk.wall(b, P, openings=(win,) if kind else ())
            if kind:
                pk.window(b, P)
    for x in (-2, 0, 2):
        with b.at(T(x, DEPTH, FLOOR) @ R("Z", 180)):
            pk.wall(b, P)
    for (x, y), rz in (((-HALF, 0), 0), ((HALF, 0), 90), ((HALF, DEPTH), 180), ((-HALF, DEPTH), 270)):
        with b.at(T(x, y, FLOOR) @ R("Z", rz)):
            pk.corner_trim(b, P)
    tri = [(-HALF, 0.0), (HALF, 0.0), (0.0, RISE)]
    b.prism(P["back"], tri, 0.1, T(0, 0.02, EAVE_Z), tint=0.85)
    b.prism(P["clap"], tri, 0.1, T(0, DEPTH - 0.12, EAVE_Z), tint=0.9)
    b.box(P["trim_x"], (2 * HALF + 0.1, 0.07, 0.14), (0, -0.03, EAVE_Z + 0.02), bevel=0.016)
    b.box(P["trim_x"], (1.14, 0.14, 0.03), (0, 0.07, FLOOR), bevel=0.008, tint=0.9)  # threshold through the wall thickness

    def keep(cx, y0, y1, hw):
        if abs(cx) + hw > (RISE - y1) / tan + 0.16 or y1 < 0.5:  # outside the gable, or behind the porch roof
            return False
        return math.hypot(cx, (y0 + y1) / 2 + 0.05 - (WIN_Z - EAVE_Z)) > WIN_R + 0.2
    pk.shingles(b, P["scale"], 2 * HALF, RISE, T(0, 0.02, EAVE_Z) @ R("X", 90), tab=0.44, exposure=0.33, keep=keep,
                palette=[(1, 1, 1)] * 4 + [(0.8, 0.85, 1.1), (1.1, 0.95, 1.25)])
    pk.round_window(b, P, WIN_R, T(0, -0.035, WIN_Z), P["paper_glow"])
    b.finish("House_Walls")

    # --- main roof, bargeboards, ridge, chimney ---
    r = Builder(102)
    length = (HALF + OVER) / math.cos(math.radians(PITCH))
    eave = EAVE_Z - OVER * tan
    width = DEPTH + 2 * OVER
    pk.roof(r, P, width, length, PITCH, T(-HALF - OVER, DEPTH / 2, eave) @ R("Z", -90), notch=(-0.95, 0.95, 0.62), hole=(-0.45, 0.45, 0.5, 1.9),
            mouth=(ALCOVE[0], (ALCOVE[1] - eave) / math.sin(math.radians(PITCH)), (ALCOVE[2] - eave) / math.sin(math.radians(PITCH))))
    pk.roof(r, P, width, length, PITCH, T(HALF + OVER, DEPTH / 2, eave) @ R("Z", 90))
    r.cyl(P["trim_y"], 0.09, width + 0.06, T(0, DEPTH / 2, RIDGE_Z + 0.02) @ R("X", 90), segments=8)
    barge = scallop_strip(length + 0.12, 0.16, 13, 0.08, 4)
    for sx in (1, -1):
        m = (S(-1, 1, 1) if sx < 0 else T()) @ R("Y", PITCH)
        r.prism(P["trim_x"], barge, 0.05, T(0, -OVER - 0.03, RIDGE_Z + 0.07) @ m, bevel=0.014)
        r.box(P["trim_x"], (length + 0.12, 0.05, 0.2), (sx * (HALF + OVER) / 2, DEPTH + OVER + 0.005, (RIDGE_Z + eave) / 2 - 0.02), bevel=0.014,
              rot=R("Y", sx * PITCH))
    r.box(P["trim_z"], (0.2, 0.09, 0.5), (0, -OVER - 0.035, RIDGE_Z - 0.16), bevel=0.02, tint=1.05)
    r.lathe(P["trim_z"], [(0.0, 0.0), (0.05, -0.04), (0.07, -0.1), (0.03, -0.15), (0.0, -0.2)], 8, T(0, -OVER - 0.035, RIDGE_Z - 0.4))
    brick = craft.tinted(E["plum"], 0.9, 0.3, 9.0)
    cx, cy = 1.45, 4.55
    z0 = EAVE_Z + (HALF - cx - 0.4) * tan
    courses = int((RIDGE_Z + 0.75 - z0) / 0.17)
    with r.at(T(cx, cy, z0) @ R("Y", 2.5)):
        for i in range(courses):
            r.box(brick, (0.62, 0.62, 0.166), (r.rng.uniform(-0.012, 0.012), r.rng.uniform(-0.012, 0.012), (i + 0.5) * 0.17), bevel=0.018,
                  tint=r.vary(0.75, 1.15))
        top = courses * 0.17
        r.box(P["trim_x"], (0.76, 0.76, 0.1), (0, 0, top + 0.04), bevel=0.025)
        for dx in (-0.15, 0.15):
            r.lathe(craft.paint(), [(0.09, 0.0), (0.1, 0.16), (0.075, 0.24), (0.06, 0.24)], 8, T(dx, 0, top + 0.09), tint=lc(E["orange"]))
    r.finish("House_Roof")

    # --- June's wall dormer on the west side ---
    d = Builder(103)
    with d.at(T(-HALF, DORMER_Y, 0) @ R("Z", -90)):
        hh = DORMER_TOP - EAVE_Z
        with d.at(T(0, 0, EAVE_Z)):
            pk.wall(d, P, 1.6, hh, openings=((-SIDE_W / 2, SIDE_W / 2, 0.0, SILL_Z + SIDE_APEX - EAVE_Z),), base=False, frieze=False)
        for sx in (1, -1):  # wall between the square opening and the arch (the interior model is behind the opening)
            spandrel = arc(0.0, SIDE_SPRING, SIDE_W / 2 + 0.012, 0, 90, 6) + [(0.0, SIDE_APEX + 0.01), (SIDE_W / 2, SIDE_APEX + 0.01)]
            d.prism(P["clap"], spandrel, 0.12, T(0, 0.0, SILL_Z) @ (S(-1, 1, 1) if sx < 0 else T()), tint=0.85)
        for sx in (-1, 1):
            d.box(P["clap"], (0.09, 1.55, hh), (sx * 0.76, 0.78, EAVE_Z + hh / 2), bevel=0.012, tint=0.9)
            d.box(P["trim_z"], (0.1, 0.06, hh + 0.3), (sx * 0.78, -0.035, EAVE_Z + hh / 2 - 0.15), bevel=0.014)
        pk.arch_window(d, P, SIDE_W, SIDE_SPRING, T(0, -0.025, SILL_Z), P["paper_dim"])
        dp, dh = 40.0, 0.8 * math.tan(math.radians(40.0))
        dl = 1.02 / math.cos(math.radians(dp))
        de = DORMER_TOP - 0.22 * math.tan(math.radians(dp))
        pk.roof(d, P, 1.95, dl, dp, T(-1.02, 0.66, de) @ R("Z", -90), rafters=False)
        pk.roof(d, P, 1.95, dl, dp, T(1.02, 0.66, de) @ R("Z", 90), rafters=False)
        d.cyl(P["trim_y"], 0.06, 1.98, T(0, 0.66, DORMER_TOP + dh + 0.02) @ R("X", 90), segments=8)
        d.prism(P["clap"], [(-0.8, 0.0), (0.8, 0.0), (0.0, dh)], 0.1, T(0, 0.0, DORMER_TOP), tint=0.95)
        for sx in (1, -1):
            d.box(P["trim_x"], (dl + 0.06, 0.05, 0.13), (sx * 0.51, -0.34, DORMER_TOP + dh / 2 - 0.1), bevel=0.014, rot=R("Y", sx * dp))
        d.box(P["orange"], (0.12, 0.06, 0.12), (0, -0.345, DORMER_TOP + dh - 0.06), bevel=0.02, rot=R("Y", 45), tint=1.0)
    d.finish("House_Dormer")

    # --- porch ---
    p = Builder(104)
    for x, skirt in ((-2, ("front", "left")), (0, ()), (2, ("front", "right"))):
        with p.at(T(x, -2, 0)):
            pk.floor(p, P, skirt=skirt)
    with p.at(T(0, -2, 0)):
        pk.steps(p, P)
    beam_z = FLOOR + POST + 0.07
    for x, br in ((-2.9, (1,)), (-1.0, (-1, 1)), (1.0, (-1, 1)), (2.9, (-1,))):
        with p.at(T(x, -1.9, FLOOR)):
            pk.post(p, P, POST, br)
    p.box(P["trim_x"], (6.0, 0.15, 0.14), (0, -1.9, beam_z), bevel=0.02)
    for sx in (-1, 1):
        p.box(P["trim_y"], (0.15, 1.9, 0.14), (sx * 2.9, -0.95, beam_z), bevel=0.02)
        with p.at(T(sx * 1.95, -1.9, FLOOR)):
            pk.rail(p, P, 1.77, rhythm=1)
        with p.at(T(sx * 2.9, -0.95, FLOOR) @ R("Z", 90)):
            pk.rail(p, P, 1.78)
        p.box(P["trim_z"], (0.13, 0.07, POST), (sx * 2.9, -0.045, FLOOR + POST / 2), bevel=0.016)
    pp = math.degrees(math.atan2(0.6, 2.3))
    pl = 2.3 / math.cos(math.radians(pp)) + 0.02
    pk.roof(p, P, 6.5, pl, pp, T(0, -2.3, 2.83))
    for i in range(9):
        x = -2.8 + i * 0.7
        p.box(P["trim_y"], (0.07, pl - 0.5, 0.1), (x, -1.2 + 0.2, 2.83 + 0.325 - 0.14), rot=R("X", pp), tint=0.8, drop=("+z", "+y", "-y"))
    p.finish("House_Porch")

    # --- the owl and its wheel ---
    o = Builder(105)
    saddle = craft.timber(E["plum"], "X", wear=0.4)
    yoke = [(-0.5, -0.44), (-0.16, 0.2), (0.16, 0.2), (0.5, -0.44), (0.34, -0.44), (0.0, -0.03), (-0.34, -0.44)]
    o.prism(saddle, yoke, 0.62, T(0, -0.27, RIDGE_Z), bevel=0.025, tint=1.0)
    for sx in (-1, 1):
        o.box(saddle, (0.07, 0.07, 0.2), (sx * 0.24, -0.02, RIDGE_Z + 0.25), bevel=0.014)
        o.disc(craft.paint(), 0.03, 0.02, T(sx * 0.3, -0.285, RIDGE_Z - 0.16), segs=8, tint=lc(E["brass"]), bevel=0.005)
    o.cyl(P["trim_x"], 0.035, 0.66, T(0, -0.02, OWL_Z - 0.01) @ R("Y", 90), segments=8)
    with o.at(T(0, -0.02, OWL_Z)):
        owl_puppet(o, P, craft)
    o.rod(craft.paint(), (0, 0.1, OWL_Z + 0.45), (0, WHEEL.y + 0.1, OWL_Z + 0.95), 0.014, 5, lc(E["bone_dim"]))
    with o.at(T(*WHEEL)):
        wind_wheel(o, P, craft)
    o.finish("House_Owl")
    if lights:
        el.point_light("AtticGlow", (0, -0.9, WIN_Z), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
        el.point_light("SideWindowGlow", (-HALF - 0.6, DORMER_Y, SILL_Z + 0.5), 10, (1.0, 0.7, 0.4), 0.2, shadow=False)
    if dress:
        dress_house(craft, P, lights)


def dress_house(craft, P, lights=True):
    """Halloween dressing: jack-o'-lanterns on the steps, bunting, bat garlands, the porch lamp, a little candy
    bowl on a stool, a broom, a doormat and paper cut-outs in the windows."""
    b = Builder(110)
    paint, paper = craft.paint(), craft.paper_white()
    spots = (((-0.62, -2.45, 0.2), 0.135, 0, None, 10), ((0.62, -2.16, 0.4), 0.125, 1, H(0xF0E3C4), -15), ((-1.45, -2.8, 0.0), 0.3, 2, None, 12),
             ((1.38, -2.62, 0.0), 0.2, 0, H(0xF59A23), -20), ((1.5, -0.55, FLOOR), 0.19, 1, None, -8), ((-2.45, -1.2, FLOOR), 0.16, 2, H(0xF59A23), 25))
    for pos, rad, style, rgb, rz in spots:
        pr.pumpkin(b, craft, rad, 0.8, T(*pos) @ R("Z", rz), style=style, rgb=rgb, per_lobe=3 if rad > 0.25 else 2)
        if lights:
            el.point_light("JackGlow", (pos[0], pos[1] - rad * 1.5, pos[2] + rad * 0.8), 1.2 + 14 * rad * rad, (1.0, 0.6, 0.22), 0.05, shadow=False)
    z = FLOOR + POST - 0.03
    for i, (x0, x1) in enumerate(((-2.83, -1.07), (-0.93, 0.93), (1.07, 2.83))):
        pr.bunting(b, craft, (x0, -1.985, z), (x1, -1.985, z), 0.2, 8, start=i * 2)
    for sx in (-1, 1):
        pr.bat_garland(b, craft, (sx * 2.83, -1.975, FLOOR + 0.84), (sx * 1.07, -1.975, FLOOR + 0.84), 0.26, 5, 0.26)
    pr.bat(b, paper, 0.3, T(2.9, -1.975, FLOOR + 1.5) @ R("Y", 38), fold=0.2)  # the one about to fall sideways
    # hanging porch lamp
    hook = Vector((-0.84, -0.75, 2.83 + 1.55 * 0.26 - 0.2))
    with b.at(T(*hook)):
        c = pr.hanging_lamp(b, craft, 0.5, 1.0, 2.8)
    lamp = hook + Vector(c)
    # little candy bowl on a three-legged stool
    sx, sy = -1.55, -0.6
    wood = P["amber"]
    b.lathe(wood, [(0.0, 0.36), (0.19, 0.36), (0.2, 0.385), (0.19, 0.41), (0.0, 0.41)], 12, T(sx, sy, FLOOR))
    for k in range(3):
        a = math.radians(120 * k + 30)
        b.rod(wood, (sx + 0.17 * math.cos(a), sy + 0.17 * math.sin(a), FLOOR), (sx + 0.1 * math.cos(a), sy + 0.1 * math.sin(a), FLOOR + 0.37), 0.022, 6)
    pr.candy_bowl(b, craft, 0.16, T(sx, sy, FLOOR + 0.41), spill=False, heap=15)
    # doormat, broom
    mat = craft.felt(el.V["orange"])
    b.box(mat, (0.8, 0.5, 0.025), (0, -0.42, FLOOR + 0.012), bevel=0.008, tint=1.0)
    b.prism(craft.felt(el.V["violet"]), el.co.star_outline(0.14), 0.004, T(0, -0.42, FLOOR + 0.027) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    foot, tip = Vector((0.98, -0.42, FLOOR)), Vector((1.02, -0.07, FLOOR + 1.55))
    b.rod(wood, foot.lerp(tip, 0.2), tip, 0.018, 6)
    b.lathe(craft.tinted(H(0xC9A24B), 0.9, 0.3, 30.0), [(0.0, 0.0), (0.13, 0.0), (0.1, 0.2), (0.035, 0.36), (0.03, 0.4)], 9,
            T(*foot) @ (tip - foot).to_track_quat("Z", "Y").to_matrix().to_4x4(), radial=lambda a, i: 1 + 0.12 * math.cos(9 * a) * (i < 3))
    b.lathe(paint, [(0.04, 0.33), (0.048, 0.35), (0.04, 0.37)], 8, T(*foot) @ (tip - foot).to_track_quat("Z", "Y").to_matrix().to_4x4(), tint=lc(E["plum"]))
    # paper cut-outs on the front window panes
    pr.bat(b, paper, 0.26, T(-2.14, -0.012, FLOOR + 1.65) @ R("Y", 15), fold=0.03, curl=0.06)
    pr.bat(b, paper, 0.2, T(-1.82, -0.012, FLOOR + 1.1) @ R("Y", -20), fold=0.03, curl=0.06)
    moon = [(0.16 * math.cos(math.radians(a)), 0.16 * math.sin(math.radians(a))) for a in range(60, 301, 30)] + \
           [(0.07 + 0.12 * math.cos(math.radians(a)), 0.12 * math.sin(math.radians(a))) for a in range(270, 89, -30)]
    b.paper(paper, moon, T(2.1, -0.012, FLOOR + 1.6), lc(E["candy"]), fold=0.02, curl=0.05, cuts=0)
    b.paper(paper, el.co.star_outline(0.07), T(1.8, -0.012, FLOOR + 1.15), lc(E["candy"]), fold=0.03, curl=0.08, cuts=0)
    # stepping stones to the gate
    stone = craft.tinted(E["stone"], 0.9, 0.3, 5.0)
    for k in range(4):
        b.lathe(stone, [(0.0, 0.0), (0.3, 0.0), (0.3, 0.025), (0.24, 0.045), (0.0, 0.045)], 9,
                T(0.12 * (-1) ** k, -3.25 - 0.72 * k, 0) @ R("Z", 37 * k) @ S(1.0, 0.8, 1.0), radial=lambda a, i: 1 + 0.08 * math.cos(3 * a + 1), tint=b.vary(0.8, 1.2))
    b.finish("House_Dressing")
    if lights:
        el.point_light("PorchLamp", lamp + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)
    return lamp


# --- review sheet ------------------------------------------------------------------------------------------

def sheet_lights():
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 60.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 60.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 60.0)


def main():
    out, quality, rest = el.args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    house, _ = grouped("House", lambda: build_house(craft))
    kit = pk.build_kit_strip(craft)
    for _name, root, _x in kit:
        root.location.y += 40.0
    el.stage_ground(craft, 200.0)
    print("TRIS House:", el.triangles(house), flush=True)
    dg = bpy.context.evaluated_depsgraph_get()
    for obj in bpy.data.objects:
        if obj.parent is house and obj.type == "MESH":
            me = obj.evaluated_get(dg).to_mesh()
            me.calc_loop_triangles()
            print(f"TRIS   {obj.name}: {len(me.loop_triangles)}", flush=True)
    for name, root, _x in kit:
        print(f"TRIS Kit {name}: {el.triangles(root)}", flush=True)
    final = quality == "final"
    cam = el.setup_render(samples=40 if final else 10, exposure=0.0)
    sheet_lights()
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    kit_names = [root.name for _n, root, _x in kit]
    views = {
        "hero": ((-10.5, -15.5, 4.6), (-0.1, 1.2, 4.3), 44, 1150, 900, ["House"]),
        "front": ((0.0, -40.0, 3.4), (0.0, 0.0, 4.75), 116, 850, 900, ["House"]),
        "side": ((-42.0, 2.0, 3.4), (0.0, 2.0, 4.75), 104, 1000, 720, ["House"]),
        "door": ((-1.6, -7.2, 1.75), (-0.05, 0.0, 1.62), 60, 500, 720, ["House"]),
        "owl": ((-3.4, -7.0, 6.9), (0.0, 0.3, 7.65), 50, 500, 720, ["House"]),
        "kit": ((-8.0, 40.0 - 80.0, 13.0), (0.0, 40.4, 1.15), 122, 2000, 440, kit_names),
    }
    todo = [v for v in rest if v in views] or list(views)
    tiles = {}
    for name in todo:
        loc, target, lens, w, h, groups = views[name]
        el.solo(groups)
        bpy.context.scene.view_settings.exposure = 0.9 if name == "kit" else 0.0
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
    if len(todo) < len(views):
        el.co.save_sheet(np.concatenate([el.pad(tiles[n], max(t.shape[1] for t in tiles.values()), tiles[n].shape[0]) for n in todo][::-1], axis=0), out)
    else:
        rows = [np.concatenate([tiles["hero"], tiles["front"]], axis=1),
                np.concatenate([tiles["side"], tiles["door"], tiles["owl"]], axis=1), tiles["kit"]]
        el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("owl house sheet ->", out)


if __name__ == "__main__":
    main()
