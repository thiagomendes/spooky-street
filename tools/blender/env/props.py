"""Hero and dressing props for Lantern Lane (spec §4): bench, candy bowl, fence and gate, hedge, street lamp,
party table, paper bat, crate, awning, plank bridge, maple and treehouse base, pumpkins, mailbox, plus the porch
dressing (bunting, bat garland, hanging lamp). Each `build_*` adds to a Builder at the origin, on the ground,
facing -Y.

Run: blender -b --python tools/blender/env/props.py -- out.png [draft|final]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

import env_lib as el
import porch_kit as pk
from env_lib import E, R, S, T, V, Builder, H, arc, catenary, grouped, lc, shade

BAT = el.co.bat_outline(1.0)  # 1.24 units wide
BUNTING = ("orange", "purple", "candy", "lime", "berry")
DETAIL = 1  # 2 for a prop that fills the frame (the key frame's foreground bowl)
JACK_GLOW = 1.0  # scene-wide scale of carved-face emission
LAMP_GLOW = 1.0  # scene-wide scale of lantern-glass emission


# --- pumpkins and candy ------------------------------------------------------------------------------------

def _pumpkin_shell(r, squash, lobes, cut=None):
    hz = r * squash
    top = 90.0 if cut is None else math.degrees(math.asin(cut))
    n = 10
    prof = [(0.0, -hz * 0.9)]
    for i in range(1, n + 1):
        phi = math.radians(-90 + (top + 90) * i / n)
        if phi >= math.radians(89):
            break
        prof.append((r * math.cos(phi), hz * math.sin(phi)))

    def radial(a, i=0):
        return 1 - 0.1 * (1 - abs(math.cos(lobes * a / 2))) ** 0.8

    def surf(dx, dz, lift=1.035):
        phi = math.asin(max(-0.95, min(0.95, dz / hz)))
        a = -math.pi / 2 + dx / (r * math.cos(phi))
        rad = r * math.cos(phi) * radial(a) * lift
        return (rad * math.cos(a), rad * math.sin(a), dz)
    return hz, prof, radial, surf


def _carve(b, glow, surf, r, hz, style):
    """Carved face as glowing cut-outs wrapped onto the shell."""
    def fan(pts):
        cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
        for i in range(len(pts)):
            b.poly(glow, [surf(cx, cz), surf(*pts[i]), surf(*pts[(i + 1) % len(pts)])], tint=1.0, smooth=True)

    e = 0.2 * r
    for s in (1, -1):
        cx, cz = s * 0.36 * r, 0.2 * hz
        if style % 3 == 0:
            fan([(cx - e, cz - e * 0.6), (cx + e, cz - e * 0.6), (cx + s * e * 0.3, cz + e)])
        elif style % 3 == 1:
            fan([(cx + e * math.cos(t), cz + e * 0.85 * math.sin(t)) for t in (math.radians(45 * k) for k in range(8))])
        else:
            fan([(cx - s * e, cz - e * 0.2), (cx + s * e, cz + e * 0.9), (cx + s * e * 0.9, cz - e * 0.5)])
    fan([(-0.07 * r, -0.08 * hz), (0.07 * r, -0.08 * hz), (0.0, 0.12 * hz)])
    n = 10
    teeth = (0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.0) if style % 2 else (0.0, 0.09, 0.0, 0.09, 0.0, 0.09, 0.0, 0.09, 0.0, 0.09, 0.0)
    xs = [(-0.62 + 1.24 * i / n) * r for i in range(n + 1)]

    def smile(x):
        return (-0.5 + 0.34 * (x / (0.62 * r)) ** 2) * hz
    up = [(x, smile(x) + (0.2 - teeth[i]) * hz * (1 - (x / (0.66 * r)) ** 2)) for i, x in enumerate(xs)]
    dn = [(x, smile(x) - 0.02 * hz + (teeth[(i + 5) % 11] * 0.9) * hz * (1 - (x / (0.66 * r)) ** 2) * (i % 2)) for i, x in enumerate(xs)]
    for i in range(n):
        b.poly(glow, [surf(*dn[i]), surf(*dn[i + 1]), surf(*up[i + 1]), surf(*up[i])], tint=1.0, smooth=True)


def pumpkin(b, craft, r, squash=0.8, M=None, style=0, rgb=None, lobes=8, glow=2.6, stem=True, per_lobe=3):
    """Jack-o'-lantern (style None = uncarved). Sits on the ground at the local origin."""
    hz, prof, radial, surf = _pumpkin_shell(r, squash, lobes)
    mat = craft.tinted(rgb or E["orange"], 0.6, 0.2, 6.0, sheen=0.25)
    with b.at((M if M is not None else T()) @ T(0, 0, hz * 0.92)):
        b.lathe(mat, prof + [(0.0, hz * 0.84)], lobes * per_lobe, radial=radial, tint=b.vary(0.92, 1.06))
        if style is not None:
            _carve(b, craft.glow(H(0xFFC64D), glow * JACK_GLOW), surf, r, hz, style)
        if stem:
            green = lc(H(0x5E7A32))
            p0, p1 = Vector((0, 0, hz * 0.8)), Vector((0.03 * r, 0.02 * r, hz * 0.8 + 0.3 * r))
            p2 = p1 + Vector((0.16 * r, 0.05 * r, 0.12 * r))
            b.rod(craft.paint(), p0, p1, 0.1 * r, 7, green, r2=0.065 * r)
            b.rod(craft.paint(), p1, p2, 0.068 * r, 7, shade(green, 0.85), r2=0.05 * r)


def candy(b, craft, kind, M, a, c, s=1.0):
    """One sweet at the local origin: 0 wrapped with twisted ends, 1 round lollipop, 2 candy corn, 3 gumball,
    4 foil-wrapped drop. All lathed and smooth-shaded; DETAIL sets the facet count."""
    paint = craft.paint()
    seg, rings = int(round(8 * DETAIL)), int(round(4 * DETAIL))

    def ball(rx, rz):
        return [(rx * math.sin(math.pi * i / rings) if 0 < i < rings else 0.0, -rz * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
    with b.at(M):
        if kind == 0:
            b.lathe(paint, ball(0.021 * s, 0.03 * s), seg, tint=lc(a))
            wrap = lc(el.blend(c, (1, 1, 1), 0.35))
            twist = [(0.008 * s, 0.024 * s), (0.0055 * s, 0.031 * s), (0.013 * s, 0.043 * s), (0.022 * s, 0.056 * s)]
            for e in (1, -1):
                b.lathe(paint, twist, seg, R("X", 180) if e < 0 else None, tint=wrap,
                        radial=lambda ang, i: 1 + (0.24 if i >= 2 else 0.0) * math.cos(seg / 2 * ang))
        elif kind == 1:
            with b.at(T(0, 0, 0.125 * s) @ R("X", 90)):
                b.lathe(paint, ball(0.043 * s, 0.011 * s), seg * 2, tint=lc(a))
                b.lathe(paint, ball(0.028 * s, 0.0135 * s), seg * 2, tint=lc(c))
                b.lathe(paint, ball(0.014 * s, 0.0155 * s), seg, tint=lc(a))
            b.rod(paint, (0, 0, -0.04 * s), (0, 0, 0.09 * s), 0.004 * s, 6, lc(V["bone"]))
        elif kind == 2:
            with b.at(S(1.0, 0.62, 1.0)):
                b.lathe(paint, [(0.0, 0.0), (0.016 * s, 0.001 * s), (0.022 * s, 0.008 * s), (0.0195 * s, 0.021 * s)], seg, tint=lc(V["candy"]))
                b.lathe(paint, [(0.0195 * s, 0.021 * s), (0.013 * s, 0.041 * s)], seg, tint=lc(V["orange"]))
                b.lathe(paint, [(0.013 * s, 0.041 * s), (0.007 * s, 0.054 * s), (0.003 * s, 0.06 * s), (0.0, 0.061 * s)], seg, tint=lc(V["white"]))
        elif kind == 3:
            b.lathe(paint, ball(0.024 * s, 0.024 * s), seg, tint=lc(a))
        else:
            b.lathe(craft.foil(), [(0.0, 0.0), (0.019 * s, 0.002 * s), (0.025 * s, 0.012 * s), (0.019 * s, 0.027 * s), (0.007 * s, 0.04 * s),
                                   (0.004 * s, 0.05 * s), (0.0, 0.052 * s)], seg, tint=lc(a),
                    radial=lambda ang, i: 1 + 0.05 * math.cos(seg / 2 * ang))
            b.poly(craft.paper_white(), [(0.0, 0.0, 0.048 * s), (0.03 * s, 0.004 * s, 0.062 * s), (0.032 * s, 0.004 * s, 0.054 * s)], tint=lc(V["bone"]))


SWEETS = ("orange", "purple", "lime", "candy", "berry", "bone")


def candy_heap(b, craft, radius, height, count, s=1.0, lollies=2):
    rng = b.rng
    foils = (H(0xE9B83A), H(0x9B5BE0), H(0x58C06A), H(0xE8527A))
    for k in range(count):
        rho = math.sqrt(rng.random()) * radius * 0.86
        ang = rng.uniform(0, 2 * math.pi)
        z = height * (1 - (rho / radius) ** 2) + 0.012 * s
        m = T(rho * math.cos(ang), rho * math.sin(ang), z) @ R("Z", rng.uniform(0, 360)) @ R("X", rng.uniform(60, 120))
        kind = rng.choice((0, 0, 0, 0, 2, 3, 4, 4))
        a, c = V[rng.choice(SWEETS)], V[rng.choice(SWEETS)]
        if kind == 4:
            a = rng.choice(foils)
        candy(b, craft, kind, m if kind not in (2, 4) else m @ T(0, 0, -0.03 * s), a, c, s)
    for k in range(lollies):
        ang = 2.2 + 2.6 * k
        m = T(radius * 0.45 * math.cos(ang), radius * 0.45 * math.sin(ang), height * 0.75) @ R("Y", 22 * (1 if k else -1)) @ R("Z", 15 + 40 * k)
        candy(b, craft, 1, m, V[("berry", "lime")[k % 2]], V[("bone", "candy")[k % 2]], s * 1.25)


def candy_bowl(b, craft, r=0.3, M=None, spill=True, heap=58, glow=2.4):
    """The candy bowl: a big carved pumpkin with its top cut off, heaped with sweets."""
    cut = 0.42
    hz, prof, radial, surf = _pumpkin_shell(r, 0.8, 9, cut)
    rim_r, rim_z = prof[-1]
    k = r / 0.3
    mat = craft.tinted(E["orange"], 0.6, 0.2, 6.0, sheen=0.25)
    with b.at((M if M is not None else T())):
        with b.at(T(0, 0, hz * 0.92)):
            b.lathe(mat, prof + [(rim_r - 0.012 * k, rim_z + 0.016 * k), (rim_r - 0.04 * k, rim_z + 0.012 * k),
                                 (rim_r - 0.05 * k, rim_z - 0.03 * k)], 9 * int(round(3 * DETAIL)), radial=radial, tint=1.0)
            b.lathe(craft.tinted(H(0x7A2A12), 0.8, 0.1), [(rim_r - 0.045 * k, rim_z - 0.02 * k), (rim_r * 0.62, rim_z + 0.055 * k),
                                                          (0.0, rim_z + 0.1 * k)], 18, tint=1.0)
            _carve(b, craft.glow(H(0xFFC64D), glow * JACK_GLOW), surf, r, hz, 3)
            with b.at(T(0, 0, rim_z - 0.01 * k)):
                candy_heap(b, craft, rim_r - 0.05 * k, 0.15 * k, heap, 1.3 * k ** 0.5)
        if spill:
            paint = craft.paint()
            for x, y, kind, a, rz in ((0.36, -0.2, 0, "purple", 30), (-0.38, -0.12, 0, "lime", -50), (0.28, -0.33, 3, "berry", 0),
                                      (-0.22, -0.36, 2, "candy", 20)):
                m = T(x * k, y * k, 0.027 if kind != 2 else 0.02) @ R("Z", rz) @ (R("X", 90) if kind != 3 else T())
                candy(b, craft, kind, m @ (T(0, 0, -0.03) if kind == 2 else T()), V[a], V["bone"], 1.25)
    return hz


def build_candy_bowl(craft, r=0.3, spill=True, seed=3):
    global DETAIL
    b = Builder(seed)
    DETAIL = 1.5  # the hero bowl; dressing bowls stay at 1
    hz = candy_bowl(b, craft, r, None, spill)
    DETAIL = 1
    b.finish("CandyBowl")
    el.point_light("CandyBowlGlow", (0, -r * 1.25, hz * 0.8), 2.5, (1.0, 0.62, 0.25), 0.06, shadow=False)


def build_pumpkins(craft):
    """Three sizes: small plain, medium ghost-white, large carved."""
    b = Builder(5)
    pumpkin(b, craft, 0.34, 0.82, T(0.12, 0.1, 0), style=0, lobes=9)
    pumpkin(b, craft, 0.22, 0.74, T(-0.52, -0.12, 0) @ R("Z", -14), style=1, rgb=H(0xF0E3C4), glow=1.6)
    pumpkin(b, craft, 0.13, 0.9, T(0.56, -0.3, 0) @ R("Z", 20), style=None, rgb=H(0xF59A23), lobes=7)
    b.finish("Pumpkins")
    el.point_light("PumpkinGlow", (0.12, -0.5, 0.25), 3.0, (1.0, 0.62, 0.25), 0.06, shadow=False)
    el.point_light("PumpkinGlowB", (-0.55, -0.5, 0.12), 0.4, (1.0, 0.7, 0.35), 0.05, shadow=False)


# --- paper dressing ----------------------------------------------------------------------------------------

def bat(b, paper, span, M, tint=None, fold=0.45, curl=0.35):
    k = span / 1.24
    b.paper(paper, [(x * k, z * k) for x, z in BAT], M, lc(E["ink"]) if tint is None else tint, fold=fold, curl=curl, cuts=1)


def bat_garland(b, craft, p0, p1, sag=0.25, count=6, span=0.24, n=14):
    """Paper bats pegged to a sagging string."""
    paper = craft.paper_white()
    pts = catenary(p0, p1, sag, n)
    for a, c in zip(pts, pts[1:]):
        b.rod(craft.paint(), a, c, 0.006, 5, lc(E["bone_dim"]))
    d = (Vector(p1) - Vector(p0))
    yaw = math.degrees(math.atan2(d.y, d.x))
    for i in range(count):
        t = (i + 0.5) / count
        pos = Vector(p0).lerp(Vector(p1), t) - Vector((0, 0, sag * 4 * t * (1 - t)))
        slope = math.degrees(math.atan2((Vector(p1) - Vector(p0)).z + sag * 4 * (2 * t - 1), max(0.01, math.hypot(d.x, d.y))))
        m = T(*pos) @ R("Z", yaw) @ R("Y", -slope + b.rng.uniform(-10, 10)) @ R("X", b.rng.uniform(-14, 14)) @ T(0, 0, -span * 0.16)
        bat(b, paper, span * b.rng.uniform(0.85, 1.15), m)
        b.box(craft.paint(), (0.012, 0.016, 0.04), (0, 0, span * 0.14), bevel=0, tint=lc(E["timber"]), M=m)


def bunting(b, craft, p0, p1, sag=0.2, count=9, w=0.2, h=0.27, n=14, start=0):
    """Fringed tissue-paper party bunting: each flag's lower half is cut into strips that curl."""
    paper = craft.paper_white()
    pts = catenary(p0, p1, sag, n)
    for a, c in zip(pts, pts[1:]):
        b.rod(craft.paint(), a, c, 0.007, 5, lc(E["bone_dim"]))
    d = Vector(p1) - Vector(p0)
    yaw = math.degrees(math.atan2(d.y, d.x))
    for i in range(count):
        t = (i + 0.5) / count
        pos = Vector(p0).lerp(Vector(p1), t) - Vector((0, 0, sag * 4 * t * (1 - t)))
        col = lc(V[BUNTING[(i + start) % len(BUNTING)]])
        with b.at(T(*pos) @ R("Z", yaw) @ R("X", b.rng.uniform(-12, 12))):
            top = h * 0.42
            b.poly(paper, [(-w / 2, 0, 0.012), (w / 2, 0, 0.012), (w / 2, 0, -top), (-w / 2, 0, -top)], tint=col)
            strips = 5
            sw = w / strips
            for k in range(strips):
                x0, x1 = -w / 2 + k * sw + 0.004, -w / 2 + (k + 1) * sw - 0.004
                c1, c2 = b.rng.uniform(-0.02, 0.02), b.rng.uniform(-0.06, 0.05)
                zm, zb = -top - (h - top) * 0.55, -h + b.rng.uniform(-0.015, 0.015)
                b.poly(paper, [(x0, 0, -top), (x1, 0, -top), (x1, c1, zm), (x0, c1, zm)], tint=shade(col, 0.92))
                b.poly(paper, [(x0, c1, zm), (x1, c1, zm), (x1 - 0.004, c2, zb), (x0 + 0.004, c2, zb)], tint=shade(col, 0.84))


def hanging_lamp(b, craft, drop=0.35, s=1.0, glow=2.4):
    """Porch lantern on a chain; local origin is the ceiling hook. Returns the lamp centre (local)."""
    ink, paint = craft.tinted(E["ink"], 0.45, 0.1), craft.paint()
    b.lathe(ink, [(0.0, 0.0), (0.035, 0.0), (0.03, -0.02), (0.0, -0.03)], 8, tint=1.0)
    links = max(2, int(drop / 0.06))
    for i in range(links):
        z = -0.02 - (i + 0.5) * drop / links
        b.box(ink, (0.022 if i % 2 else 0.008, 0.008 if i % 2 else 0.022, drop / links + 0.008), (0, 0, z), bevel=0.003, tint=1.0)
    z0 = -drop - 0.02
    with b.at(T(0, 0, z0) @ S(s)):
        b.lathe(ink, [(0.0, 0.0), (0.03, -0.01), (0.05, -0.05), (0.15, -0.1), (0.16, -0.115), (0.11, -0.12)], 6, tint=1.0)
        b.lathe(craft.glow(E["glow"] if LAMP_GLOW >= 1 else E["amber"], glow * LAMP_GLOW), [(0.105, -0.115), (0.12, -0.2), (0.115, -0.3), (0.085, -0.36)], 6, tint=1.0)
        for k in range(6):
            a = math.radians(60 * k)
            b.rod(ink, (0.108 * math.cos(a), 0.108 * math.sin(a), -0.115), (0.124 * math.cos(a), 0.124 * math.sin(a), -0.2), 0.008, 4, 1.0)
            b.rod(ink, (0.124 * math.cos(a), 0.124 * math.sin(a), -0.2), (0.089 * math.cos(a), 0.089 * math.sin(a), -0.36), 0.008, 4, 1.0)
        b.lathe(ink, [(0.095, -0.35), (0.1, -0.37), (0.05, -0.4), (0.02, -0.41), (0.025, -0.44), (0.0, -0.46)], 6, tint=1.0)
        b.lathe(paint, [(0.125, -0.19), (0.132, -0.2), (0.125, -0.21)], 6, tint=lc(E["brass"]))
    return (0, 0, z0 - 0.24 * s)


def build_dressing(craft):
    """Porch dressing on a little display frame: bunting, bat garland, hanging lamp."""
    b = Builder(8)
    P = pk.paints(craft)
    for sx in (-1, 1):
        with b.at(T(sx * 1.2, 0, 0)):
            pk.post(b, P, 2.0, (-sx,))
    b.box(P["trim_x"], (2.6, 0.12, 0.12), (0, 0, 2.06), bevel=0.02)
    bunting(b, craft, (-1.14, -0.07, 1.96), (1.14, -0.07, 1.96), 0.22, 10)
    bat_garland(b, craft, (-1.16, 0.0, 1.25), (1.16, 0.0, 1.1), 0.28, 6, 0.26)
    with b.at(T(0.0, 0, 2.0)):
        c = hanging_lamp(b, craft, 0.3)
    b.finish("Dressing")
    el.point_light("DressingLamp", (c[0], c[1] - 0.25, 2.0 + c[2]), 12, (1.0, 0.72, 0.4), 0.1, shadow=False)


def build_paper_bat(craft):
    """The collectible: a folded black-paper bat with a thin glowing candy-yellow edge and pin-prick eyes,
    shown on a wire stand (in game it flutters free)."""
    b = Builder(9)
    P = pk.paints(craft)
    b.lathe(P["shutter"], [(0.0, 0.0), (0.1, 0.0), (0.1, 0.02), (0.07, 0.035), (0.0, 0.035)], 14, T(0, 0.13, 0), tint=1.0)
    wire = [(0.0, 0.13, 0.03), (0.0, 0.14, 0.2), (0.0, 0.12, 0.32), (0.0, 0.1, 0.39)]
    for a, c in zip(wire, wire[1:]):
        b.rod(craft.paint(), a, c, 0.004, 5, lc(E["bone_dim"]))

    def crease(x, z):  # accordion folds across each wing
        return 0.012 * math.cos(abs(x) * 60.0)
    k = 0.34 / 1.24
    out = [(x * k, z * k) for x, z in BAT]
    m = T(0, 0.085, 0.4) @ R("X", -8)
    b.paper(craft.paper_white(), out, m, lc(H(0x231D30)), fold=0.5, curl=0.5, cuts=2, crease=crease)
    rim = [(x * 1.07, z * 1.1 + 0.001) for x, z in out]  # the same fold, a hair bigger, just behind: a lit edge
    b.paper(craft.glow(E["candy"], 1.7), rim, m @ T(0, 0.012, 0), 1.0, fold=0.5, curl=0.5, cuts=2, crease=crease)
    eye = craft.glow(E["candy"], 4.0)
    for s in (1, -1):
        b.poly(eye, [(s * 0.013 - 0.006, -0.011, 0.03), (s * 0.013 + 0.006, -0.011, 0.03), (s * 0.013, -0.012, 0.04)], tint=1.0, M=m)
    star = el.co.star_outline(0.016, 4)
    for x, z, sc in ((-0.2, 0.52, 1.0), (0.19, 0.5, 0.7), (0.07, 0.29, 0.8)):
        b.poly(eye, [(x + px * sc, 0.06, z + pz * sc) for px, pz in star], tint=1.0)
    b.finish("PaperBat")
    el.point_light("PaperBatGlow", (0, -0.12, 0.44), 0.35, (1.0, 0.8, 0.4), 0.03, shadow=False)


# --- timber props ------------------------------------------------------------------------------------------

def build_bench(craft):
    """Mr. Vale's park bench: slatted seat and back on painted side frames, with a knitted cushion."""
    b = Builder(11)
    slat = craft.timber(E["timber"], "X", wear=0.5, bare=E["timber_dark"])
    frame = craft.timber(E["purple"], "Z", wear=0.5)
    frame_y = craft.timber(E["purple"], "Y", wear=0.5)
    w = 1.5
    for sx in (-1, 1):
        x = sx * (w / 2 - 0.1)
        b.box(frame, (0.07, 0.08, 0.62), (x, -0.2, 0.31), bevel=0.014)
        b.box(frame, (0.07, 0.08, 0.95), (x, 0.26, 0.47), bevel=0.014, rot=R("X", -10))
        b.box(frame_y, (0.07, 0.52, 0.07), (x, 0.0, 0.403), bevel=0.014)
        arm = [(0.22, 0.0), (0.22, 0.05), (-0.27, 0.05), (-0.3, 0.036), (-0.3, 0.012), (-0.27, 0.0)]
        b.prism(frame_y, arm, 0.09, T(x + 0.045, 0, 0.62) @ R("Z", 90), bevel=0.012)
        b.disc(craft.paint(), 0.018, 0.012, T(x + sx * 0.035, -0.2, 0.4) @ R("Z", 90 * sx), segs=8, tint=lc(E["brass"]))
    b.box(frame, (w - 0.3, 0.05, 0.07), (0, 0.0, 0.2), bevel=0.012, rot=R("Y", 0))
    for i in range(4):
        b.box(slat, (w, 0.1, 0.035), (0, -0.2 + i * 0.118, 0.455 + (0.008 if i in (0, 3) else 0)), bevel=0.012,
              rot=R("Z", b.rng.uniform(-0.4, 0.4)))
    for i in range(3):
        z = 0.6 + i * 0.13
        b.box(slat, (w, 0.03, 0.1), (0, 0.215 + (z - 0.47) * math.tan(math.radians(10)), z), bevel=0.012, rot=R("X", -10))
    for sx in (-1, 1):
        for i in range(4):
            b.disc(craft.paint(), 0.01, 0.006, T(sx * (w / 2 - 0.1), -0.2 + i * 0.118, 0.475) @ R("X", -90), segs=6, tint=lc(E["ink"]))
    b.finish("Bench")
    cb = Builder(12)
    knit = craft.knit(V["orange"])
    cb.lathe(knit, [(0.0, 0.0), (0.12, 0.005), (0.17, 0.035), (0.17, 0.06), (0.12, 0.09), (0.0, 0.095)], 14,
             T(-0.42, -0.03, 0.472), radial=lambda a, i: 1 + 0.04 * math.cos(4 * a))
    cb.lathe(craft.paint(), [(0.0, 0.0), (0.02, 0.0), (0.016, 0.012), (0.0, 0.016)], 8, T(-0.42, -0.03, 0.56), tint=lc(V["purple"]))
    cb.finish("BenchCushion")


def build_fence(craft, length=3.4, gate_at=0.55, gate=True, name="Fence"):
    """Picket fence run with a little arched gate, slightly crooked pickets and a pegged paper bat."""
    b = Builder(13)
    P = pk.paints(craft)
    gate_w = 0.9
    g0 = -length / 2 + gate_at
    g1 = g0 + gate_w
    if not gate:
        g0 = g1 = -length / 2 - 0.08

    def picket(x, top, yaw=0.0, mat=None, y=-0.035):
        w = 0.085
        out = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, top - 0.07), (0.0, top), (-w / 2, top - 0.07)]
        b.prism(mat or P["trim_z"], out, 0.025, T(x, y, 0.06) @ R("Y", yaw), bevel=0.007)

    def newel(x, hgt=1.12):
        b.box(P["trim_z"], (0.12, 0.12, hgt), (x, 0.03, hgt / 2), bevel=0.018)
        b.box(P["trim_z"], (0.16, 0.16, 0.04), (x, 0.03, hgt + 0.0), bevel=0.012)
        b.lathe(P["trim_z"], [(0.065, 0.0), (0.075, 0.03), (0.04, 0.09), (0.0, 0.14)], 4, T(x, 0.03, hgt + 0.02) @ R("Z", 45))

    for x in ((-length / 2, g0 - 0.08, g1 + 0.08, length / 2) if gate else (-length / 2, 0.0, length / 2)):
        newel(x)
    for x0, x1 in ((-length / 2, g0 - 0.08), (g1 + 0.08, length / 2)):
        if x1 - x0 < 0.3:
            continue
        for z in (0.28, 0.78):
            b.box(P["trim_x"], (x1 - x0, 0.04, 0.07), ((x0 + x1) / 2, 0.0, z), bevel=0.012)
        n = max(1, round((x1 - x0 - 0.16) / 0.135))
        for i in range(n):
            x = x0 + 0.08 + (i + 0.5) * (x1 - x0 - 0.16) / n
            picket(x, 0.9 + b.rng.uniform(-0.025, 0.025), b.rng.uniform(-2.5, 2.5))
    # the gate, ajar
    if gate:
        with b.at(T(g0 - 0.01, 0.0, 0.0) @ R("Z", -24)):
            n = 6
            for i in range(n):
                x = 0.06 + (i + 0.5) * (gate_w - 0.1) / n
                t = (i + 0.5) / n
                picket(x, 0.82 + 0.2 * math.sin(math.pi * t), 0.0, P["shutter_z"])
            for z in (0.26, 0.74):
                b.box(P["shutter"], (gate_w - 0.06, 0.04, 0.07), (gate_w / 2, 0.0, z), bevel=0.012)
            brace = math.degrees(math.atan2(0.48, gate_w - 0.1))
            b.box(P["shutter"], (math.hypot(0.48, gate_w - 0.1), 0.035, 0.06), (gate_w / 2, 0.005, 0.5), bevel=0.01, rot=R("Y", -brace))
            b.box(P["ink"], (0.1, 0.02, 0.03), (gate_w - 0.08, -0.07, 0.74), bevel=0.005, tint=1.0)
            for z in (0.26, 0.74):
                b.box(P["ink"], (0.12, 0.015, 0.035), (0.05, -0.068, z), bevel=0.005, tint=1.0)
            b.prism(craft.paint(), el.co.star_outline(0.07), 0.006, T(gate_w / 2, -0.066, 0.52), tint=lc(E["candy"]))
    bat(b, craft.paper_white(), 0.26, T(length / 2 - 0.62, -0.038, 0.74) @ R("Y", 12), fold=0.2)
    return b.finish(name)


def build_hedge(craft, w=2.2, d=0.8, h=1.15, seed=21, name="Hedge"):
    """Trimmed hedge block: clipped faces with softly rounded top edges, dense small leaves breaking the
    outline, darker toward the ground, bare stems underneath and a couple of fallen maple leaves on top."""
    b = Builder(seed)
    mat = craft.tinted(E["hedge"], 0.95, 0.4, 13.0)
    rng = b.rng
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
    hh = h - 0.12
    for v in bm.verts:
        n = v.co * 2
        soft = 0.03 + 0.075 * max(0.0, n.z) ** 2  # the top edges are rounded over, the foot stays crisp
        k = 1 - soft * (abs(n.x) ** 6 + abs(n.y) ** 6 + abs(n.z) ** 6 - 1)
        taper = 1 - 0.07 * (v.co.z + 0.5)  # clipped a little narrower toward the top
        p = Vector((v.co.x * w * taper * k, v.co.y * d * taper * k, v.co.z * hh * (1 - (1 - k) * 0.5)))
        bump = noise.noise(p * 2.6 + Vector((seed, 0, 0))) * 0.04
        v.co = p + Vector((n.x, n.y, n.z)).normalized() * bump + Vector((0, 0, hh / 2 + 0.12))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()

    def grade(z):
        return 0.55 + 0.75 * max(0.0, min(1.0, z / h)) ** 1.3
    cards = []
    for f in bm.faces:
        c, nrm = f.calc_center_median(), f.normal.copy()
        for _ in range(3):
            p = c + Vector((rng.uniform(-0.07, 0.07), rng.uniform(-0.05, 0.05), rng.uniform(-0.06, 0.06)))
            tan = nrm.cross(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))))
            if tan.length < 0.1:
                continue
            tan.normalize()
            out = (nrm * rng.uniform(0.2, 0.7) + tan * 0.9).normalized()
            side = out.cross(nrm).normalized()
            ln, wd = rng.uniform(0.045, 0.08), rng.uniform(0.016, 0.027)
            lightness = rng.uniform(0.8, 1.4) * grade(p.z) * (1.15 if nrm.z > 0.5 else 1.0)
            cards.append(([p - out * 0.02, p + out * ln * 0.45 + side * wd, p + out * ln, p + out * ln * 0.45 - side * wd],
                          (lightness * 0.95, lightness, lightness * 0.74)))
    b.add_bm(bm, mat, tint=lambda c: (grade(c.z) * 0.85,) * 3)
    for pts, tint in cards:
        b.poly(mat, pts, tint=tint)
    for i in range(5):
        x = -w / 2 + 0.25 + i * (w - 0.5) / 4 + rng.uniform(-0.08, 0.08)
        b.rod(craft.paint(), (x, rng.uniform(-0.1, 0.1), 0.0), (x + rng.uniform(-0.05, 0.05), 0, 0.22), 0.028, 6, lc(E["bark"]), r2=0.02)
    for k in range(3):
        leaf(b, craft, T(rng.uniform(-w / 2.6, w / 2.6), rng.uniform(-d / 4, d / 4), h + 0.03) @ R("Z", rng.uniform(0, 360)) @ R("X", 80), 0.16,
             (E["leaf_a"], E["leaf_b"], E["leaf_c"])[k])
    return b.finish(name)


MAPLE = [(0.0, -0.5), (0.1, -0.5), (0.08, -0.2), (0.42, -0.34), (0.36, -0.1), (0.62, 0.06), (0.4, 0.14), (0.5, 0.42), (0.24, 0.34),
         (0.2, 0.52), (0.0, 0.8)]


MAPLE_MID = [(0.0, -0.55), (0.045, -0.52), (0.05, -0.2), (0.42, -0.3), (0.33, -0.06), (0.62, 0.1), (0.37, 0.19), (0.44, 0.47), (0.19, 0.36), (0.0, 0.8)]


def leaf(b, craft, M, size, rgb, curl=0.25, low=False, mat=None, tint=None, fold=0.25):
    """A paper maple leaf (five points and a stem), folded on its midrib with curled edges."""
    half = MAPLE_MID if low else MAPLE
    out = [(x * size, z * size) for x, z in half] + [(-x * size, z * size) for x, z in reversed(half[1:-1])]
    b.paper(mat or craft.paper_white(), out, M, tint if tint is not None else lc(rgb), fold=fold, curl=curl, cuts=0)


def build_street_lamp(craft, height=3.1):
    """Painted street lamp with a hanging paper-glass lantern and paper moths circling it."""
    b = Builder(31)
    iron = craft.tinted(H(0x3A2F5E), 0.45, 0.2, 8.0)
    b.lathe(iron, [(0.0, 0.0), (0.2, 0.0), (0.2, 0.06), (0.13, 0.1), (0.11, 0.34), (0.14, 0.38), (0.075, 0.46), (0.06, 1.2),
                   (0.085, 1.24), (0.055, 1.3), (0.045, height - 0.5), (0.07, height - 0.46), (0.04, height - 0.4),
                   (0.035, height), (0.0, height + 0.03)], 10, tint=1.0)
    b.lathe(craft.paint(), [(0.079, 1.215), (0.092, 1.24), (0.079, 1.265)], 10, tint=lc(E["orange"]))
    crook = [(0.0, height - 0.12)] + [(0.34 - 0.34 * math.cos(math.radians(a)), height - 0.12 + 0.3 * math.sin(math.radians(a)))
                                      for a in (30, 60, 90, 120, 150, 180)]
    for (x0, z0), (x1, z1) in zip(crook, crook[1:]):
        b.rod(iron, (x0, 0, z0), (x1, 0, z1), 0.022, 6, 1.0)
    curl_pts = [(0.3 + 0.1 * math.cos(math.radians(a)) * (1 - a / 500), height - 0.32 + 0.1 * math.sin(math.radians(a)) * (1 - a / 500))
                for a in range(90, 420, 45)]
    for (x0, z0), (x1, z1) in zip(curl_pts, curl_pts[1:]):
        b.rod(iron, (x0, 0, z0), (x1, 0, z1), 0.012, 5, 1.0)
    b.rod(iron, (0.06, 0, height - 0.38), (0.26, 0, height - 0.22), 0.012, 5, 1.0)
    with b.at(T(0.68, 0, height - 0.12)):
        c = hanging_lamp(b, craft, 0.08, 1.6, 2.6)
    lamp = Vector((0.68, 0, height - 0.12)) + Vector(c)
    paper = craft.paper_white()
    moth = [(0.0, 0.01), (0.05, 0.035), (0.062, 0.0), (0.03, -0.012), (0.036, -0.04), (0.0, -0.022)]
    moth = moth + [(-x, z) for x, z in reversed(moth[1:-1])]
    for k in range(7):
        a = 0.9 * k + 0.4
        rad = 0.34 + 0.09 * (k % 3)
        p = lamp + Vector((rad * math.cos(a), rad * math.sin(a), 0.16 * math.sin(k * 2.1) - 0.02))
        m = T(*p) @ R("Z", math.degrees(a) + 90) @ R("X", -50 + 25 * (k % 3)) @ S(1.5 + 0.4 * (k % 2))
        b.paper(paper, moth, m, lc(E["bone"]) if k % 3 else lc(E["candy"]), fold=0.5 + 0.3 * (k % 2), curl=0.2, cuts=0)
    b.finish("StreetLamp")
    el.point_light("StreetLampGlow", lamp + Vector((0, 0, -0.8)), 60, (1.0, 0.74, 0.42), 0.12, shadow=True)
    return lamp


def _rrect(w, d, rad, n):
    """n points around a rounded rectangle, with outward normals and arc length."""
    pts = []
    straight_x, straight_y = w - 2 * rad, d - 2 * rad
    per = 2 * (straight_x + straight_y) + 2 * math.pi * rad
    for i in range(n):
        s = per * i / n
        segs = [(straight_x, (-straight_x / 2, -d / 2), (1, 0), None), (math.pi * rad / 2, (straight_x / 2, -straight_y / 2), None, -90),
                (straight_y, (w / 2, -straight_y / 2), (0, 1), None), (math.pi * rad / 2, (straight_x / 2, straight_y / 2), None, 0),
                (straight_x, (straight_x / 2, d / 2), (-1, 0), None), (math.pi * rad / 2, (-straight_x / 2, straight_y / 2), None, 90),
                (straight_y, (-w / 2, straight_y / 2), (0, -1), None), (math.pi * rad / 2, (-straight_x / 2, -straight_y / 2), None, 180)]
        for ln, origin, direction, a0 in segs:
            if s <= ln:
                if direction:
                    p = (origin[0] + direction[0] * s, origin[1] + direction[1] * s)
                    nrm = (direction[1], -direction[0])
                else:
                    a = math.radians(a0 + 90 * s / ln)
                    nrm = (math.cos(a), math.sin(a))
                    p = (origin[0] + rad * nrm[0], origin[1] + rad * nrm[1])
                pts.append((p, nrm, s))
                break
            s -= ln
    return pts


def build_party_table(craft):
    """Party table: striped cloth with a scalloped, rippling drop, cupcake stand, punch bowl, cookies, candy
    apples and a small jack-o'-lantern."""
    w, d, top = 1.5, 0.75, 0.74
    b = Builder(41)
    P = pk.paints(craft)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box(P["amber"], (0.07, 0.07, top - 0.02), (sx * (w / 2 - 0.1), sy * (d / 2 - 0.09), (top - 0.02) / 2), bevel=0.012)
    b.box(P["amber"], (w - 0.04, d - 0.04, 0.04), (0, 0, top - 0.025), bevel=0.01)
    b.finish("TableFrame")
    c = Builder(42)
    cloth = craft.striped_cloth(E["orange"], E["purple"], 0.24, "X")
    loop = _rrect(w + 0.03, d + 0.03, 0.05, 72)
    bm = bmesh.new()
    rings = []
    for z, off, amp in ((top + 0.004, 0.0, 0.0), (top - 0.02, 0.012, 0.0), (top - 0.16, 0.02, 0.012), (top - 0.3, 0.03, 0.026)):
        ring = []
        for (p, nrm, s) in loop:
            r = off + amp * math.sin(s * 21.0)
            zz = z - (0.035 * abs(math.sin(s * 10.5)) if amp > 0.02 else 0.0)
            ring.append(bm.verts.new((p[0] + nrm[0] * r, p[1] + nrm[1] * r, zz)))
        rings.append(ring)
    bm.faces.new(rings[0])
    for lo, hi in zip(rings, rings[1:]):
        for i in range(len(lo)):
            j = (i + 1) % len(lo)
            bm.faces.new((lo[i], hi[i], hi[j], lo[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    c.add_bm(bm, cloth, tint=1.0)
    c.finish("TableCloth", sharp=80)
    t = Builder(43)
    paint = craft.paint()
    z = top + 0.006
    # cupcake stand
    with t.at(T(-0.42, 0.05, z)):
        t.lathe(paint, [(0.0, 0.0), (0.09, 0.0), (0.085, 0.012), (0.02, 0.03), (0.016, 0.16), (0.17, 0.175), (0.175, 0.19), (0.0, 0.19)], 12,
                tint=lc(E["bone"]))
        for k in range(4):
            a = math.radians(90 * k + 30)
            with t.at(T(0.105 * math.cos(a), 0.105 * math.sin(a), 0.19)):
                t.lathe(paint, [(0.0, 0.0), (0.03, 0.0), (0.042, 0.05)], 8, tint=lc(V[("purple", "orange")[k % 2]]))
                t.lathe(paint, [(0.045, 0.05), (0.05, 0.065), (0.035, 0.085), (0.022, 0.1), (0.0, 0.115)], 8, tint=lc(V[("bone", "lime", "candy", "berry")[k]]))
                t.lathe(paint, [(0, 0.11), (0.012, 0.12), (0, 0.132)], 6, tint=lc(V["berry"] if k != 3 else V["candy"]))
    # punch bowl with ladle
    with t.at(T(0.12, 0.08, z)):
        t.lathe(paint, [(0.0, 0.0), (0.08, 0.0), (0.07, 0.015), (0.03, 0.03), (0.03, 0.05), (0.13, 0.09), (0.17, 0.17), (0.175, 0.2),
                        (0.16, 0.2), (0.0, 0.185)], 14, tint=lc(H(0xBFD9E8)))
        t.lathe(craft.glow(E["green"], 0.55), [(0.158, 0.192), (0.0, 0.196)], 14, tint=1.0)
        t.rod(paint, (0.06, 0.02, 0.19), (0.2, 0.06, 0.33), 0.008, 5, lc(E["brass"]))
        for k, (x, y) in enumerate(((-0.07, 0.03), (0.02, -0.08), (0.05, 0.07))):
            t.lathe(paint, [(0, 0.19), (0.022, 0.2), (0, 0.212)], 6, T(x, y, 0), tint=lc(V[("orange", "berry", "candy")[k]]))
    # cookie plate
    with t.at(T(0.5, -0.12, z)):
        t.lathe(paint, [(0.0, 0.0), (0.09, 0.0), (0.14, 0.02), (0.135, 0.026), (0.0, 0.012)], 12, tint=lc(V["purple"]))
        for k in range(5):
            a = math.radians(72 * k)
            t.disc(paint, 0.04, 0.012, T(0.07 * math.cos(a), 0.07 * math.sin(a), 0.022 + 0.004 * k) @ R("X", 90 + 6) @ T(0, -0.006, 0), segs=9,
                   tint=lc(H(0xD99A4E)) if k % 2 else lc(H(0x5A3320)), bevel=0.004)
    # candy apples
    for k, (x, y) in enumerate(((-0.1, -0.22), (0.02, -0.25), (-0.2, -0.26))):
        with t.at(T(x, y, z)):
            t.lathe(paint, [(0.0, 0.0), (0.03, 0.005), (0.048, 0.04), (0.04, 0.075), (0.0, 0.08)], 9, tint=lc(V[("berry", "lime", "berry")[k]]))
            t.rod(paint, (0, 0, 0.07), (0.01 * (k - 1), 0.0, 0.17), 0.005, 5, lc(V["bone"]))
    pumpkin(t, craft, 0.085, 0.8, T(0.6, 0.16, z) @ R("Z", 12), style=2, glow=2.0)
    # paper cups
    for k in range(3):
        t.lathe(paint, [(0.0, 0.0), (0.026, 0.0), (0.036, 0.085), (0.032, 0.085)], 8, T(-0.62 + 0.085 * k, -0.2 + 0.03 * (k % 2), z),
                tint=lc(V[("lime", "orange", "purple")[k]]))
    bat(t, craft.paper_white(), 0.2, T(0.0, -d / 2 - 0.034, top - 0.14) @ R("Y", -8), fold=0.1, curl=0.2)
    t.finish("TableTreats")


def build_crate(craft, size=0.6):
    """Fixed wooden crate: slats on a batten frame, diagonal brace, stencilled star, nail heads."""
    b = Builder(51)
    wood_x = craft.timber(E["timber"], "X", wear=0.5, bare=E["timber_dark"])
    wood_z = craft.timber(E["timber"], "Z", wear=0.5, bare=E["timber_dark"])
    dark = craft.tinted(H(0x2A1C22), 0.9, 0.1)
    hs = size / 2
    b.box(dark, (size - 0.05, size - 0.05, size - 0.05), (0, 0, hs), bevel=0, tint=1.0)
    for side in range(4):
        with b.at(T(0, 0, 0) @ R("Z", 90 * side)):
            n = 4
            for i in range(n):
                z = (i + 0.5) * size / n
                b.box(wood_x, (size - 0.02, 0.025, size / n - 0.016), (0, -hs + 0.012, z), bevel=0.008, tint=b.vary(0.8, 1.12))
            for sx in (-1, 1):
                b.box(wood_z, (0.085, 0.03, size), (sx * (hs - 0.0425), -hs - 0.012, hs), bevel=0.01)
                for z in (0.08, size - 0.08, hs):
                    b.disc(craft.paint(), 0.009, 0.006, T(sx * (hs - 0.0425), -hs - 0.03, z), segs=6, tint=lc(E["ink"]))
            if side in (0, 2):
                ln = math.hypot(size - 0.17, size - 0.02)
                b.box(wood_x, (ln, 0.028, 0.075), (0, -hs - 0.01, hs), bevel=0.01,
                      rot=R("Y", -math.degrees(math.atan2(size - 0.02, size - 0.17)) * (1 if side == 0 else -1)))
            if side == 1:
                star = el.co.star_outline(0.11)
                b.prism(craft.paint(), star, 0.004, T(0, -hs - 0.003, hs), tint=lc(E["orange"]))
    n = 4
    for i in range(n):
        y = -hs + (i + 0.5) * size / n
        b.box(wood_x, (size + 0.02, size / n - 0.014, 0.028), (0, y, size - 0.002), bevel=0.008, tint=b.vary(0.85, 1.12))
    b.finish("Crate")


def build_awning(craft):
    """Sloped awning on a slice of shed wall: striped canvas on a timber frame with a scalloped valance.
    The slope is walkable (the skilled route up from the crate)."""
    b = Builder(61)
    P = pk.paints(craft)
    W, Hh = 2.6, 2.5
    pk.wall(b, P, W, Hh)
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.12, 0.14, Hh), (sx * (W / 2 + 0.03), 0.04, Hh / 2), bevel=0.016)
    b.box(P["roofdeck"], (W + 0.2, 0.3, 0.08), (0, 0.05, Hh + 0.03), bevel=0.02)
    aw, run, z_hi, z_lo = 2.1, 1.5, 2.1, 1.35
    pitch = math.degrees(math.atan2(z_hi - z_lo, run))
    ln = math.hypot(run, z_hi - z_lo)
    wood = P["trim_y"]
    b.box(P["trim_x"], (aw + 0.1, 0.06, 0.1), (0, -0.05, z_hi - 0.03), bevel=0.014)
    b.box(P["trim_x"], (aw + 0.1, 0.07, 0.07), (0, -run, z_lo - 0.03), bevel=0.014)
    for sx in (-1, 0, 1):
        b.box(wood, (0.06, ln, 0.06), (sx * aw / 2, -run / 2, (z_hi + z_lo) / 2 - 0.035), bevel=0.012, rot=R("X", pitch))
    for sx in (-1, 1):
        b.rod(wood, (sx * aw / 2, -0.03, z_lo - 0.55), (sx * aw / 2, -run + 0.03, z_lo - 0.04), 0.03, 6)
        b.box(P["trim_z"], (0.1, 0.05, 0.2), (sx * aw / 2, -0.04, z_lo - 0.55), bevel=0.012)
    b.finish("AwningWall")
    c = Builder(62)
    cloth = craft.striped_cloth(E["orange"], E["purple"], 0.42, "X")
    c.box(cloth, (aw + 0.16, ln + 0.06, 0.03), (0, -run / 2, (z_hi + z_lo) / 2 + 0.012), bevel=0.01, rot=R("X", pitch), tint=1.0)
    val = el.scallop_strip(aw + 0.16, 0.08, 10, 0.09, 5)
    c.prism(cloth, val, 0.018, T(-(aw + 0.16) / 2, -run - 0.045, z_lo + 0.005), tint=0.94)
    for sx in (-1, 1):
        side = [(0.0, 0.0), (ln + 0.03, 0.0), (ln + 0.03, -0.15), (ln * 0.5, -0.1), (0.0, -0.02)]
        c.prism(cloth, side, 0.014, T(sx * (aw / 2 + 0.07) + 0.007, -0.02, z_hi + 0.01) @ R("Z", -90) @ R("Y", pitch), tint=0.9)
    c.finish("AwningCloth", sharp=70)


def build_bridge(craft, length=2.6, width=1.0):
    """Short plank bridge: uneven planks on two stringers, rope rails on four posts, resting on trestles."""
    b = Builder(71)
    wood_y = craft.timber(E["timber"], "Y", wear=0.5, bare=E["timber_dark"])
    wood_x = craft.timber(E["timber_dark"], "X", wear=0.3)
    wood_z = craft.timber(E["timber_dark"], "Z", wear=0.3)
    z = 0.5
    for sx in (-1, 1):  # trestles
        x = sx * (length / 2 - 0.12)
        b.box(wood_x, (0.16, width + 0.1, 0.12), (x, 0, 0.06), bevel=0.02, rot=R("Z", 0))
        for sy in (-1, 1):
            b.box(wood_z, (0.12, 0.12, z - 0.2), (x, sy * (width / 2 - 0.12), 0.12 + (z - 0.2) / 2 - 0.01), bevel=0.018)
        b.box(wood_x, (0.14, width, 0.1), (x, 0, z - 0.13), bevel=0.016)
    for sy in (-1, 1):
        b.box(wood_x, (length, 0.09, 0.13), (0, sy * (width / 2 - 0.14), z - 0.065 - 0.01), bevel=0.016)
    n = round(length / 0.2)
    for i in range(n):
        x = -length / 2 + (i + 0.5) * length / n
        b.box(wood_y, (length / n - 0.012, width + b.rng.uniform(-0.05, 0.06), 0.04), (x, b.rng.uniform(-0.015, 0.015), z + 0.012),
              bevel=0.01, rot=R("Z", b.rng.uniform(-1.6, 1.6)), tint=b.vary(0.78, 1.12))
        for sy in (-1, 1):
            b.disc(craft.paint(), 0.008, 0.005, T(x, sy * (width / 2 - 0.14), z + 0.034) @ R("X", -90), segs=6, tint=lc(E["ink"]))
    rope = lc(H(0xC9A66B))
    for sy in (-1, 1):
        posts = []
        for sx in (-1, 1):
            p = Vector((sx * (length / 2 - 0.1), sy * (width / 2 - 0.02), z))
            b.box(wood_z, (0.08, 0.08, 0.66), (p.x, p.y, z + 0.28), bevel=0.014)
            b.lathe(wood_z, [(0.05, 0.0), (0.058, 0.02), (0.03, 0.06), (0.0, 0.075)], 4, T(p.x, p.y, z + 0.61) @ R("Z", 45))
            posts.append(Vector((p.x, p.y, z + 0.52)))
        pts = catenary(posts[0], posts[1], 0.16, 10)
        for a, c2 in zip(pts, pts[1:]):
            b.rod(craft.paint(), a, c2, 0.016, 6, rope)
        for p in posts:
            b.lathe(craft.paint(), [(0.047, -0.03), (0.06, 0.0), (0.047, 0.03)], 8, T(*p), tint=shade(rope, 0.8))
    b.finish("Bridge")


def foliage(b, craft, centre, radii, seed, cards=96):
    """One clump of maple crown: a smooth painted mass under layered paper maple leaves that hang tip-down and
    curl outward. Colour runs from rust underneath to warm amber on top (the `foliage` material)."""
    rng = b.rng
    fol = craft.foliage(H(0x9E3420), H(0xF8AE3C), 3.7, 7.3)
    tints = ((1.0, 1.0, 1.0), (1.0, 1.0, 1.0), (1.15, 1.08, 0.8), (0.95, 0.72, 0.62), (1.1, 0.58, 0.5))
    cx, cy, cz = centre

    def radial(a, i):
        return 1 + 0.13 * noise.noise(Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, i * 0.6 + seed * 3.1)))
    n = 9
    prof = [(math.sin(math.pi * i / n) if 0 < i < n else 0.0, -math.cos(math.pi * i / n) * (0.9 if i == 0 else 1.0)) for i in range(n + 1)]
    b.lathe(fol, prof, 16, T(cx, cy, cz) @ S(*radii), radial=radial, tint=b.vary(0.42, 0.52))
    up = Vector((0, 0, 1))
    for _ in range(cards):
        u, v = rng.uniform(0, 2 * math.pi), rng.uniform(-1.0, 1.0)
        nrm = Vector((math.cos(u) * math.sqrt(1 - v * v), math.sin(u) * math.sqrt(1 - v * v), v))
        p = Vector((cx + nrm.x * radii[0], cy + nrm.y * radii[1], cz + nrm.z * radii[2])) + nrm * rng.uniform(0.02, 0.16)
        face = (nrm + up * 0.25 + Vector((0.01, 0.0, 0.0))).normalized()
        tip = nrm * 0.3 - up * 0.8 + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0))
        zc = (tip - face * tip.dot(face)).normalized()
        yc = -face
        xc = yc.cross(zc)
        m = Matrix((xc, yc, zc)).transposed().to_4x4()
        t = rng.choice(tints)
        k = rng.uniform(0.85, 1.2)
        leaf(b, craft, T(*p) @ m, rng.uniform(0.3, 0.44), None, curl=0.5, low=True, mat=fol, tint=(t[0] * k, t[1] * k, t[2] * k), fold=0.2)


def build_maple(craft, treehouse=True, crown=True):
    """The maple and the treehouse platform base at 2.4 m (simplified): leaning trunk with root flare, three
    boughs, an autumn crown, and a braced plank deck with kit rails on two sides."""
    b = Builder(81)
    bark = craft.timber(E["bark"], "Z", wear=0.9, bare=H(0x7A5A78), rough=0.9)

    def radial(a, i):
        return 1 + (0.22 if i < 2 else 0.07) * math.cos(5 * a + i) + 0.05 * math.cos(11 * a)
    lean = lambda z: (0.05 * z + 0.015 * z * z, -0.02 * z)  # noqa: E731
    rings = [(0.62, 0.0), (0.46, 0.25), (0.38, 0.7), (0.34, 1.5), (0.31, 2.4), (0.29, 3.2), (0.24, 3.9)]
    bm = bmesh.new()
    prev = None
    segs = 12
    for i, (r, z) in enumerate(rings):
        ox, oy = lean(z)
        ring = [bm.verts.new((ox + r * radial(2 * math.pi * s / segs, i) * math.cos(2 * math.pi * s / segs),
                              oy + r * radial(2 * math.pi * s / segs, i) * math.sin(2 * math.pi * s / segs), z)) for s in range(segs)]
        if prev:
            for s in range(segs):
                bm.faces.new((prev[s], prev[(s + 1) % segs], ring[(s + 1) % segs], ring[s]))
        prev = ring
    bm.faces.new(prev)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    b.add_bm(bm, bark, tint=1.0)
    top = Vector((lean(3.9)[0], lean(3.9)[1], 3.85))
    boughs = [(top, Vector((-1.5, 0.3, 5.2)), 0.2), (top, Vector((1.9, -0.2, 5.5)), 0.2), (top, Vector((0.5, 0.9, 6.0)), 0.18),
              (Vector((lean(3.0)[0], lean(3.0)[1], 3.0)), Vector((-1.9, -0.5, 4.1)), 0.14),
              (Vector((lean(3.3)[0], lean(3.3)[1], 3.3)), Vector((2.3, 0.4, 4.2)), 0.13)]
    for a, c2, r in boughs:
        mid = a.lerp(c2, 0.5) + Vector((0, 0, 0.25))
        b.rod(bark, a, mid, r, 7, 1.0, r2=r * 0.75)
        b.rod(bark, mid, c2, r * 0.75, 7, 0.95, r2=r * 0.4)
    if crown:
        for k, (c2, rad) in enumerate((((-1.6, 0.3, 5.5), (1.5, 1.3, 1.0)), ((1.9, -0.1, 5.8), (1.6, 1.4, 1.1)), ((0.3, 0.7, 6.5), (1.7, 1.5, 1.15)),
                                       ((-2.1, -0.5, 4.4), (1.1, 1.0, 0.75)), ((2.5, 0.4, 4.5), (1.1, 1.0, 0.75)), ((0.2, -0.6, 5.6), (1.3, 1.1, 0.9)))):
            foliage(b, craft, c2, rad, k)
    for k in range(7):  # fallen leaves round the roots
        a = 0.9 * k
        leaf(b, craft, T(1.0 * math.cos(a) + 0.2, 0.9 * math.sin(a), 0.012 + 0.002 * k) @ R("Z", 50 * k) @ R("X", 86), 0.2,
             (E["leaf_a"], E["leaf_b"], E["leaf_c"])[k % 3])
    b.finish("Maple")
    if not treehouse:
        return
    t = Builder(82)
    P = pk.paints(craft)
    wood_y = craft.timber(E["timber"], "Y", wear=0.5, bare=E["timber_dark"])
    wood_x = craft.timber(E["timber_dark"], "X", wear=0.3)
    zt, half = 2.4, 1.3
    ox, oy = lean(zt)
    with t.at(T(ox, oy, 0)):
        n = 13
        for i in range(n):
            x = -half + (i + 0.5) * 2 * half / n
            near = abs(x) < 0.36
            for y0, y1 in (((-half, -0.36), (0.36, half)) if near else ((-half, half),)):
                t.box(wood_y, (2 * half / n - 0.012, y1 - y0 + t.rng.uniform(0.0, 0.05), 0.045), (x, (y0 + y1) / 2, zt - 0.0225), bevel=0.01,
                      tint=t.vary(0.78, 1.12))
        for sy in (-1, 1):
            t.box(wood_x, (2 * half, 0.09, 0.14), (0, sy * (half - 0.1), zt - 0.115), bevel=0.016)
            t.box(wood_x, (2 * half, 0.09, 0.14), (0, sy * 0.42, zt - 0.115), bevel=0.016)
        for sx in (-1, 1):
            t.box(P["trim_y"], (0.07, 2 * half + 0.04, 0.16), (sx * (half + 0.0), 0, zt - 0.1), bevel=0.016)
        for sx in (-1, 1):
            for sy in (-1, 1):
                a = Vector((sx * 0.26, sy * 0.24, 1.25))
                c2 = Vector((sx * (half - 0.2), sy * (half - 0.2), zt - 0.18))
                d = c2 - a
                t.box(wood_x, (d.length, 0.09, 0.09), tuple((a + c2) / 2), bevel=0.016,
                      rot=d.to_track_quat("X", "Z").to_matrix().to_4x4())
        with t.at(T(0, -half + 0.04, zt)):
            pk.rail(t, P, 2 * half - 0.2, rhythm=1)
        with t.at(T(half - 0.04, 0, zt) @ R("Z", 90)):
            pk.rail(t, P, 2 * half - 0.2, rhythm=1)
        for x, y in ((-half + 0.05, -half + 0.05), (half - 0.05, -half + 0.05), (half - 0.05, half - 0.05)):
            with t.at(T(x, y, zt)):
                pk.post(t, P, 0.95, ())
        bunting(t, craft, (-half + 0.05, -half - 0.02, zt + 0.74), (half - 0.05, -half - 0.02, zt + 0.74), 0.16, 9, 0.17, 0.22)
    t.finish("TreehouseBase")


def build_mailbox(craft):
    """Mailbox on a braced post: arched tin box in haunted purple, raised orange flag, a letter in the door."""
    b = Builder(91)
    P = pk.paints(craft)
    b.box(P["trim_z"], (0.11, 0.11, 1.02), (0, 0.05, 0.51), bevel=0.018)
    b.box(P["trim_x"], (0.16, 0.5, 0.05), (0, 0.0, 1.03), bevel=0.014, rot=R("Z", 0))
    brace = [(0.0, 0.0), (0.0, 0.07), (-0.2, 0.27), (-0.2, 0.2)]
    b.prism(P["trim_z"], brace, 0.05, T(0.025, 0.0, 0.76) @ R("Z", 90), bevel=0.01)
    body = craft.tinted(E["purple"], 0.4, 0.2, 9.0)
    out = el.arch_outline(0.24, 0.12, 8)
    b.prism(body, out, 0.46, T(0, -0.25, 1.055), tint=1.0, bevel=0.012)
    b.prism(body, [(x * 0.84, z * 0.86) for x, z in out], 0.012, T(0, -0.262, 1.068), tint=1.3, bevel=0.006)
    b.lathe(craft.paint(), [(0.0, 0.0), (0.012, 0.0), (0.016, 0.014), (0.0, 0.02)], 8, T(0, -0.262, 1.24) @ R("X", 90), tint=lc(E["brass"]))
    b.box(craft.paint(), (0.13, 0.006, 0.075), (0.02, -0.2635, 1.14), bevel=0, rot=R("Y", -14), tint=lc(E["bone"]))
    b.box(craft.paint(), (0.03, 0.002, 0.03), (0.055, -0.267, 1.155), bevel=0, rot=R("Y", -14), tint=lc(E["orange"]))
    b.box(craft.paint(), (0.012, 0.035, 0.24), (0.125, -0.06, 1.25), bevel=0.004, tint=lc(E["orange"]))
    b.prism(craft.paint(), [(0.0, 0.0), (0.12, 0.0), (0.085, -0.04), (0.12, -0.085), (0.0, -0.085)], 0.012,
            T(0.119, -0.045, 1.37) @ R("Z", -90), tint=lc(E["orange"]))
    b.disc(craft.paint(), 0.02, 0.02, T(0.14, -0.06, 1.14) @ R("Z", 90), segs=8, tint=lc(E["brass"]))
    star = el.co.star_outline(0.045)
    b.prism(craft.paint(), star, 0.004, T(-0.1235, -0.02, 1.17) @ R("Z", -90), tint=lc(E["candy"]))
    bat(b, craft.paper_white(), 0.17, T(0.0, 0.07, 0.62) @ T(0, -0.085, 0) @ R("Y", -10), fold=0.15, curl=0.25)
    for k in range(4):  # a tuft of grass at the foot
        a = 1.6 * k
        b.paper(craft.paper_white(), [(-0.02, 0), (0.02, 0), (0.0, 0.2 + 0.04 * k)], T(0.1 * math.cos(a), 0.05 + 0.1 * math.sin(a), 0) @ R("Z", 70 * k) @ R("X", 12),
                lc(E["hedge_light"]), fold=0.2, curl=0.4, cuts=0)
    b.finish("Mailbox")


def build_standee(craft, height=1.2):
    """Scale reference: a cardboard cut-out of a child Pip's height (1.2 m) on a little foot."""
    b = Builder(99)
    k = height / 1.2
    half = [(0.0, 1.2), (0.05, 1.19), (0.075, 1.25), (0.1, 1.17), (0.135, 1.1), (0.15, 1.0), (0.13, 0.91), (0.07, 0.86), (0.16, 0.83),
            (0.25, 0.56), (0.2, 0.52), (0.14, 0.7), (0.13, 0.45), (0.14, 0.05), (0.2, 0.0), (0.02, 0.0), (0.015, 0.38), (0.0, 0.4)]
    out = [(x * k, z * k) for x, z in half] + [(-x * k, z * k) for x, z in reversed(half[1:-1])]
    card = craft.cardboard(H(0x8D7BB5))
    b.prism(card, out, 0.012, T(0, 0, 0.0), tint=1.0)
    b.prism(card, [(0.0, 0.0), (0.22, 0.0), (0.0, 0.5)], 0.012, T(0.006, 0.012, 0) @ R("Z", -90) @ S(-1, 1, 1), tint=0.8)
    for z in (0.3, 0.6, 0.9):
        b.box(craft.paint(), (0.07, 0.004, 0.012), (0.0, -0.002, z * k), bevel=0, tint=lc(E["bone"]))
    b.finish("Standee")


def build_pip_ref(craft):
    """The real 1.2 m Pip, so the cardboard scale figure in the other tiles can be trusted."""
    import pip_vampire
    pip_vampire.build_pip(craft)


# --- review sheet ------------------------------------------------------------------------------------------

# name, builder, frame width in metres, look-at height, standee offset x (None = too small for one), yaw, target dx
TILES = [
    ("Bench", build_bench, 2.5, 0.55, 1.15, 30, 0.2), ("CandyBowl", build_candy_bowl, 1.0, 0.24, None, 20, 0.0),
    ("Fence", build_fence, 4.6, 0.6, 2.05, 24, 0.12), ("Hedge", build_hedge, 3.6, 0.6, 1.6, 30, 0.25),
    ("StreetLamp", build_street_lamp, 4.0, 1.75, 1.2, 20, 0.5),
    ("PartyTable", build_party_table, 2.6, 0.55, 1.2, 28, 0.2), ("PaperBat", build_paper_bat, 0.62, 0.3, None, 16, 0.0),
    ("Crate", build_crate, 1.9, 0.5, 0.75, 32, 0.25), ("Awning", build_awning, 4.3, 1.3, 1.75, 34, 0.2),
    ("Bridge", build_bridge, 4.0, 0.6, 1.75, 34, 0.25),
    ("Maple", build_maple, 10.5, 3.9, 2.6, 22, 0.12), ("Pumpkins", build_pumpkins, 1.6, 0.26, None, 14, 0.0),
    ("Mailbox", build_mailbox, 2.3, 0.75, 0.6, 30, 0.2), ("Dressing", build_dressing, 3.6, 1.15, 1.95, 22, 0.3),
    ("Pip", build_pip_ref, 2.0, 0.62, 0.62, 24, 0.3),
]
SPACING = 16.0


def build_all(craft, only=None):
    placed = {}
    for i, (name, build, *_rest) in enumerate(TILES):
        if only and name not in only:
            continue
        root, _ = grouped(name, lambda: build(craft))
        root.location.x = i * SPACING
        placed[name] = root
    return placed


def main():
    out, quality, rest = el.args()
    only = rest or None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    placed = build_all(craft, only)
    stand_root, _ = grouped("StandeeRoot", lambda: build_standee(craft))
    el.stage_ground(craft, 400.0)
    cam = el.setup_render(samples=48 if quality == "final" else 12, exposure=-0.1)
    el.night_world(0.9, top=H(0x241B4A), horizon=H(0x3A2A66))
    el.sun_light("Key", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 30.0)
    el.sun_light("Rim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 30.0)
    el.sun_light("Fill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 30.0)
    px = 500 if quality == "final" else 250
    tmp = os.path.splitext(out)[0]
    tiles = []
    for i, (name, _build, frame, look, stand, yaw, dx) in enumerate(TILES):
        if name not in placed:
            continue
        root = placed[name]
        print(f"TRIS {name}: {el.triangles(root)}", flush=True)
        el.solo([name, "StandeeRoot"] if stand is not None else [name])
        stand_root.location = (root.location.x + (stand or 0.0), {"Maple": -1.6, "Awning": -1.3}.get(name, -0.25), 0)
        if name == "Maple":
            stand_root.location.x = root.location.x + 2.6
        lens = 70
        dist = frame * lens / 36
        elev = math.radians(13)
        a = math.radians(yaw)
        target = Vector((root.location.x + dx * frame * 0.5 * (1 if stand is not None else 0), 0, look))
        loc = target + Vector((-math.sin(a) * math.cos(elev), -math.cos(a) * math.cos(elev), math.sin(elev))) * dist
        tiles.append(el.render_view(cam, loc, target, lens, px, px, f"{tmp}_{name}.png"))
    cols = 5
    blank = np.zeros_like(tiles[0])
    blank[:, :, :3] = (0.02, 0.016, 0.05)
    blank[:, :, 3] = 1
    while len(tiles) % cols:
        tiles.append(blank)
    rows = [np.concatenate(tiles[r:r + cols], axis=1) for r in range(0, len(tiles), cols)]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("props sheet ->", out)


if __name__ == "__main__":
    main()
