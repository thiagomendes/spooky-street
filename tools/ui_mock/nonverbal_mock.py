#!/usr/bin/env python3
"""Non-verbal UX mocks for Spooky Street (design sketch, docs/nonverbal-ux.md).

Draws felt-and-paper pictograms procedurally (reusing tools/ui_mock/ui_mock.py helpers) and composites five mocks
over the S002 CI frames, plus an icon sheet with every pictogram in the inventory.

    python3 tools/ui_mock/nonverbal_mock.py [--out docs/evidence/s002/nonverbal] [--only nv_icons]

Pictogram IDs here are the language-neutral IDs proposed in the doc (`pg.<name>` in the game; bare names here).
"""
from __future__ import annotations

import argparse
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_mock as U  # noqa: E402
from ui_mock import (BONE, BONE_D, CANDY, F_BOLD, F_DISPLAY, GRAPHITE, GREEN, H, INK, NIGHT, ORANGE,  # noqa: E402
                     ORANGE_D, PLUM, PURPLE, SILVER, SS, Sprite, W, circle_path, deckle, felt_disc, hexc,
                     icon_draw, paper_card, place, rrect_path)

ROOT = U.ROOT
FRAMES = os.path.join(ROOT, "docs", "evidence", "s002")
OUT = os.path.join(FRAMES, "nonverbal")

# Extra craft colours (costume and set families from the art bible; silver stays June's cape only).
INDIGO = hexc("394461")
WOOD = hexc("B7834F")
WOOD_D = hexc("8A5A33")
HEDGE = hexc("2F5A3A")
HEDGE_L = hexc("4E8250")
VIOLET = hexc("7A4CC0")      # June's hat and Bea's bike (vivid costume violet)
MAGENTA = hexc("C2307A")     # Kit's shawl
RUST = hexc("C8552A")        # Bea's hair
HAIR_D = hexc("2B2238")
HAIR_B = hexc("3A2620")
SKIN_PIP = hexc("EDC09A")
SKIN_BEA = hexc("F6D2B8")
SKIN_VALE = hexc("F0C8A8")
SKIN_JUNE = hexc("B07650")
SKIN_KIT = hexc("8E5A3A")
BERRY = hexc("B0305A")
FOIL = hexc("C9C3E0")        # foil streamer: lavender foil, never cloth silver
STEEL = hexc("8E8AA8")
WARM = hexc("FFD98A")

SC = W / 1278.0  # CI frames are 1278x590; mocks are 2532x1170


# ----------------------------------------------------------------------------------------------- texture cache

_shade = {}


def _shade_for(w, h, kind):
    key = (w, h, kind)
    if key not in _shade:
        t = np.asarray(U.texture(w, h, (128, 128, 128), kind, seed=w % 97 + (3 if kind == "felt" else 5)),
                       np.float32)[..., :3] - 128
        _shade[key] = t
    return _shade[key]


def ffill(sp, pts, col, kind="felt"):
    m = sp.mask(pts)
    sh = _shade_for(m.size[0], m.size[1], kind)
    rgb = np.clip(np.array(col[:3], np.float32)[None, None, :] + sh, 0, 255).astype(np.uint8)
    lay = Image.fromarray(rgb, "RGB").convert("RGBA")
    if len(col) > 3 and col[3] < 255:
        m = m.point(lambda v: v * col[3] // 255)
    out = sp.layer()
    out.paste(lay, (0, 0), m)
    sp.comp(out)


# ----------------------------------------------------------------------------------------------- glyph context

class G:
    """Draw a pictogram in a unit box (x, y in -0.5..0.5, y down) centred at (cx, cy) with side s."""

    def __init__(self, sp, cx, cy, s):
        self.sp, self.cx, self.cy, self.s = sp, cx, cy, s
        self.ow = max(2.0, s * 0.022)

    def P(self, x, y):
        return (self.cx + x * self.s, self.cy + y * self.s)

    def pts(self, pts):
        return [self.P(x, y) for x, y in pts]

    def shape(self, pts, col, kind="felt", outline=True, ow=1.0):
        p = self.pts(pts)
        ffill(self.sp, p, col, kind)
        if outline:
            self.sp.line(p, INK, self.ow * ow, closed=True)

    def ell(self, x, y, rx, ry, col, kind="felt", outline=True, ow=1.0, a0=0.0, a1=2 * math.pi):
        n = 48
        pts = [(x + rx * math.cos(a0 + (a1 - a0) * i / n), y + ry * math.sin(a0 + (a1 - a0) * i / n))
               for i in range(n + 1)]
        self.shape(pts, col, kind, outline, ow)

    def circ(self, x, y, r, col, **k):
        self.ell(x, y, r, r, col, **k)

    def rect(self, x0, y0, x1, y1, col, r=0.03, kind="felt", outline=True, ow=1.0):
        pts = rrect_path(x0 * self.s, y0 * self.s, x1 * self.s, y1 * self.s, max(0.5, r * self.s), 2)
        self.shape([(x / self.s, y / self.s) for x, y in pts], col, kind, outline, ow)

    def line(self, pts, col=INK, w=1.0):
        self.sp.line(self.pts(pts), col, self.ow * w)

    def arc(self, x, y, r, a0, a1, col=INK, w=1.0):
        a0, a1 = math.radians(a0), math.radians(a1)
        self.line([(x + r * math.cos(a0 + (a1 - a0) * i / 24), y + r * math.sin(a0 + (a1 - a0) * i / 24))
                   for i in range(25)], col, w)

    def dot(self, x, y, r, col=INK):
        a, b = self.P(x - r, y - r), self.P(x + r, y + r)
        self.sp.ellipse((*a, *b), fill=col)

    def text(self, x, y, t, px, col, stroke=0.0):
        self.sp.text(*self.P(x, y), t, F_DISPLAY, px * self.s, col, "mm", stroke=stroke * self.ow,
                     stroke_fill=INK)


# ----------------------------------------------------------------------------------------------- faces

def eyes(g, y, sep, r, x=0.0, look=0.0):
    for sx in (-1, 1):
        g.ell(x + sx * sep, y, r * 0.8, r, INK, outline=False)
        g.dot(x + sx * sep + look + r * 0.25, y - r * 0.35, r * 0.32, BONE)


def smile(g, x, y, w, open_=False):
    if open_:
        g.shape([(x - w, y), (x + w, y), (x + w * 0.6, y + w * 0.55), (x, y + w * 0.7), (x - w * 0.6, y + w * 0.55)],
                hexc("8C2F3A"), outline=True, ow=0.7)
    else:
        g.line([(x - w + 2 * w * i / 12, y + w * 0.45 * math.sin(math.pi * i / 12)) for i in range(13)], INK, 0.9)


def cheeks(g, y, sep, col=hexc("E8907A", 150)):
    for sx in (-1, 1):
        g.dot(sx * sep, y, 0.035, col)


def g_pip(g):
    g.ell(0, -0.02, 0.31, 0.29, HAIR_D)
    for sx in (-1, 1):  # felt bat-ear headband
        g.shape([(sx * 0.08, -0.26), (sx * 0.34, -0.48), (sx * 0.28, -0.18)], HAIR_D)
        g.shape([(sx * 0.13, -0.26), (sx * 0.3, -0.41), (sx * 0.26, -0.22)], BERRY, outline=False)
    g.ell(0, 0.1, 0.25, 0.23, SKIN_PIP)
    g.shape([(-0.27, -0.02), (-0.2, 0.06), (-0.12, -0.02), (-0.04, 0.06), (0.04, -0.02), (0.12, 0.06), (0.2, -0.02),
             (0.27, 0.04), (0.24, -0.16), (-0.24, -0.16)], HAIR_D, outline=False)
    eyes(g, 0.12, 0.1, 0.045)
    cheeks(g, 0.2, 0.17)
    smile(g, 0, 0.22, 0.07)
    for sx in (-1, 1):
        g.shape([(sx * 0.035 - 0.015, 0.245), (sx * 0.035 + 0.015, 0.245), (sx * 0.035, 0.285)], BONE, ow=0.5)
    g.shape([(-0.2, 0.3), (0.2, 0.3), (0.28, 0.44), (-0.28, 0.44)], HAIR_D)  # collar


def g_bea(g):
    g.ell(0, 0.08, 0.31, 0.3, RUST)
    g.ell(0, 0.14, 0.24, 0.22, SKIN_BEA)
    for sx, sy in ((-0.12, 0.2), (-0.15, 0.24), (0.12, 0.2), (0.15, 0.24)):
        g.dot(sx, sy, 0.012, hexc("C06A4A"))
    eyes(g, 0.13, 0.095, 0.045)
    smile(g, 0, 0.25, 0.08, open_=True)
    # pumpkin bike helmet
    g.ell(0, -0.02, 0.34, 0.27, ORANGE, a0=math.pi, a1=2 * math.pi)
    for x in (-0.15, 0.0, 0.15):
        g.line([(x * 0.8, -0.27), (x * 1.25, -0.03)], ORANGE_D, 0.7)
    g.shape([(-0.36, -0.03), (0.36, -0.03), (0.33, 0.03), (-0.33, 0.03)], ORANGE_D)
    g.shape([(-0.15, -0.12), (-0.08, -0.12), (-0.115, -0.19)], INK, outline=False)
    g.shape([(0.08, -0.12), (0.15, -0.12), (0.115, -0.19)], INK, outline=False)
    g.shape([(-0.03, -0.27), (0.03, -0.27), (0.06, -0.38), (0.0, -0.37)], hexc("5E8A2E"))
    g.shape([(-0.24, 0.35), (-0.1, 0.42), (0.0, 0.36), (0.1, 0.42), (0.24, 0.35), (0.3, 0.46), (-0.3, 0.46)], GREEN)


def g_vale(g):
    g.ell(0, 0.02, 0.28, 0.32, BONE)
    for y, k in ((-0.2, 0.06), (-0.1, -0.05), (-0.22, -0.12)):
        hw = 0.27 * math.sqrt(max(0, 1 - (y / 0.32) ** 2))
        g.line([(-hw, y + k), (hw, y - k)], BONE_D, 0.8)
    g.rect(-0.2, -0.04, 0.2, 0.25, SKIN_VALE, r=0.09)
    eyes(g, 0.06, 0.09, 0.042)
    g.shape([(-0.24, -0.08), (0.02, -0.04), (0.0, 0.13), (-0.24, 0.07)], BONE)  # slipping bandage
    g.line([(-0.22, 0.0), (0.0, 0.03)], BONE_D, 0.6)
    g.dot(0.0, 0.13, 0.03, hexc("D89A80"))
    g.shape([(-0.12, 0.18), (0.0, 0.15), (0.12, 0.18), (0.1, 0.22), (0.0, 0.19), (-0.1, 0.22)], hexc("B8B0B4"),
            ow=0.6)
    smile(g, 0, 0.24, 0.05)
    g.shape([(-0.2, 0.34), (0.2, 0.34), (0.3, 0.46), (-0.3, 0.46)], PURPLE)


def g_june(g):
    g.rect(-0.25, -0.06, 0.25, 0.42, HAIR_B, r=0.1)
    g.ell(0, 0.13, 0.2, 0.2, SKIN_JUNE)
    eyes(g, 0.13, 0.085, 0.042)
    smile(g, 0, 0.23, 0.07)
    cheeks(g, 0.19, 0.14, hexc("D07A6A", 140))
    g.shape([(-0.2, -0.06), (0.0, -0.5), (0.08, -0.43), (0.2, -0.06)], VIOLET)
    g.shape([(-0.16, -0.12), (0.16, -0.12), (0.18, -0.06), (-0.18, -0.06)], CANDY, ow=0.7)
    g.ell(0, -0.04, 0.42, 0.07, VIOLET)
    star(g, 0.0, -0.1, 0.05, ORANGE)
    g.shape([(-0.18, 0.34), (0.18, 0.34), (0.32, 0.46), (-0.32, 0.46)], SILVER)  # cape collar


def g_kit(g):
    for a in range(0, 360, 40):
        r = math.radians(a)
        g.circ(0.27 * math.cos(r), 0.12 + 0.25 * math.sin(r), 0.08, HAIR_D)
    g.ell(0, 0.13, 0.22, 0.22, SKIN_KIT)
    eyes(g, 0.13, 0.09, 0.042)
    smile(g, 0, 0.24, 0.08)
    for sx in (-1, 1):
        g.ell(sx * 0.24, 0.2, 0.035, 0.045, CANDY, ow=0.6)
    g.ell(0, -0.0, 0.29, 0.22, ORANGE, a0=math.pi, a1=2 * math.pi)
    g.shape([(-0.29, 0.0), (0.29, 0.0), (0.27, 0.05), (-0.27, 0.05)], ORANGE_D)
    g.shape([(0.22, -0.06), (0.4, -0.16), (0.36, 0.0)], ORANGE)
    g.shape([(-0.26, 0.36), (0.0, 0.42), (0.26, 0.36), (0.34, 0.47), (-0.34, 0.47)], MAGENTA)


# ----------------------------------------------------------------------------------------------- objects

def star(g, x, y, r, col, outline=True):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    g.shape(pts, col, outline=outline, ow=0.6)


def sparkle(g, x, y, r, col=CANDY):
    g.shape([(x, y - r), (x + r * 0.22, y - r * 0.22), (x + r, y), (x + r * 0.22, y + r * 0.22), (x, y + r),
             (x - r * 0.22, y + r * 0.22), (x - r, y), (x - r * 0.22, y - r * 0.22)], col, ow=0.5)


def cape_pts(x=0.0, y=0.0, k=1.0):
    p = [(-0.11, -0.36), (0.11, -0.36), (0.36, 0.34), (0.27, 0.34), (0.17, 0.16), (0.1, 0.34), (-0.36, 0.34)]
    return [(x + a * k, y + b * k) for a, b in p]


def g_cape(g):
    g.shape(cape_pts(), SILVER)
    g.line([(-0.04, -0.3), (-0.16, 0.3)], hexc("AEB8CC"), 0.8)
    g.line([(0.04, -0.3), (0.0, 0.3)], hexc("AEB8CC"), 0.8)
    g.rect(-0.16, -0.42, 0.16, -0.33, hexc("AEB8CC"), r=0.03)
    star(g, -0.17, 0.12, 0.06, CANDY)
    star(g, 0.07, -0.05, 0.04, CANDY)


def g_cloth(g):
    g.shape([(-0.42, -0.14), (0.42, -0.2), (0.42, -0.1), (0.05, -0.06), (-0.42, -0.04)], WOOD)
    g.shape([(0.0, -0.08), (0.12, -0.16), (0.1, -0.06)], WOOD_D, ow=0.6)
    g.shape([(-0.06, -0.1), (0.2, -0.1), (0.26, 0.28), (0.18, 0.22), (0.12, 0.32), (0.05, 0.2), (-0.1, 0.3)],
            SILVER)
    g.line([(0.04, -0.05), (0.02, 0.2)], hexc("AEB8CC"), 0.7)
    sparkle(g, -0.25, 0.18, 0.08)


def g_streamer(g):
    top, bot = [], []
    for i in range(25):
        t = i / 24
        x = -0.4 + 0.8 * t
        y = 0.14 * math.sin(t * 2 * math.pi * 1.3)
        top.append((x, y - 0.07))
        bot.append((x, y + 0.07))
    g.shape(top + bot[::-1], FOIL)
    for t in (0.15, 0.45, 0.75):
        x = -0.4 + 0.8 * t
        y = 0.14 * math.sin(t * 2 * math.pi * 1.3)
        g.line([(x - 0.03, y - 0.04), (x + 0.03, y + 0.04)], BONE, 1.0)
    sparkle(g, 0.26, -0.24, 0.07, BONE)


def g_house(g, door="shut", side_ring=False):
    g.shape([(-0.33, 0.0), (0.33, 0.0), (0.33, 0.42), (-0.33, 0.42)], INDIGO)
    g.shape([(-0.42, 0.04), (0.0, -0.4), (0.42, 0.04)], PURPLE)
    g.circ(0, -0.1, 0.1, WARM)
    g.line([(0, -0.2), (0, 0.0)], INK, 0.7)
    g.line([(-0.1, -0.1), (0.1, -0.1)], INK, 0.7)
    if door == "open":
        g.rect(-0.08, 0.17, 0.08, 0.42, WARM, r=0.01)
        g.shape([(-0.08, 0.17), (-0.17, 0.14), (-0.17, 0.44), (-0.08, 0.42)], PLUM)
    else:
        g.rect(-0.08, 0.17, 0.08, 0.42, PLUM, r=0.01)
    g.rect(0.16, 0.12, 0.27, 0.26, WARM, r=0.05)
    if side_ring:
        g.ell(0.215, 0.19, 0.12, 0.13, ORANGE, outline=False, kind="felt")
        g.rect(0.16, 0.12, 0.27, 0.26, WARM, r=0.05)
    g.shape([(-0.42, 0.33), (-0.3, 0.26), (-0.22, 0.33), (-0.22, 0.44), (-0.42, 0.44)], HEDGE)


def g_window(g):
    g.rect(-0.36, -0.4, 0.36, 0.4, INDIGO, r=0.05)
    pts = [(-0.18, 0.22), (-0.18, -0.06)] + [(0.18 * math.cos(a), -0.06 - 0.18 * math.sin(a))
                                             for a in np.linspace(math.pi, 0, 20)] + [(0.18, 0.22)]
    g.shape(pts, WARM)
    g.line([(0, -0.24), (0, 0.22)], INK, 0.8)
    g.line([(-0.18, 0.02), (0.18, 0.02)], INK, 0.8)
    g.rect(-0.26, 0.22, 0.26, 0.3, WOOD, r=0.02)


def g_door(g, state="shut"):
    g.rect(-0.27, -0.42, 0.27, 0.42, WOOD, r=0.04)
    if state == "open":
        g.rect(-0.2, -0.35, 0.2, 0.42, WARM, r=0.02)
        g.shape([(-0.2, -0.35), (-0.36, -0.42), (-0.36, 0.48), (-0.2, 0.42)], PLUM)
        g.dot(-0.31, 0.06, 0.025, CANDY)
        for a in (-30, 0, 30):
            r = math.radians(a)
            g.line([(0.05 + 0.1 * math.cos(r), 0.0 + 0.1 * math.sin(r)),
                    (0.05 + 0.18 * math.cos(r), 0.0 + 0.18 * math.sin(r))], ORANGE, 0.7)
    else:
        g.rect(-0.2, -0.35, 0.2, 0.42, PLUM, r=0.02)
        g.rect(-0.13, -0.27, 0.13, -0.05, hexc("8E4E80"), r=0.02, ow=0.6)
        g.rect(-0.13, 0.04, 0.13, 0.32, hexc("8E4E80"), r=0.02, ow=0.6)
        g.circ(0.13, 0.03, 0.03, CANDY, ow=0.6)


def g_door_shut(g):
    g_door(g, "shut")


def g_door_open(g):
    g_door(g, "open")


def padlock(g, x, y, k):
    g.arc(x, y - 0.06 * k, 0.07 * k, 180, 360, BONE_D, 2.2)
    g.arc(x, y - 0.06 * k, 0.07 * k, 180, 360, INK, 0.6)
    g.rect(x - 0.11 * k, y - 0.06 * k, x + 0.11 * k, y + 0.11 * k, ORANGE, r=0.03 * k)
    g.dot(x, y + 0.01 * k, 0.022 * k)


def g_lock(g):
    padlock(g, 0, 0, 3.2)


def g_stuck(g):
    sub = G(g.sp, g.cx - 0.04 * g.s, g.cy, g.s * 0.9)
    g_door(sub, "shut")
    for sx in (-1, 1):
        x = sx * 0.36 - 0.04
        g.line([(x, -0.18), (x + sx * 0.05, -0.1), (x, -0.02), (x + sx * 0.05, 0.06), (x, 0.14)], INK, 0.9)
    padlock(g, 0.26, 0.28, 1.2)


def g_latch(g):
    g.rect(-0.4, -0.08, 0.4, 0.08, WOOD)
    for x in (-0.26, 0.26):
        g.rect(x - 0.07, -0.13, x + 0.07, 0.15, hexc("6A5A70"), r=0.02)
    g.line([(0.0, -0.18), (0.0, -0.42)], ORANGE, 1.6)
    g.line([(-0.09, -0.32), (0.0, -0.43), (0.09, -0.32)], ORANGE, 1.6)
    g.line([(-0.3, 0.3), (0.3, 0.3)], PLUM, 2.5)


def hedge_pts(x0, x1, ytop, ybot, n=5):
    pts = [(x0, ybot)]
    for i in range(n * 6 + 1):
        t = i / (n * 6)
        x = x0 + (x1 - x0) * t
        pts.append((x, ytop - 0.05 * abs(math.sin(t * n * math.pi))))
    pts.append((x1, ybot))
    return pts


def g_hedge(g):
    g.shape(hedge_pts(-0.42, 0.42, -0.08, 0.32), HEDGE)
    for x, y in ((-0.22, 0.05), (0.05, 0.12), (0.25, 0.02), (-0.05, -0.02)):
        g.ell(x, y, 0.06, 0.035, HEDGE_L, outline=False)


def g_hedge_cape(g):
    sub = G(g.sp, g.cx + 0.05 * g.s, g.cy - 0.2 * g.s, g.s * 0.55)
    sub.shape(cape_pts(), SILVER)
    g.shape(hedge_pts(-0.42, 0.42, -0.06, 0.36), HEDGE)
    for x, y in ((-0.22, 0.08), (0.08, 0.16), (0.25, 0.05)):
        g.ell(x, y, 0.06, 0.035, HEDGE_L, outline=False)


def g_bench(g):
    g.rect(-0.4, -0.3, 0.4, -0.18, PURPLE, r=0.03)
    g.rect(-0.4, -0.12, 0.4, 0.0, PURPLE, r=0.03)
    g.rect(-0.42, 0.05, 0.42, 0.16, VIOLET, r=0.03)
    for x in (-0.33, 0.33):
        g.rect(x - 0.04, 0.12, x + 0.04, 0.4, PURPLE, r=0.01)
    g.rect(-0.34, -0.3, -0.27, 0.06, PURPLE, r=0.01)
    g.rect(0.27, -0.3, 0.34, 0.06, PURPLE, r=0.01)


def g_eye(g):
    up = [(-0.4 + 0.8 * i / 20, -0.26 * math.sin(math.pi * i / 20)) for i in range(21)]
    dn = [(0.4 - 0.8 * i / 20, 0.22 * math.sin(math.pi * i / 20)) for i in range(21)]
    g.shape(up + dn, BONE)
    g.circ(0, -0.01, 0.15, VIOLET)
    g.circ(0, -0.01, 0.07, INK, outline=False)
    g.dot(0.05, -0.07, 0.035, BONE)
    for x in (-0.24, -0.08, 0.08, 0.24):
        y = -0.26 * math.sin(math.pi * (x + 0.4) / 0.8)
        g.line([(x, y), (x * 1.2, y - 0.1)], INK, 0.9)


def g_ear(g):
    pts = [(-0.05 + 0.2 * math.cos(a), -0.05 + 0.3 * math.sin(a)) for a in np.linspace(math.pi * 0.95, math.pi * 2.4,
                                                                                         30)]
    pts += [(0.02, 0.3), (-0.12, 0.34), (-0.2, 0.22)]
    g.shape(pts, SKIN_PIP)
    g.arc(-0.03, -0.05, 0.1, 200, 400, INK, 0.8)
    g.line([(-0.06, 0.05), (-0.02, 0.14)], INK, 0.8)
    for r in (0.12, 0.22):
        g.arc(0.18, -0.03, r, -50, 50, ORANGE, 1.4)


def g_knock3(g):
    for i, x in enumerate((-0.31, -0.07, 0.3)):
        g.circ(x, 0.08, 0.11, CANDY)
        g.circ(x, 0.08, 0.045, ORANGE, outline=False)
        for a in (-55, -90, -125):
            r = math.radians(a)
            g.line([(x + 0.15 * math.cos(r), 0.08 + 0.15 * math.sin(r)),
                    (x + 0.24 * math.cos(r), 0.08 + 0.24 * math.sin(r))], ORANGE, 1.3)
    for x in (0.07, 0.12, 0.17):
        g.dot(x, 0.08, 0.012, GRAPHITE)


def g_knock_rings(g):
    """The knock as a travelling sound ring (world cue)."""
    for r, a in ((0.14, 255), (0.26, 200), (0.38, 130)):
        g.arc(0, 0, r, 0, 360, CANDY[:3] + (a,), 1.6)
    g.circ(0, 0, 0.07, ORANGE)


def g_treehouse(g):
    g.shape([(-0.06, 0.42), (-0.04, 0.0), (0.04, 0.0), (0.07, 0.42)], WOOD_D)
    g.ell(0.0, -0.2, 0.36, 0.2, hexc("B4502A"))
    for x, y in ((-0.2, -0.28), (0.15, -0.3), (0.25, -0.12)):
        g.ell(x, y, 0.1, 0.07, hexc("D9762E"), outline=False)
    g.rect(-0.2, -0.1, 0.18, 0.08, PURPLE, r=0.02)
    g.shape([(-0.25, -0.1), (-0.01, -0.24), (0.23, -0.1)], PLUM)
    g.rect(-0.04, -0.06, 0.06, 0.04, WARM, r=0.01, ow=0.6)
    g.rect(-0.28, 0.08, 0.26, 0.12, WOOD, r=0.01)
    for y in (0.17, 0.25, 0.33):
        g.line([(-0.2, y), (-0.1, y)], WOOD_D, 1.0)
    g.line([(-0.2, 0.12), (-0.2, 0.42)], WOOD_D, 0.8)
    g.line([(-0.1, 0.12), (-0.1, 0.42)], WOOD_D, 0.8)


def g_bridge(g):
    for i in range(7):
        x = -0.38 + i * 0.12
        y = 0.04 * math.sin(math.pi * i / 6)
        g.rect(x, y - 0.06 + 0.05, x + 0.09, y + 0.12 + 0.05, WOOD, r=0.01)
    g.line([(-0.4, -0.12 + 0.02 * k) for k in (0,)] + [(0.4, -0.12)], WOOD_D, 1.0)
    g.line([(-0.4, -0.13), (0.0, -0.06), (0.4, -0.13)], WOOD_D, 1.0)
    for x in (-0.4, 0.4):
        g.line([(x, -0.2), (x, 0.2)], WOOD_D, 1.4)


def g_feet(g):
    for sx in (-1, 1):
        x = sx * 0.16
        g.rect(x - 0.06, -0.3, x + 0.06, 0.1, hexc("4A3070"), r=0.03)
        for y in (-0.22, -0.1, 0.02):
            g.line([(x - 0.06, y), (x + 0.06, y)], VIOLET, 1.4)
        g.shape([(x - 0.08, 0.08), (x + 0.08, 0.08), (x + 0.08 + sx * 0.08, 0.22), (x + sx * 0.08 - 0.1 * sx,
                                                                                    0.28), (x - 0.08, 0.26)], INK)


def g_floor_hum(g):
    g.rect(-0.42, -0.3, 0.42, -0.02, WOOD)
    for x in (-0.14, 0.14):
        g.line([(x, -0.3), (x, -0.02)], WOOD_D, 0.8)
    for i, x in enumerate((-0.24, -0.08, 0.2)):
        g.line([(x + 0.05 * math.sin(k * 1.3), 0.06 + k * 0.05) for k in range(6)], ORANGE, 1.5)


def g_grille(g):
    g.rect(-0.38, -0.3, 0.38, 0.3, hexc("3A2E48"), r=0.05)
    sub = G(g.sp, g.cx, g.cy + 0.02 * g.s, g.s * 0.5)
    cam_shape(sub, 0, 0)
    for x in (-0.24, -0.12, 0.0, 0.12, 0.24):
        g.line([(x, -0.28), (x, 0.28)], BONE, 1.3)
    g.rect(-0.38, -0.3, 0.38, 0.3, (0, 0, 0, 0), r=0.05)


def cam_shape(g, x, y):
    pts = [(x + (0.22 + 0.1 * math.cos(3 * a)) * math.cos(a), y + (0.22 + 0.1 * math.cos(3 * a)) * math.sin(a))
           for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)]
    g.shape(pts, WOOD)
    g.circ(x, y, 0.06, WOOD_D)


def g_cam(g):
    cam_shape(g, -0.05, 0.05)
    g.arc(-0.05, 0.05, 0.4, -70, -10, ORANGE, 1.3)
    g.line([(0.27, -0.34), (0.33, -0.22), (0.2, -0.24)], ORANGE, 1.3)
    g.rect(0.25, 0.0, 0.42, 0.08, hexc("D9A86A"), r=0.02)


def g_arm(g):
    p0, p1 = (-0.38, 0.2), (0.38, -0.2)
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy)
    nx, ny = -dy / L * 0.08, dx / L * 0.08
    for i in range(7):
        a, b = i / 7, (i + 1) / 7
        q = [(p0[0] + dx * a + nx, p0[1] + dy * a + ny), (p0[0] + dx * b + nx, p0[1] + dy * b + ny),
             (p0[0] + dx * b - nx, p0[1] + dy * b - ny), (p0[0] + dx * a - nx, p0[1] + dy * a - ny)]
        g.shape(q, ORANGE if i % 2 == 0 else PURPLE, outline=False)
    g.shape([(p0[0] + nx, p0[1] + ny), (p1[0] + nx, p1[1] + ny), (p1[0] - nx, p1[1] - ny), (p0[0] - nx, p0[1] - ny)],
            (0, 0, 0, 0))
    g.circ(*p0, 0.05, WOOD_D)
    g.circ(*p1, 0.05, WOOD_D)


def g_connect(g):
    sub = G(g.sp, g.cx - 0.08 * g.s, g.cy + 0.05 * g.s, g.s * 0.75)
    g_arm(sub)
    g.circ(0.28, -0.12, 0.09, WOOD_D)
    check_mark(g, 0.22, 0.26, 0.15)


def g_gap(g):
    sub = G(g.sp, g.cx - 0.12 * g.s, g.cy + 0.06 * g.s, g.s * 0.55)
    g_arm(sub)
    g.circ(0.3, -0.12, 0.08, WOOD_D)
    for i in range(4):
        x = 0.1 + i * 0.045
        g.line([(x, -0.03 - i * 0.025), (x + 0.02, -0.04 - i * 0.025)], INK, 1.0)
    g.text(0.18, -0.3, "?", 0.26, CANDY, stroke=1.0)


def g_crank(g):
    g.circ(-0.06, 0.06, 0.24, WOOD)
    g.circ(-0.06, 0.06, 0.05, WOOD_D)
    g.line([(-0.06, 0.06), (0.22, -0.18)], WOOD_D, 3.0)
    g.line([(-0.06, 0.06), (0.22, -0.18)], WOOD, 1.8)
    g.circ(0.24, -0.2, 0.07, ORANGE)
    g.arc(-0.06, 0.06, 0.36, 100, 200, ORANGE, 1.3)
    g.line([(-0.33, -0.12), (-0.4, 0.0), (-0.27, -0.0)], ORANGE, 1.3)


def owl_body(g, x, y, k=1.0, tilt=0.0):
    sub = G(g.sp, g.cx + x * g.s, g.cy + y * g.s, g.s * k)

    def R(px, py):
        c, s = math.cos(tilt), math.sin(tilt)
        return (px * c - py * s, px * s + py * c)
    body = [R(0.3 * math.cos(a), 0.05 + 0.34 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 40)]
    sub.shape(body, INDIGO)
    for sx in (-1, 1):
        sub.shape([R(sx * 0.12, -0.22), R(sx * 0.28, -0.4), R(sx * 0.27, -0.16)], INDIGO)
    for sx in (-1, 1):
        cx, cy = R(sx * 0.12, -0.06)
        sub.circ(cx, cy, 0.11, WARM)
        sub.circ(cx, cy, 0.045, INK, outline=False)
    sub.shape([R(-0.05, 0.06), R(0.05, 0.06), R(0.0, 0.15)], ORANGE, ow=0.6)
    for i in range(3):
        cx, cy = R(-0.1 + i * 0.1, 0.25)
        sub.arc(cx, cy, 0.05, 20, 160, BONE, 0.8)


def g_owl(g):
    owl_body(g, 0, 0.02)


def g_owl_bow(g):
    owl_body(g, -0.04, 0.06, 0.85, tilt=0.5)
    g.arc(-0.04, 0.06, 0.42, -100, -40, ORANGE, 1.3)
    g.line([(0.17, -0.31), (0.19, -0.18), (0.07, -0.22)], ORANGE, 1.3)


def g_wind(g):
    for y, k in ((-0.18, 0.9), (0.0, 1.0), (0.18, 0.75)):
        pts = [(-0.42 + 0.6 * k * i / 20, y + 0.05 * math.sin(i / 20 * 2 * math.pi)) for i in range(21)]
        end = pts[-1]
        curl = [(end[0] + 0.07 * math.sin(a), end[1] - 0.07 + 0.07 * math.cos(a))
                for a in np.linspace(0, 1.6 * math.pi, 12)]
        g.line(pts + curl, INK, 2.6)
        g.line(pts + curl, hexc("9FB4E0"), 1.5)
    # rooftop wheel (pinwheel)
    for i in range(4):
        a = math.radians(i * 90 + 20)
        b = a + math.radians(55)
        g.shape([(0.28, -0.1), (0.28 + 0.16 * math.cos(a), -0.1 + 0.16 * math.sin(a)),
                 (0.28 + 0.12 * math.cos(b), -0.1 + 0.12 * math.sin(b))], ORANGE if i % 2 else CANDY, ow=0.6)
    g.line([(0.28, -0.1), (0.28, 0.4)], WOOD_D, 1.2)


def g_booth(g):
    g.rect(-0.36, -0.2, 0.36, 0.42, WOOD)
    g.rect(-0.22, -0.08, 0.22, 0.22, hexc("3A2E48"), r=0.02)
    owl_body(g, 0, 0.07, 0.42)
    for i in range(6):
        x0 = -0.42 + i * 0.14
        g.shape([(x0, -0.34), (x0 + 0.14, -0.34), (x0 + 0.14, -0.2), (x0 + 0.07, -0.14), (x0, -0.2)],
                ORANGE if i % 2 == 0 else PURPLE)
    for sx in (-1, 1):
        g.shape([(sx * 0.22, -0.08), (sx * 0.36, -0.08), (sx * 0.36, 0.26), (sx * 0.27, 0.26)], PLUM, ow=0.7)


def g_porch(g):
    g.rect(-0.42, -0.04, 0.42, 0.08, WOOD)
    g.rect(-0.3, 0.08, 0.3, 0.2, WOOD)
    g.rect(-0.18, 0.2, 0.18, 0.32, WOOD)
    for x in (-0.36, -0.12, 0.12, 0.36):
        g.line([(x, -0.04), (x, -0.3)], BONE, 1.2)
    g.line([(-0.4, -0.3), (0.4, -0.3)], BONE, 1.4)
    star(g, 0.0, -0.17, 0.09, CANDY)


def g_candy(g):
    g.shape([(-0.42, -0.16), (-0.2, 0.0), (-0.42, 0.16)], CANDY)
    g.shape([(0.42, -0.16), (0.2, 0.0), (0.42, 0.16)], CANDY)
    g.ell(0, 0, 0.22, 0.16, ORANGE)
    g.line([(-0.1, -0.12), (0.06, 0.13)], CANDY, 0.9)


def g_bowl(g):
    for x, y, c in ((-0.15, -0.08, ORANGE), (0.02, -0.14, GREEN), (0.16, -0.07, CANDY), (-0.02, -0.05, VIOLET)):
        g.circ(x, y, 0.08, c)
    g.ell(0, 0.0, 0.36, 0.08, hexc("3A3046"))
    g.shape([(-0.36, 0.0), (0.36, 0.0), (0.3, 0.26), (0.18, 0.34), (-0.18, 0.34), (-0.3, 0.26)], hexc("3A3046"))
    g.line([(-0.3, 0.12), (0.3, 0.12)], hexc("5A4A70"), 0.8)


def g_sweet(g):
    g.ell(0, 0.05, 0.3, 0.32, STEEL)
    g.rect(-0.24, -0.02, 0.24, 0.06, INK, r=0.02, outline=False)
    for x in (-0.12, 0.0, 0.12):
        g.line([(x, 0.12), (x, 0.28)], hexc("5A5670"), 0.8)
    g.shape([(-0.04, -0.27), (0.04, -0.27), (0.08, -0.45), (-0.06, -0.44)], ORANGE)
    sub = G(g.sp, g.cx + 0.27 * g.s, g.cy + 0.3 * g.s, g.s * 0.38)
    g_candy(sub)


def g_bike(g):
    for x in (-0.24, 0.24):
        g.circ(x, 0.14, 0.17, (0, 0, 0, 0), ow=1.6)
        g.dot(x, 0.14, 0.025)
    g.line([(-0.24, 0.14), (-0.04, -0.12), (0.16, -0.12), (0.24, 0.14)], VIOLET, 2.0)
    g.line([(-0.04, -0.12), (0.04, 0.14), (-0.24, 0.14)], VIOLET, 2.0)
    g.line([(0.16, -0.12), (0.14, -0.24), (0.24, -0.26)], INK, 1.2)
    g.rect(-0.12, -0.18, 0.02, -0.12, INK, r=0.02)
    star(g, 0.32, -0.3, 0.06, CANDY)


def hand_pts(k=1.0, x=0.0, y=0.0):
    p = [(-0.12, 0.42), (-0.16, 0.1), (-0.26, -0.02), (-0.22, -0.08), (-0.1, 0.0), (-0.08, -0.38), (-0.02, -0.42),
         (0.04, -0.38), (0.05, -0.12), (0.12, -0.13), (0.15, -0.08), (0.2, -0.1), (0.24, -0.04), (0.26, 0.12),
         (0.18, 0.42)]
    return [(x + a * k, y + b * k) for a, b in p]


def g_pointer(g, alpha=255):
    col = BONE[:3] + (alpha,)
    g.shape(hand_pts(), col)
    g.line([(0.05, -0.12), (0.06, 0.02)], INK, 0.6)
    g.line([(0.15, -0.08), (0.16, 0.04)], INK, 0.6)


def g_hands(g):
    for sx in (-1, 1):
        p = [(sx * x, y) for x, y in [(0.42, 0.3), (0.2, 0.18), (0.06, 0.06), (0.02, -0.04), (0.08, -0.08),
                                     (0.18, -0.02), (0.14, -0.14), (0.2, -0.18), (0.3, -0.06), (0.42, 0.0)]]
        g.shape(p, SKIN_PIP if sx < 0 else SKIN_JUNE)
    g.arc(0, -0.02, 0.32, 230, 310, ORANGE, 1.2)


def check_mark(g, x, y, k):
    pts = [(-0.28, 0.02), (-0.08, 0.22), (0.3, -0.2)]
    q = [(x + a * k * 2.4, y + b * k * 2.4) for a, b in pts]
    g.line(q, INK, min(26 * k, 5.5))
    g.line(q, GREEN, min(17 * k, 3.6))


def g_check(g):
    check_mark(g, 0, 0.02, 0.4)


def g_not(g):
    g.circ(0, 0, 0.34, (0, 0, 0, 0), ow=1.0)
    g.arc(0, 0, 0.3, 0, 360, ORANGE, 2.4)
    g.line([(-0.2, 0.2), (0.2, -0.2)], ORANGE, 2.4)
    g.circ(0, 0, 0.36, (0, 0, 0, 0), ow=0.8)


def g_question(g):
    g.text(0, 0.02, "?", 0.75, CANDY, stroke=1.6)


def g_heart(g):
    pts = []
    for t in np.linspace(0, 2 * math.pi, 60):
        x = 16 * math.sin(t) ** 3
        y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((x * 0.022, y * 0.022 + 0.02))
    g.shape(pts, ORANGE)
    g.ell(-0.13, -0.08, 0.05, 0.03, hexc("F7B27A"), outline=False)


def g_moon(g):
    a = [(0.34 * math.cos(t), 0.34 * math.sin(t)) for t in np.linspace(0.42 * math.pi, 1.58 * math.pi, 40)]
    b = [(0.16 + 0.3 * math.cos(t), -0.04 + 0.3 * math.sin(t)) for t in np.linspace(1.6 * math.pi, 0.5 * math.pi, 40)]
    g.shape([(x + 0.02, y) for x, y in a + b], CANDY)
    star(g, -0.26, -0.26, 0.06, CANDY)


def g_lamp(g):
    for r, al in ((0.42, 90), (0.32, 130)):
        g.dot(0, 0.04, r, hexc("F6AD58")[:3] + (al,))
    g.rect(-0.17, -0.22, 0.17, 0.3, WARM, r=0.06)
    g.line([(0, -0.22), (0, 0.3)], ORANGE_D, 0.8)
    g.rect(-0.21, -0.3, 0.21, -0.2, PLUM, r=0.02)
    g.rect(-0.21, 0.28, 0.21, 0.38, PLUM, r=0.02)
    g.arc(0, -0.34, 0.08, 180, 360, INK, 1.4)


def g_star(g):
    star(g, 0, 0.02, 0.36, CANDY)


def g_bat(g):
    U.paper_bat(g.sp, g.cx, g.cy, g.s * 0.85, CANDY, flap=0.1)


def g_sound_off(g):
    icon_draw(g.sp, "sound", g.cx, g.cy, g.s * 0.9, INK, g.ow * 1.2)
    g.line([(-0.36, 0.36), (0.36, -0.36)], ORANGE, 2.0)


def g_pip_mini(g, x=0.0, y=0.0, k=1.0, cape_open=False, alpha=255):
    sub = G(g.sp, g.cx + x * g.s, g.cy + y * g.s, g.s * k)
    col = INK[:3] + (alpha,)
    if cape_open:
        sub.shape([(-0.06, -0.05), (-0.42, -0.1), (-0.36, 0.06), (-0.26, 0.0), (-0.18, 0.12), (-0.08, 0.06),
                   (0.0, 0.16), (0.08, 0.06), (0.18, 0.12), (0.26, 0.0), (0.36, 0.06), (0.42, -0.1), (0.06, -0.05)],
                  BERRY[:3] + (alpha,))
    else:
        sub.shape([(-0.08, -0.05), (0.08, -0.05), (0.18, 0.3), (-0.18, 0.3)], col)
    sub.circ(0, -0.16, 0.12, SKIN_PIP[:3] + (alpha,))
    sub.ell(0, -0.2, 0.13, 0.09, HAIR_D[:3] + (alpha,), outline=False, a0=math.pi, a1=2 * math.pi)
    for sx in (-1, 1):
        sub.shape([(sx * 0.04, -0.27), (sx * 0.14, -0.38), (sx * 0.12, -0.22)], HAIR_D[:3] + (alpha,), ow=0.5)
    sub.line([(-0.06, 0.3), (-0.07, 0.42)], col, 1.4)
    sub.line([(0.06, 0.3), (0.07, 0.42)], col, 1.4)


def g_pip_jump(g):
    g.arc(0, 0.4, 0.36, 180, 360, GRAPHITE, 0.6)
    g_pip_mini(g, 0.0, -0.06, 0.55)
    g.line([(-0.3, 0.42), (0.3, 0.42)], WOOD_D, 1.2)


def g_tip_hold(g):
    g.arc(0.0, 0.36, 0.13, 180, 360, GRAPHITE[:3] + (140,), 0.9)
    pts = [(-0.3 + 0.6 * t, 0.36 - 0.74 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 30)]
    g.line(pts, ORANGE, 1.4)
    g_pip_mini(g, 0.0, -0.3, 0.4)
    g.line([(-0.42, 0.38), (0.42, 0.38)], WOOD_D, 1.2)
    g.arc(-0.3, 0.27, 0.07, 0, 360, CANDY, 1.4)


def g_tip_chain(g):
    xs = [(-0.42, -0.2, 0.12), (-0.2, 0.06, 0.24), (0.06, 0.38, 0.4)]
    for x0, x1, h in xs:
        pts = [(x0 + (x1 - x0) * t, 0.36 - h * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
        g.line(pts, ORANGE, 1.3)
    g_pip_mini(g, 0.22, -0.17, 0.32)
    g.line([(-0.44, 0.38), (0.44, 0.38)], WOOD_D, 1.2)
    for x in (-0.2, 0.06):
        sparkle(g, x, 0.32, 0.05)


def g_tip_glide(g):
    g_pip_mini(g, -0.08, -0.12, 0.55, cape_open=True)
    for i in range(5):
        x = 0.0 + i * 0.07
        y = 0.12 + i * 0.05
        g.dot(x, y, 0.018, ORANGE)
    g.line([(-0.44, 0.38), (0.44, 0.38)], WOOD_D, 1.2)


def g_tip_dive(g):
    pts = [(-0.4 + 0.3 * t, 0.3 - 0.4 * math.sin(math.pi * t * 0.5)) for t in np.linspace(0, 1, 12)]
    g.line(pts, GRAPHITE, 0.8)
    g.line([(-0.1, -0.1), (0.3, 0.2)], ORANGE, 1.6)
    for d in (-0.06, 0.06):
        g.line([(-0.2, -0.1 + d), (-0.05, -0.0 + d)], ORANGE, 0.7)
    sub = G(g.sp, g.cx + 0.18 * g.s, g.cy + 0.08 * g.s, g.s * 0.4)
    sub.sp.comp(sub.sp.layer())
    g_pip_mini(g, 0.2, 0.06, 0.4)
    g.line([(-0.44, 0.38), (0.44, 0.38)], WOOD_D, 1.2)


def g_tip_walk(g):
    g.circ(0, 0.05, 0.3, NIGHT[:3] + (200,), ow=0.8)
    g.circ(0.1, 0.0, 0.11, BONE, ow=0.8)
    g.line([(-0.02, 0.05), (0.32, -0.12)], ORANGE, 1.2)
    g.shape([(0.36, -0.18), (0.26, -0.18), (0.33, -0.06)], ORANGE, ow=0.5)
    sub = G(g.sp, g.cx + 0.14 * g.s, g.cy + 0.18 * g.s, g.s * 0.55)
    g_pointer(sub, 220)


def g_house_cut(g):
    """Cutaway house: the question of where (under the floor)."""
    g_house(g)
    g.rect(-0.3, 0.3, 0.3, 0.36, WOOD_D, r=0.01, ow=0.6)
    g.text(0.0, 0.17, "?", 0.18, CANDY, stroke=0.8)


def g_june_window(g):
    sub = G(g.sp, g.cx, g.cy, g.s)
    g_window(sub)
    j = G(g.sp, g.cx, g.cy + 0.02 * g.s, g.s * 0.42)
    g_june(j)


def g_pin(g):
    g.shape([(0, 0.42), (-0.22, 0.0), (-0.24, -0.16), (-0.1, -0.34), (0.1, -0.34), (0.24, -0.16), (0.22, 0.0)], ORANGE)
    g.circ(0, -0.12, 0.09, BONE)


def g_memory(g):
    for x, y, r in ((-0.2, -0.05, 0.2), (0.05, -0.15, 0.22), (0.22, 0.02, 0.18), (0.0, 0.12, 0.2),
                    (-0.22, 0.12, 0.14)):
        g.circ(x, y, r, BONE_D, kind="paper", outline=False)
    for x, y, r in ((-0.2, -0.05, 0.2), (0.05, -0.15, 0.22), (0.22, 0.02, 0.18), (0.0, 0.12, 0.2),
                    (-0.22, 0.12, 0.14)):
        g.arc(x, y, r, 0, 360, GRAPHITE, 0.6)
    for x, y, r in ((-0.2, -0.05, 0.2), (0.05, -0.15, 0.22), (0.22, 0.02, 0.18), (0.0, 0.12, 0.2),
                    (-0.22, 0.12, 0.14)):
        g.dot(x, y, r - 0.01, BONE_D)
    g.dot(-0.28, 0.33, 0.05, BONE_D)
    g.arc(-0.28, 0.33, 0.05, 0, 360, GRAPHITE, 0.6)
    g.dot(-0.38, 0.42, 0.03, BONE_D)
    g.text(0.0, -0.02, "…", 0.3, GRAPHITE)


# Emotes (balloons over a character's head)
def emote_balloon(g, inner):
    g.shape([(-0.36, -0.3), (0.36, -0.3), (0.38, 0.18), (0.0, 0.2), (-0.16, 0.4), (-0.12, 0.2), (-0.38, 0.18)], BONE,
            kind="paper")
    inner(G(g.sp, g.cx, g.cy - 0.05 * g.s, g.s * 0.5))


def g_emo_wow(g):
    emote_balloon(g, lambda s: s.text(0, 0.02, "!", 0.9, ORANGE, stroke=1.4))


def g_emo_huh(g):
    emote_balloon(g, lambda s: s.text(0, 0.02, "?", 0.9, VIOLET, stroke=1.4))


def g_emo_love(g):
    emote_balloon(g, g_heart)


def g_emo_tune(g):
    def note(s):
        s.ell(-0.12, 0.2, 0.14, 0.1, PURPLE)
        s.line([(0.0, 0.18), (0.0, -0.35)], INK, 2.6)
        s.line([(0.0, 0.18), (0.0, -0.35)], PURPLE, 1.5)
        s.shape([(0.0, -0.36), (0.24, -0.2), (0.22, -0.08), (0.02, -0.2)], PURPLE)
    emote_balloon(g, note)


def g_emo_think(g):
    emote_balloon(g, lambda s: s.text(0, -0.05, "…", 0.9, GRAPHITE))


def g_emo_proud(g):
    emote_balloon(g, g_star)


GLYPHS = {
    # characters
    "pip": g_pip, "bea": g_bea, "vale": g_vale, "june": g_june, "kit": g_kit,
    # places and things
    "house": g_house, "house_open": lambda g: g_house(g, "open"), "house_side": lambda g: g_house(g, side_ring=True),
    "house_cut": g_house_cut, "window": g_window, "june_window": g_june_window, "door_shut": g_door_shut,
    "door_open": g_door_open, "stuck": g_stuck, "lock": g_lock, "latch": g_latch, "hedge": g_hedge,
    "hedge_cape": g_hedge_cape, "bench": g_bench, "treehouse": g_treehouse, "bridge": g_bridge, "porch": g_porch,
    "bike": g_bike, "lamp": g_lamp, "pin": g_pin,
    # clues
    "cape": g_cape, "cloth": g_cloth, "streamer": g_streamer, "feet": g_feet, "sweet": g_sweet, "bowl": g_bowl,
    "candy": g_candy,
    # the machine
    "knock3": g_knock3, "knock_ring": g_knock_rings, "floor_hum": g_floor_hum, "grille": g_grille, "cam": g_cam,
    "arm": g_arm, "connect": g_connect, "gap": g_gap, "crank": g_crank, "wind": g_wind, "owl": g_owl,
    "owl_bow": g_owl_bow, "booth": g_booth,
    # senses, logic, feelings
    "eye": g_eye, "ear": g_ear, "hands": g_hands, "pointer": g_pointer, "question": g_question, "check": g_check,
    "not": g_not, "heart": g_heart, "moon": g_moon, "star": g_star, "bat": g_bat, "memory": g_memory,
    "quiet": g_sound_off,
    # moves
    "tip_walk": g_tip_walk, "tip_jump": g_pip_jump, "tip_hold": g_tip_hold, "tip_chain": g_tip_chain,
    "tip_glide": g_tip_glide, "tip_dive": g_tip_dive,
    # emotes
    "emo_wow": g_emo_wow, "emo_huh": g_emo_huh, "emo_love": g_emo_love, "emo_tune": g_emo_tune,
    "emo_think": g_emo_think, "emo_proud": g_emo_proud,
}

GROUPS = [
    ("Characters", ["pip", "bea", "vale", "june", "kit"]),
    ("Places", ["house", "house_side", "house_open", "house_cut", "window", "june_window", "door_shut", "door_open",
                "stuck", "lock", "latch", "hedge", "hedge_cape", "bench", "treehouse", "bridge", "porch", "bike",
                "lamp", "pin"]),
    ("Clues", ["cape", "cloth", "streamer", "feet", "sweet", "bowl", "candy"]),
    ("Machine", ["knock3", "knock_ring", "floor_hum", "grille", "cam", "arm", "connect", "gap", "crank", "wind",
                 "owl", "owl_bow", "booth"]),
    ("Senses + logic", ["eye", "ear", "hands", "pointer", "question", "check", "not", "heart", "moon", "star", "bat",
                        "memory", "quiet"]),
    ("Moves", ["tip_walk", "tip_jump", "tip_hold", "tip_chain", "tip_glide", "tip_dive"]),
    ("Emotes", ["emo_wow", "emo_huh", "emo_love", "emo_tune", "emo_think", "emo_proud"]),
]


# ----------------------------------------------------------------------------------------------- chips and strips

def chip(name, d=180, seed=1, ring=None, x3=False, fill=BONE, bare=False):
    """A die-cut paper sticker holding one pictogram. ring = orange stitched ring for the key clue."""
    sp = Sprite(d, d, 22)
    if not bare:
        pts = deckle(rrect_path(0, 0, d, d, d * 0.26, 3), 1.6, seed, 0.12, 0.5)
        U.Sprite.fill(sp, pts, fill, "paper", seed)
        if ring:
            sp.stitches(rrect_path(9, 9, d - 9, d - 9, d * 0.21, 2), ring, 9, 6, 3.2, shadow=False)
        sp.line(pts, INK, 3.0, closed=True)
    GLYPHS[name](G(sp, d / 2, d / 2 + (0 if not x3 else -d * 0.04), d * (0.86 if not x3 else 0.76)))
    if x3:
        for i in range(3):
            x = d / 2 + (i - 1) * d * 0.16
            sp.ellipse((x - d * 0.05, d * 0.83 - d * 0.05, x + d * 0.05, d * 0.83 + d * 0.05), fill=ORANGE,
                       outline=INK, width=2.4)
    return sp


def connector(kind, h=180):
    w = int(h * 0.42)
    sp = Sprite(w, h, 10)
    c, y = w / 2, h / 2
    col = PURPLE
    if kind == "->":
        sp.line([(4, y), (w - 6, y)], col, 9)
        sp.line([(w - 26, y - 20), (w - 4, y), (w - 26, y + 20)], col, 9)
    elif kind in ("=", "!="):
        sp.line([(8, y - 12), (w - 8, y - 12)], col, 9)
        sp.line([(8, y + 12), (w - 8, y + 12)], col, 9)
        if kind == "!=":
            sp.line([(w - 16, y - 34), (16, y + 34)], ORANGE, 9)
    elif kind == "+":
        sp.line([(8, y), (w - 8, y)], col, 9)
        sp.line([(c, y - 26), (c, y + 26)], col, 9)
    return sp


def strip(tokens, d=180, seed=0):
    """tokens: glyph names (suffix '*3' = three-times pips, '!' = key-clue ring) and connectors '->' '=' '!=' '+'."""
    parts = []
    for i, t in enumerate(tokens):
        if t in ("->", "=", "!=", "+"):
            parts.append(connector(t, d))
        else:
            x3 = t.endswith("*3")
            ring = ORANGE if t.endswith("!") else None
            name = t.rstrip("!").replace("*3", "")
            parts.append(chip(name, d, seed + i * 7, ring, x3))
    return parts


def strip_width(parts, gap=18):
    return sum(p.w for p in parts) + gap * (len(parts) - 1)


def draw_strip(sp, parts, x, y, gap=18, angles=None):
    """Paste parts left to right onto sprite sp, vertically centred at y."""
    cx = x
    for i, p in enumerate(parts):
        a = (angles[i] if angles else ((-1) ** i) * 1.6)
        im = p.render(a)
        sh = im.split()[3].point(lambda v: v * 110 // 255)
        shadow = Image.new("RGBA", im.size, INK[:3] + (0,))
        shadow.putalpha(sh.filter(ImageFilter.GaussianBlur(2)))
        sp.paste_img(shadow, cx + p.w / 2 - im.size[0] / 2 + 4, y - im.size[1] / 2 + 6, shadow)
        sp.paste_img(im, cx + p.w / 2 - im.size[0] / 2, y - im.size[1] / 2, im)
        cx += p.w + gap


def glyph_sprite(name, d, pad=10):
    sp = Sprite(d, d, pad)
    GLYPHS[name](G(sp, d / 2, d / 2, d * 0.86))
    return sp


# ----------------------------------------------------------------------------------------------- backgrounds

def frame(name):
    im = Image.open(os.path.join(FRAMES, name)).convert("RGB").resize((W, H), Image.LANCZOS)
    return im.convert("RGBA")


def inpaint(img, box, feather=6):
    """Push-pull fill of a rectangle from its surroundings (no OpenCV here); adds faint grain to avoid smears."""
    x0, y0, x1, y1 = [int(v) for v in box]
    arr = np.asarray(img.convert("RGB"), np.float32)
    w = np.ones(arr.shape[:2], np.float32)
    w[y0:y1, x0:x1] = 0
    levels = []
    c, ww = arr * w[..., None], w
    while min(ww.shape) > 4:
        levels.append((c, ww))
        h2, w2 = ww.shape[0] // 2, ww.shape[1] // 2
        c = c[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2, 3).sum((1, 3))
        ww = ww[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2).sum((1, 3))
    est = c / np.maximum(ww, 1e-6)[..., None]
    for c, ww in reversed(levels):
        up = np.asarray(Image.fromarray(np.clip(est, 0, 255).astype(np.uint8)).resize(
            (ww.shape[1], ww.shape[0]), Image.BILINEAR), np.float32)
        k = np.clip(ww, 0, 1)[..., None]
        est = (c / np.maximum(ww, 1e-6)[..., None]) * k + up * (1 - k)
    rng = np.random.default_rng(3)
    est += rng.normal(0, 3, est.shape)
    out = arr.copy()
    m = np.zeros(arr.shape[:2], np.float32)
    m[y0:y1, x0:x1] = 1
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(feather)),
                   np.float32)[..., None] / 255
    m = np.maximum(m, np.pad(np.ones((y1 - y0, x1 - x0)), ((y0, arr.shape[0] - y1), (x0, arr.shape[1] - x1)))[..., None])
    out = arr * (1 - m) + est * m
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).convert("RGBA")


def inpaint_rows(img, box, feather=8, band=24):
    """Row-wise fill for wide boxes over horizontal scenery (hedges, ground): each row blends the mean colour of a
    band left of the box into the band right of it, with grain borrowed from the right-hand neighbour."""
    x0, y0, x1, y1 = [int(v) for v in box]
    arr = np.asarray(img.convert("RGB"), np.float32).copy()
    w = x1 - x0
    lb = arr[y0:y1, max(0, x0 - band):x0].mean(1) if x0 > 0 else arr[y0:y1, x1:x1 + band].mean(1)
    rb = arr[y0:y1, x1:x1 + band].mean(1)
    t = np.linspace(0, 1, w)[None, :, None]
    fill = lb[:, None, :] * (1 - t) + rb[:, None, :] * t
    fill += np.random.default_rng(5).normal(0, 4, fill.shape)  # grain, so the fill reads as texture, not fog
    fill = np.asarray(Image.fromarray(np.clip(fill, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)),
                      np.float32)
    m = np.zeros(arr.shape[:2], np.float32)
    m[y0:y1, x0:x1] = 1
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(feather)),
                   np.float32) / 255
    m[y0:y1, x0:x1] = np.maximum(m[y0:y1, x0:x1], 0.0)
    full = arr.copy()
    full[y0:y1, x0:x1] = fill
    inner = np.zeros_like(m)
    inner[y0 + feather:y1 - feather, x0 + feather:x1 - feather] = 1
    m = np.maximum(m, inner)[..., None]
    out = arr * (1 - m) + full * m
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).convert("RGBA")


def blur_world(img, r=5):
    return img.filter(ImageFilter.GaussianBlur(r))


def callout(c, text, x, y, tx, ty, w=None):
    """Design annotation (not game UI): candy sticky note with a pencil leader to (tx, ty)."""
    f = U.font(F_BOLD, 12 * U.PT)
    lines = text.split("\n")
    tw = int(max(f.getlength(t) for t in lines) + 40) if w is None else w
    th = int(len(lines) * 12 * U.PT * 1.25 + 26)
    d = ImageDraw.Draw(c)
    d.line([(x, y), (tx, ty)], fill=CANDY, width=4)
    d.ellipse((tx - 9, ty - 9, tx + 9, ty + 9), fill=CANDY, outline=INK, width=2)
    sp = paper_card(tw, th, 900 + int(x) % 50, CANDY, INK, None, 8, 1.0, 2.2, 0, pad=8)
    for i, t in enumerate(lines):
        sp.text(20, 16 + i * 12 * U.PT * 1.25, t, F_BOLD, 12 * U.PT, INK, "la")
    place(c, sp, x, y, angle=1.0, shadow=(3, 4), shadow_alpha=110)


def emote_at(c, name, x, y, d=150, angle=-4):
    sp = Sprite(d, d, 14)
    GLYPHS[name](G(sp, d / 2, d / 2, d))
    place(c, sp, x, y, angle=angle, shadow=(3, 5), shadow_alpha=120)


def babble(sp, x, y, k=1.0, col=CANDY):
    """Babble marks beside a portrait mouth: three short arcs (the voice is non-verbal)."""
    for i, r in enumerate((18, 32, 46)):
        pts = [(x + r * k * math.cos(a), y + r * k * math.sin(a)) for a in np.linspace(-0.6, 0.6, 10)]
        sp.line(pts, col, 5 * k)


def portrait_disc(name, d=300, fill=ORANGE, seed=107):
    fr = felt_disc(d + 40, fill, 255, CANDY, 11, seed=seed)
    inner = Sprite(d, d, 0)
    inner.ellipse((0, 0, d, d), fill=BONE_D)
    GLYPHS[name](G(inner, d / 2, d / 2 + d * 0.05, d * 0.95))
    im = inner.render()
    m = Image.new("L", im.size, 0)
    ImageDraw.Draw(m).ellipse((0, 0, im.size[0] - 1, im.size[1] - 1), fill=255)
    lay = fr.layer()
    im = im.resize((im.size[0] * SS, im.size[1] * SS), Image.LANCZOS)
    m = m.resize(im.size, Image.LANCZOS)
    lay.paste(im, (int((fr.pad + 20) * SS), int((fr.pad + 20) * SS)), m)
    fr.comp(lay)
    fr.ellipse((20, 20, 20 + d, 20 + d), outline=INK, width=4)
    return fr


def next_disc(d=130, seed=104):
    nx = felt_disc(d, ORANGE, 255, CANDY, 9, seed=seed)
    icon_draw(nx, "next", d / 2, d / 2, d * 0.56, INK, 5)
    return nx


def skip_disc(c, x, y):
    s = felt_disc(150, NIGHT, 230, BONE, 12, seed=111)
    icon_draw(s, "skip", 75, 75, 74, BONE, 5.5)
    place(c, s, x, y, shadow=(3, 5), shadow_alpha=130)


def line_pips(sp, x, y, n, cur):
    for i in range(n):
        r = 11
        xx = x + i * 38
        sp.ellipse((xx - r, y - r, xx + r, y + r), fill=PURPLE if i <= cur else None, outline=PURPLE, width=3)


def dialogue_card(c, speaker, lines_tokens, text=None, cw=1620, ch=380, fill=ORANGE, cur=0, n=2, memory=False):
    cx, cy = W / 2 + 110, H - U.SAFE_B - ch / 2 - 22
    card = paper_card(cw, ch, 101, BONE if not memory else hexc("E9DDC2"), INK, PURPLE, 34, 3.4, 3.4, 15, 11, 8)
    d = 196 if text else 214
    rows = [strip(t, d, seed=17 * (k + 1)) for k, t in enumerate(lines_tokens)]
    TX = 330
    yy = ch / 2 - (34 if text else 0)
    for parts in rows:
        draw_strip(card, parts, TX, yy, gap=20)
    if text:
        card.text(TX + 6, ch - 58, text, F_BOLD, 15 * U.PT, GRAPHITE, "lm")
    nx = next_disc(130).render()
    card.paste_img(nx, cw - nx.size[0] - 14, ch - nx.size[1] - 12, nx)
    line_pips(card, cw - 100 - (n - 1) * 38, 52, n, cur)
    place(c, card, cx, cy, angle=-0.5, shadow=(6, 9))
    fr = portrait_disc(speaker, 300, fill)
    fim = fr
    place(c, fim, cx - cw / 2 + 70, cy - 60, angle=-3, shadow=(5, 8))
    b = Sprite(80, 120, 10)
    babble(b, 6, 60, 1.2)
    place(c, b, cx - cw / 2 + 70 + 205, cy - 40, shadow=None)
    return cx, cy, cw, ch


# ----------------------------------------------------------------------------------------------- mocks

def clean_dialogue_frame():
    """14_dialogue without its text UI: keep the sky, hedge and Bea's head; rebuild the dark ground below."""
    c = frame("14_dialogue_run60.jpg")
    # Skip button -> copy of the sky just to its left
    c = inpaint(c, (2120, 15, 2520, 195), feather=10)   # old Skip button
    c = inpaint(c, (680, 545, 1850, 705), feather=10)   # old caption strip
    c = inpaint(c, (400, 680, 2200, 1160), feather=12)  # old speech card + portrait
    return c


def mock_dialogue_bea():
    c = clean_dialogue_frame()
    U.gradient_scrim(c, NIGHT, 0.0, 0.8, 520, H, axis="y")
    # pictogram caption for the knock (direction cue kept)
    cap = paper_card(560, 150, 95, NIGHT, INK, CANDY[:3] + (200,), 30, 1.2, 3, 10, kind="felt", fuzz=True)
    d = Sprite(96, 96, 6)
    d.ellipse((0, 0, 96, 96), fill=PURPLE)
    icon_draw(d, "arrow", 48, 48, 72, CANDY, 6.5)
    im = d.render(180)
    cap.paste_img(im, 30, 75 - im.size[0] / 2, im)
    parts = strip(["knock3", "house"], 118, seed=3)
    draw_strip(cap, parts, 160, 75, gap=14)
    k = Sprite(140, 140, 0)
    place(c, cap, W / 2 + 200, 610, angle=0.6, shadow=(3, 5), shadow_alpha=120)
    # Bea: emote + pointing pose cue
    emote_at(c, "emo_wow", 1390, 330, 150, -6)
    ptr = Sprite(150, 150, 10)
    g = G(ptr, 75, 75, 140)
    g_pointer(g)
    place(c, ptr, 1120, 470, angle=200, shadow=(3, 5), shadow_alpha=120)
    dialogue_card(c, "bea", [["eye", "cape!", "->", "house_side"]],
                  text="I saw June's silver cape go in. Right there!", fill=ORANGE)
    skip_disc(c, W - U.SAFE_R - 84, 96)
    callout(c, "Bea plays Point: her arm swings toward\nthe house (behind you), the !-emote pops", 560, 330, 1080, 450)
    callout(c, "Pictures + Words: the strip leads,\na small text line follows", 2050, 470, 1700, 760)
    callout(c, "Knock caption as pictures,\narrow still turns to the source", 560, 520, 1610, 610)
    return c


def mock_dialogue_vale():
    c = frame("02_play_start_run60.jpg")
    c = inpaint_rows(c, (80, 515, 810, 690), feather=10)   # move tip card (HUD hidden in dialogue)
    c = inpaint(c, (180, 700, 500, 1020), feather=10)  # stick
    c = inpaint(c, (1890, 840, 2460, 1150), feather=10)  # Use, Roll, Jump
    c = inpaint(c, (2160, 10, 2520, 180), feather=8)   # notebook + pause
    c = blur_world(c, 4.5)  # dialogue depth of field: the world softens, the look-line stays crisp
    U.gradient_scrim(c, NIGHT, 0.0, 0.82, 600, H, axis="y")
    # world cue: Vale's sightline from the bench to the front door, dotted candy
    bench = (1842, 575)
    door = (1262, 535)
    dr = ImageDraw.Draw(c)
    for i in range(1, 22):
        t = i / 22
        x = bench[0] + (door[0] - bench[0]) * t
        y = bench[1] + (door[1] - bench[1]) * t - 40 * math.sin(math.pi * t)
        dr.ellipse((x - 7, y - 7, x + 7, y + 7), fill=CANDY, outline=INK, width=2)
    U.radial_soft(c, door[0], door[1], 90, CANDY, 0.35)
    emote_at(c, "emo_think", 1890, 440, 140, 5)
    dialogue_card(c, "vale", [["bench", "->", "eye", "door_shut!"]], text=None, fill=PURPLE)
    skip_disc(c, W - U.SAFE_R - 84, 96)
    callout(c, "Pictures mode: no text at all.\nThe dotted look-line runs from\nVale's bench to the front door", 640, 380,
            1240, 520)
    callout(c, "Door chip: closed door, no wiggle\n(shut, not stuck). Orange ring = key clue", 1960, 300, 1590, 870)
    return c


def sticker(names, d=120, seed=1, found=True):
    """A notebook clue sticker: die-cut paper with 1-2 mini glyphs; unfound = dashed pencil outline."""
    w = d * len(names) + 26
    h = d + 24
    sp = Sprite(w, h, 14)
    pts = deckle(rrect_path(0, 0, w, h, 28, 3), 1.4, seed, 0.12, 0.5)
    if found:
        U.Sprite.fill(sp, pts, hexc("FFF6E2"), "paper", seed)
        sp.line(pts, INK, 2.6, closed=True)
        for i, n in enumerate(names):
            GLYPHS[n](G(sp, 13 + d / 2 + i * d, h / 2, d * 0.86))
    else:
        sp.stitches(pts, GRAPHITE[:3] + (170,), 10, 8, 2.6, shadow=False)
        sp.text(w / 2, h / 2 + 4, "?", F_DISPLAY, d * 0.55, GRAPHITE[:3] + (150,), "mm")
    return sp


def comic(panels, pw=230, ph=210, seed=5):
    """Little comic strip: framed panels with one glyph each and pencil arrows between."""
    n = len(panels)
    gap = 54
    w = n * pw + (n - 1) * gap
    sp = Sprite(w, ph, 16)
    for i, p in enumerate(panels):
        x = i * (pw + gap)
        pts = deckle(rrect_path(x, 0, x + pw, ph, 14, 3), 1.0, seed + i, 0.15, 0.4)
        U.Sprite.fill(sp, pts, hexc("FFF6E2"), "paper", seed + i)
        sp.line(pts, INK, 3.0, closed=True)
        GLYPHS[p](G(sp, x + pw / 2, ph / 2, min(pw, ph) * 0.84))
        if i < n - 1:
            ax = x + pw + 8
            sp.line([(ax, ph / 2), (ax + gap - 16, ph / 2)], PURPLE, 7)
            sp.line([(ax + gap - 30, ph / 2 - 14), (ax + gap - 14, ph / 2), (ax + gap - 30, ph / 2 + 14)], PURPLE, 7)
    return sp


def found_stamp(d=170):
    sp = Sprite(d, d, 10)
    sp.ellipse((0, 0, d, d), fill=None, outline=GREEN, width=9)
    sp.ellipse((16, 16, d - 16, d - 16), fill=None, outline=GREEN[:3] + (180,), width=4)
    g = G(sp, d / 2, d / 2, d)
    check_mark(g, 0, 0.02, 0.3)
    for i in range(10):
        a = 2 * math.pi * i / 10
        sp.ellipse((d / 2 + 0.43 * d * math.cos(a) - 4, d / 2 + 0.43 * d * math.sin(a) - 4,
                    d / 2 + 0.43 * d * math.cos(a) + 4, d / 2 + 0.43 * d * math.sin(a) + 4), fill=GREEN)
    return sp


def question_row(sp, x, y, tokens, num, d=132):
    # number badge: dots, not digits, so the order reads without counting skills
    badge = Sprite(70, 70, 4)
    badge.ellipse((0, 0, 70, 70), fill=ORANGE, outline=INK, width=3)
    for i in range(num):
        bx = 35 + (i - (num - 1) / 2) * 17
        badge.ellipse((bx - 6, 29, bx + 6, 41), fill=INK)
    im = badge.render()
    sp.paste_img(im, x, y - im.size[1] / 2, im)
    parts = strip(tokens, d, seed=num * 11)
    draw_strip(sp, parts, x + 96, y, gap=10)


def mock_notebook():
    c = frame("15_notebook_run60.jpg")
    c = inpaint(c, (2080, 990, 2520, 1160), feather=8)  # old text Hint button
    c = inpaint(c, (2350, 5, 2525, 185), feather=8)     # old close disc
    sh = Image.new("L", (W, H), 0)
    ImageDraw.Draw(sh).rectangle((300, 905, 2230, 1010), fill=235)  # hide the old cover's bottom edge in shadow
    sh = sh.filter(ImageFilter.GaussianBlur(18))
    lay = Image.new("RGBA", (W, H), NIGHT[:3] + (0,))
    lay.putalpha(sh)
    c.alpha_composite(lay)
    c.alpha_composite(Image.new("RGBA", (W, H), NIGHT[:3] + (90,)))
    pw, ph = 1060, 860
    cx, cy = W / 2 - 10, 44 + ph / 2
    cover = paper_card(2 * pw + 70, ph + 50, 201, PLUM, INK, CANDY, 40, 1.6, 3.4, 14, 12, 8, kind="felt", fuzz=True)
    place(c, cover, cx, cy, shadow=(6, 10))
    L = paper_card(pw, ph, 203, BONE, INK, PURPLE, 26, 2.0, 3.0, 13, 10, 7)
    R = paper_card(pw, ph, 205, BONE, INK, PURPLE, 26, 2.0, 3.0, 13, 10, 7)
    # left page: question 1 (found) as a comic, plus its clue stickers
    question_row(L, 44, 104, ["june", "question", "house"], 1)
    cm = comic(["treehouse", "bridge", "june_window"], 236, 214)
    im = cm.render(-0.8)
    L.paste_img(im, 60, 196, im)
    st = found_stamp(170).render(-12)
    L.paste_img(st, pw - 230, 40, st)
    stick = [(["bea", "cape"], True), (["vale", "door_shut"], True), (["bench", "hedge"], False),
             (["streamer", "not"], True), (["cloth"], False), (["window", "cloth"], False), (["june", "treehouse"], True)]
    xs, ys = 52, 440
    x = xs
    row = 0
    for i, (names, found) in enumerate(stick):
        s = sticker(names, 92, 300 + i, found)
        im = s.render(((-1) ** i) * 2.5)
        if x + s.w > pw - 40:
            row += 1
            x = xs + 30
        L.paste_img(im, x, ys + row * 150, im)
        x += s.w + 22
    # right page: Q2 (open, knocks sticker), Q3 (open)
    question_row(R, 44, 104, ["knock3", "question", "house_cut"], 2)
    sl = Sprite(560, 200, 10)
    sl.stitches(rrect_path(0, 0, 560, 200, 26, 2), GRAPHITE[:3] + (170,), 12, 10, 3, shadow=False)
    U.silhouette_unknown(sl, 120, 100, 70, 4)
    for i in range(2):
        sl.stitches(rrect_path(240 + i * 150, 40, 360 + i * 150, 160, 18, 2), GRAPHITE[:3] + (120,), 8, 8, 2.2,
                    shadow=False)
    im = sl.render(0.6)
    R.paste_img(im, 70, 190, im)
    s = sticker(["knock3", "house"], 112, 400, True)
    im = s.render(-3)
    R.paste_img(im, 680, 210, im)
    question_row(R, 44, 470, ["knock3", "->", "question"], 3)
    im = sl.render(-0.6)
    R.paste_img(im, 70, 545, im)
    s = sticker(["sweet"], 112, 401, False)
    im = s.render(2)
    R.paste_img(im, 700, 590, im)
    # paper bat progress: 10 slots, 7 filled (count without numbers)
    for i in range(10):
        bx = 120 + i * 84
        if i < 7:
            U.paper_bat(R, bx, ph - 80, 74, CANDY, flap=0.1)
        else:
            pts = U.bat_points(bx, ph - 80, 74)
            R.stitches(pts, GRAPHITE[:3] + (150,), 6, 5, 2.0, shadow=False)
    place(c, L, cx - pw / 2 - 8, cy, angle=-0.4, shadow=(3, 5))
    place(c, R, cx + pw / 2 + 8, cy, angle=0.4, shadow=(3, 5))
    # close (top right), radio hint disc (bottom right), both icon-only
    cl = felt_disc(150, NIGHT, 235, BONE, 12, seed=71)
    icon_draw(cl, "close", 75, 75, 70, BONE, 6)
    place(c, cl, 2437, 93)
    rd = felt_disc(200, ORANGE, 255, CANDY, 13, seed=81)
    icon_draw(rd, "radio", 100, 100, 104, INK, 7)
    place(c, rd, W - U.SAFE_R - 60, H - U.SAFE_B - 110)
    callout(c, "Stickers fill dashed slots;\ntap one to replay its moment", 520, 1085, 820, 560)
    callout(c, "Answer = 3-panel comic\n+ green stamp", 1120, 1085, 900, 400)
    callout(c, "Open question: ? silhouette\n+ empty slots waiting", 1740, 1085, 1640, 760)
    return c


def ghost_path(c, pts, n=26, col=CANDY):
    """A soft dotted trail of little paper bats/footprints along a polyline (world cue, hint tier 2)."""
    seg = []
    for i in range(len(pts) - 1):
        seg.append((pts[i], pts[i + 1], math.dist(pts[i], pts[i + 1])))
    total = sum(s[2] for s in seg)
    for k in range(n):
        t = (k + 0.5) / n * total
        for a, b, L in seg:
            if t <= L:
                x = a[0] + (b[0] - a[0]) * t / L
                y = a[1] + (b[1] - a[1]) * t / L
                ang = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
                break
            t -= L
        persp = 0.45 + 0.55 * (y - 450) / 550
        s = max(26, 64 * persp)
        sp = Sprite(int(s * 1.2), int(s * 1.2), 8)
        U.paper_bat(sp, s * 0.6, s * 0.6, s, col, flap=0.15 * ((-1) ** k))
        im = sp.render(-ang * 0.2)
        a = im.split()[3].point(lambda v: int(v * (0.35 + 0.65 * (1 - k / n) ** 0.4)))
        im.putalpha(a)
        U.radial_soft(c, x, y, s * 1.1, CANDY, 0.18)
        c.alpha_composite(im, (int(x - im.size[0] / 2), int(y - im.size[1] / 2)))


def hint_bubble(tokens, d=150, tier=2):
    parts = strip(tokens, d, seed=41)
    w = int(strip_width(parts, 14) + 60 + 200)
    h = d + 70
    sp = paper_card(w, h, 501, NIGHT, INK, CANDY[:3] + (210,), 40, 1.4, 3.2, 12, kind="felt", fuzz=True)
    rd = felt_disc(150, ORANGE, 255, CANDY, 11, seed=82)
    icon_draw(rd, "radio", 75, 75, 80, INK, 6)
    im = rd.render()
    sp.paste_img(im, 22, h / 2 - im.size[1] / 2, im)
    # crackle lines
    for r in (14, 28):
        pts = [(190 + r * math.cos(a), h / 2 - 40 + r * math.sin(a)) for a in np.linspace(-1.0, 0.2, 8)]
        sp.line(pts, CANDY, 4)
    draw_strip(sp, parts, 220, h / 2, gap=14)
    # tier pips: 1 = look, 2 = where, 3 = show me
    for i in range(3):
        x = w - 40 - (2 - i) * 30
        sp.ellipse((x - 9, 22, x + 9, 40), fill=CANDY if i < tier else None, outline=CANDY, width=3)
    return sp


def mock_hint():
    c = frame("02_play_start_run60.jpg")
    c = inpaint_rows(c, (80, 515, 810, 690), feather=10)  # text move tip (replaced by pictures)
    # ghost path from Pip along the path, left past the maple toward the side-yard gap
    path = [(1150, 1010), (1150, 860), (1120, 730), (1020, 650), (880, 605), (760, 572), (640, 548)]
    ghost_path(c, path, 22)
    # destination marker: floating treehouse sticker with a soft beacon over the side-yard gap
    U.radial_soft(c, 600, 470, 210, CANDY, 0.35)
    dest = chip("treehouse", 190, 77, ring=ORANGE)
    place(c, dest, 600, 400, angle=-3, shadow=(4, 6))
    tri = Sprite(40, 30, 4)
    tri.poly([(2, 2), (38, 2), (20, 28)], BONE)
    tri.line([(2, 2), (20, 28), (38, 2)], INK, 3)
    place(c, tri, 600, 520, shadow=None)
    hb = hint_bubble(["pip", "->", "treehouse", "->", "window"], 150, tier=2)
    place(c, hb, W / 2 + 40, H - U.SAFE_B - 120, angle=-0.5, shadow=(4, 7))
    callout(c, "Hint tier 2 (where): the radio bubble\nshows the route, the world shows the way", 1500, 300,
            1380, 960)
    callout(c, "Ghost path: paper bats fade from Pip\ntoward the side yard, ~8 s, then fade", 1100, 160, 950, 590)
    callout(c, "Beacon: the treehouse sticker floats\nover the hidden goal, gently pulsing", 340, 230, 560, 330)
    return c


def ghost_hand(c, x, y, s=210, alpha=210, angle=-20):
    sp = Sprite(s, s, 12)
    g = G(sp, s / 2, s / 2, s)
    g.shape(hand_pts(), BONE[:3] + (alpha,), outline=False)
    sp.line(g.pts(hand_pts()), INK[:3] + (alpha,), 3.5, closed=True)
    place(c, sp, x, y, angle=angle, shadow=(4, 6), shadow_alpha=90)


def mock_onboarding():
    c = frame("02_play_start_run60.jpg")
    c = inpaint_rows(c, (80, 515, 810, 690), feather=10)  # text tip
    c = inpaint(c, (1890, 1060, 2140, 1150), feather=6)  # 'Roll' word tag (pictures mode keeps the glyph only)
    jump = (2314, 941)
    # hold ring filling around Jump (candy arc ~70%) + press pulse
    dr = ImageDraw.Draw(c)
    r = 140
    dr.arc((jump[0] - r, jump[1] - r, jump[0] + r, jump[1] + r), -90, -90 + 360, fill=INK[:3] + (120,), width=20)
    dr.arc((jump[0] - r, jump[1] - r, jump[0] + r, jump[1] + r), -90, -90 + 250, fill=CANDY, width=14)
    for rr, a in ((175, 120), (205, 60)):
        lay = Image.new("RGBA", c.size, (0, 0, 0, 0))
        ImageDraw.Draw(lay).ellipse((jump[0] - rr, jump[1] - rr, jump[0] + rr, jump[1] + rr), outline=CANDY[:3] + (a,),
                                    width=5)
        c.alpha_composite(lay)
    ghost_hand(c, jump[0] + 40, jump[1] + 150, 260, 220, angle=-15)
    # demonstration card: tap = small hop, hold = big jump
    card = paper_card(720, 400, 611, BONE, INK, PURPLE, 34, 2.4, 3.2, 13)
    # left mini: tap
    g1 = G(card, 180, 210, 300)
    g_pip_jump(g1)
    tap = Sprite(90, 90, 6)
    tap.ellipse((10, 10, 80, 80), fill=NIGHT, outline=CANDY, width=6)
    icon_draw(tap, "jump", 45, 42, 40, BONE, 4)
    im = tap.render()
    card.paste_img(im, 30, 300, im)
    card.line([(360, 60), (360, 350)], GRAPHITE[:3] + (120,), 3)
    g2 = G(card, 540, 200, 330)
    g_tip_hold(g2)
    hold = Sprite(100, 100, 6)
    hold.ellipse((10, 10, 90, 90), fill=NIGHT, outline=INK, width=4)
    hold.line([(50 + 40 * math.cos(math.radians(a)), 50 + 40 * math.sin(math.radians(a)))
               for a in range(-90, 160, 6)], CANDY, 8)
    icon_draw(hold, "jump", 50, 46, 44, BONE, 4)
    im = hold.render()
    card.paste_img(im, 390, 290, im)
    place(c, card, 1640, 520, angle=-1.2, shadow=(5, 8))
    tail = Sprite(120, 120, 6)
    tail.poly([(0, 0), (70, 0), (110, 110)], BONE)
    tail.line([(0, 0), (110, 110), (70, 0)], INK, 3.5)
    place(c, tail, 1980, 700, shadow=None)
    # walking ghost-hand ghost on the stick, faded (already learned)
    ghost_hand(c, 380, 930, 200, 90, angle=10)
    callout(c, "Ghost hand presses and HOLDS Jump;\nthe candy ring fills as the jump grows", 1700, 1110, 2150, 1000)
    callout(c, "Tap = small hop | hold = big jump,\nshown side by side, no words", 900, 300, 1300, 420)
    callout(c, "No text tip any more. The stick's ghost\nhand stays dim: walking is learned", 480, 560, 400, 880)
    return c


# ----------------------------------------------------------------------------------------------- icon sheet

def mock_icons():
    cols = 11
    cell = 220
    labels_h = 46
    group_h = 70
    rows = sum(math.ceil(len(g[1]) / cols) for g in GROUPS)
    w = cols * cell + 120
    h = 220 + rows * (cell + labels_h) + len(GROUPS) * group_h + 40
    sheet = Image.new("RGBA", (w, h), NIGHT)
    tex = U.texture(w, h, NIGHT, "paper", 9)
    sheet.alpha_composite(tex)
    t = Sprite(w, 200, 0)
    t.text(60, 70, "Spooky Street · pictograms", F_DISPLAY, 34 * U.PT, BONE, "lm")
    t.text(62, 150, "Non-verbal UX inventory · felt and paper stickers · IDs are language-neutral (pg.<id>)",
           F_BOLD, 15 * U.PT, BONE[:3] + (210,), "lm")
    sheet.alpha_composite(t.render(), (0, 0))
    y = 220
    for gname, names in GROUPS:
        gl = Sprite(w, group_h, 0)
        gl.text(60, group_h / 2, gname, F_DISPLAY, 18 * U.PT, CANDY, "lm")
        sheet.alpha_composite(gl.render(), (0, y))
        y += group_h
        for i, n in enumerate(names):
            r, k = divmod(i, cols)
            x0 = 60 + k * cell
            y0 = y + r * (cell + labels_h)
            ch = chip(n, cell - 40, seed=i + 3)
            place(sheet, ch, x0 + cell / 2, y0 + cell / 2 - 10, angle=((-1) ** i) * 1.2, shadow=(3, 5),
                  shadow_alpha=120)
            lab = Sprite(cell, labels_h, 0)
            lab.text(cell / 2, labels_h / 2, n, F_BOLD, 12 * U.PT, BONE, "mm")
            sheet.alpha_composite(lab.render(), (x0, y0 + cell - 18))
        y += math.ceil(len(names) / cols) * (cell + labels_h)
    return sheet


def phone_check(img, path):
    """Phone-size preview (iPhone landscape at 1x points, 844x390) to judge legibility."""
    img.convert("RGB").resize((844, 390), Image.LANCZOS).save(path, quality=90)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--copy", default=None, help="also copy the outputs to this folder")
    ap.add_argument("--only", default=None)
    ap.add_argument("--phone", default=None, help="write 844x390 phone-size previews to this folder")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    jobs = [("nv_dialogue_bea", mock_dialogue_bea), ("nv_dialogue_vale", mock_dialogue_vale),
            ("nv_notebook", mock_notebook), ("nv_hint", mock_hint), ("nv_onboarding", mock_onboarding),
            ("nv_icons", mock_icons)]
    for name, fn in jobs:
        if a.only and a.only != name:
            continue
        im = fn().convert("RGB")
        ext = ".png" if name == "nv_icons" else ".jpg"
        paths = [os.path.join(a.out, name + ext)]
        if a.copy:
            os.makedirs(a.copy, exist_ok=True)
            paths.append(os.path.join(a.copy, name + ext))
        for p in paths:
            if ext == ".png":
                im.save(p, optimize=True)
            else:
                im.save(p, quality=88)
        if a.phone and name != "nv_icons":
            os.makedirs(a.phone, exist_ok=True)
            phone_check(im, os.path.join(a.phone, name + "_phone.jpg"))
        print("wrote", paths[0])


if __name__ == "__main__":
    main()
