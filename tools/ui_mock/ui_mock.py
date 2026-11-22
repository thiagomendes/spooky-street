#!/usr/bin/env python3
"""Spooky Street UI language mocks (identity stage 3).

Composites procedurally drawn UI (paper cards, felt buttons, running stitches, ink outlines, line icons) over the
approved opening key frame at iPhone landscape resolution (2532 x 1170 px, @3x, 844 x 390 pt).

Everything is drawn here with Pillow + numpy; the only inputs are the repo's approved renders and OFL fonts.

    python3 tools/ui_mock/ui_mock.py [--out DIR] [--guides]
    python3 tools/ui_mock/ui_mock.py --export [DIR] [--export-sheet SHEET.jpg]   # baked game sprites

Units: final pixels unless noted. PT = 3 px (device @3x). Unity reference units (1334 x 750 canvas matched by
height) are ~2 per pt, so 96 ref = 48 pt = 144 px.
"""
from __future__ import annotations

import argparse
import math
import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EVID = os.path.join(ROOT, "docs", "evidence", "s000.2")
FONTS = os.path.join(ROOT, "Assets", "SpookyStreet", "Content", "Resources", "Fonts")
F_DISPLAY = os.path.join(FONTS, "Sniglet-ExtraBold.ttf")
F_BODY = os.path.join(FONTS, "Andika-Regular.ttf")
F_BOLD = os.path.join(FONTS, "Andika-Bold.ttf")

W, H = 2532, 1170
PT = 3.0
SS = 3  # supersampling for sprites
SAFE_L, SAFE_R, SAFE_B, SAFE_T = 141, 141, 63, 0  # iPhone landscape safe insets (47/47/21/0 pt)


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


# Palette (spec v1.3 families)
NIGHT = hexc("2A2550")
PURPLE = hexc("5B3A7A")
PLUM = hexc("7A3E6E")
ORANGE = hexc("EE7F2D")
ORANGE_D = hexc("C2601C")
GREEN = hexc("8DBF3F")
CANDY = hexc("F6D365")
BONE = hexc("F4E6C8")
BONE_D = hexc("D9C6A0")
INK = hexc("1E1A26")
GRAPHITE = hexc("4A4458")
SILVER = hexc("DAE2EE")  # June's cape clue only

SWATCHES = [
    ("Night violet", "#2A2550", "scrims, felt discs, caption backing"),
    ("Haunted purple", "#5B3A7A", "secondary felt, stitches on paper"),
    ("Plum", "#7A3E6E", "pressed felt, tab backs"),
    ("Jack-o'-lantern", "#EE7F2D", "primary action, Use button"),
    ("Candy corn", "#F6D365", "highlight, focus, new, stitches on felt"),
    ("Bone paper", "#F4E6C8", "cards, labels, icons on felt"),
    ("Ink", "#1E1A26", "outlines, text on paper, shadows"),
    ("Witch green", "#8DBF3F", "accent only: toggle on, solved tick"),
]

_font_cache = {}


def font(path, px):
    key = (path, int(px))
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(path, int(px))
    return _font_cache[key]


# --------------------------------------------------------------------------------------------------- noise/textures

def value_noise(w, h, cell, seed, octaves=1):
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        c = max(2, int(cell / (2 ** o)))
        gw, gh = w // c + 2, h // c + 2
        g = Image.fromarray((rng.random((gh, gw)) * 255).astype(np.uint8), "L")
        g = g.resize((gw * c, gh * c), Image.BICUBIC).crop((0, 0, w, h))
        out += np.asarray(g, np.float32) / 255.0 * amp
        tot += amp
        amp *= 0.5
    return out / tot


def texture(w, h, base, kind, seed=1):
    """RGBA texture of size (w, h) in sprite pixels."""
    base = np.array(base[:3], np.float32)
    rng = np.random.default_rng(seed)
    if kind == "paper":
        mott = value_noise(w, h, 90 * SS, seed, 3) - 0.5
        fib = rng.normal(0, 1, (h, w)).astype(np.float32)
        fib = np.asarray(Image.fromarray(np.clip(fib * 40 + 128, 0, 255).astype(np.uint8)).filter(
            ImageFilter.GaussianBlur(0.6)), np.float32) / 128 - 1
        shade = mott[..., None] * 18 + fib[..., None] * 5
    elif kind == "felt":
        mott = value_noise(w, h, 40 * SS, seed, 2) - 0.5
        fine = rng.normal(0, 1, (h, w)).astype(np.float32)
        fine = np.asarray(Image.fromarray(np.clip(fine * 50 + 128, 0, 255).astype(np.uint8)).filter(
            ImageFilter.GaussianBlur(0.9)), np.float32) / 128 - 1
        shade = mott[..., None] * 16 + fine[..., None] * 14
    else:
        shade = np.zeros((h, w, 1), np.float32)
    rgb = np.clip(base[None, None, :] + shade, 0, 255).astype(np.uint8)
    a = np.full((h, w, 1), 255, np.uint8)
    return Image.fromarray(np.concatenate([rgb, a], 2), "RGBA")


# --------------------------------------------------------------------------------------------------- geometry

def rrect_path(x0, y0, x1, y1, r, step=4.0):
    r = min(r, (x1 - x0) / 2, (y1 - y0) / 2)
    pts = []
    corners = [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]
    for cx, cy, a0 in corners:
        n = max(3, int(r * math.pi / 2 / step))
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return dense(pts, step)


def circle_path(cx, cy, r, step=4.0):
    n = max(24, int(2 * math.pi * r / step))
    return [(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def dense(pts, step):
    out = []
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        d = math.dist(a, b)
        k = max(1, int(d / step))
        for j in range(k):
            t = j / k
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def deckle(pts, amp, seed, freq=0.08, fine=0.35):
    """Jitter a closed path along its normals: low-frequency wobble plus fine torn-paper tooth."""
    rng = random.Random(seed)
    n = len(pts)
    ph = [rng.random() * 6.28 for _ in range(3)]
    out = []
    for i, (x, y) in enumerate(pts):
        a, b = pts[i - 1], pts[(i + 1) % n]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty) or 1
        nx, ny = ty / L, -tx / L
        s = i * freq
        o = amp * (0.55 * math.sin(s + ph[0]) + 0.3 * math.sin(s * 2.7 + ph[1]) + 0.15 * math.sin(s * 6.1 + ph[2]))
        o += amp * fine * (rng.random() - 0.5) * 2
        out.append((x + nx * o, y + ny * o))
    return out


def path_len(pts, closed=True):
    seq = pts + [pts[0]] if closed else pts
    return sum(math.dist(seq[i], seq[i + 1]) for i in range(len(seq) - 1))


# --------------------------------------------------------------------------------------------------- sprite

class Sprite:
    """A supersampled RGBA drawing surface; coordinates are final pixels relative to the box's top-left."""

    def __init__(self, w, h, pad=30):
        self.w, self.h, self.pad = w, h, pad
        self.img = Image.new("RGBA", (int((w + 2 * pad) * SS), int((h + 2 * pad) * SS)), (0, 0, 0, 0))

    def S(self, x, y):
        return ((x + self.pad) * SS, (y + self.pad) * SS)

    def Sp(self, pts):
        return [self.S(x, y) for x, y in pts]

    def layer(self):
        return Image.new("RGBA", self.img.size, (0, 0, 0, 0))

    def comp(self, lay):
        self.img = Image.alpha_composite(self.img, lay)

    def mask(self, pts):
        m = Image.new("L", self.img.size, 0)
        ImageDraw.Draw(m).polygon(self.Sp(pts), fill=255)
        return m

    def fill(self, pts_or_mask, color, kind="paper", seed=1, fuzz=False):
        m = self.mask(pts_or_mask) if isinstance(pts_or_mask, list) else pts_or_mask
        if fuzz:
            noise = value_noise(m.size[0], m.size[1], 3, seed + 7)
            mb = np.asarray(m.filter(ImageFilter.GaussianBlur(1.3 * SS)), np.float32) / 255
            mm = np.clip((mb - 0.5 + (noise - 0.5) * 0.45) * 4 + 0.5, 0, 1)
            m = Image.fromarray((mm * 255).astype(np.uint8), "L")
        tex = texture(m.size[0], m.size[1], color, kind, seed)
        lay = self.layer()
        lay.paste(tex, (0, 0), m)
        self.comp(lay)
        return m

    def line(self, pts, color, width, closed=False, round_caps=True):
        lay = self.layer()
        d = ImageDraw.Draw(lay)
        seq = pts + [pts[0]] if closed else pts
        d.line(self.Sp(seq), fill=color, width=max(1, int(width * SS)), joint="curve")
        if round_caps and not closed:
            r = width * SS / 2
            for x, y in (self.S(*seq[0]), self.S(*seq[-1])):
                d.ellipse((x - r, y - r, x + r, y + r), fill=color)
        self.comp(lay)

    def ellipse(self, box, fill=None, outline=None, width=0):
        lay = self.layer()
        x0, y0 = self.S(box[0], box[1])
        x1, y1 = self.S(box[2], box[3])
        ImageDraw.Draw(lay).ellipse((x0, y0, x1, y1), fill=fill, outline=outline, width=int(width * SS))
        self.comp(lay)

    def poly(self, pts, fill):
        lay = self.layer()
        ImageDraw.Draw(lay).polygon(self.Sp(pts), fill=fill)
        self.comp(lay)

    def text(self, x, y, s, path, px, fill, anchor="la", stroke=0, stroke_fill=None, spacing=0):
        lay = self.layer()
        d = ImageDraw.Draw(lay)
        f = font(path, px * SS)
        d.text(self.S(x, y), s, font=f, fill=fill, anchor=anchor, stroke_width=int(stroke * SS),
               stroke_fill=stroke_fill, spacing=spacing * SS)
        self.comp(lay)

    def stitches(self, pts, color, dash=9, gap=7, width=3.0, closed=True, shadow=True):
        """Running stitch along a path: thread dashes with a small hole shadow under each end."""
        seq = pts + [pts[0]] if closed else pts
        segs = []
        on = True
        period = [dash, gap]
        idx = 0
        rem = period[0]
        cur = [seq[0]]
        for i in range(len(seq) - 1):
            a, b = seq[i], seq[i + 1]
            d = math.dist(a, b)
            t0 = 0.0
            while d - t0 > rem:
                t0 += rem
                p = (a[0] + (b[0] - a[0]) * t0 / d, a[1] + (b[1] - a[1]) * t0 / d)
                if on:
                    cur.append(p)
                    segs.append(cur)
                cur = [p]
                on = not on
                idx += 1
                rem = period[idx % 2]
            rem -= d - t0
            if on:
                cur.append(b)
        lay = self.layer()
        dr = ImageDraw.Draw(lay)
        wpx = max(1, int(width * SS))
        if shadow:
            sh = (0, 0, 0, 90)
            for s in segs:
                if len(s) >= 2:
                    dr.line([(x + 0.8 * SS, y + 1.2 * SS) for x, y in self.Sp(s)], fill=sh, width=wpx)
        for s in segs:
            if len(s) >= 2:
                dr.line(self.Sp(s), fill=color, width=wpx, joint="curve")
                for x, y in (self.S(*s[0]), self.S(*s[-1])):
                    r = wpx / 2
                    dr.ellipse((x - r, y - r, x + r, y + r), fill=color)
        self.comp(lay)

    def paste_img(self, im, x, y, mask=None, scaled=True):
        """Paste a final-resolution image (upscaled to the sprite's supersampling) at local (x, y)."""
        if scaled:
            im = im.resize((im.size[0] * SS, im.size[1] * SS), Image.BICUBIC)
            if mask is not None:
                mask = im
        lay = self.layer()
        lay.paste(im, (int((x + self.pad) * SS), int((y + self.pad) * SS)), mask)
        self.comp(lay)

    def render(self, angle=0.0):
        im = self.img
        if angle:
            im = im.rotate(angle, resample=Image.BICUBIC, expand=False)
        return im.resize((im.size[0] // SS, im.size[1] // SS), Image.LANCZOS)


def place(canvas, sprite, cx, cy, angle=0.0, shadow=(5, 7), shadow_alpha=150, blur=2.5):
    """Paste a sprite centred at (cx, cy) with a hard-ish ink drop shadow (paper on a desk)."""
    im = sprite.render(angle)
    x = int(round(cx - im.size[0] / 2))
    y = int(round(cy - im.size[1] / 2))
    if shadow:
        a = im.split()[3].point(lambda v: v * shadow_alpha // 255)
        sh = Image.new("RGBA", im.size, INK[:3] + (0,))
        sh.putalpha(a)
        if blur:
            sh = sh.filter(ImageFilter.GaussianBlur(blur))
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(sh, (x + shadow[0], y + shadow[1]))
        canvas.alpha_composite(layer)
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    layer.paste(im, (x, y))
    canvas.alpha_composite(layer)


# --------------------------------------------------------------------------------------------------- materials

def paper_card(w, h, seed=1, fill=BONE, outline=INK, stitch=PURPLE, radius=26, deck=3.0, ow=3.0, inset=13,
               dash=10, gap=8, pad=30, kind="paper", fuzz=False, stitch_w=3.0):
    sp = Sprite(w, h, pad)
    pts = deckle(rrect_path(0, 0, w, h, radius, 3), deck, seed)
    sp.fill(pts, fill, kind, seed, fuzz=fuzz)
    # soft inner edge shade gives the paper a little thickness/curl
    edge = Image.new("L", sp.img.size, 0)
    ImageDraw.Draw(edge).line(sp.Sp(pts + [pts[0]]), fill=255, width=int(10 * SS))
    edge = ImageChops.multiply(edge.filter(ImageFilter.GaussianBlur(5 * SS)), sp.mask(pts))
    shade = Image.new("RGBA", sp.img.size, (90, 60, 40, 0))
    shade.putalpha(edge.point(lambda v: v * 0.22))
    sp.comp(shade)
    if stitch:
        ip = rrect_path(inset, inset, w - inset, h - inset, max(4, radius - inset * 0.6), 2)
        sp.stitches(ip, stitch, dash, gap, stitch_w)
    if outline:
        sp.line(pts, outline, ow, closed=True)
    sp.outline_pts = pts
    return sp


def felt_disc(d, fill=NIGHT, alpha=255, stitch=BONE, ring_inset=11, outline=INK, seed=3, pad=24, ow=3.0,
              stitch_w=3.2, dash=9, gap=7):
    sp = Sprite(d, d, pad)
    pts = deckle(circle_path(d / 2, d / 2, d / 2, 3), 1.2, seed, 0.2, 0.6)
    col = fill[:3] + (255,)
    sp.fill(pts, col, "felt", seed, fuzz=True)
    if alpha < 255:
        a = sp.img.split()[3].point(lambda v: v * alpha // 255)
        sp.img.putalpha(a)
    if stitch:
        sp.stitches(circle_path(d / 2, d / 2, d / 2 - ring_inset, 2), stitch, dash, gap, stitch_w)
    if outline:
        sp.line(pts, outline[:3] + (min(255, alpha + 60),), ow, closed=True)
    return sp


def felt_button(w, h, label, fill=ORANGE, text=INK, stitch=CANDY, seed=5, icon=None, sub=None, pressed=False,
                size=20, radius=34):
    if icon and not sub:
        w = max(w, int(40 + h * 0.62 + font(F_BOLD, size * PT).getlength(label) + 44))
    f = fill
    if pressed:
        f = tuple(int(c * 0.86) for c in fill[:3]) + (255,)
    sp = paper_card(w, h, seed, f, INK, stitch, radius, 1.6, 3.4, 12, 11, 7, kind="felt", fuzz=True, stitch_w=3.2)
    # top highlight / bottom lip for a tactile cushion (flattened when pressed)
    if not pressed:
        lip = Sprite(w, h, sp.pad)
        lp = rrect_path(6, h - 16, w - 6, h - 3, 10, 3)
        lip.poly(lp, (0, 0, 0, 55))
        lay = lip.img.filter(ImageFilter.GaussianBlur(3 * SS))
        lay.putalpha(ImageChops.multiply(lay.split()[3], sp.img.split()[3]))
        sp.comp(lay)
    tx = w / 2
    anchor = "mm"
    if icon:
        icon_draw(sp, icon, 26 + h * 0.32, h / 2 + (2 if pressed else 0), h * 0.42, text, 4.6)
        if sub:
            tx = w / 2 + h * 0.22
        else:
            tx, anchor = 40 + h * 0.62, "lm"
    ty = h / 2 - (14 if sub else 0) + (2 if pressed else 0)
    sp.text(tx, ty, label, F_BOLD, size * PT, text, anchor)
    if sub:
        sp.text(tx, ty + 0.62 * size * PT + 6, sub, F_BOLD, 12 * PT, text[:3] + (200,), "mm")
    return sp


def paper_button(w, h, label, seed=7, icon=None, pressed=False, size=18, stitch=PURPLE, fill=BONE):
    if icon:
        w = max(w, int(44 + h * 0.6 + font(F_BOLD, size * PT).getlength(label) + 40))
    f = fill if not pressed else hexc("E3D1AE")
    sp = paper_card(w, h, seed, f, INK, stitch, 28, 2.2, 3.2, 11, 9, 7)
    x = 34
    if icon:
        icon_draw(sp, icon, 30 + h * 0.3, h / 2, h * 0.46, INK, 4.0)
        x = 44 + h * 0.6
        sp.text(x, h / 2, label, F_BOLD, size * PT, INK, "lm")
    else:
        sp.text(w / 2, h / 2, label, F_BOLD, size * PT, INK, "mm")
    return sp


# --------------------------------------------------------------------------------------------------- icons

BAT = [(0.0, -0.06), (0.05, -0.2), (0.075, -0.07), (0.16, -0.12), (0.3, -0.17), (0.43, -0.2), (0.5, -0.1),
       (0.44, -0.02), (0.38, -0.05), (0.33, 0.05), (0.25, 0.0), (0.18, 0.1), (0.1, 0.04), (0.0, 0.14)]


def bat_points(cx, cy, s, flap=0.0):
    right = [(x, y + flap * x) for x, y in BAT]
    left = [(-x, y + flap * x) for x, y in reversed(BAT[1:-1])]
    pts = right + left
    return [(cx + x * s, cy + y * s) for x, y in pts]


def paper_bat(sp, cx, cy, s, glow=CANDY, flap=0.0):
    pts = bat_points(cx, cy, s, flap)
    if glow:
        sp.line(pts, glow, max(2.5, s * 0.028), closed=True)
    sp.poly(pts, INK)
    # fold creases in lighter paper
    for sx in (1, -1):
        sp.line([(cx, cy + 0.02 * s), (cx + sx * 0.44 * s, cy - 0.15 * s)], hexc("4A4458"), max(1.5, s * 0.012))
        sp.line([(cx + sx * 0.1 * s, cy + 0.0 * s), (cx + sx * 0.22 * s, cy + 0.04 * s)], hexc("4A4458"),
                max(1.2, s * 0.01))
    # eyes
    for sx in (1, -1):
        sp.ellipse((cx + sx * 0.03 * s - s * 0.012, cy - 0.06 * s - s * 0.008, cx + sx * 0.03 * s + s * 0.012,
                    cy - 0.06 * s + s * 0.008), fill=glow or BONE)


def icon_draw(sp, name, cx, cy, s, color, w=4.0):
    """Line icons on a 1-unit box: rounded strokes, one weight, no fills except where stated."""
    P = lambda x, y: (cx + x * s, cy + y * s)
    L = lambda pts, ww=w: sp.line([P(*p) for p in pts], color, ww)
    if name == "pause":
        for x in (-0.15, 0.15):
            sp.poly([P(*p) for p in [(x - 0.07, -0.28), (x + 0.07, -0.28), (x + 0.07, 0.28), (x - 0.07, 0.28)]], color)
            sp.ellipse((*P(x - 0.07, -0.35), *P(x + 0.07, -0.21)), fill=color)
            sp.ellipse((*P(x - 0.07, 0.21), *P(x + 0.07, 0.35)), fill=color)
    elif name == "jump":
        L([(-0.32, 0.1), (0.0, -0.22), (0.32, 0.1)], w * 1.3)
        L([(-0.2, 0.32), (0.2, 0.32)], w * 0.8)
    elif name == "examine":
        r = 0.26
        sp.ellipse((*P(-0.12 - r, -0.12 - r), *P(-0.12 + r, -0.12 + r)), outline=color, width=w)
        L([(0.08, 0.08), (0.36, 0.36)], w * 1.5)
        L([(-0.24, -0.2), (-0.16, -0.27)], w * 0.8)
    elif name == "talk":
        sp.line([(cx + x, cy + y) for x, y in rrect_path(-0.38 * s, -0.3 * s, 0.38 * s, 0.18 * s, 0.14 * s, 2)],
                color, w, closed=True)
        L([(-0.16, 0.18), (-0.24, 0.36), (0.02, 0.18)])
        for x in (-0.16, 0.0, 0.16):
            sp.ellipse((*P(x - 0.045, -0.1), *P(x + 0.045, -0.01)), fill=color)
    elif name == "notebook":
        sp.line([(cx + x, cy + y) for x, y in rrect_path(-0.3 * s, -0.36 * s, 0.32 * s, 0.36 * s, 0.06 * s, 2)],
                color, w, closed=True)
        L([(-0.16, -0.36), (-0.16, 0.36)])
        for y in (-0.22, -0.06, 0.1, 0.26):
            L([(-0.3, y), (-0.22, y)], w * 0.7)
        L([(0.04, -0.14), (0.2, -0.14)], w * 0.8)
        L([(0.04, 0.02), (0.2, 0.02)], w * 0.8)
        sp.poly([P(0.12, 0.36), P(0.22, 0.36), P(0.22, 0.5), P(0.17, 0.44), P(0.12, 0.5)], color)
    elif name == "bat":
        sp.poly(bat_points(cx, cy, s * 0.95), color)
    elif name == "radio":
        sp.line([(cx + x, cy + y) for x, y in rrect_path(-0.38 * s, -0.12 * s, 0.38 * s, 0.34 * s, 0.08 * s, 2)],
                color, w, closed=True)
        L([(-0.18, -0.12), (0.18, -0.4)])
        sp.ellipse((*P(0.14, -0.45), *P(0.24, -0.35)), fill=color)
        sp.ellipse((*P(-0.3, -0.03), *P(-0.02, 0.25)), outline=color, width=w * 0.8)
        L([(0.1, 0.02), (0.26, 0.02)], w * 0.7)
        L([(0.1, 0.11), (0.26, 0.11)], w * 0.7)
        L([(0.1, 0.2), (0.26, 0.2)], w * 0.7)
    elif name == "gear":
        pts = []
        for i in range(48):
            a = 2 * math.pi * i / 48
            tooth = (i // 3) % 2 == 0
            r = 0.38 if tooth else 0.29
            pts.append(P(r * math.cos(a), r * math.sin(a)))
        sp.line(pts, color, w, closed=True)
        sp.ellipse((*P(-0.11, -0.11), *P(0.11, 0.11)), outline=color, width=w)
    elif name == "globe":
        sp.ellipse((*P(-0.36, -0.36), *P(0.36, 0.36)), outline=color, width=w)
        sp.ellipse((*P(-0.15, -0.36), *P(0.15, 0.36)), outline=color, width=w * 0.8)
        L([(-0.36, 0.0), (0.36, 0.0)], w * 0.8)
    elif name == "star":
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            r = 0.4 if i % 2 == 0 else 0.18
            pts.append(P(r * math.cos(a), r * math.sin(a) + 0.03))
        sp.line(pts, color, w, closed=True)
    elif name == "close":
        L([(-0.25, -0.25), (0.25, 0.25)], w * 1.2)
        L([(0.25, -0.25), (-0.25, 0.25)], w * 1.2)
    elif name == "play":
        sp.poly([P(-0.16, -0.28), P(0.26, 0.0), P(-0.16, 0.28)], color)
        sp.line([P(-0.16, -0.28), P(0.26, 0.0), P(-0.16, 0.28)], color, w, closed=True)
    elif name == "skip":
        L([(-0.3, -0.24), (-0.06, 0.0), (-0.3, 0.24)])
        L([(-0.02, -0.24), (0.22, 0.0), (-0.02, 0.24)])
        L([(0.32, -0.24), (0.32, 0.24)])
    elif name == "next":
        sp.poly([P(-0.2, -0.22), P(0.24, 0.0), P(-0.2, 0.22)], color)
    elif name == "home":
        L([(-0.34, 0.0), (0.0, -0.32), (0.34, 0.0)])
        L([(-0.24, -0.08), (-0.24, 0.32), (0.24, 0.32), (0.24, -0.08)])
        L([(-0.06, 0.32), (-0.06, 0.12), (0.06, 0.12), (0.06, 0.32)], w * 0.8)
    elif name == "arrow":  # points up; rotate by drawing variants
        L([(0.0, 0.3), (0.0, -0.28)], w * 1.2)
        L([(-0.2, -0.08), (0.0, -0.3), (0.2, -0.08)], w * 1.2)
    elif name == "captions":
        sp.line([(cx + x, cy + y) for x, y in rrect_path(-0.38 * s, -0.26 * s, 0.38 * s, 0.26 * s, 0.08 * s, 2)],
                color, w, closed=True)
        L([(-0.24, -0.04), (0.08, -0.04)], w * 0.8)
        L([(-0.24, 0.1), (0.22, 0.1)], w * 0.8)
    elif name == "motion":
        for y in (-0.16, 0.12):
            pts = [P(-0.34 + 0.68 * i / 20, y + 0.08 * math.sin(i / 20 * 2 * math.pi)) for i in range(21)]
            sp.line(pts, color, w)
        L([(-0.36, 0.34), (0.36, -0.34)], w * 0.9)
    elif name == "vibrate":
        sp.line([(cx + x, cy + y) for x, y in rrect_path(-0.14 * s, -0.3 * s, 0.14 * s, 0.3 * s, 0.05 * s, 2)],
                color, w, closed=True)
        for sx in (1, -1):
            L([(sx * 0.26, -0.16), (sx * 0.34, -0.08), (sx * 0.26, 0.0), (sx * 0.34, 0.08), (sx * 0.26, 0.16)], w * 0.8)
    elif name == "hand":
        L([(-0.34, -0.1), (0.3, -0.1)], w)
        L([(0.14, -0.26), (0.32, -0.1), (0.14, 0.06)], w)
        L([(0.34, 0.16), (-0.3, 0.16)], w)
        L([(-0.14, 0.0), (-0.32, 0.16), (-0.14, 0.32)], w)
    elif name == "sound":
        sp.poly([P(-0.34, -0.1), P(-0.18, -0.1), P(0.0, -0.28), P(0.0, 0.28), P(-0.18, 0.1), P(-0.34, 0.1)], color)
        for r in (0.16, 0.3):
            sp.line([P(0.04 + r * math.cos(a), r * math.sin(a)) for a in np.linspace(-0.9, 0.9, 12)], color, w * 0.8)
    elif name == "knock":
        sp.ellipse((*P(-0.36, -0.06), *P(-0.2, 0.1)), fill=color)
        sp.ellipse((*P(-0.12, -0.06), *P(0.04, 0.1)), fill=color)
        sp.ellipse((*P(0.2, -0.06), *P(0.36, 0.1)), fill=color)
    elif name == "check":
        L([(-0.28, 0.02), (-0.08, 0.22), (0.3, -0.2)], w * 1.3)
    elif name == "roll":  # tumble: a loop turning forward (clockwise) with an arrowhead, speed dashes behind
        ox, oy, r = 0.1, -0.04, 0.25
        a0, a1 = math.radians(150), math.radians(395)
        arc = [(ox + r * math.cos(a), oy + r * math.sin(a)) for a in np.linspace(a0, a1, 30)]
        L(arc, w * 1.1)
        ex, ey = arc[-1]
        tx, ty = -math.sin(a1), math.cos(a1)  # clockwise tangent at the arrow tip
        nx, ny = -ty, tx
        L([(ex - 0.15 * tx + 0.11 * nx, ey - 0.15 * ty + 0.11 * ny), (ex + 0.03 * tx, ey + 0.03 * ty),
           (ex - 0.15 * tx - 0.11 * nx, ey - 0.15 * ty - 0.11 * ny)], w * 1.1)
        L([(-0.44, -0.08), (-0.27, -0.08)], w * 0.8)
        L([(-0.4, 0.1), (-0.25, 0.1)], w * 0.8)
        L([(-0.18, 0.34), (0.34, 0.34)], w * 0.8)


ICONS = ["jump", "examine", "talk", "pause", "notebook", "bat", "radio", "gear", "globe", "star", "close", "play",
         "skip", "next", "home", "arrow", "knock", "captions", "motion", "vibrate", "hand", "sound", "check", "roll"]


# --------------------------------------------------------------------------------------------------- backgrounds

def keyframe():
    """Cover-crop the 16:9 key frame to 19.5:9, keeping the owl, the attic window and Pip's cape."""
    im = Image.open(os.path.join(EVID, "env_keyframe_v2.png")).convert("RGB")
    s = W / im.size[0]
    im = im.resize((W, int(round(im.size[1] * s))), Image.LANCZOS)
    top = 118  # keeps the owl's ears and Pip's shoulders
    return im.crop((0, top, W, top + H)).convert("RGBA")


def gradient_scrim(canvas, color, a0, a1, x0, x1, axis="x", ease=True):
    arr = np.zeros((H, W), np.float32)
    if axis == "x":
        t = np.clip((np.arange(W) - x0) / (x1 - x0), 0, 1)
        if ease:
            t = t * t * (3 - 2 * t)
        arr[:] = (a0 + (a1 - a0) * t)[None, :]
    else:
        t = np.clip((np.arange(H) - x0) / (x1 - x0), 0, 1)
        if ease:
            t = t * t * (3 - 2 * t)
        arr[:] = (a0 + (a1 - a0) * t)[:, None]
    lay = Image.new("RGBA", (W, H), color[:3] + (0,))
    lay.putalpha(Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8), "L"))
    canvas.alpha_composite(lay)


def radial_soft(canvas, cx, cy, r, color, alpha):
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
    a = np.clip(1 - d, 0, 1) ** 1.6 * alpha
    lay = Image.new("RGBA", (W, H), color[:3] + (0,))
    lay.putalpha(Image.fromarray((a * 255).astype(np.uint8), "L"))
    canvas.alpha_composite(lay)


def guides(canvas):
    d = ImageDraw.Draw(canvas)
    for x in (SAFE_L, W - SAFE_R):
        for y in range(0, H, 24):
            d.line((x, y, x, y + 12), fill=(0, 255, 200, 160), width=2)
    y = H - SAFE_B
    for x in range(0, W, 24):
        d.line((x, y, x + 12, y), fill=(0, 255, 200, 160), width=2)


# --------------------------------------------------------------------------------------------------- shared HUD bits

STICK_C = (SAFE_L + 335, H - SAFE_B - 304)          # ref (215,195) from the safe corner
JUMP_C = (W - SAFE_R - 218, H - SAFE_B - 218)       # ref (-140,140)
USE_C = (W - SAFE_R - 500, H - SAFE_B - 175)        # ref (-320,110)
PAUSE_C = (W - SAFE_R - 84, 96)
NOTE_C = (W - SAFE_R - 84 - 176, 96)


def logo_sprite(seed=11):
    """'Spooky Street' as cut-out letters on a stitched violet felt backing."""
    words = [("Spooky", 70, ORANGE, "felt", -10), ("Street", 70, hexc("EADBB8"), "paper", 0)]
    fpx = 70 * PT * SS
    f = font(F_DISPLAY, fpx)
    rng = random.Random(seed)
    letter_masks = []
    # layout in sprite px (SS scale)
    x_start = [0, 54 * PT * SS]
    y_line = [0, 76 * PT * SS]
    total_w = 0
    for wi, (word, _, col, kind, _) in enumerate(words):
        x = x_start[wi]
        for ch in word:
            adv = f.getlength(ch)
            cw, chh = int(adv + 80 * SS), int(fpx * 1.5)
            m = Image.new("L", (cw, chh), 0)
            ImageDraw.Draw(m).text((40 * SS, fpx * 0.25), ch, font=f, fill=255)
            ang = rng.uniform(-7, 7)
            m = m.rotate(ang, resample=Image.BICUBIC, expand=False)
            dy = rng.uniform(-6, 6) * SS
            letter_masks.append((wi, m, int(x - 40 * SS), int(y_line[wi] + dy), col, kind))
            x += adv * 0.96 + 2 * SS
        total_w = max(total_w, x)
    pad = 60 * SS
    Wd, Hd = int(total_w + 2 * pad), int(y_line[1] + fpx * 1.5 + 2 * pad)
    word_mask = Image.new("L", (Wd, Hd), 0)
    per_word = [Image.new("L", (Wd, Hd), 0) for _ in words]
    for wi, m, x, y, col, kind in letter_masks:
        word_mask.paste(255, (x + pad, y + pad), m)
        per_word[wi].paste(255, (x + pad, y + pad), m)

    def dilate(m, r):
        b = m.filter(ImageFilter.GaussianBlur(r * SS / 2))
        return b.point(lambda v: 255 if v > 18 else 0).filter(ImageFilter.GaussianBlur(0.8 * SS))

    def erode(m, r):
        b = m.filter(ImageFilter.GaussianBlur(r * SS / 2))
        return b.point(lambda v: 255 if v > 236 else 0)

    img = Image.new("RGBA", (Wd, Hd), (0, 0, 0, 0))

    def paint(mask, color, kind, sd, fuzz=False):
        nonlocal img
        if fuzz:
            noise = value_noise(Wd, Hd, 3, sd)
            mb = np.asarray(mask.filter(ImageFilter.GaussianBlur(1.2 * SS)), np.float32) / 255
            mask = Image.fromarray((np.clip((mb - 0.5 + (noise - 0.5) * 0.4) * 4 + 0.5, 0, 1) * 255).astype(
                np.uint8), "L")
        tex = texture(Wd, Hd, color, kind, sd)
        lay = Image.new("RGBA", (Wd, Hd), (0, 0, 0, 0))
        lay.paste(tex, (0, 0), mask)
        img = Image.alpha_composite(img, lay)

    def solid(mask, color):
        nonlocal img
        lay = Image.new("RGBA", (Wd, Hd), color[:3] + (0,))
        lay.putalpha(ImageChops.multiply(mask, Image.new("L", (Wd, Hd), color[3])))
        img = Image.alpha_composite(img, lay)

    backing = dilate(word_mask, 46)
    solid(dilate(backing, 7), INK)
    paint(backing, NIGHT, "felt", seed + 1, fuzz=True)
    # running stitch inside the backing edge: band intersected with a dot lattice
    band = ImageChops.subtract(erode(backing, 14), erode(backing, 19))
    yy, xx = np.mgrid[0:Hd, 0:Wd]
    per = 15 * SS
    dots = ((np.sin(xx * 2 * np.pi / per) > 0.05) ^ (np.sin(yy * 2 * np.pi / per) > 0.05)).astype(np.uint8) * 255
    st = ImageChops.multiply(band, Image.fromarray(dots, "L"))
    st = st.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6 * SS))
    st_sh = Image.new("L", (Wd, Hd), 0)
    st_sh.paste(st, (int(1 * SS), int(1.5 * SS)))
    solid(st_sh, (0, 0, 0, 110))
    solid(st, CANDY)
    for wi, (word, _, col, kind, _) in enumerate(words):
        m = per_word[wi]
        sh = Image.new("L", (Wd, Hd), 0)
        sh.paste(dilate(m, 6), (int(5 * SS), int(7 * SS)))
        solid(sh, INK[:3] + (200,))
        solid(dilate(m, 6), INK)
        paint(m, col, kind, seed + 10 + wi, fuzz=(kind == "felt"))
        # inner stitch on the letters themselves
        lb = ImageChops.subtract(erode(m, 7), erode(m, 10))
        per2 = 11 * SS
        dots2 = ((np.sin(xx * 2 * np.pi / per2) > 0.0) ^ (np.sin(yy * 2 * np.pi / per2) > 0.0)).astype(np.uint8) * 255
        s2 = ImageChops.multiply(lb, Image.fromarray(dots2, "L")).filter(ImageFilter.GaussianBlur(0.5 * SS))
        solid(s2, (INK if kind == "felt" else PURPLE)[:3] + (190,))
    sp = Sprite(Wd / SS - 0, Hd / SS - 0, 0)
    sp.img = img
    return sp


# --------------------------------------------------------------------------------------------------- screens

def mock_title():
    c = keyframe()
    gradient_scrim(c, NIGHT, 0.82, 0.0, 0, 1350)
    gradient_scrim(c, NIGHT, 0.0, 0.35, 900, H, axis="y")

    logo = logo_sprite()
    lw = logo.render().size[0]
    place(c, logo, SAFE_L + 10 + lw / 2, 290, angle=2.0, shadow=(6, 10), shadow_alpha=170, blur=4)

    # paper bat hanging from the logo backing by a thread
    bat = Sprite(200, 230, 10)
    bat.line([(100, 0), (101, 110)], BONE_D, 2.2)
    paper_bat(bat, 101, 150, 170, CANDY, flap=0.15)
    place(c, bat, 1075, 300, angle=-8, shadow=(4, 6), shadow_alpha=110)

    # case-name strip
    sw = int(font(F_BOLD, 18 * PT).getlength("Three Knocks on Lantern Lane") + 90)
    strip = paper_card(sw, 86, 21, BONE, INK, PLUM, 10, 2.6, 3, 10, 10, 8)
    strip.text(sw / 2, 41, "Three Knocks on Lantern Lane", F_BOLD, 18 * PT, INK, "mm")
    place(c, strip, SAFE_L + 70 + sw / 2, 590, angle=-1.5)

    cont = felt_button(620, 156, "Continue", ORANGE, INK, CANDY, 31, icon="play",
                       sub="Find June  ·  7 paper bats", size=24)
    place(c, cont, SAFE_L + 60 + 310, 770, angle=1.0, shadow=(5, 8))
    new = paper_button(520, 116, "New game", 33, size=20)
    place(c, new, SAFE_L + 60 + 260, 930, angle=-1.2)

    # settings + language (small, bottom right)
    lang = paper_button(330, 120, "English", 41, icon="globe", size=17)
    place(c, lang, W - SAFE_R - 165 - 150, H - SAFE_B - 70, angle=-1.0)
    gear = felt_disc(130, NIGHT, 235, BONE, 10, seed=43)
    icon_draw(gear, "gear", 65, 65, 66, BONE, 5)
    place(c, gear, W - SAFE_R - 65, H - SAFE_B - 70)
    sp = Sprite(400, 40, 4)
    sp.text(0, 20, "Version 0.3 · s000.2", F_BODY, 11 * PT, BONE[:3] + (150,), "lm")
    place(c, sp, SAFE_L + 50 + 200, H - SAFE_B - 34, shadow=None)
    return c


def stick_ghost(c):
    radial_soft(c, *STICK_C, 300, INK, 0.28)
    base = felt_disc(343, NIGHT, 105, BONE[:3] + (170,), 16, seed=51, ow=2.5)
    for ang in range(0, 360, 90):
        a = math.radians(ang)
        tip = (171.5 + math.sin(a) * 146, 171.5 - math.cos(a) * 146)
        l = (171.5 + math.sin(a) * 124 - math.cos(a) * 18, 171.5 - math.cos(a) * 124 - math.sin(a) * 18)
        r = (171.5 + math.sin(a) * 124 + math.cos(a) * 18, 171.5 - math.cos(a) * 124 + math.sin(a) * 18)
        base.line([l, tip, r], BONE[:3] + (180,), 5)
    place(c, base, *STICK_C, shadow=None)
    knob = felt_disc(150, BONE, 205, PURPLE, 14, seed=52, ow=3)
    knob.ellipse((52, 52, 98, 98), fill=None, outline=PURPLE[:3] + (120,), width=3)
    place(c, knob, *STICK_C, shadow=(3, 5), shadow_alpha=120)


def action_cluster(c, use_label="Examine", use_icon="examine", pressed_jump=False):
    radial_soft(c, JUMP_C[0] - 120, JUMP_C[1], 420, INK, 0.3)
    jump = felt_disc(234, NIGHT, 225, CANDY, 14, seed=61)
    icon_draw(jump, "jump", 117, 96, 96, BONE, 6.5)
    jump.text(117, 164, "Jump", F_BOLD, 13 * PT, BONE, "mm")
    place(c, jump, *JUMP_C, shadow=(4, 6), shadow_alpha=140)
    if use_label:
        use = felt_disc(196, ORANGE, 255, CANDY, 13, seed=62)
        icon_draw(use, use_icon, 98, 92, 104, INK, 7)
        place(c, use, *USE_C, shadow=(4, 7), shadow_alpha=150)
        tw = int(font(F_BOLD, 15 * PT).getlength(use_label) + 52)
        tag = paper_card(tw, 64, 63, BONE, INK, None, 30, 1.6, 2.6, 0, pad=10)
        tag.text(tw / 2, 31, use_label, F_BOLD, 15 * PT, INK, "mm")
        place(c, tag, USE_C[0], USE_C[1] + 112, angle=-2, shadow=(3, 5), shadow_alpha=130)


def top_bar(c, notebook_badge=None):
    radial_soft(c, W - SAFE_R - 170, 90, 330, INK, 0.25)
    pause = felt_disc(150, NIGHT, 225, BONE, 12, seed=71)
    icon_draw(pause, "pause", 75, 75, 72, BONE, 5)
    place(c, pause, *PAUSE_C, shadow=(3, 5), shadow_alpha=130)
    nb = felt_disc(150, NIGHT, 225, BONE, 12, seed=72)
    icon_draw(nb, "notebook", 75, 73, 78, BONE, 5)
    place(c, nb, *NOTE_C, shadow=(3, 5), shadow_alpha=130)
    if notebook_badge:
        tag = paper_card(200, 58, 75, CANDY, INK, None, 28, 1.4, 2.6, 0, pad=8)
        tag.text(100, 29, notebook_badge, F_BOLD, 14 * PT, INK, "mm")
        place(c, tag, NOTE_C[0] - 5, NOTE_C[1] + 98, angle=3, shadow=(3, 4), shadow_alpha=120)


def bat_counter(c, n=7, total=30, x=None, y=96):
    radial_soft(c, SAFE_L + 160, 90, 300, INK, 0.25)
    n1 = font(F_DISPLAY, 24 * PT).getlength(f"{n}")
    n2 = font(F_DISPLAY, 15 * PT).getlength(f"/{total}")
    tw = int(150 + n1 + n2 + 46)
    tag = paper_card(tw, 118, 81, NIGHT, INK, BONE[:3] + (220,), 50, 1.6, 3, 11, 9, 7, kind="felt", fuzz=True)
    tag.ellipse((26, 14, 128, 112), fill=BONE, outline=INK, width=3)
    paper_bat(tag, 77, 66, 92, None, flap=0.12)
    tag.text(150, 62, f"{n}", F_DISPLAY, 24 * PT, BONE, "lm")
    tag.text(150 + n1 + 4, 70, f"/{total}", F_DISPLAY, 15 * PT, BONE[:3] + (190,), "lm")
    place(c, tag, x or SAFE_L + 24 + tw / 2, y, angle=-1.5, shadow=(3, 5), shadow_alpha=130)


def caption(c, text, cx, cy, cue_angle=None, size=17):
    """Caption strip: opaque night felt backing, bone Andika Bold, optional direction arrow toward the source."""
    f = font(F_BOLD, size * PT)
    tw = f.getlength(text)
    cue = 92 if cue_angle is not None else 0
    cw, ch = int(tw + 64 + cue), int(size * PT * 1.75)
    cap = paper_card(cw, ch, 95, NIGHT, INK, None, 24, 1.2, 3, 0, kind="felt", fuzz=True)
    if cue_angle is not None:
        d = Sprite(72, 72, 6)
        d.ellipse((0, 0, 72, 72), fill=PURPLE)
        icon_draw(d, "arrow", 36, 36, 56, CANDY, 5.5)
        im = d.render(cue_angle)
        cap.paste_img(im, 22 + 36 - im.size[0] / 2, ch / 2 - im.size[1] / 2, im)
    cap.text(32 + cue + tw / 2, ch / 2 - 2, text, F_BOLD, size * PT, BONE, "mm")
    place(c, cap, cx, cy, shadow=(3, 5), shadow_alpha=120)


def mock_hud(guides_on=False):
    c = keyframe()
    # interaction prompt over the mailbox: a small pinned stitched ring with the verb icon, below window brightness
    mb = (912, 1022 - 118 - 64)
    pr = felt_disc(96, NIGHT, 200, CANDY, 9, seed=91, ow=2.5)
    icon_draw(pr, "examine", 48, 48, 50, BONE, 4.2)
    place(c, pr, mb[0], mb[1] - 40, shadow=(2, 4), shadow_alpha=110)
    tri = Sprite(30, 22, 4)
    tri.poly([(2, 2), (28, 2), (15, 20)], NIGHT[:3] + (200,))
    place(c, tri, mb[0], mb[1] + 17, shadow=None)

    stick_ghost(c)
    action_cluster(c)
    top_bar(c)
    bat_counter(c)

    caption(c, "[Three knocks from the blue house]", 1170, H - SAFE_B - 72, -35)
    if guides_on:
        guides(c)
    return c


def portrait(d):
    im = Image.open(os.path.join(EVID, "cast_v5_standard_fable.png")).convert("RGBA").crop((60, 905, 410, 1255))
    im = im.resize((d * SS, d * SS), Image.LANCZOS)
    m = Image.new("L", im.size, 0)
    ImageDraw.Draw(m).ellipse((0, 0, d * SS - 1, d * SS - 1), fill=255)
    return im, m


def mock_dialogue():
    c = keyframe()
    gradient_scrim(c, NIGHT, 0.0, 0.78, 560, H, axis="y")
    # card
    cw, ch = 1640, 300
    cx, cy = W / 2 + 90, H - SAFE_B - ch / 2 - 30
    card = paper_card(cw, ch, 101, BONE, INK, PURPLE, 34, 3.4, 3.4, 15, 11, 8)
    TX = 300
    card.text(TX, 112, "I saw June's silver cape go in.", F_BODY, 26 * PT, INK, "ls")
    card.text(TX, 196, "Right there!", F_BODY, 26 * PT, INK, "ls")
    # clue words marked with a pencil underline (notebook-worthy), never with UI silver
    fb = font(F_BODY, 26 * PT)
    x0 = TX + fb.getlength("I saw June's ")
    x1 = x0 + fb.getlength("silver cape")
    pts = [(x0 + (x1 - x0) * i / 30, 128 + 2.5 * math.sin(i * 0.9)) for i in range(31)]
    card.line(pts, ORANGE, 5)
    # continue affordance
    nx = felt_disc(112, ORANGE, 255, CANDY, 9, seed=104)
    icon_draw(nx, "next", 56, 56, 64, INK, 5)
    ni = nx.render()
    card.paste_img(ni, cw - ni.size[0] - 18, ch - ni.size[1] - 16, ni)
    card.text(cw - 170, ch - 84, "Tap to continue", F_BOLD, 14 * PT, GRAPHITE, "rm")
    # progress pips (line 1 of 2)
    for i in range(2):
        r = 9
        x = TX + 12 + i * 34
        card.ellipse((x - r, ch - 52 - r, x + r, ch - 52 + r), fill=PURPLE if i == 0 else None, outline=PURPLE,
                     width=3)
    place(c, card, cx, cy, angle=-0.6, shadow=(6, 9))
    # portrait
    pd = 300
    fr = felt_disc(pd + 40, ORANGE, 255, CANDY, 11, seed=107)
    im, m = portrait(pd)
    lay = fr.layer()
    lay.paste(im, (int((fr.pad + 20) * SS), int((fr.pad + 20) * SS)), m)  # already at SS
    fr.comp(lay)
    fr.ellipse((20, 20, 20 + pd, 20 + pd), outline=INK, width=4)
    place(c, fr, cx - cw / 2 + 60, cy - 70, angle=-3, shadow=(5, 8))
    # name tab
    tab = paper_card(190, 80, 109, ORANGE, INK, CANDY, 22, 1.4, 3, 9, 8, 6, kind="felt", fuzz=True)
    tab.text(95, 38, "Bea", F_DISPLAY, 26 * PT, INK, "mm")
    place(c, tab, cx - cw / 2 + 270 + 110, cy - ch / 2 - 10, angle=-3)
    # skip + log (top right, out of the reading path)
    skip = paper_button(250, 110, "Skip", 111, icon="skip", size=17)
    place(c, skip, W - SAFE_R - 150, 96, angle=1.2)
    return c


def pencil(sp, pts, color=GRAPHITE, w=2.6, passes=2, jitter=1.4, seed=0):
    rng = random.Random(seed)
    for p in range(passes):
        q = [(x + rng.uniform(-jitter, jitter), y + rng.uniform(-jitter, jitter)) for x, y in pts]
        sp.line(q, color[:3] + (170 if p else 220,), w * (0.8 if p else 1.0))


def sketch_house(sp, ox, oy, s, seed=5):
    """Pencil sketch: treehouse, plank bridge to the round attic window, torn cape on the sill."""
    P = lambda x, y: (ox + x * s, oy + y * s)
    pc = lambda pts, **k: pencil(sp, [P(*p) for p in pts], seed=seed + len(pts) + int(pts[0][0]), **k)
    # house gable
    pc([(160, 200), (160, 70), (270, -20), (380, 70), (380, 200)])
    pc([(140, 75), (270, -38), (400, 75)], w=2.2)
    pc([(110, 200), (420, 200)])
    win = [P(270 + 40 * math.cos(a), 60 + 40 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 40)]
    pencil(sp, win, seed=seed + 2)
    pc([(270, 20), (270, 100)], w=2)
    pc([(230, 60), (310, 60)], w=2)
    pc([(222, 104), (318, 104)], w=3)  # sill
    # door (shut)
    pc([(250, 200), (250, 140), (290, 140), (290, 200)], w=2.2)
    # tree + treehouse
    pc([(0, 200), (8, 110), (4, 60)], w=3)
    pc([(30, 200), (24, 110), (32, 60)], w=3)
    blob = [P(16 + 80 * math.cos(a) * (1 + 0.08 * math.sin(5 * a)), -20 + 52 * math.sin(a) * (1 + 0.1 * math.cos(7 * a)))
            for a in np.linspace(0, 2 * math.pi, 50)]
    pencil(sp, blob, w=2.2, seed=seed + 9)
    pc([(-40, 60), (80, 60)], w=3)
    pc([(-34, 60), (-34, 34), (74, 34), (74, 60)], w=2)
    # plank bridge to the sill, in orange pencil = the route you proved
    for i in range(9):
        x = 86 + i * 15
        y = 62 + i * 4.6
        pencil(sp, [P(x, y - 7), P(x + 6, y + 7)], ORANGE_D, 3.2, 1, 0.4, seed + 30 + i)
    pencil(sp, [P(80, 54), P(222, 92)], ORANGE_D, 2, 1, 0.6, seed + 50)
    pencil(sp, [P(80, 70), P(222, 108)], ORANGE_D, 2, 1, 0.6, seed + 51)
    # cape snag on the sill: silver swatch (the clue) with a torn corner
    sp.poly([P(300, 104), P(326, 104), P(330, 140), P(320, 132), P(312, 144), P(302, 130)], SILVER)
    pc([(300, 104), (326, 104), (330, 140), (320, 132), (312, 144), (302, 130), (300, 104)], w=2)


def silhouette_unknown(sp, cx, cy, r, seed):
    lay = sp.layer()
    d = ImageDraw.Draw(lay)
    x0, y0 = sp.S(cx - r, cy - r)
    x1, y1 = sp.S(cx + r, cy + r)
    d.ellipse((x0, y0, x1, y1), fill=GRAPHITE[:3] + (36,))
    sp.comp(lay)
    sp.stitches(circle_path(cx, cy, r, 2), GRAPHITE[:3] + (200,), 10, 9, 2.6, shadow=False)
    sp.text(cx, cy + 6, "?", F_DISPLAY, r * 1.3, GRAPHITE[:3] + (200,), "mm")


def note_tag(sp, x, y, text, seed, color=BONE, angle=0, swatch=None, anchor="m"):
    """Small evidence annotation: a pinned paper slip; a swatch replaces the pin hole for physical evidence."""
    f = font(F_BOLD, 13 * PT)
    lead = 58 if swatch else 40
    w = int(f.getlength(text) + lead + 22)
    t = paper_card(w, 58, seed, color, INK, None, 10, 1.2, 2.4, 0, pad=8)
    if swatch:
        t.poly([(14, 12), (44, 10), (42, 46), (32, 38), (24, 48), (14, 40)], swatch)
        t.line([(14, 12), (44, 10), (42, 46), (32, 38), (24, 48), (14, 40)], INK, 2, closed=True)
    else:
        t.ellipse((14, 23, 26, 35), fill=None, outline=INK, width=2.4)
    t.text(lead, 29, text, F_BOLD, 13 * PT, INK, "lm")
    im = t.render(angle)
    if anchor == "l":
        x = x + im.size[0] / 2 - 8
    sp.paste_img(im, x - im.size[0] / 2, y - im.size[1] / 2, im)


def wrap(sp, x, y, text, path, px, maxw, fill, lh=1.2, anchor="la"):
    f = font(path, px)
    words, lines, cur = text.split(), [], ""
    for wd in words:
        t = (cur + " " + wd).strip()
        if f.getlength(t) > maxw and cur:
            lines.append(cur)
            cur = wd
        else:
            cur = t
    lines.append(cur)
    for i, ln in enumerate(lines):
        sp.text(x, y + i * px * lh, ln, path, px, fill, anchor)
    return len(lines)


def mock_notebook():
    c = keyframe()
    c.alpha_composite(Image.new("RGBA", (W, H), NIGHT[:3] + (215,)))
    pw, ph = 1040, 830
    cx, cy = W / 2 - 10, 74 + ph / 2
    cover = paper_card(2 * pw + 70, ph + 50, 201, PLUM, INK, CANDY, 40, 1.6, 3.4, 14, 12, 8, kind="felt", fuzz=True)
    place(c, cover, cx, cy, shadow=(6, 10))
    pages = []
    for i in range(2):
        pg = paper_card(pw, ph, 210 + i, BONE, INK, None, 18, 2.2, 2.6, 0)
        for y in range(196, ph - 30, 56):
            pg.line([(48, y), (pw - 48, y)], hexc("9DB4C8", 60), 1.6)
        pages.append(pg)
    L, R = pages
    Q = 17 * PT
    # ---- left page: heading + question 1, answered
    L.text(64, 70, "Case notes", F_DISPLAY, 28 * PT, INK, "lm")
    L.text(66, 128, "Three Knocks on Lantern Lane", F_BOLD, 14 * PT, GRAPHITE, "lm")
    L.text(64, 212, "1", F_DISPLAY, 22 * PT, ORANGE_D, "lm")
    L.text(116, 212, "How did June get inside?", F_BOLD, Q, INK, "lm")
    sketch_house(L, 150, 330, 1.0, seed=7)
    st = Sprite(350, 120, 40)
    st.line(rrect_path(0, 0, 350, 120, 16, 3), GREEN, 5, closed=True)
    st.stitches(rrect_path(10, 10, 340, 110, 10, 2), GREEN, 10, 7, 3, shadow=False)
    icon_draw(st, "check", 62, 60, 80, GREEN, 7)
    st.text(112, 60, "Found it!", F_BOLD, 15 * PT, GREEN, "lm")
    sti = st.render(-9)
    L.paste_img(sti, 600, 300, sti)
    L.text(64, 586, "Through the attic window!", F_BOLD, 19 * PT, INK, "lm")
    L.text(64, 642, "The treehouse bridge reaches the sill.", F_BODY, 16 * PT, GRAPHITE, "lm")
    note_tag(L, 60, 730, "Silver cloth, torn", 221, angle=-2, swatch=SILVER, anchor="l")
    note_tag(L, 540, 724, "Bea saw the cape", 222, angle=1.5, anchor="l")
    note_tag(L, 300, 800, "Vale: the door stayed shut", 223, angle=-1, anchor="l")
    # ---- right page: questions 2 and 3 unknown, paper bats
    R.text(64, 70, "2", F_DISPLAY, 22 * PT, ORANGE_D, "lm")
    R.text(116, 70, "Where are the knocks coming from?", F_BOLD, Q, INK, "lm")
    silhouette_unknown(R, 140, 200, 68, 1)
    R.text(240, 172, "Not found yet.", F_BODY, 16 * PT, GRAPHITE, "lm")
    note_tag(R, 236, 244, "Three knocks, blue house", 231, angle=1, anchor="l")
    R.text(64, 352, "3", F_DISPLAY, 22 * PT, ORANGE_D, "lm")
    R.text(116, 352, "What did the knocking move?", F_BOLD, Q, INK, "lm")
    silhouette_unknown(R, 140, 482, 68, 2)
    R.text(240, 482, "Not found yet.", F_BODY, 16 * PT, GRAPHITE, "lm")
    R.stitches([(60, 584), (pw - 60, 584)], PURPLE[:3] + (160,), 10, 9, 2.4, closed=False, shadow=False)
    R.text(64, 636, "Paper bats", F_DISPLAY, 20 * PT, INK, "lm")
    R.text(pw - 64, 636, "7 / 30", F_DISPLAY, 20 * PT, INK, "rm")
    for i in range(30):
        g, k = divmod(i, 10)
        x = 96 + g * 316 + (k % 5) * 58
        y = 704 + (k // 5) * 42
        if i < 7:
            paper_bat(R, x, y, 50, None)
        else:
            R.poly(bat_points(x, y, 50), GRAPHITE[:3] + (60,))
    for g, lab in enumerate(["Sketch page", "Cape colour", "Sketch page"]):
        R.text(96 + g * 316 + 116, 786, lab, F_BOLD, 13 * PT, GRAPHITE, "mm")
    place(c, L, cx - pw / 2 - 4, cy, shadow=(3, 5), shadow_alpha=90)
    place(c, R, cx + pw / 2 + 4, cy, shadow=(3, 5), shadow_alpha=90)
    sp = Sprite(60, ph - 40, 6)
    for i in range(14):
        y = 30 + i * (ph - 100) / 13
        sp.line([(10, y), (50, y + 6)], CANDY, 4.5)
    place(c, sp, cx, cy, shadow=(1, 2), shadow_alpha=120)
    # controls outside the pages
    close = felt_disc(130, NIGHT, 255, BONE, 11, seed=241)
    icon_draw(close, "close", 65, 65, 62, BONE, 5)
    place(c, close, W - SAFE_R - 50, 72, shadow=(3, 5))
    hint = felt_button(300, 120, "Hint radio", ORANGE, INK, CANDY, 243, icon="radio", size=18)
    hw = hint.render().size[0]
    place(c, hint, W - SAFE_R - hw / 2 + 10, H - SAFE_B - 56, angle=-1.5)
    back = paper_button(300, 112, "Back to the street", 244, icon="home", size=17)
    bw = back.render().size[0]
    place(c, back, SAFE_L + bw / 2 - 10, H - SAFE_B - 56, angle=1)
    return c


def toggle(sp, x, y, on, label, icon):
    icon_draw(sp, icon, x + 24, y, 50, INK, 3.6)
    sp.text(x + 70, y, label, F_BOLD, 17 * PT, INK, "lm")
    tw, th = 132, 66
    tx = sp.w - 60 - tw
    track = rrect_path(tx, y - th / 2, tx + tw, y + th / 2, th / 2, 2)
    sp.poly(track, (GREEN if on else hexc("CDBB98"))[:3] + (255,))
    sp.line(track, INK, 3, closed=True)
    kx = tx + (tw - th / 2) if on else tx + th / 2
    sp.ellipse((kx - 27, y - 27, kx + 27, y + 27), fill=BONE, outline=INK, width=3)
    if on:
        icon_draw(sp, "check", kx, y, 34, INK, 3.6)


def mock_pause():
    c = keyframe()
    c.alpha_composite(Image.new("RGBA", (W, H), NIGHT[:3] + (190,)))
    cw, ch = 640, 960
    cx, cy = W / 2 - 400, H / 2 + 6
    card = paper_card(cw, ch, 301, BONE, INK, PURPLE, 36, 3.6, 3.4, 15, 11, 8)
    card.text(cw / 2, 88, "Paused", F_DISPLAY, 32 * PT, INK, "mm")
    place(c, card, cx, cy, angle=-1.0, shadow=(6, 10))
    res = felt_button(520, 132, "Resume", ORANGE, INK, CANDY, 303, icon="play", size=22)
    place(c, res, cx, cy - 238, angle=-1.0)
    items = [("Notebook", "notebook"), ("Settings", "gear"), ("Language", "globe"), ("Credits", "star")]
    for i, (t, ic) in enumerate(items):
        pressed = (t == "Settings")
        b = paper_button(500, 108, t, 310 + i, icon=ic, size=19, pressed=pressed,
                         stitch=ORANGE if pressed else PURPLE)
        place(c, b, cx + (0 if not pressed else 2), cy - 92 + i * 126 + (4 if pressed else 0), angle=-1.0,
              shadow=(5, 7) if not pressed else (2, 3))
    home = Sprite(500, 60, 4)
    icon_draw(home, "home", 30, 30, 44, GRAPHITE, 3.6)
    home.text(62, 30, "Back to title", F_BOLD, 15 * PT, GRAPHITE, "lm")
    place(c, home, cx + 40, cy + 412, angle=-1.0, shadow=None)
    # settings card, opened from Settings
    sw, sh = 980, 860
    sx, sy = W / 2 + 400, H / 2 + 10
    s = paper_card(sw, sh, 321, BONE, INK, ORANGE, 30, 3.0, 3.2, 14, 11, 8)
    s.text(70, 92, "Settings", F_DISPLAY, 28 * PT, INK, "lm")
    rows = [("Large captions", "captions", True), ("Caption backing", "captions", True),
            ("Reduced motion", "motion", False), ("Vibration", "vibrate", True), ("Left-handed controls", "hand",
                                                                                   False)]
    for i, (t, ic, on) in enumerate(rows):
        y = 210 + i * 116
        toggle(s, 60, y, on, t, ic)
        if i < len(rows) - 1:
            s.stitches([(60, y + 58), (sw - 60, y + 58)], hexc("CDBB98"), 8, 8, 2, closed=False, shadow=False)
    s.text(70, sh - 90, "Volume sliders are on page 2.", F_BODY, 14 * PT, GRAPHITE, "lm")
    place(c, s, sx, sy, angle=0.8, shadow=(6, 10))
    tab = felt_disc(130, NIGHT, 255, BONE, 11, seed=331)
    icon_draw(tab, "close", 65, 65, 62, BONE, 5)
    place(c, tab, sx + sw / 2 - 20, sy - sh / 2 + 20)
    return c


# --------------------------------------------------------------------------------------------------- contact sheet

def mock_sheet(mocks, out_dir):
    SW = 3000
    thumb_w = 940
    tw, th = thumb_w, int(thumb_w * H / W)
    gap = 50
    sheet_h = 3300
    sheet = texture(SW, sheet_h, NIGHT, "felt", 900)
    d = ImageDraw.Draw(sheet)
    d.text((60, 60), "Spooky Street · UI language", font=font(F_DISPLAY, 84), fill=BONE)
    d.text((64, 170), "Identity stage 3 · handmade, stitched, pronounced · iPhone landscape 2532×1170 (@3x)",
           font=font(F_BOLD, 40), fill=BONE[:3] + (200,))
    labels = ["1 Title", "2 HUD (play)", "3 Dialogue", "4 Notebook", "5 Pause + Settings"]
    for i, (lab, im) in enumerate(zip(labels, mocks)):
        r, k = divmod(i, 3)
        x = 60 + k * (tw + gap)
        y = 260 + r * (th + 120)
        t = im.convert("RGB").resize((tw, th), Image.LANCZOS)
        d.rectangle((x - 6, y - 6, x + tw + 6, y + th + 6), fill=INK)
        sheet.paste(t, (x, y))
        d.text((x, y + th + 18), lab, font=font(F_BOLD, 44), fill=BONE)
    # panel in the 6th slot: swatches
    x0 = 60 + 2 * (tw + gap)
    y0 = 260 + th + 120
    d.text((x0, y0 - 6), "Colour", font=font(F_DISPLAY, 50), fill=BONE)
    for i, (nm, hx, use) in enumerate(SWATCHES):
        yy = y0 + 70 + i * 62
        d.rounded_rectangle((x0, yy, x0 + 90, yy + 50), 10, fill=hexc(hx), outline=BONE[:3] + (200,), width=2)
        d.text((x0 + 110, yy + 2), f"{nm}  {hx}", font=font(F_BOLD, 28), fill=BONE)
        d.text((x0 + 110, yy + 32), use, font=font(F_BODY, 20), fill=BONE[:3] + (190,))
    # component strip
    y = 260 + 2 * (th + 120) + 40
    d.text((60, y), "Components · normal / pressed", font=font(F_DISPLAY, 50), fill=BONE)
    sheet = sheet.convert("RGBA")
    row = y + 90
    comps = [
        (felt_button(330, 124, "Continue", ORANGE, INK, CANDY, 501, icon="play", size=19), False, "primary"),
        (felt_button(330, 124, "Continue", ORANGE, INK, CANDY, 501, icon="play", size=19, pressed=True), True,
         "pressed"),
        (paper_button(320, 112, "New game", 503, size=18), False, "secondary"),
        (paper_button(320, 112, "New game", 503, size=18, pressed=True, stitch=ORANGE), True, "pressed"),
    ]
    x = 70
    for sp, pr, lab in comps:
        im = sp.render()
        place(sheet, sp, x + im.size[0] / 2 - 30, row + 90 + (4 if pr else 0), shadow=(2, 3) if pr else (5, 8))
        ImageDraw.Draw(sheet).text((x + im.size[0] / 2 - 30, row + 190), lab, font=font(F_BODY, 26),
                                   fill=BONE[:3] + (200,), anchor="mm")
        x += im.size[0] - 10
    dx = x + 10
    for i, (nm, f, ic, col, pr) in enumerate([("Jump", NIGHT, "jump", BONE, False),
                                             ("Jump pressed", NIGHT, "jump", BONE, True),
                                             ("Use: Examine", ORANGE, "examine", INK, False),
                                             ("Use: Talk", ORANGE, "talk", INK, False)]):
        dd = 150 if not pr else 138
        fd = felt_disc(dd, f if not pr else PURPLE, 240, CANDY, 11, seed=520 + i)
        icon_draw(fd, ic, dd / 2, dd / 2, dd * 0.5, col, 6)
        place(sheet, fd, dx + 85, row + 90, shadow=(4, 6) if not pr else (1, 2))
        ImageDraw.Draw(sheet).text((dx + 85, row + 190), nm, font=font(F_BODY, 26), fill=BONE[:3] + (200,),
                                   anchor="mm")
        dx += 185
    tg = paper_card(380, 190, 530, BONE, INK, None, 20, 2, 3, 0)
    for j, on in enumerate((True, False)):
        toggle(tg, 14, 52 + j * 88, on, "On" if on else "Off", "captions")
    place(sheet, tg, dx + 200, row + 90, angle=-1, shadow=(4, 6))
    # icon set
    y2 = row + 250
    d = ImageDraw.Draw(sheet)
    d.text((60, y2), "Icons · one stroke weight, round caps, bone on felt / ink on paper", font=font(F_DISPLAY, 50),
           fill=BONE)
    ic = Sprite(SW - 120, 170, 4)
    for i, nm in enumerate(ICONS):
        x = 60 + i * 124
        ic.ellipse((x - 54, 10, x + 54, 118), fill=NIGHT[:3] + (255,), outline=BONE[:3] + (120,), width=2)
        icon_draw(ic, nm, x, 64, 72, BONE, 4.5)
        ic.text(x, 148, nm, F_BODY, 22, BONE[:3] + (200,), "mm")
    place(sheet, ic, SW / 2, y2 + 180, shadow=None)
    # paper bat + type scale
    y3 = y2 + 330
    d = ImageDraw.Draw(sheet)
    d.text((60, y3), "Type scale · pt (ref units = 2 × pt at 1334×750)", font=font(F_DISPLAY, 50), fill=BONE)
    scale = [("Logo 52 pt · Sniglet ExtraBold", F_DISPLAY, 52, "Spooky Street"),
             ("Heading 32 pt · Sniglet", F_DISPLAY, 32, "Paused"),
             ("Button 20 pt · Andika Bold", F_BOLD, 20, "Continue"),
             ("Dialogue 26 pt · Andika", F_BODY, 26, "Right there!"),
             ("Body / caption 17–19 pt · Andika Bold", F_BOLD, 18, "[Three knocks]"),
             ("Label 13 pt min · Andika Bold", F_BOLD, 13, "Silver cloth, torn")]
    yy = y3 + 90
    card = paper_card(SW - 120, 260, 601, BONE, INK, PURPLE, 26, 2.4, 3, 13, 10, 8)
    cx = 50
    for nm, f, pt, sample in scale:
        card.text(cx, 140, sample, f, pt * 1.6, INK, "ls")
        card.text(cx, 186, nm, F_BODY, 22, GRAPHITE, "la")
        cx += max(font(f, int(pt * 1.6)).getlength(sample), 330) + 70
    place(sheet, card, SW / 2, yy + 150, shadow=(5, 8))
    bt = Sprite(300, 300, 6)
    paper_bat(bt, 150, 150, 260, CANDY, 0.12)
    place(sheet, bt, SW - 240, y3 + 30, angle=-6, shadow=(4, 6))
    ImageDraw.Draw(sheet).text((SW - 240, y3 + 120), "paper bat", font=font(F_BODY, 26), fill=BONE[:3] + (200,),
                               anchor="mm")
    sheet = sheet.crop((0, 0, SW, yy + 330))
    return sheet


# --------------------------------------------------------------------------------------------------- Unity export

# Baked UI sprites for the game (art bible v2 §6): written to Assets/SpookyStreet/Content/Resources/UI and imported by
# Editor/UiSpriteImportRules.cs. Sizes are sprite pixels at 2 px per Unity reference unit (4 px per pt), matching the
# importer's 200 pixels per unit, so 9-slice borders below are in the same pixels as UiSpriteCatalog.cs. Keep names,
# sizes and borders in step with Assets/SpookyStreet/Core/Art/UiSpriteCatalog.cs (a Core test checks the PNGs).
UNITY_UI = os.path.join(ROOT, "Assets", "SpookyStreet", "Content", "Resources", "UI")
WHITE = (255, 255, 255, 255)
EXPORT_ICONS = ["jump", "examine", "pause", "notebook", "bat", "gear", "globe", "play", "close", "roll"]
STITCH_TILE, STITCH_BORDER, STITCH_INSET, STITCH_R = 144, 48, 18, 20
STITCH_DASH, STITCH_PERIOD, STITCH_W = 13.0, 24.0, 4.2


def _flat(sp):
    return sp.render()


def export_felt_disc(fill, seed):
    """256 px felt disc: fuzzy edge, nap noise, ink outline. No stitch (stitch_ring is a separate, tinted layer)."""
    sp = Sprite(240, 240, 8)
    pts = deckle(circle_path(120, 120, 120, 3), 1.6, seed, 0.2, 0.6)
    sp.fill(pts, fill[:3] + (255,), "felt", seed, fuzz=True)
    # soft cushion: darker lower rim so the disc reads as a padded cut-out, not a flat circle
    rim = Image.new("L", sp.img.size, 0)
    ImageDraw.Draw(rim).ellipse(sp.S(14, 26) + sp.S(226, 238), fill=255)
    rim = ImageChops.subtract(sp.mask(pts), rim.filter(ImageFilter.GaussianBlur(8 * SS)))
    shade = Image.new("RGBA", sp.img.size, (10, 6, 20, 0))
    shade.putalpha(rim.point(lambda v: v * 0.28))
    sp.comp(shade)
    sp.line(pts, INK, 4.0, closed=True)
    return sp.render()


def export_stitch_ring():
    """White running stitch on a circle with hole shadows; tinted candy (Jump), bone (system) or ink at use."""
    sp = Sprite(240, 240, 8)
    sp.stitches(circle_path(120, 120, 120 - 17, 2), WHITE, 12, 9, 4.4)
    return sp.render()


def export_paper(w, h, pad, radius, deck, ow, seed):
    sp = paper_card(w, h, seed, BONE, INK, None, radius, deck, ow, 0, pad=pad)
    return sp.render()


def export_felt_button(seed=31):
    """Orange felt slab with a cushion lip at the bottom; stitches come from the tiled stitch_rect layer."""
    w, h, pad = 176, 128, 8
    sp = paper_card(w, h, seed, ORANGE, INK, None, 34, 1.8, 4.0, 0, pad=pad, kind="felt", fuzz=True)
    lip = Sprite(w, h, pad)
    lip.poly(rrect_path(8, h - 20, w - 8, h - 4, 12, 3), (0, 0, 0, 70))
    lay = lip.img.filter(ImageFilter.GaussianBlur(3 * SS))
    lay.putalpha(ImageChops.multiply(lay.split()[3], sp.img.split()[3]))
    sp.comp(lay)
    sp.line(sp.outline_pts, INK, 4.0, closed=True)
    return sp.render()


def dense_open(pts, step):
    """Like dense() but for an open polyline (no closing segment back to the start)."""
    out = []
    for a, b in zip(pts, pts[1:]):
        k = max(1, int(math.dist(a, b) / step))
        out += [(a[0] + (b[0] - a[0]) * j / k, a[1] + (b[1] - a[1]) * j / k) for j in range(k)]
    return out + [pts[-1]]


def _dash_run(sp, pts, period, dash):
    """Dashes along an open polyline whose length is an exact number of periods, half a gap at each end."""
    total = path_len(pts, closed=False)
    k = max(1, round(total / period))
    per = total / k
    d = dash * per / period
    half_gap = (per - d) / 2
    marks = []
    for i in range(k):
        marks.append((i * per + half_gap, i * per + half_gap + d))
    segs = []
    cum = [0.0]
    for i in range(len(pts) - 1):
        cum.append(cum[-1] + math.dist(pts[i], pts[i + 1]))

    def at(t):
        for i in range(len(pts) - 1):
            if cum[i + 1] >= t:
                a, b = pts[i], pts[i + 1]
                u = (t - cum[i]) / max(1e-6, cum[i + 1] - cum[i])
                return (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u)
        return pts[-1]

    for t0, t1 in marks:
        n = max(2, int((t1 - t0) / 1.5))
        segs.append([at(t0 + (t1 - t0) * j / (n - 1)) for j in range(n)])
    return segs


def export_stitch_rect():
    """Tileable running stitch for 9-slice cards (Image.Type.Tiled): exact periods on the straight edges and
    mirrored corners, so tiles join with no doubled gap or stretched dash."""
    S, B, I, R = STITCH_TILE, STITCH_BORDER, STITCH_INSET, STITCH_R
    sp = Sprite(S, S, 0)
    # top-left corner run from the top junction (B, I) round the corner to the left junction (I, B)
    corner = [(B, I), (I + R, I)]
    for j in range(1, 16):
        a = math.radians(270 - 90 * j / 16)
        corner.append((I + R + R * math.cos(a), I + R + R * math.sin(a)))
    corner += [(I, I + R), (I, B)]
    segs = _dash_run(sp, dense_open(corner, 1.0), STITCH_PERIOD, STITCH_DASH)
    runs = []
    for seg in segs:
        for fx in (False, True):
            for fy in (False, True):
                runs.append([((S - x) if fx else x, (S - y) if fy else y) for x, y in seg])
    # straight edges: one dash centred in each period between the junctions
    gap = (STITCH_PERIOD - STITCH_DASH) / 2
    x = B
    while x + STITCH_PERIOD <= S - B + 0.01:
        for y in (I, S - I):
            runs.append([(x + gap, y), (x + gap + STITCH_DASH, y)])
            runs.append([(y, x + gap), (y, x + gap + STITCH_DASH)])
        x += STITCH_PERIOD
    lay = sp.layer()
    dr = ImageDraw.Draw(lay)
    wpx = int(STITCH_W * SS)
    for col, off in (((0, 0, 0, 90), (0.8, 1.2)), (WHITE, (0, 0))):
        for s in runs:
            q = [(px + off[0] * SS, py + off[1] * SS) for px, py in sp.Sp(s)]
            dr.line(q, fill=col, width=wpx)
            for px, py in (q[0], q[-1]):
                r = wpx / 2
                dr.ellipse((px - r, py - r, px + r, py + r), fill=col)
    sp.comp(lay)
    return sp.render()


def export_icon(name, size=128):
    sp = Sprite(size, size, 0)
    s = size * 0.92
    if name == "bat":
        s = size * 1.05
    icon_draw(sp, name, size / 2, size / 2, s, WHITE, size * 0.068)
    return sp.render()


def export_stick_chevron(size=128):
    """Stitched chevron (points up): two running-stitch arms with hole shadows, tinted bone at use."""
    sp = Sprite(size, size, 0)
    c = size / 2
    apex = (c, c - 24)
    for end in ((c - 42, c + 18), (c + 42, c + 18)):
        # two thread dashes per arm, the first starting just off the apex so the tip reads as a stitched point
        for t0, t1 in ((0.1, 0.4), (0.64, 0.96)):
            a = (apex[0] + (end[0] - apex[0]) * t0, apex[1] + (end[1] - apex[1]) * t0)
            b = (apex[0] + (end[0] - apex[0]) * t1, apex[1] + (end[1] - apex[1]) * t1)
            sp.line([(a[0] + 1.2, a[1] + 1.8), (b[0] + 1.2, b[1] + 1.8)], (0, 0, 0, 90), 10)
            sp.line([a, b], WHITE, 10)
    return sp.render()


def export_logo(width=1024):
    im = logo_sprite().render()
    x0, y0, x1, y1 = im.split()[3].point(lambda v: 255 if v > 8 else 0).getbbox()
    m = 10  # keep the soft fuzz; the layout's empty top band is trimmed so the sprite rect is the visible logo
    im = im.crop((max(0, x0 - m), max(0, y0 - m), min(im.size[0], x1 + m), min(im.size[1], y1 + m)))
    h = int(round(im.size[1] * width / im.size[0] / 4) * 4)
    return im.resize((width, h), Image.LANCZOS)


def export_unity(out_dir):
    """Write every baked UI sprite; returns {name: image} in catalog order."""
    os.makedirs(out_dir, exist_ok=True)
    sprites = {
        "felt_disc_night": export_felt_disc(NIGHT, 61),
        "felt_disc_orange": export_felt_disc(ORANGE, 62),
        "felt_disc_bone": export_felt_disc(BONE, 52),
        "stitch_ring": export_stitch_ring(),
        "paper_card": export_paper(176, 176, 8, 34, 4.0, 4.0, 301),
        "paper_tag": export_paper(112, 80, 8, 22, 2.2, 3.6, 63),
        "felt_button_orange": export_felt_button(),
        "stitch_rect": export_stitch_rect(),
        "stick_chevron": export_stick_chevron(),
        "logo": export_logo(),
    }
    for name in EXPORT_ICONS:
        sprites["icon_" + name] = export_icon(name)
    for name, im in sprites.items():
        assert im.size[0] % 4 == 0 and im.size[1] % 4 == 0, (name, im.size)
        im.save(os.path.join(out_dir, name + ".png"), optimize=True)
        print("exported", name, im.size)
    return sprites


def export_sheet(sprites, path):
    """Contact sheet of the exported sprites: raw (on a checker) and as composed in game (tinted, 9-sliced)."""
    cell, cols = 300, 6
    names = [n for n in sprites if n != "logo"]
    rows = (len(names) + cols - 1) // cols
    SWd, SHd = cols * cell + 40, 160 + rows * (cell + 40) + 760
    sheet = texture(SWd, SHd, NIGHT, "felt", 77)
    d = ImageDraw.Draw(sheet)
    d.text((30, 30), "Spooky Street · baked UI sprites (Resources/UI)", font=font(F_DISPLAY, 56), fill=BONE)
    d.text((32, 100), "raw PNG on a checker (white layers are tinted in game)", font=font(F_BOLD, 28),
           fill=BONE[:3] + (200,))
    for i, n in enumerate(names):
        r, k = divmod(i, cols)
        x, y = 20 + k * cell, 160 + r * (cell + 40)
        chk = Image.new("RGBA", (cell - 20, cell - 20), (120, 120, 130, 255))
        cd = ImageDraw.Draw(chk)
        for yy in range(0, cell, 20):
            for xx in range(0, cell, 20):
                if (xx // 20 + yy // 20) % 2:
                    cd.rectangle((xx, yy, xx + 19, yy + 19), fill=(90, 90, 100, 255))
        im = sprites[n]
        sc = min((cell - 40) / im.size[0], (cell - 40) / im.size[1], 1.0)
        t = im.resize((max(1, int(im.size[0] * sc)), max(1, int(im.size[1] * sc))), Image.LANCZOS)
        chk.alpha_composite(t, ((chk.size[0] - t.size[0]) // 2, (chk.size[1] - t.size[1]) // 2))
        sheet.alpha_composite(chk, (x, y))
        d.text((x + (cell - 20) / 2, y + cell - 6), f"{n} {im.size[0]}×{im.size[1]}", font=font(F_BOLD, 22),
               fill=BONE, anchor="mm")
    y0 = 160 + rows * (cell + 40) + 20
    d.text((30, y0), "composed (tinted / 9-sliced, as GameUi builds them)", font=font(F_DISPLAY, 40), fill=BONE)
    logo = sprites["logo"]
    lg = logo.resize((560, int(560 * logo.size[1] / logo.size[0])), Image.LANCZOS)
    sheet.alpha_composite(lg, (30, y0 + 70))

    def tint(im, col, alpha=1.0):
        arr = np.asarray(im, np.float32)
        arr[..., :3] *= np.array(col[:3], np.float32) / 255
        arr[..., 3] *= alpha
        return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")

    def nine(im, w, h, b):
        """Plain 9-slice stretch (for the preview; Unity tiles stitch_rect instead of stretching)."""
        out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        W0, H0 = im.size
        xs = [(0, b, 0, b), (b, W0 - b, b, w - b), (W0 - b, W0, w - b, w)]
        ys = [(0, b, 0, b), (b, H0 - b, b, h - b), (H0 - b, H0, h - b, h)]
        for sx0, sx1, dx0, dx1 in xs:
            for sy0, sy1, dy0, dy1 in ys:
                if dx1 > dx0 and dy1 > dy0:
                    out.paste(im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.BICUBIC),
                              (dx0, dy0))
        return out

    def tiled(im, w, h, b):
        out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        W0, H0 = im.size
        m = W0 - 2 * b
        for x in range(b, w - b, m):
            cw = min(m, w - b - x)
            out.paste(im.crop((b, 0, b + cw, b)), (x, 0))
            out.paste(im.crop((b, H0 - b, b + cw, H0)), (x, h - b))
        for y in range(b, h - b, m):
            ch = min(m, h - b - y)
            out.paste(im.crop((0, b, b, b + ch)), (0, y))
            out.paste(im.crop((W0 - b, b, W0, b + ch)), (w - b, y))
        for sx, dx in ((0, 0), (W0 - b, w - b)):
            for sy, dy in ((0, 0), (H0 - b, h - b)):
                out.paste(im.crop((sx, sy, sx + b, sy + b)), (dx, dy))
        return out

    x = 640
    card = nine(sprites["paper_card"], 520, 380, 44)
    card.alpha_composite(tint(tiled(sprites["stitch_rect"], 520, 380, 48), PURPLE))
    btn = nine(sprites["felt_button_orange"], 440, 140, 48)
    btn.alpha_composite(tint(tiled(sprites["stitch_rect"], 440, 140, 48), CANDY))
    pl = tint(sprites["icon_play"], INK).resize((64, 64), Image.LANCZOS)
    btn.alpha_composite(pl, (50, 38))
    ImageDraw.Draw(btn).text((250, 68), "Resume", font=font(F_BOLD, 56), fill=INK, anchor="mm")
    card.alpha_composite(btn, (40, 120))
    ImageDraw.Draw(card).text((260, 70), "Paused", font=font(F_DISPLAY, 70), fill=INK, anchor="mm")
    sheet.alpha_composite(card, (x, y0 + 70))
    x += 560
    for i, (disc, ring, icon, ic) in enumerate([("felt_disc_night", CANDY, "icon_jump", BONE),
                                                ("felt_disc_orange", CANDY, "icon_examine", INK),
                                                ("felt_disc_night", BONE, "icon_pause", BONE)]):
        dd = tint(sprites[disc], (255, 255, 255), 0.92 if "night" in disc else 1.0)
        dd.alpha_composite(tint(sprites["stitch_ring"], ring))
        ic_im = tint(sprites[icon], ic).resize((120, 120), Image.LANCZOS)
        dd.alpha_composite(ic_im, (68, 60 if i == 0 else 68))
        dd = dd.resize((180, 180), Image.LANCZOS)
        sheet.alpha_composite(dd, (x + (i % 2) * 200, y0 + 80 + (i // 2) * 200))
    tag = nine(sprites["paper_tag"], 300, 84, 34)
    ImageDraw.Draw(tag).text((150, 42), "Drag here to walk.", font=font(F_BOLD, 34), fill=INK, anchor="mm")
    sheet.alpha_composite(tag, (x, y0 + 500))
    stick = tint(sprites["felt_disc_night"], (255, 255, 255), 0.55)
    stick.alpha_composite(tint(sprites["stitch_ring"], BONE, 0.8))
    for k in range(4):
        ch = tint(sprites["stick_chevron"], BONE, 0.85).resize((48, 48), Image.LANCZOS).rotate(-90 * k)
        cx, cy = 128 + [0, 92, 0, -92][k], 128 + [-92, 0, 92, 0][k]
        stick.alpha_composite(ch, (cx - 24, cy - 24))
    kn = sprites["felt_disc_bone"].resize((100, 100), Image.LANCZOS)
    stick.alpha_composite(tint(kn, (255, 255, 255), 0.9), (78, 78))
    sheet.alpha_composite(stick.resize((220, 220), Image.LANCZOS), (x + 420, y0 + 80))
    sheet.convert("RGB").save(path, quality=92)
    print("wrote", path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(os.environ.get("UI_MOCK_OUT", "/tmp/ui_mock")))
    ap.add_argument("--guides", action="store_true", help="draw iPhone safe-area guides")
    ap.add_argument("--only", default=None)
    ap.add_argument("--export", nargs="?", const=UNITY_UI, default=None,
                    help="write the baked Unity UI sprites (default Assets/SpookyStreet/Content/Resources/UI)")
    ap.add_argument("--export-sheet", default=None, help="with --export: also write a contact sheet to this path")
    a = ap.parse_args()
    if a.export:
        sprites = export_unity(a.export)
        if a.export_sheet:
            export_sheet(sprites, a.export_sheet)
        return
    os.makedirs(a.out, exist_ok=True)
    builders = [("ui_title", mock_title), ("ui_hud", lambda: mock_hud(a.guides)), ("ui_dialogue", mock_dialogue),
                ("ui_notebook", mock_notebook), ("ui_pause", mock_pause)]
    mocks = []
    for name, fn in builders:
        if a.only and a.only != name and a.only != "sheet":
            continue
        im = fn()
        if a.guides and name != "ui_hud":
            guides(im)
        im.convert("RGB").save(os.path.join(a.out, name + ".png"))
        mocks.append(im)
        print("wrote", name)
    if not a.only or a.only == "sheet":
        mock_sheet(mocks, a.out).convert("RGB").save(os.path.join(a.out, "ui_sheet.png"))
        print("wrote ui_sheet")


if __name__ == "__main__":
    main()
