"""The three old sketchbook pages that Pip's paper bats piece together (S004 plan section 2.1): pencil drawings,
made as Freestyle line renders of small scripted scenes and finished in numpy (graphite grain, hatching in the
shade, old cream paper, the creases of the ten bats each page was folded into).

    blender -b -t 2 --python tools/blender/pages/sketch_pages.py -- [out_dir] [only=1,2,3] [sheet=review.png]

  sketch_page_1.png  lanterns all along Lantern Lane: the lane on the Night of Lanterns, paper lanterns strung
                     across it, the booth with the owl crest over its shutter, small costumed children, and the
                     parade of five puppet carriers seen from behind
  sketch_page_2.png  five friends painting lanterns at a long table, their puppets leaning on the wall behind
  sketch_page_3.png  tall narrow houses with washing lines and roof lamps, and a crescent-moon puppet sitting on a
                     chimney (Bramble Row; nothing of case 02's answer: no lamp trolley, no wire)

Rules (plan section 2.1): no page shows the owl machine, the cam or arm, the treehouse route, the side yard or the
bowing owl, and the art contains no text. Output: 1024 x 768 RGBA, in `Content/Resources/UI/SketchPages/`.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "env"):
    if os.path.join(TOOLS, sub) not in sys.path:
        sys.path.insert(0, os.path.join(TOOLS, sub))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import env_lib as el  # noqa: E402
from env_lib import R, S, T, Builder  # noqa: E402

OUT_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "UI", "SketchPages")
W, H = 1024, 768
SS = 2  # supersampling of the renders
PAPER, GRAPHITE = (0.955, 0.905, 0.79), (0.33, 0.30, 0.285)
FOLDS = (5, 2)  # ten bats to a page

OWL = [(x / 81.0, (z + 37) / 81.0 - 0.5) for x, z in el.OWL_HALF] + [(-x / 81.0, (z + 37) / 81.0 - 0.5) for x, z in reversed(el.OWL_HALF[1:-1])]
FOX = [(0.0, -0.5), (0.35, -0.13), (0.55, 0.52), (0.22, 0.27), (0.0, 0.33), (-0.22, 0.27), (-0.55, 0.52), (-0.35, -0.13)]
MOTH = [(0.0, 0.1), (0.42, 0.5), (0.6, 0.15), (0.3, -0.05), (0.36, -0.38), (0.14, -0.45), (0.0, -0.1), (-0.14, -0.45), (-0.36, -0.38), (-0.3, -0.05),
        (-0.6, 0.15), (-0.42, 0.5)]
MOON = [(0.5 * math.cos(math.radians(a)), 0.5 * math.sin(math.radians(a))) for a in range(50, 311, 20)] + \
       [(0.23 + 0.375 * math.cos(math.radians(a)), 0.375 * math.sin(math.radians(a))) for a in range(270, 89, -20)]
BELL = [(0.0, 0.5), (0.12, 0.44), (0.22, 0.3), (0.27, -0.05), (0.46, -0.3), (0.12, -0.3), (0.0, -0.45), (-0.12, -0.3), (-0.46, -0.3), (-0.27, -0.05),
        (-0.22, 0.3), (-0.12, 0.44)]
PUPPETS = (OWL, FOX, MOTH, MOON, BELL)


def log(*a):
    print("[sketch_pages]", *a, flush=True)


# --- scene pieces (one white material: only the lines and the shade matter) ---------------------------------------

class Kit:
    def __init__(self, craft):
        self.craft = craft
        self.m = craft.paint()
        self.b = Builder(7)

    def box(self, size, centre, rot=None):
        self.b.box(self.m, size, centre, bevel=0, rot=rot, tint=1.0)

    def house(self, x, y, w, d, h, roof, rows=2, cols=2, door=True, chimney=None, face=-1, dormer=False):
        """A house with its gable toward the viewer (-Y). `face`: which side (x sign) carries no windows."""
        b = self.b
        self.box((w, d, h), (x, y + d / 2, h / 2))
        b.prism(self.m, [(-w / 2 - 0.15, 0.0), (w / 2 + 0.15, 0.0), (0.0, roof)], d + 0.3, T(x, y - 0.15, h), tint=1.0)
        cw, ch = w / (cols * 2 + 1), 0.62 * h / (rows + 1.2)
        for r in range(rows):
            for c in range(cols):
                if door and r == 0 and c == cols // 2:
                    continue
                cx = x - w / 2 + cw * (1.5 + 2 * c)
                cz = h * (0.36 + 0.5 * r / max(rows - 1, 1)) if rows > 1 else h * 0.6
                self.box((cw, 0.06, ch), (cx, y - 0.03, cz))
                self.box((cw + 0.14, 0.1, 0.06), (cx, y - 0.05, cz - ch / 2 - 0.03))
                self.box((0.03, 0.08, ch), (cx, y - 0.04, cz))
        if door:
            cx = x - w / 2 + cw * (1.5 + 2 * (cols // 2))
            self.box((cw * 1.1, 0.06, h * 0.3), (cx, y - 0.03, h * 0.15))
            b.cyl(self.m, 0.03, 0.05, T(cx + cw * 0.35, y - 0.07, h * 0.15) @ R("X", 90), segments=8, tint=1.0)
        b.cyl(self.m, min(w, roof) * 0.13, 0.05, T(x, y - 0.17, h + roof * 0.4) @ R("X", 90), segments=14, tint=1.0)  # attic round window
        if chimney is not None:
            cx, top = chimney
            self.box((0.45, 0.45, 1.2), (x + cx, y + d * 0.5, top - 0.6))
            self.box((0.6, 0.6, 0.12), (x + cx, y + d * 0.5, top))

    def lantern(self, p, r=0.16):
        b = self.b
        n = 6
        prof = [(r * 0.25, r)] + [(r * math.sin(math.pi * (i + 1) / (n + 1)) ** 0.7, r * math.cos(math.pi * (i + 1) / (n + 1))) for i in range(n)] + [(r * 0.25, -r)]
        b.lathe(self.m, prof, 12, T(*p), tint=1.0)
        for zz in (-0.45, 0.0, 0.45):
            rr = r * math.sin(math.acos(zz)) ** 0.7 + 0.004
            b.lathe(self.m, [(rr, zz * r - 0.012), (rr + 0.012, zz * r), (rr, zz * r + 0.012)], 12, T(*p), tint=1.0)
        b.cyl(self.m, r * 0.3, r * 0.14, T(p[0], p[1], p[2] + r * 1.05), segments=8, tint=1.0)

    def string(self, p0, p1, sag, count, r=0.16, n=14):
        pts = el.catenary(p0, p1, sag, n)
        for a, c in zip(pts, pts[1:]):
            self.b.rod(self.m, a, c, 0.012, 4, 1.0)
        for i in range(count):
            t = (i + 0.5) / count
            p = Vector(p0).lerp(Vector(p1), t) - Vector((0, 0, sag * 4 * t * (1 - t)))
            self.b.rod(self.m, p, p - Vector((0, 0, 0.1)), 0.008, 4, 1.0)
            self.lantern((p.x, p.y, p.z - 0.1 - r * 1.1), r)

    def person(self, x, y, h=1.1, hat=None, yaw=0.0, pole=None, puppet=None, arm=1, z0=0.0, behind=False):
        """A little costumed figure: a bell of a costume, a round head, a hat. `pole`: height of the puppet's centre;
        `behind`: seen from the back (the puppet still faces the viewer)."""
        b = self.b
        s = h / 1.1
        with b.at(T(x, y, z0) @ R("Z", yaw) @ S(s)):
            b.lathe(self.m, [(0.0, 0.0), (0.26, 0.0), (0.27, 0.05), (0.17, 0.5), (0.12, 0.7), (0.0, 0.72)], 12, tint=1.0)
            b.lathe(self.m, [(0.0, 0.7), (0.1, 0.74), (0.16, 0.86), (0.13, 0.98), (0.0, 1.04)], 12, tint=1.0)  # head
            if not behind:
                for sx in (-1, 1):
                    b.cyl(self.m, 0.018, 0.02, T(sx * 0.055, -0.15, 0.88) @ R("X", 90), segments=6, tint=1.0)
            if hat == "scarf":
                b.lathe(self.m, [(0.165, 0.88), (0.15, 0.99), (0.08, 1.06), (0.0, 1.08)], 12, tint=1.0)
                b.prism(self.m, [(0.0, 0.0), (0.22, -0.2), (0.1, 0.06)], 0.03, T(0.1, 0.02, 0.95), tint=1.0)
            elif hat == "fox":
                for sx in (-1, 1):
                    b.prism(self.m, [(-0.06, 0.0), (0.06, 0.0), (0.0, 0.17)], 0.05, T(sx * 0.09, -0.02, 0.98), tint=1.0)
            elif hat == "moth":
                for sx in (-1, 1):
                    b.rod(self.m, (sx * 0.05, 0, 1.02), (sx * 0.14, 0, 1.2), 0.012, 4, 1.0)
                    b.lathe(self.m, [(0.0, -0.03), (0.03, 0.0), (0.0, 0.03)], 6, T(sx * 0.14, 0, 1.2), tint=1.0)
            elif hat == "cap":
                b.lathe(self.m, [(0.16, 0.95), (0.1, 1.12), (0.03, 1.3), (0.0, 1.32)], 10, T(0.0, 0, 0) @ R("Y", 8), tint=1.0)
                b.lathe(self.m, [(0.0, -0.04), (0.04, 0.0), (0.0, 0.04)], 6, T(0.07, 0, 1.33), tint=1.0)
            elif hat == "bowler":
                b.lathe(self.m, [(0.24, 0.97), (0.24, 0.99), (0.15, 1.0), (0.14, 1.1), (0.0, 1.15)], 12, tint=1.0)
            elif hat == "witch":
                b.lathe(self.m, [(0.27, 0.97), (0.27, 0.985), (0.13, 1.0), (0.0, 1.36)], 12, tint=1.0)
            elif hat == "ears":
                for sx in (-1, 1):
                    b.lathe(self.m, [(0.0, -0.06), (0.06, 0.0), (0.0, 0.06)], 8, T(sx * 0.13, 0, 1.03), tint=1.0)
            elif hat == "crown":
                b.prism(self.m, [(-0.13, 0.0), (0.13, 0.0), (0.15, 0.14), (0.075, 0.07), (0.0, 0.15), (-0.075, 0.07), (-0.15, 0.14)], 0.02, T(0, -0.13, 0.98), tint=1.0)
            if pole is not None:
                hx = arm * 0.24
                b.rod(self.m, (arm * 0.13, 0, 0.62), (hx, -0.05, 0.56), 0.035, 5, 1.0)
                b.rod(self.m, (hx, -0.05, 0.3), (hx, -0.05, pole - 0.2), 0.016, 5, 1.0)
            else:
                b.rod(self.m, (arm * 0.13, 0, 0.62), (arm * 0.2, -0.02, 0.36), 0.035, 5, 1.0)
            b.rod(self.m, (-arm * 0.13, 0, 0.62), (-arm * 0.21, -0.02, 0.36), 0.035, 5, 1.0)
        if pole is not None and puppet is not None:
            a = math.radians(yaw)
            hx = arm * 0.24 * s
            px, py = x + hx * math.cos(a) + 0.05 * s * math.sin(a), y + hx * math.sin(a) - 0.05 * s * math.cos(a)
            self.puppet(puppet, (px, py - 0.03, z0 + pole * s), 0.5 * s)

    def puppet(self, outline, p, size, tilt=0.0):
        b = self.b
        pts = [(x * size, z * size) for x, z in outline]
        with b.at(T(*p) @ R("Y", tilt)):
            b.prism(self.m, pts, 0.03, T(0, -0.015, 0), tint=1.0)
            if outline is OWL:
                for sx in (-1, 1):
                    b.cyl(self.m, size * 0.11, 0.02, T(sx * size * 0.14, -0.02, size * 0.13) @ R("X", 90), segments=10, tint=1.0)
                    b.cyl(self.m, size * 0.045, 0.02, T(sx * size * 0.14, -0.03, size * 0.13) @ R("X", 90), segments=6, tint=1.0)
            elif outline is FOX:
                for sx in (-1, 1):
                    b.cyl(self.m, size * 0.045, 0.02, T(sx * size * 0.15, -0.02, size * 0.06) @ R("X", 90), segments=6, tint=1.0)
                b.cyl(self.m, size * 0.05, 0.02, T(0, -0.02, -size * 0.42) @ R("X", 90), segments=6, tint=1.0)
            elif outline is MOTH:
                for sx in (-1, 1):
                    b.cyl(self.m, size * 0.08, 0.02, T(sx * size * 0.36, -0.02, size * 0.22) @ R("X", 90), segments=8, tint=1.0)
            elif outline is BELL:
                b.box(self.m, (size * 0.5, 0.02, size * 0.06), (0, -0.02, -size * 0.2), bevel=0, tint=1.0)

    def finish(self):
        return self.b.finish("Page")


def page_1(k):
    """Lanterns all along Lantern Lane."""
    b = k.b
    k.box((40.0, 60.0, 0.1), (0, 20, -0.05))
    for i, (h, roof, w) in enumerate(((3.4, 1.9, 4.2), (4.2, 1.6, 3.8), (3.2, 2.2, 4.4), (4.4, 1.8, 3.6))):
        y = 2.5 + 5.2 * i
        k.house(-5.6 + 0.2 * (i % 2), y, w, 4.2, h, roof, rows=2, cols=2, chimney=(0.9, h + roof * 0.9) if i % 2 == 0 else None)
        h2 = (3.8, 3.3, 4.5, 3.5)[i]
        k.house(5.6 - 0.2 * (i % 2), y + 0.8, w, 4.2, h2, roof * 0.9, rows=2, cols=2, chimney=(-0.9, h2 + roof * 0.8) if i % 2 else None)
    for i, (y, z, n) in enumerate(((1.2, 4.9, 6), (7.5, 5.3, 5))):  # strings of paper lanterns across the lane
        k.string((-3.7, y, z), (3.7, y + 0.4, z + 0.1), 0.55, n, 0.21)
    # the owl booth, freshly painted, on the right, with the crest over its shutter
    with b.at(T(2.9, 0.9, 0) @ R("Z", -24)):
        k.box((1.7, 0.9, 2.3), (0, 0.45, 1.15))
        k.box((1.2, 0.06, 0.75), (0, -0.03, 1.5))
        for i in range(4):
            k.box((1.16, 0.05, 0.035), (0, -0.07, 1.2 + 0.19 * i))
        k.box((1.5, 0.3, 0.07), (0, -0.15, 1.08))
        for i in range(6):
            if i % 2 == 0:
                k.box((1.7 / 6, 0.04, 0.95), (-0.85 + 1.7 / 6 * (i + 0.5), -0.02, 0.5))
        b.prism(k.m, el.scallop_strip(1.9, 0.08, 7, 0.09, 4), 0.06, T(-0.95, -0.1, 2.32), tint=1.0)
        arch = [(-1.0, 2.3)] + [(1.05 * math.sin(math.radians(a)), 2.3 + 0.75 * math.cos(math.radians(a)) ** 0.8) for a in range(-66, 67, 12)] + [(1.0, 2.3)]
        b.prism(k.m, arch[::-1], 0.1, T(0, 0.0, 0), tint=1.0)
        el.crest(b, k.craft, 0.62, T(0, -0.01, 2.66), step=0.014, fine=True)
    # small costumed children watching, left front
    for x, y, h, hat, yaw in ((-2.7, -1.6, 0.9, "witch", 150), (-1.9, -1.0, 0.78, "ears", 165), (-3.3, -0.6, 0.95, "crown", 140), (1.5, -1.5, 0.82, "ears", -160)):
        k.person(x, y, h, hat, yaw=yaw, behind=True)
    # the parade: five puppet carriers going up the lane, seen from behind
    for i, (out, hat) in enumerate(zip(PUPPETS, ("scarf", "fox", "moth", "cap", "bowler"))):
        x = (-1.7, -0.75, 0.25, 1.2, -0.2)[i]
        y = (1.9, 2.5, 2.0, 2.9, 4.6)[i]
        k.person(x, y, 1.3, hat, yaw=180, pole=2.15 + 0.3 * (i % 2), puppet=out, arm=1 if i % 2 else -1, behind=True)
    return (0.0, -6.4, 2.3), (0.15, 6.0, 2.45), 30


def page_2(k):
    """Five friends painting lanterns."""
    b = k.b
    k.box((14.0, 0.2, 5.0), (0, 2.2, 2.5))  # the wall
    k.box((14.0, 8.0, 0.1), (0, -1.5, -0.05))
    k.box((13.8, 0.06, 0.12), (0, 2.08, 0.9))  # dado rail
    for i in range(12):
        k.box((0.02, 0.03, 0.84), (-6.3 + 1.15 * i, 2.09, 0.45))
    # the puppets lean on the wall behind them, as silhouettes on their poles
    for i, out in enumerate(PUPPETS):
        x = -3.4 + 1.7 * i
        tilt = (-9, 7, -5, 8, -7)[i]
        with b.at(T(x, 2.0, 0) @ R("Y", tilt)):
            b.rod(k.m, (0, 0, 0.0), (0, 0, 2.55), 0.022, 5, 1.0)
        a = math.radians(tilt)
        k.puppet(out, (x + 2.9 * math.sin(a), 1.95, 2.9 * math.cos(a)), 0.78, tilt)
    k.string((-5.6, 1.6, 3.95), (5.6, 1.6, 3.95), 0.3, 7, 0.17)
    # the long table
    k.box((6.6, 1.2, 0.08), (0, 0.0, 0.76))
    for sx in (-1, 1):
        k.box((0.1, 0.1, 0.72), (sx * 3.15, -0.5, 0.36))
    b.prism(k.m, [(6.3, 0.0), (0.0, 0.0)] + [(6.3 * i / 18, -0.42 - 0.07 * (i % 2)) for i in range(19)], 0.03, T(-3.15, -0.62, 0.74), tint=1.0)  # the cloth
    # five friends behind the table
    for i, hat in enumerate(("scarf", "fox", "moth", "cap", "bowler")):
        x = -2.6 + 1.3 * i
        k.person(x, 0.8, 1.62, hat, yaw=(8, -6, 0, 5, -9)[i], arm=1 if i % 2 else -1)
    # on the table: lanterns being painted, pots, brushes
    for x, y, r in ((-1.95, -0.05, 0.17), (-0.65, 0.0, 0.19), (0.65, -0.1, 0.16), (1.95, 0.0, 0.18), (3.0, -0.2, 0.14)):
        k.lantern((x, y, 0.8 + r * 1.02), r)
    for x, y in ((-1.6, -0.3), (-0.2, -0.32), (1.1, -0.3), (2.2, -0.36), (-2.9, -0.3)):
        b.lathe(k.m, [(0.0, 0.0), (0.07, 0.0), (0.08, 0.11), (0.07, 0.12), (0.0, 0.12)], 10, T(x, y, 0.8), tint=1.0)
        b.rod(k.m, (x + 0.02, y, 0.85), (x + 0.16, y - 0.03, 1.1), 0.012, 4, 1.0)
    for x, y, rz in ((-1.2, -0.42, 20), (0.8, -0.44, -35), (1.6, -0.4, 60)):
        with b.at(T(x, y, 0.815) @ R("Z", rz)):
            b.rod(k.m, (-0.14, 0, 0), (0.14, 0, 0), 0.011, 4, 1.0)
    for x in (-2.75, 2.75):  # stools at the ends
        b.lathe(k.m, [(0.0, 0.5), (0.2, 0.5), (0.21, 0.54), (0.0, 0.55)], 10, T(x * 1.3, -0.3, 0), tint=1.0)
        for a in (30, 150, 270):
            b.rod(k.m, (x * 1.3 + 0.17 * math.cos(math.radians(a)), -0.3 + 0.17 * math.sin(math.radians(a)), 0),
                  (x * 1.3 + 0.1 * math.cos(math.radians(a)), -0.3 + 0.1 * math.sin(math.radians(a)), 0.5), 0.02, 5, 1.0)
    return (0.0, -7.6, 2.5), (0.0, 0.6, 1.95), 36


def page_3(k):
    """Tall houses, and a moon on a chimney."""
    b = k.b
    k.box((40.0, 40.0, 0.1), (0, 5, -0.05))
    specs = ((-6.3, 2.3, 6.2, 2.0, 3), (-3.9, 2.1, 7.6, 2.4, 4), (-1.6, 2.4, 6.6, 1.8, 3), (0.9, 2.2, 8.4, 2.6, 4), (3.3, 2.3, 7.0, 2.1, 3), (5.7, 2.2, 6.0, 2.3, 3))
    tops = {}
    for i, (x, w, h, roof, rows) in enumerate(specs):
        chim = (0.55 if i % 2 else -0.55, h + roof * 0.5 + 0.9)
        k.house(x, 0.2 * (i % 2), w, 3.6, h, roof, rows=rows, cols=2, door=True, chimney=chim)
        tops[i] = (x + chim[0], 0.2 * (i % 2) + 1.8, chim[1] + 0.06)
        if i in (0, 2, 4):  # roof lamps on little brackets by the gable
            lx, lz = x - w / 2 - 0.02, h + 0.35
            b.rod(k.m, (lx + 0.3, -0.2, h - 0.2), (lx + 0.3, -0.2, lz + 0.5), 0.025, 5, 1.0)
            b.rod(k.m, (lx + 0.3, -0.2, lz + 0.5), (lx - 0.12, -0.2, lz + 0.5), 0.02, 5, 1.0)
            k.lantern((lx - 0.12, -0.2, lz + 0.2), 0.17)
    # washing lines between the windows, with the washing on them
    for (x0, z0), (x1, z1), n in (((-5.3, 3.3), (-2.8, 3.9), 4), ((-0.5, 4.6), (2.0, 4.1), 4), ((2.0, 2.7), (4.4, 3.1), 3), ((-2.8, 2.2), (-0.5, 2.5), 3)):
        p0, p1 = (x0, -0.5, z0), (x1, -0.5, z1)
        pts = el.catenary(p0, p1, 0.22, 10)
        for a, c in zip(pts, pts[1:]):
            b.rod(k.m, a, c, 0.011, 4, 1.0)
        for j in range(n):
            t = (j + 0.6) / (n + 0.2)
            p = Vector(p0).lerp(Vector(p1), t) - Vector((0, 0, 0.22 * 4 * t * (1 - t)))
            if j % 2 == 0:  # a shirt
                shirt = [(-0.16, 0.0), (0.16, 0.0), (0.3, -0.14), (0.22, -0.24), (0.15, -0.16), (0.15, -0.5), (-0.15, -0.5), (-0.15, -0.16), (-0.22, -0.24), (-0.3, -0.14)]
                b.prism(k.m, shirt, 0.02, T(p.x, p.y - 0.02, p.z), tint=1.0)
            else:  # a sock or a cloth
                b.prism(k.m, [(-0.12, 0.0), (0.12, 0.0), (0.12, -0.36), (-0.12, -0.36)], 0.02, T(p.x, p.y - 0.02, p.z), tint=1.0)
    # the crescent-moon puppet sits on the tallest chimney
    x3, w3, h3 = specs[3][0], specs[3][1], specs[3][2]
    cx, cy, cz = x3 + 0.72, 0.5, h3 + 1.75
    k.box((0.5, 0.5, 1.9), (cx, cy, cz - 0.95))
    k.box((0.66, 0.66, 0.13), (cx, cy, cz))
    b.rod(k.m, (cx, cy - 0.15, cz), (cx, cy - 0.15, cz + 0.5), 0.03, 5, 1.0)
    k.puppet(MOON, (cx + 0.05, cy - 0.15, cz + 1.2), 1.6, -14)
    for sx in (-1, 1):  # its little legs dangle over the chimney's edge
        b.rod(k.m, (cx + sx * 0.14, cy - 0.32, cz + 0.02), (cx + sx * 0.16, cy - 0.36, cz - 0.42), 0.03, 5, 1.0)
        b.lathe(k.m, [(0.0, -0.05), (0.06, 0.0), (0.0, 0.05)], 6, T(cx + sx * 0.16, cy - 0.4, cz - 0.45), tint=1.0)
    for x, z in ((-6.6, 10.6), (-2.6, 11.4), (3.9, 11.0), (6.6, 9.6), (-5.0, 9.4)):  # stars
        b.prism(k.m, el.co.star_outline(0.2), 0.02, T(x, 6.0, z), tint=1.0)
    return (-1.2, -17.5, 1.5), (0.1, 0.0, 6.1), 40


PAGES = (page_1, page_2, page_3)


# --- render and pencil -------------------------------------------------------------------------------------------

def _flat(name, rgb, emit):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    if emit:
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    return mat


def _render(path):
    scene = bpy.context.scene
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H * SS, W * SS, 4)
    bpy.data.images.remove(img)
    os.remove(path)
    return px


def _down(a):
    return a.reshape(H, SS, W, SS, -1).mean(axis=(1, 3))


def _noise(rng, cell, octaves=3):
    """Smooth value noise in 0..1 on the page."""
    out = np.zeros((H, W), dtype=np.float32)
    amp, total = 1.0, 0.0
    for _ in range(octaves):
        gh, gw = H // cell + 3, W // cell + 3
        g = rng.random((gh, gw), dtype=np.float32)
        ys, xs = np.arange(H) / cell, np.arange(W) / cell
        y0, x0 = ys.astype(int), xs.astype(int)
        fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        v = (g[y0][:, x0] * (1 - fy) * (1 - fx) + g[y0 + 1][:, x0] * fy * (1 - fx) + g[y0][:, x0 + 1] * (1 - fy) * fx + g[y0 + 1][:, x0 + 1] * fy * fx)
        out += v * amp
        total += amp
        amp *= 0.5
        cell = max(1, cell // 2)
    return out / total


def draw_page(index, tmp):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.0)
    k = Kit(craft)
    eye, target, lens = PAGES[index](k)
    obj = k.finish()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = W * SS, H * SS
    scene.render.film_transparent = True
    scene.eevee.taa_render_samples = 16
    scene.eevee.use_gtao, scene.eevee.gtao_distance, scene.eevee.gtao_factor = True, 1.2, 1.4
    scene.eevee.use_soft_shadows = True
    scene.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens, cam.data.clip_end = lens, 300.0
    scene.collection.objects.link(cam)
    cam.location = eye
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.22
    scene.world = world
    el.sun_light("Sun", (0.9, 1.6, -1.5), 1.5, (1, 1, 1), 4.0, 80.0)

    def paint(mat):
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        obj.data.polygons.foreach_set("material_index", np.zeros(len(obj.data.polygons), dtype=np.int32))
    for m in list(obj.modifiers):
        obj.modifiers.remove(m)
    # pass 1: shade (grey clay under one sun)
    paint(_flat("Clay", (0.8, 0.8, 0.8), False))
    scene.render.use_freestyle = False
    shade = _render(tmp + "_shade.png")
    # pass 2: the lines (Freestyle on flat white)
    paint(_flat("White", (1, 1, 1), True))
    scene.render.use_freestyle = True
    scene.render.line_thickness = 1.15 * SS
    vl = bpy.context.view_layer
    vl.use_freestyle = True
    fs = vl.freestyle_settings
    fs.crease_angle = math.radians(128)
    ls = fs.linesets[0] if fs.linesets else fs.linesets.new("Pencil")
    ls.select_silhouette = ls.select_border = ls.select_crease = True
    style = bpy.data.linestyles.new("Pencil")
    ls.linestyle = style
    style.color = (0, 0, 0)
    style.thickness = 1.15 * SS
    wob = style.geometry_modifiers.new("Wobble", "PERLIN_NOISE_1D")
    wob.frequency, wob.amplitude, wob.octaves = 9.0, 1.5 * SS, 3
    over = style.geometry_modifiers.new("Overshoot", "BACKBONE_STRETCHER")
    over.backbone_length = 4.5 * SS
    lines = _render(tmp + "_lines.png")
    log(f"page {index + 1}: rendered ({len(obj.data.polygons)} faces)")
    # --- pencil ---
    rng = np.random.default_rng(100 + index)
    la = _down(lines)
    line = np.clip((1.0 - la[:, :, :3].mean(axis=2)) * la[:, :, 3] * 1.5, 0, 1)
    sh = _down(shade)
    covered = sh[:, :, 3]
    lum = np.where(covered > 0.05, sh[:, :, :3].mean(axis=2) / np.maximum(covered, 1e-4), 1.0)
    lum = np.clip(lum / max(float(np.percentile(lum[covered > 0.5], 96)) if (covered > 0.5).any() else 1.0, 1e-3), 0, 1)
    log(f"page {index + 1}: shade quartiles {np.percentile(lum[covered > 0.5], [10, 25, 50, 75]).round(2)}")
    grain = 0.55 + 0.45 * rng.random((H, W), dtype=np.float32)  # graphite catches the paper's tooth
    tooth = 0.75 + 0.25 * _noise(rng, 3, 1)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    drift = _noise(rng, 60, 2) * 22.0
    h1 = (np.sin((xx + yy + drift) * (2 * math.pi / 6.5)) > 0.35).astype(np.float32)
    h2 = (np.sin((xx - yy * 0.8 + drift * 1.3) * (2 * math.pi / 5.5)) > 0.45).astype(np.float32)
    patch = np.clip(_noise(rng, 26, 2) * 1.7 - 0.25, 0, 1)  # hatching comes in patches, as a hand lays it
    hatch = (h1 * np.clip((0.74 - lum) / 0.2, 0, 1) * 0.42 + h2 * np.clip((0.46 - lum) / 0.2, 0, 1) * 0.4) * covered * (0.45 + 0.55 * patch)
    press = 0.74 + 0.26 * _noise(rng, 34, 2)  # the hand presses harder here and there
    ink = np.clip(line * press * (0.8 + 0.2 * tooth) * grain ** 0.5 + hatch * grain * tooth, 0, 1) * 0.92
    # old cream paper: fibres, a few stains, the ten folds, darker toward the edge
    fibre = _noise(rng, 2, 1) * 0.035 + _noise(rng, 90, 3) * 0.07
    paper = np.array(PAPER, dtype=np.float32)[None, None, :] * (0.94 + fibre)[:, :, None]
    paper[:, :, 2] *= 0.97 + 0.03 * _noise(rng, 140, 2)
    stain = np.clip(_noise(rng, 110, 2) * 2.4 - 1.45, 0, 1)
    paper *= (1.0 - 0.1 * stain)[:, :, None] * np.array([1.0, 0.97, 0.9], dtype=np.float32)[None, None, :] ** stain[:, :, None]
    crease = np.zeros((H, W), dtype=np.float32)
    for i in range(1, FOLDS[0]):
        x = W * i / FOLDS[0] + (_noise(rng, 80, 1)[:, 0] - 0.5) * 5.0
        d = np.abs(xx - x[:, None])
        crease += np.exp(-(d / 1.3) ** 2) - 0.55 * np.exp(-((d - 2.6) / 1.6) ** 2)
    for j in range(1, FOLDS[1]):
        y = H * j / FOLDS[1] + (_noise(rng, 80, 1)[0, :] - 0.5) * 5.0
        d = np.abs(yy - y[None, :])
        crease += np.exp(-(d / 1.3) ** 2) - 0.55 * np.exp(-((d - 2.6) / 1.6) ** 2)
    paper *= (1.0 - 0.085 * np.clip(crease, -1, 1))[:, :, None]
    cells = rng.uniform(0.975, 1.02, size=(FOLDS[1], FOLDS[0])).astype(np.float32)  # each piece aged a little differently
    paper *= np.kron(cells, np.ones((H // FOLDS[1], W // FOLDS[0] + 1), dtype=np.float32))[:H, :W, None]
    edge = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy))
    paper *= (1.0 - 0.1 * np.exp(-edge / 38.0))[:, :, None]
    rub = 1.0 - 0.3 * np.clip(crease, 0, 1)  # graphite has worn off along the folds
    ink = (ink * rub)[:, :, None]
    rgb = paper * (1 - ink) + np.array(GRAPHITE, dtype=np.float32)[None, None, :] * ink
    ragged = edge + (_noise(rng, 9, 2) - 0.5) * 9.0 + (_noise(rng, 40, 1) - 0.5) * 6.0
    alpha = np.clip((ragged - 5.0) / 1.6, 0, 1)
    return np.concatenate([np.clip(rgb, 0, 1), alpha[:, :, None]], axis=2).astype(np.float32)


def save(px, path):
    img = bpy.data.images.new("Page", px.shape[1], px.shape[0], alpha=True)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw, img.file_format = path, "PNG"
    img.save()
    bpy.data.images.remove(img)


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = flags[0] if flags else OUT_DIR
    only = [int(i) - 1 for i in opts["only"].split(",")] if "only" in opts else range(len(PAGES))
    os.makedirs(out_dir, exist_ok=True)
    pages = []
    for i in only:
        px = draw_page(i, os.path.join(out_dir, f"_tmp_{i}"))
        save(px, os.path.join(out_dir, f"sketch_page_{i + 1}.png"))
        pages.append(px)
        log(f"page {i + 1} -> {out_dir}")
    if "sheet" in opts:  # the pages on the notebook's night violet, full size and at the size of a notebook page on a phone
        bg = np.array([0.165, 0.145, 0.31, 1.0], dtype=np.float32)
        tiles = []
        for px in pages:
            for scale in (1, 3):
                small = px[::scale, ::scale] if scale > 1 else px
                if scale > 1:
                    small = px[:H - H % scale, :W - W % scale].reshape(H // scale, scale, W // scale, scale, 4).mean(axis=(1, 3))
                tile = np.empty((small.shape[0] + 40, small.shape[1] + 40, 4), dtype=np.float32)
                tile[:] = bg
                a = small[:, :, 3:4]
                tile[20:-20, 20:-20, :3] = small[:, :, :3] * a + bg[:3] * (1 - a)
                tiles.append(tile)
        width = max(t.shape[1] for t in tiles[::2]) + max(t.shape[1] for t in tiles[1::2])
        rows = []
        for big, small in zip(tiles[::2], tiles[1::2]):
            row = np.empty((big.shape[0], width, 4), dtype=np.float32)
            row[:] = bg
            row[:, :big.shape[1]] = big
            y0 = (big.shape[0] - small.shape[0]) // 2
            row[y0:y0 + small.shape[0], big.shape[1]:big.shape[1] + small.shape[1]] = small
            rows.append(row)
        save(np.concatenate(rows[::-1], axis=0), opts["sheet"])
        log("sheet ->", opts["sheet"])


if __name__ == "__main__":
    main()
