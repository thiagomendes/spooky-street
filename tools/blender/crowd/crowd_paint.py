"""Painted details of the crowd atlas, laid over the baked craft colours from a baked rest-position map.

Every texel of the atlas knows where it sits on its part in the bind pose (`rest`) and which piece it belongs to
(`pid`), so features are painted where they belong in 3D, the way the cast's faces are drawn: big friendly eyes
with two glints, brows, a small open smile and blush; the closed-eye lash line on the blink lids; face paint
(cat whiskers, a skull); the skeleton suit's bones; the ghost's eye holes and running stitch; the robot's paper
panel; the crown's paper gems; the sweets in the pails; the lantern's paper ribs. Colours are the art bible's.
"""
import math
import os

import bpy
import numpy as np

import char_export as ce
from crowd_build import FACE, GHOST_HEM
from crowd_kit import PAL, SKIN, HAIR, IRIS

INK = np.array(PAL["ink"])
MOUTH = np.array((0.43, 0.12, 0.2))
TONGUE = np.array((0.94, 0.45, 0.5))


def sstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


class Canvas:
    """The texels of one piece: rest positions (N x 3) and colours (N x 3) to paint over."""

    def __init__(self, pos, rgb):
        self.p = pos
        self.c = rgb

    def lay(self, alpha, colour):
        a = np.clip(alpha, 0, 1)[:, None]
        self.c = self.c * (1 - a) + np.asarray(colour)[None, :] * a

    def shade(self, alpha, factor):
        a = np.clip(alpha, 0, 1)[:, None]
        self.c = self.c * (1 - a + a * factor)


def ellipse(u, v, cu, cv, ru, rv, feather=0.06):
    """1 inside, a soft edge `feather` (in units of the radius) wide."""
    d = np.sqrt(((u - cu) / ru) ** 2 + ((v - cv) / rv) ** 2)
    return sstep((1 - d) / feather + 0.5), d


def line(u, v, a, b, width, feather=0.35):
    a, b = np.asarray(a), np.asarray(b)
    ab = b - a
    t = np.clip(((u - a[0]) * ab[0] + (v - a[1]) * ab[1]) / (ab @ ab), 0, 1)
    d = np.hypot(u - (a[0] + t * ab[0]), v - (a[1] + t * ab[1]))
    return sstep((width - d) / (width * feather) + 0.5)


# --- faces ---------------------------------------------------------------------------------------------------

def paint_face(cv, P, opts, lid=False):
    r, hc = P.head_r, np.array(P.head_c)
    L = (cv.p - hc) / r
    u, v = L[:, 0], L[:, 2]
    front = sstep((-L[:, 1] - 0.2) / 0.2)
    F = FACE[P.child]
    tone = opts.get("tone", "t2")
    paint = opts.get("paint", "plain")
    hair = np.array(HAIR["dark"])
    if lid:  # a closed eye: skin (or the skull's white paint) with one curved lash line and two little lashes
        if paint == "skull":
            cv.lay(np.ones_like(u), PAL["white"])
        for s in (1, -1):
            ex, ez, rx, rz = s * F["eye_x"], F["eye_z"], F["eye_rx"], F["eye_rz"]
            du = (u - ex) / rx
            curve = ez - rz * (0.1 - 0.32 * du ** 2)
            on = sstep((0.05 - np.abs(v - curve)) / 0.02 + 0.5) * (np.abs(du) < 1.05)
            cv.lay(on, INK)
            for k in (0.55, 0.85):
                cv.lay(line(u, v, (ex + s * k * rx, ez - rz * (0.1 - 0.32 * k * k)),
                            (ex + s * (k + 0.12) * rx, ez - rz * 0.55), 0.022), INK)
        return
    if paint == "skull":
        mask, _ = ellipse(u, v, 0.0, -0.05, 0.9, 1.0, 0.12)
        cv.lay(mask * front, PAL["white"])
        for s in (1, -1):
            sock, _ = ellipse(u, v, s * F["eye_x"], F["eye_z"] - 0.02, F["eye_rx"] * 1.55, F["eye_rz"] * 1.4, 0.15)
            cv.lay(sock * front, (0.2, 0.17, 0.3))
        nose, _ = ellipse(u, v, 0.0, -0.33, 0.075, 0.06, 0.2)
        cv.lay(nose * front, INK)
    if opts.get("beard"):
        beard, _ = ellipse(u, v, 0.0, -0.62, 0.78, 0.42, 0.15)
        cv.lay(beard * front * (v < -0.42), hair)
        for s in (1, -1):
            mo, _ = ellipse(u, v, s * 0.12, -0.44, 0.15, 0.055, 0.25)
            cv.lay(mo * front, hair)
    blush = np.array(SKIN[tone][1])
    if paint not in ("skull",):
        for s in (1, -1):
            b, _ = ellipse(u, v, s * (F["eye_x"] + 0.16), F["eye_z"] - 0.34, 0.17, 0.12, 0.9)
            cv.lay(b * front * 0.45, blush)
    if opts.get("freckles"):
        rng = np.random.default_rng(3)
        for _ in range(7):
            fu, fv = rng.uniform(0.18, 0.42), rng.uniform(-0.38, -0.22)
            for s in (1, -1):
                f, _ = ellipse(u, v, s * fu, fv, 0.022, 0.022, 0.4)
                cv.lay(f * front * 0.6, (0.75, 0.45, 0.3))
    # eyes
    iris_c = np.array(IRIS[opts.get("iris", "brown")])
    for s in (1, -1):
        ex, ez, rx, rz = s * F["eye_x"], F["eye_z"], F["eye_rx"], F["eye_rz"]
        sclera, d = ellipse(u, v, ex, ez, rx, rz, 0.08)
        cv.lay(sclera * front, PAL["white"])
        iris, di = ellipse(u, v, ex, ez - 0.02, rx * 0.8, rz * 0.76, 0.1)
        grad = np.clip((v - (ez - 0.02)) / (rz * 0.76), -1, 1)
        col = iris_c[None, :] * (1.0 - 0.35 * np.clip(grad, 0, 1)[:, None] + 0.15 * np.clip(-grad, 0, 1)[:, None])
        a = (iris * front)[:, None]
        cv.c = cv.c * (1 - a) + col * a
        ring = sstep((di - 0.82) / 0.1) * iris
        cv.shade(ring * front, 0.55)
        pupil, _ = ellipse(u, v, ex, ez - 0.03, rx * 0.4, rz * 0.38, 0.12)
        cv.lay(pupil * front, INK)
        g1, _ = ellipse(u, v, ex - 0.3 * rx, ez + 0.27 * rz, rx * 0.2, rx * 0.2, 0.2)
        g2, _ = ellipse(u, v, ex + 0.3 * rx, ez - 0.32 * rz, rx * 0.09, rx * 0.09, 0.3)
        cv.lay((g1 + g2) * front, PAL["white"])
        upper = sstep((v - (ez - 0.15 * rz)) / (0.15 * rz))
        rim = sstep((d - 0.96) / 0.06) * sstep((1.14 - d) / 0.06)
        cv.lay(rim * upper * front, INK)
        cv.lay(line(u, v, (ex + s * 0.82 * rx, ez + 0.55 * rz), (ex + s * 1.16 * rx, ez + 0.82 * rz), 0.024) * front,
               INK)
        if paint != "skull":
            du = (u - ex) / rx
            brow_v = ez + rz * 1.55 - 0.1 * du ** 2
            brow = sstep((0.034 - np.abs(v - brow_v)) / 0.012 + 0.5) * (np.abs(du) < 0.8)
            cv.lay(brow * front, hair * 0.8)
    # mouth
    my = -0.5 if P.child else -0.55
    w = 0.2 if opts.get("mouth") == "grin" else 0.14
    top = my + 0.05 * (u / w) ** 2
    inside, _ = ellipse(u, v, 0.0, my, w, 0.11, 0.1)
    m = inside * (v < top)
    cv.lay(m * front, MOUTH)
    tongue, _ = ellipse(u, v, 0.0, my - 0.08, w * 0.5, 0.05, 0.2)
    cv.lay(tongue * m * front, TONGUE)
    lip = sstep((0.018 - np.abs(v - top)) / 0.008 + 0.5) * (np.abs(u) < w * 1.05)
    cv.lay(lip * front, INK)
    if paint == "skull":
        for k in (-0.6, -0.2, 0.2, 0.6):
            cv.lay(line(u, v, (k * w, my + 0.05 * k * k + 0.05), (k * w, my - 0.06), 0.012) * front,
                   INK)
    if paint == "cat":
        nose, _ = ellipse(u, v, 0.0, -0.31, 0.095, 0.07, 0.15)
        cv.lay(nose * front, (0.25, 0.14, 0.22))
        for s in (1, -1):
            for k in (-1, 0, 1):
                cv.lay(line(u, v, (s * 0.24, -0.4 + 0.04 * k), (s * 0.66, -0.36 + 0.11 * k), 0.016) * front, INK)


# --- body prints and costume details -----------------------------------------------------------------------------

def _segment(p, a, b):
    a, b = np.asarray(a), np.asarray(b)
    ab = b - a
    t = np.clip((p - a) @ ab / (ab @ ab), 0, 1)
    closest = a + t[:, None] * ab
    return t, p - closest


def paint_skeleton(cv, P):
    p = cv.p
    x, y, zz = p[:, 0], p[:, 1], p[:, 2]
    z = P.z
    T = P.torso
    bone = PAL["bone"]
    torso = (np.abs(x) < P.shoulder_x * 0.92) & (zz > z["hip"] - 0.02 * P.h) & (zz < z["neck"])
    front = sstep((-y - 0.25 * T) / (0.2 * T)) * torso
    back = sstep((y - 0.25 * T) / (0.2 * T)) * torso
    X = x / T
    Zr = (zz - z["waist"]) / (z["shoulder"] - z["waist"])
    for i in range(4):
        zc = 0.12 + 0.2 * i - 0.22 * X ** 2
        rib = sstep((0.055 - np.abs(Zr - zc)) / 0.02 + 0.5) * sstep((np.abs(X) - 0.14) / 0.05) * \
            sstep((0.92 - 0.07 * i - np.abs(X)) / 0.05)
        cv.lay(rib * front, bone)
    cv.lay(sstep((0.085 - np.abs(X)) / 0.02 + 0.5) * (Zr > 0.08) * (Zr < 0.82) * front, bone)
    for s in (1, -1):
        pel, _ = ellipse(X, (zz - z["pelvis"]) / P.h, s * 0.5, 0.005, 0.42, 0.045, 0.15)
        cv.lay(pel * front, bone)
    vert = (np.mod((zz - z["hip"]) / (0.04 * P.h), 1.0) < 0.62)
    cv.lay(sstep((0.12 - np.abs(X)) / 0.03 + 0.5) * vert * back, bone)
    L = P.limb
    for s in (1, -1):
        sh = (s * P.shoulder_x, 0.0, z["shoulder"])
        el = tuple(P.elbow(s))
        wr = tuple(P.wrist(s))
        hip = (s * P.hip_x, 0.0, z["hip"])
        knee = (s * P.hip_x * 1.02, -0.006, z["knee"])
        ank = (s * P.hip_x, 0.004, z["ankle"])
        for a, b, thick in ((sh, el, 1.0), (el, wr, 0.85), (hip, knee, 1.15), (knee, ank, 1.0)):
            t, off = _segment(p, a, b)
            dist = np.linalg.norm(off, axis=1)
            near = dist < L * 1.6
            facing = sstep((-off[:, 1] / np.maximum(dist, 1e-6) - 0.2) / 0.2)
            ax = np.asarray(b) - np.asarray(a)
            ax = ax / np.linalg.norm(ax)
            lat = np.abs(off[:, 0] * ax[2] - off[:, 2] * ax[0])
            knob = np.maximum(sstep((0.16 - t) / 0.06), sstep((t - 0.84) / 0.06))
            half = L * thick * (0.26 + 0.2 * knob)
            shaft = sstep((half - lat) / (0.08 * L) + 0.5) * (t > 0.03) * (t < 0.97)
            cv.lay(shaft * near * facing, bone)


def paint_cat_belly(cv, P):
    p = cv.p
    z = P.z
    T = P.torso
    front = sstep((-p[:, 1] - 0.3 * T) / (0.2 * T)) * (np.abs(p[:, 0]) < P.shoulder_x * 0.9)
    patch, _ = ellipse(p[:, 0], p[:, 2], 0.0, z["waist"] + 0.2 * (z["chest"] - z["waist"]), 0.55 * T,
                       0.55 * (z["chest"] - z["hip"]), 0.08)
    cv.lay(patch * front, PAL["bone"])


def _hem_stitch(cv, hem_z, colour, above=0.03, height=0.007, per_turn=110):
    p = cv.p
    a = np.arctan2(p[:, 1], p[:, 0])
    on = (np.abs(p[:, 2] - (hem_z(a) + above)) < height * 0.5) & (np.mod(a / (2 * np.pi) * per_turn, 1.0) < 0.55)
    cv.lay(on.astype(float), colour)


def paint_ghost(cv, P):
    r, hc = P.head_r, np.array(P.head_c)
    L = (cv.p - hc) / r
    u, v = L[:, 0], L[:, 2]
    front = sstep((-L[:, 1] - 0.4) / 0.2)
    for s in (1, -1):
        e, _ = ellipse(u, v, s * 0.37, -0.02, 0.19, 0.27, 0.1)
        cv.lay(e * front, INK)
        g, _ = ellipse(u, v, s * 0.37 - 0.06, 0.08, 0.05, 0.05, 0.3)
        cv.lay(g * front * 0.6, (0.35, 0.3, 0.5))
    m, _ = ellipse(u, v, 0.0, -0.52, 0.11, 0.13, 0.12)
    cv.lay(m * front, INK)
    hem = GHOST_HEM(P)

    def hem_z(a):
        t = np.mod(a + np.pi / 2, 2 * np.pi) / (2 * np.pi)
        return hem + 0.018 * np.cos(2 * np.pi * 8 * t) - 0.008
    _hem_stitch(cv, hem_z, PAL["plum"], above=0.035, height=0.008)


def paint_corn(cv, P):
    z = P.z
    for b in (z["chest"] + 0.02 * P.h, z["waist"] - 0.05 * P.h):
        _hem_stitch(cv, lambda a, b=b: b + 0 * a, PAL["bone"] if b < z["chest"] else PAL["orange"], above=-0.012,
                    height=0.006)


def paint_tunic(cv, P, opts):
    z = P.z
    hem = z["hip"] - opts["length"] * (z["hip"] - z["knee"])
    band = 0.035 * P.h
    _hem_stitch(cv, lambda a: hem + 0 * a, PAL["bone"], above=band + 0.012, height=0.007)
    if opts.get("ermine"):  # a king's paper-and-felt ermine: ink tufts on the bone trim
        p = cv.p
        a = np.arctan2(p[:, 1], p[:, 0])
        cell = np.mod(a / (2 * np.pi) * 26, 1.0)
        row = (p[:, 2] - hem) / band
        tuft = (np.abs(cell - 0.5) < 0.13) & (np.abs(row - 0.5) < 0.2)
        cv.lay(tuft.astype(float), INK)


def paint_robot_box(cv, P):
    p = cv.p
    z = P.z
    w = P.shoulder_x * 2.25
    d = P.torso * 1.75
    z0, z1 = z["hip"] - 0.035 * P.h, z["shoulder"] + 0.012 * P.h
    front = sstep((0.004 - d / 2 + 0.012 - p[:, 1]) / 0.004)
    X = p[:, 0] / (w / 2)
    Z = (p[:, 2] - z0) / (z1 - z0)
    label = sstep((0.62 - np.abs(X)) / 0.03) * sstep((0.17 - np.abs(Z - 0.3)) / 0.02)
    cv.lay(label * front, PAL["bone"])
    for k, col in ((-0.34, PAL["orange"]), (0.0, PAL["green"]), (0.34, PAL["berry"])):
        b, _ = ellipse(X, Z * (z1 - z0) / (w / 2), k, 0.3 * (z1 - z0) / (w / 2), 0.11, 0.11, 0.12)
        cv.lay(b * front, col)
    grille = sstep((0.5 - np.abs(X)) / 0.03) * (Z > 0.84) * (Z < 0.94) * (np.mod(Z * 60, 1.0) < 0.45)
    cv.lay(grille * front, INK)
    for sx in (-1, 1):
        tape = (np.abs((X - sx * 0.95) + (Z - 0.05)) < 0.09) & (np.abs(X) < 1.02) & (Z < 0.25)
        cv.lay(tape * front * 0.85, PAL["bone"])


def paint_crown(cv, P):
    r, hc = P.head_r, np.array(P.head_c)
    z0 = hc[2] + r * (0.5 if P.child else 0.58)
    pivot = np.array((0.0, 0.06 * r, z0))
    a = math.radians(8)
    q = cv.p - pivot
    y = q[:, 1] * math.cos(a) - q[:, 2] * math.sin(a)
    zz = q[:, 1] * math.sin(a) + q[:, 2] * math.cos(a)
    ang = np.arctan2(y, q[:, 0])
    R = r * (1.0 if P.child else 0.98)
    cols = [PAL["orange"], PAL["berry"], PAL["green"], PAL["orange"], PAL["berry"]]
    for m in range(5):
        c = 2 * np.pi * m / 5
        da = np.mod(ang - c + np.pi, 2 * np.pi) - np.pi
        g, _ = ellipse(da * R, zz, 0.0, 0.17 * r, 0.095 * r, 0.1 * r, 0.12)
        cv.lay(g, cols[m])
    edge = np.abs(zz - 0.03 * r) < 0.012 * r
    cv.lay(edge * (np.mod(ang * R / 0.012, 1.0) < 0.5), PAL["orange"])


def paint_sweets(cv):
    cells = np.floor(cv.p * 70).astype(np.int64)
    h = (cells[:, 0] * 73856093 ^ cells[:, 1] * 19349663 ^ cells[:, 2] * 83492791) % 5
    cols = np.array([PAL["orange"], PAL["berry"], PAL["green"], PAL["white"], PAL["purple"]])
    cv.c = cols[h] * 0.92 + cv.c * 0.08


def paint_bag(cv, P, grip):
    k = 1.0 if P.child else 1.2
    top = grip[2] - 0.06 * k
    zz = cv.p[:, 2]
    cv.lay(((zz < top - 0.025 * k) & (zz > top - 0.05 * k)).astype(float), PAL["orange"])
    cv.lay(((zz < top - 0.058 * k) & (zz > top - 0.066 * k)).astype(float), PAL["plum"])


def lantern_ribs(p, grip):
    """1 on the eight paper folds of the lantern (around the vertical through the grip)."""
    a = np.arctan2(p[:, 1] - grip[1], p[:, 0] - grip[0])
    return np.abs(np.mod(a / (2 * np.pi) * 8 + 0.5, 1.0) - 0.5) < 0.06


# --- compose and save ------------------------------------------------------------------------------------------

def _down(px, f):
    h, w, c = px.shape
    return px.reshape(h // f, f, w // f, f, c).mean(axis=(1, 3))


def finish(images, pieces, parts, builders, out, tex):
    colour = ce.pixels(images["colour"]).copy()
    rgb = colour[:, :, :3].reshape(-1, 3)
    pos = (ce.pixels(images["rest"])[:, :, :3].reshape(-1, 3) - 0.5) / 0.25
    pid = np.rint(ce.pixels(images["pid"])[:, :, 0].reshape(-1)).astype(np.int64)
    emit = ce.pixels(images["emit"]).copy()
    erg = emit[:, :, :3].reshape(-1, 3)
    by_key = {p.key: p for p in parts}
    for i, (key, src) in pieces.items():
        idx = np.nonzero(pid == i)[0]
        if len(idx) == 0:
            continue
        part = by_key[key]
        P = builders[part.body].P
        grip = np.array(builders[part.body].info["grip"])
        cv = Canvas(pos[idx], rgb[idx])
        if part.slot == "Head" and (src == "lid" or src.endswith("_Skull") or src.endswith("_Nose")):
            paint_face(cv, P, part.opts, lid=src == "lid")
        elif part.slot == "Body" and src.endswith(part.name) and part.opts.get("print") == "skeleton":
            paint_skeleton(cv, P)
        elif part.slot == "Body" and src.endswith(part.name) and part.opts.get("print") == "cat_belly":
            paint_cat_belly(cv, P)
        elif part.kind == "ghost_sheet":
            paint_ghost(cv, P)
        elif part.kind == "candy_corn":
            paint_corn(cv, P)
        elif part.kind == "tunic":
            paint_tunic(cv, P, part.opts)
        elif part.kind == "robot_box" and src.endswith("Robot_Box"):
            paint_robot_box(cv, P)
        elif part.kind == "crown":
            paint_crown(cv, P)
        elif part.kind == "pail" and src.endswith("Sweets"):
            paint_sweets(cv)
        elif part.kind == "bag" and src.endswith("Bag_Paper"):
            paint_bag(cv, P, grip)
        elif part.kind == "lantern" and src.endswith("Lantern_Paper"):
            ribs = lantern_ribs(cv.p, grip).astype(float)
            cv.shade(ribs, 0.7)
            erg[idx] *= (1 - 0.45 * ribs)[:, None]
        rgb[idx] = cv.c
    shade = ce.pixels(images["ao"])[:, :, :3].reshape(-1, 3).copy()
    for i, (_key, src) in pieces.items():
        if src == "lid":  # the lids wait inside the skull, where the occlusion pass finds them dark
            shade[pid == i] = 1.0
    tint = np.array((0.80, 0.74, 0.92), dtype=np.float32)
    k = 0.4 * (1.0 - np.clip(shade, 0.0, 1.0))
    rgb *= 1.0 - k * (1.0 - tint * 0.55)
    colour[:, :, :3] = np.clip(rgb.reshape(colour.shape[0], colour.shape[1], 3), 0, 1)
    colour[:, :, 3] = 1.0
    os.makedirs(out, exist_ok=True)
    paths = {"base": ce.save_png(images["colour"], colour, os.path.join(out, "Crowd_BaseColor.png"))}
    normal = _down(ce.pixels(images["normal"]), 2)
    v = normal[:, :, :3] * 2 - 1
    v /= np.maximum(np.linalg.norm(v, axis=2, keepdims=True), 1e-6)
    normal[:, :, :3] = v * 0.5 + 0.5
    normal[:, :, 3] = 1.0
    nimg = bpy.data.images.new("Crowd_Normal", tex // 2, tex // 2, alpha=False, is_data=True)
    paths["normal"] = ce.save_png(nimg, normal, os.path.join(out, "Crowd_Normal.png"))
    emit[:, :, :3] = erg.reshape(emit.shape[0], emit.shape[1], 3)
    small = _down(emit, 4)
    small[:, :, 3] = 1.0
    eimg = bpy.data.images.new("Crowd_Emission", tex // 4, tex // 4, alpha=False)
    paths["emission"] = ce.save_png(eimg, small, os.path.join(out, "Crowd_Emission.png"))
    return paths


def final_images(paths):
    base = bpy.data.images.load(paths["base"])
    normal = bpy.data.images.load(paths["normal"])
    normal.colorspace_settings.name = "Non-Color"
    emission = bpy.data.images.load(paths["emission"])
    return base, normal, emission
