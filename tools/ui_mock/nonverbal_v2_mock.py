#!/usr/bin/env python3
"""Non-verbal UX v2 animated mocks for Spooky Street (docs/nonverbal-ux.md, v2).

The owner found the v1 pictogram rows too busy for small children and asked for animated sequences. This script
draws short looping paper-puppet animations procedurally (reusing the v1 pictogram parts from nonverbal_mock.py and
the craft helpers from ui_mock.py), composites them over the CI frames and writes animated GIFs plus a 6-frame
contact sheet per GIF.

    python3 tools/ui_mock/nonverbal_v2_mock.py [--out docs/evidence/s002/nonverbal_v2] [--copy DIR] [--only NAME]

Design coordinates are the full mock size (2532x1170, as in v1); GIFs are written at half size (1266x585).
"""
from __future__ import annotations

import argparse
import math
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nonverbal_mock as N  # noqa: E402
import ui_mock as U  # noqa: E402
from nonverbal_mock import G, HAIR_B, INDIGO, SKIN_JUNE, VIOLET, WARM, WOOD, WOOD_D  # noqa: E402
from ui_mock import (BONE, CANDY, F_BOLD, GRAPHITE, H, INK, NIGHT, ORANGE, PURPLE,  # noqa: E402
                     SILVER, Sprite, W, deckle, felt_disc, hexc, icon_draw, paper_card, rrect_path)

OUT = os.path.join(N.FRAMES, "nonverbal_v2")
K = 0.5                       # output scale
OW, OH = int(W * K), int(H * K)
FPS = 15
STAGE = hexc("332D62")        # vignette stage: a slightly lifted night felt, so dark shapes still read
STAGE_L = hexc("413A78")


# ----------------------------------------------------------------------------------------------- timing helpers

def clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def seg(t, t0, t1):
    """0..1 progress of t through [t0, t1]."""
    return clamp((t - t0) / (t1 - t0)) if t1 > t0 else float(t >= t1)


def ease(x):
    return x * x * (3 - 2 * x)


def back(x, s=1.70158):
    x -= 1
    return x * x * ((s + 1) * x + s) + 1


def wiggle(x, n=3, amp=1.0):
    """Damped wiggle that settles to 0 at x=1 (sticker 'wiggle into place')."""
    return amp * math.sin(x * n * 2 * math.pi) * (1 - x) ** 1.5


def lerp(a, b, t):
    return a + (b - a) * t


def path_at(pts, t):
    """Point and heading at fraction t along a polyline."""
    lens = [math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
    d = clamp(t) * sum(lens)
    for (a, b), L in zip(zip(pts, pts[1:]), lens):
        if d <= L or L == lens[-1]:
            u = d / L if L else 0
            return (lerp(a[0], b[0], u), lerp(a[1], b[1], u)), math.atan2(b[1] - a[1], b[0] - a[0])
        d -= L
    return pts[-1], 0.0


# ----------------------------------------------------------------------------------------------- cut-outs

def half(im):
    return im.resize((max(1, round(im.width * K)), max(1, round(im.height * K))), Image.LANCZOS)


class Cut:
    """A pre-rendered paper cut-out (half-size RGBA) pasted per frame at full-size design coordinates."""

    def __init__(self, sp_or_img, angle=0.0):
        im = sp_or_img.render(angle) if isinstance(sp_or_img, Sprite) else sp_or_img
        self.im = half(im)

    def draw(self, c, cx, cy, angle=0.0, scale=1.0, alpha=1.0, shadow=True, sx=1.0):
        if scale <= 0.01 or alpha <= 0.01:
            return
        im = self.im
        if scale != 1.0 or sx != 1.0:
            im = im.resize((max(1, round(im.width * scale * abs(sx))), max(1, round(im.height * scale))),
                           Image.BICUBIC)
            if sx < 0:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
        if angle:
            im = im.rotate(angle, resample=Image.BICUBIC, expand=True)
        if alpha < 1.0:
            im = im.copy()
            im.putalpha(im.split()[3].point(lambda v: int(v * alpha)))
        x = int(round(cx * K - im.width / 2))
        y = int(round(cy * K - im.height / 2))
        if shadow:
            sh = Image.new("RGBA", im.size, INK[:3] + (0,))
            sh.putalpha(im.split()[3].point(lambda v: v * 120 // 255).filter(ImageFilter.GaussianBlur(1.2)))
            c.alpha_composite(sh, (x + 2, y + 3)) if x + 2 >= 0 and y + 3 >= 0 else _safe_comp(c, sh, x + 2, y + 3)
        _safe_comp(c, im, x, y)


def _safe_comp(c, im, x, y):
    lay = Image.new("RGBA", c.size, (0, 0, 0, 0))
    lay.paste(im, (x, y))
    c.alpha_composite(lay)


def glyph(name, d, angle=0.0, pad=12):
    sp = Sprite(d, d, pad)
    N.GLYPHS[name](G(sp, d / 2, d / 2, d * 0.86))
    return Cut(sp, angle)


def soft_halo(d, col, a):
    yy, xx = np.mgrid[0:d, 0:d]
    r = np.sqrt((xx - d / 2) ** 2 + (yy - d / 2) ** 2) / (d / 2)
    al = (np.clip(1 - r, 0, 1) ** 1.8 * 255 * a).astype(np.uint8)
    im = Image.new("RGBA", (d, d), col[:3] + (0,))
    im.putalpha(Image.fromarray(al))
    cut = Cut.__new__(Cut)
    cut.im = half(im)
    return cut


def ghostify(cut, col=hexc("EEF2FF"), a=0.55, edge=CANDY):
    """Memory ghost: the puppet's silhouette as translucent pale paper with a soft candy rim."""
    al = cut.im.split()[3]
    body = Image.new("RGBA", cut.im.size, col[:3] + (0,))
    body.putalpha(al.point(lambda v: int(v * a)))
    rim = al.filter(ImageFilter.MaxFilter(5))
    rim = Image.fromarray(np.clip(np.asarray(rim, np.int16) - np.asarray(al, np.int16), 0, 255).astype(np.uint8))
    rl = Image.new("RGBA", cut.im.size, edge[:3] + (0,))
    rl.putalpha(rim.point(lambda v: int(v * 0.85)))
    out = Image.alpha_composite(body, rl)
    g = Cut.__new__(Cut)
    g.im = out
    return g


# ----------------------------------------------------------------------------------------------- puppets

def june_puppet(phase=0.0, d=200):
    """Tiny paper June running right: violet hat, silver cape flowing back, striped stockings."""
    return Cut(june_puppet_sprite(phase, d))


def june_puppet_sprite(phase=0.0, d=200, pad=16):
    """The running June puppet as a Sprite (also baked into the game's PuppetAtlas by puppet_atlas.py)."""
    sp = Sprite(d, d, pad)
    g = G(sp, d / 2, d / 2, d)
    swing = math.sin(phase * 2 * math.pi)
    # legs (striped stockings) behind the cape
    for k, s in ((0, swing), (1, -swing)):
        hx = 0.0 + (0.02 if k else -0.02)
        fx, fy = hx + 0.13 * s, 0.42 - 0.03 * abs(s)
        g.line([(hx, 0.22), (fx, fy)], PURPLE, 3.6)
        g.line([(hx + (fx - hx) * 0.5, 0.22 + (fy - 0.22) * 0.5), (hx + (fx - hx) * 0.55, 0.22 + (fy - 0.22) * 0.55)],
               BONE, 3.6)
        g.ell(fx + 0.03, fy + 0.02, 0.055, 0.03, INK, outline=False)
    # silver cape streaming back (left), its triangular tear notch kept as the identity mark
    fl = 0.04 * math.sin(phase * 4 * math.pi)
    g.shape([(0.06, -0.12), (-0.04, -0.12), (-0.4, 0.12 + fl), (-0.33, 0.2 + fl), (-0.25, 0.14 + fl),
             (-0.22, 0.26 + fl), (-0.02, 0.26), (0.1, 0.2)], SILVER)
    N.star(g, -0.18, 0.1 + fl, 0.04, CANDY)
    # dress
    g.shape([(-0.06, -0.06), (0.1, -0.06), (0.14, 0.24), (-0.1, 0.24)], VIOLET)
    # head + hat (tipped forward when running)
    g.ell(0.04, -0.2, 0.1, 0.1, SKIN_JUNE)
    g.ell(-0.04, -0.2, 0.07, 0.1, HAIR_B, outline=False)
    g.dot(0.09, -0.21, 0.016, INK)
    g.shape([(-0.1, -0.27), (0.0, -0.52), (0.07, -0.47), (0.16, -0.27)], VIOLET)
    g.shape([(-0.08, -0.32), (0.13, -0.32), (0.15, -0.27), (-0.1, -0.27)], CANDY, ow=0.6)
    g.ell(0.03, -0.27, 0.19, 0.04, VIOLET)
    return sp


def pointer_hand(d=170, alpha=255):
    sp = Sprite(d, d, 12)
    N.g_pointer(G(sp, d / 2, d / 2, d), alpha)
    return sp


def ring_sprite(d, col=ORANGE, w=9):
    sp = Sprite(d, d, 10)
    sp.stitches(U.circle_path(d / 2, d / 2, d / 2 - w, 2), col, 14, 9, w, shadow=False)
    return Cut(sp)


def puff_sprite(d=120):
    sp = Sprite(d, d, 8)
    for a in range(0, 360, 45):
        r = math.radians(a)
        x, y = d / 2 + d * 0.34 * math.cos(r), d / 2 + d * 0.34 * math.sin(r)
        sp.ellipse((x - d * 0.09, y - d * 0.09, x + d * 0.09, y + d * 0.09), fill=BONE, outline=INK, width=2)
    return Cut(sp)


# ----------------------------------------------------------------------------------------------- speech bubble

BUB = (440, 590, 2092, 1100)          # outer bubble (bone paper) in design coords
STG = (480, 630, 2052, 990)          # inner stage
NH = 310                              # vignettes are authored on a nominal stage NW x NH, then scaled to STG
SS_ = (STG[3] - STG[1]) / NH
NW = round((STG[2] - STG[0]) / SS_)
SL = (0, 0, NW, NH)                   # stage-local rectangle used by the vignette code


def stage_mask():
    m = Image.new("L", (round((STG[2] - STG[0]) * K), round((STG[3] - STG[1]) * K)), 0)
    ImageDraw.Draw(m).rounded_rectangle((2, 2, m.width - 3, m.height - 3), radius=12, fill=255)
    return m


def stage_layer():
    return Image.new("RGBA", (round(NW * K), round(NH * K)), (0, 0, 0, 0))


def stage_put(c, st, mask):
    im = st.resize(mask.size, Image.BICUBIC)
    im.putalpha(Image.fromarray((np.asarray(im.split()[3], np.float32) * np.asarray(mask, np.float32) / 255)
                                .astype(np.uint8)))
    c.alpha_composite(im, (round(STG[0] * K), round(STG[1] * K)))


def bubble_base(bg, speaker, tail_to, text, fill_col):
    """Static part of the animated speech bubble: paper bubble, tail to the speaker, dark stage, text line."""
    c = bg.copy()
    x0, y0, x1, y1 = BUB
    if not text:  # pictures-only reading mode: the bubble ends just under the stage
        y1 = STG[3] + 40
    bw, bh = x1 - x0, y1 - y0
    card = paper_card(bw, bh, 211, BONE, INK, fill_col, 40, 3.0, 3.4, 14, 11, 8)
    # stage: a felt panel inset in the bubble (clean background for the puppets)
    sx0, sy0, sx1, sy1 = [v - o for v, o in zip(STG, (x0, y0, x0, y0))]
    pts = deckle(rrect_path(sx0, sy0, sx1, sy1, 26, 3), 1.2, 5, 0.1, 0.4)
    card.fill(pts, STAGE, "felt", 9, fuzz=False)
    card.line(pts, INK, 3.0, closed=True)
    if text:
        card.text(bw / 2 + 60, (sy1 + bh) / 2 + 4, text, F_BOLD, 14 * U.PT, GRAPHITE, "mm")
    # tail up to the speaker in the world
    tx, ty = tail_to
    bx = clamp(tx, x0 + 260, x1 - 260)
    tri = [(bx - 56, y0 + 30), (tx, ty), (bx + 56, y0 + 30)]
    lx, ly = min(p[0] for p in tri), min(p[1] for p in tri)
    rx, ry = max(p[0] for p in tri), max(p[1] for p in tri)
    tail = Sprite(rx - lx, ry - ly, 10)
    loc = [(p[0] - lx, p[1] - ly) for p in tri]
    tail.fill(loc, BONE, "paper", 3)
    tail.line(loc[:2], INK, 3.4)
    tail.line(loc[1:], INK, 3.4)
    U.place(c, tail, (lx + rx) / 2, (ly + ry) / 2, shadow=(4, 6), shadow_alpha=110)
    cx = (x0 + x1) / 2
    U.place(c, card, cx, (y0 + y1) / 2, shadow=(6, 9))
    # speaker medallion on the bubble's left edge (who is talking, for pre-readers)
    pd = N.portrait_disc(speaker, 190, fill_col)
    U.place(c, pd, x0 + 10, y1 - 100, angle=-4, shadow=(4, 7))
    # next disc + line pips (chrome stays icon-only)
    U.place(c, N.next_disc(120), x1 - 6, y1 - 20, shadow=(4, 6))
    N.skip_disc(c, W - U.SAFE_R - 84, 96)
    return half(c)


def babble_cut(k):
    sp = Sprite(90, 110, 8)
    for i, r in enumerate((18, 32, 46)):
        if i <= k:
            pts = [(6 + r * math.cos(a), 55 + r * math.sin(a)) for a in np.linspace(-0.6, 0.6, 10)]
            sp.line(pts, CANDY, 6)
    return Cut(sp)


# ----------------------------------------------------------------------------------------------- Bea vignette

def bea_vignette():
    bg = N.clean_dialogue_frame()
    U.gradient_scrim(bg, NIGHT, 0.0, 0.75, 560, H, axis="y")
    base = bubble_base(bg, "bea", (1268, 560), "I saw June's silver cape go in. Right there!", ORANGE)
    # static scene inside the stage: treehouse platform (left) -> plank bridge -> house wall with round window
    sc = Sprite(NW, NH, 0)
    g = G(sc, 0, 0, 1)
    sw, sh = sc.w, sc.h
    deck_y = 200
    # treehouse hut on its platform
    for x, y, rx, ry, col in ((150, 40, 170, 80, hexc("B4502A")), (60, 30, 80, 50, hexc("D9762E")),
                              (250, 20, 70, 44, hexc("D9762E"))):
        g.ell(x, y, rx, ry, col)
    g.shape([(70, deck_y), (70, 100), (150, 60), (230, 100), (230, deck_y)], PURPLE)
    g.rect(125, 120, 175, 165, WARM, r=6)
    g.rect(30, deck_y, 290, deck_y + 22, WOOD, r=4)
    g.shape([(140, deck_y + 22), (165, deck_y + 22), (172, sh + 4), (132, sh + 4)], WOOD_D)
    # rope-and-plank bridge (sags gently)
    bx0, bx1 = 290, NW - 330
    for i in range(12):
        u0, u1 = i / 12, (i + 0.8) / 12
        y0 = deck_y + 6 + 26 * math.sin(math.pi * u0)
        y1 = deck_y + 6 + 26 * math.sin(math.pi * u1)
        g.shape([(lerp(bx0, bx1, u0), y0), (lerp(bx0, bx1, u1), y1), (lerp(bx0, bx1, u1), y1 + 18),
                 (lerp(bx0, bx1, u0), y0 + 18)], WOOD)
    rope = [(lerp(bx0, bx1, i / 30), deck_y - 44 + 16 * math.sin(math.pi * i / 30)) for i in range(31)]
    sc.line(rope, WOOD_D, 5)
    for i in range(0, 31, 5):
        x, y = rope[i]
        sc.line([(x, y), (x, deck_y + 8 + 26 * math.sin(math.pi * i / 30))], WOOD_D, 3)
    # house wall with the round-topped window (the target)
    g.shape([(bx1, -4), (sw + 4, -4), (sw + 4, sh + 4), (bx1, sh + 4)], INDIGO)
    for y in range(30, sh, 46):
        sc.line([(bx1 + 4, y), (sw, y)], hexc("2C3550"), 3)
    wx, wy, ww, wh = bx1 + 120, 70, 120, 170   # window box (arch top)
    r_ = ww / 2
    arch = ([(wx, wy + wh), (wx, wy + r_)]
            + [(wx + r_ - r_ * math.cos(a), wy + r_ - r_ * math.sin(a)) for a in np.linspace(0, math.pi, 24)]
            + [(wx + ww, wy + wh)])
    g.shape(arch, hexc("2A2246"))
    g.rect(wx - 14, wy + wh, wx + ww + 14, wy + wh + 18, WOOD, r=4)
    scene = Cut(sc)
    win_c = (SL[0] + wx + ww / 2, SL[1] + wy + wh * 0.55)
    lit = Sprite(ww, wh, 0)
    lg = G(lit, 0, 0, 1)
    lg.shape([(p[0] - wx, p[1] - wy) for p in arch], WARM)
    lit.line([(ww / 2, 8), (ww / 2, wh)], INK, 4)
    lit.line([(0, wh * 0.52), (ww, wh * 0.52)], INK, 4)
    lit = Cut(lit)
    jface = Sprite(110, 110, 6)
    N.g_june(G(jface, 55, 55, 110))
    jface = Cut(jface)
    run = [june_puppet(p / 4) for p in range(4)]
    hand = Cut(pointer_hand(190), -90)
    ring = ring_sprite(300)
    puff = puff_sprite(150)
    bab = [babble_cut(k) for k in range(3)]
    mask = stage_mask()
    by_ = BUB[3] - 100

    # bridge route in design coords (June's feet on the planks)
    def bridge_y(x):
        u = clamp((x - (SL[0] + bx0)) / (bx1 - bx0))
        return SL[1] + deck_y + 6 + 26 * math.sin(math.pi * u) if 0 < u < 1 else SL[1] + deck_y
    T = 3.6
    frames = []
    n = int(T * FPS)
    sc_x, sc_y = (SL[0] + SL[2]) / 2, (SL[1] + SL[3]) / 2
    for f in range(n):
        t = f / FPS
        out = base.copy()
        c = stage_layer()
        scene.draw(c, sc_x, sc_y, shadow=False)
        # beat 1 (0.2-1.7 s): June scurries across the bridge
        u = ease(seg(t, 0.2, 1.7))
        if t < 1.7:
            x = lerp(SL[0] + 270, SL[0] + bx1 + 30, u)
            y = bridge_y(x) - 78 - 8 * abs(math.sin(t * 14))
            run[int(t * 12) % 4].draw(c, x, y, alpha=seg(t, 0.0, 0.25), scale=0.7 + 0.3 * back(seg(t, 0.0, 0.3)))
        # beat 2 (1.7-2.0 s): she hops up and pops into the window (puff, the window lights up with her inside)
        hop = seg(t, 1.7, 2.0)
        if 1.7 <= t < 2.0:
            x = lerp(SL[0] + bx1 + 30, win_c[0], hop)
            y = lerp(bridge_y(SL[0] + bx1 + 30) - 78, win_c[1], hop) - 90 * math.sin(math.pi * hop)
            run[1].draw(c, x, y, scale=1 - 0.55 * hop)
        if t >= 1.95:
            lit.draw(c, win_c[0], SL[1] + wy + wh / 2, shadow=False)
            jface.draw(c, win_c[0], win_c[1] + 8, scale=0.9, shadow=False)
        p = seg(t, 1.95, 2.35)
        if 0 < p < 1:
            puff.draw(c, win_c[0], win_c[1], scale=0.6 + 0.8 * p, alpha=1 - p, shadow=False)
        # beat 3 (2.2-3.6 s): Bea's pointing hand slides in, the orange key-clue ring pulses on the window
        hp = back(seg(t, 2.2, 2.6))
        if t >= 2.2:
            hx = lerp(SL[0] + 700, win_c[0] - 230, hp) + 10 * math.sin(max(0, t - 2.6) * 9) * (t > 2.6)
            hand.draw(c, hx, win_c[1] + 10)
        rp = seg(t, 2.5, 3.6)
        if rp > 0:
            pulse = 1.0 + 0.08 * math.sin((t - 2.5) * 2 * math.pi * 1.6)
            ring.draw(c, win_c[0], win_c[1] - 10, scale=back(min(1, rp * 4)) * pulse, shadow=False)
        # babble arcs beside the medallion while she "talks" (whole vignette)
        stage_put(out, c, mask)
        k = int(t * 8) % 4
        if k < 3:
            bab[k].draw(out, BUB[0] + 130, by_, shadow=False)
        frames.append(out)
    return frames


# ----------------------------------------------------------------------------------------------- Vale vignette

def vale_frame():
    c = N.frame("02_play_start_run60.jpg")
    c = N.inpaint_rows(c, (80, 515, 810, 690), feather=10)
    c = N.inpaint(c, (180, 700, 500, 1020), feather=10)
    c = N.inpaint(c, (1890, 840, 2460, 1150), feather=10)
    c = N.inpaint(c, (2160, 10, 2520, 180), feather=8)
    c = N.blur_world(c, 4.5)
    # frame the camera a little lower so Vale's bench sits above the bubble (shift the world up)
    up = 170
    sh = Image.new("RGBA", c.size, NIGHT)
    sh.paste(c.crop((0, up, W, H)), (0, 0))
    c = sh
    U.gradient_scrim(c, NIGHT, 0.0, 0.8, 560, H, axis="y")
    return c


def eye_look(g, look):
    """The eye pictogram with its pupil turned toward the door (right)."""
    up = [(-0.4 + 0.8 * i / 20, -0.26 * math.sin(math.pi * i / 20)) for i in range(21)]
    dn = [(0.4 - 0.8 * i / 20, 0.22 * math.sin(math.pi * i / 20)) for i in range(21)]
    g.shape(up + dn, BONE)
    g.circ(look, -0.01, 0.15, VIOLET)
    g.circ(look, -0.01, 0.07, INK, outline=False)
    g.dot(look + 0.05, -0.07, 0.035, BONE)
    for x in (-0.24, -0.08, 0.08, 0.24):
        y = -0.26 * math.sin(math.pi * (x + 0.4) / 0.8)
        g.line([(x, y), (x * 1.2, y - 0.1)], INK, 0.9)


def vale_vignette():
    bg = vale_frame()
    base = bubble_base(bg, "vale", (1842, 470), None, PURPLE)
    sw, sh = NW, NH
    DX = (NW - 1252) / 2
    sc = Sprite(sw, sh, 0)
    g = G(sc, DX, 0, 1)
    # one big subject: the front door in its wall, porch step below
    g.shape([(420, -4), (840, -4), (840, sh + 4), (420, sh + 4)], INDIGO)
    for y in range(30, sh, 46):
        sc.line([(DX + 424, y), (DX + 836, y)], hexc("2C3550"), 3)
    dg = G(sc, DX + 630, 150, 330)
    N.g_door(dg, "shut")
    g.rect(470, 282, 790, sh + 4, WOOD, r=4)
    # Vale's watching eye (left) and the clock (right) as two calm side subjects
    clock = Sprite(220, 220, 10)
    clock.ellipse((0, 0, 220, 220), fill=BONE, outline=INK, width=5)
    for i in range(12):
        a = i * math.pi / 6
        r0, r1 = (78, 96) if i % 3 == 0 else (86, 96)
        clock.line([(110 + r0 * math.cos(a), 110 + r0 * math.sin(a)), (110 + r1 * math.cos(a), 110 + r1 * math.sin(a))],
                   INK, 5 if i % 3 == 0 else 3)
    clock.ellipse((96, 96, 124, 124), fill=INK)
    scene = Cut(sc)
    clock = Cut(clock)
    eye_open = Sprite(230, 230, 12)
    eye_look(G(eye_open, 115, 115, 230 * 0.86), 0.13)
    eye_open = Cut(eye_open)
    eye_shut = Sprite(230, 230, 12)
    eg = G(eye_shut, 115, 115, 230 * 0.86)
    eg.line([(-0.4 + 0.8 * i / 20, 0.04 * math.sin(math.pi * i / 20)) for i in range(21)], INK, 1.6)
    eye_shut = Cut(eye_shut)
    hand = Sprite(20, 110, 6)
    hand.line([(10, 100), (10, 6)], ORANGE, 9)
    hand = Cut(hand)
    stamp = Sprite(200, 200, 10)
    N.g_not(G(stamp, 100, 100, 200))
    stamp = Cut(stamp)
    bab = [babble_cut(k) for k in range(3)]
    mask = stage_mask()
    by_ = STG[3] - 30
    T = 3.6
    n = int(T * FPS)
    frames = []
    sc_x, sc_y = (SL[0] + SL[2]) / 2, (SL[1] + SL[3]) / 2
    door_c = (SL[0] + 630 + (NW - 1252) / 2, SL[1] + 150)
    clk = (SL[0] + 1060 + (NW - 1252) / 2, SL[1] + 140)
    eye = (SL[0] + 200 + (NW - 1252) / 2, SL[1] + 150)
    for f in range(n):
        t = f / FPS
        out = base.copy()
        c = stage_layer()
        scene.draw(c, sc_x, sc_y, shadow=False)
        clock.draw(c, *clk)
        # clock hand ticks: one snap every 0.5 s for the first 2.5 s (time passes, door stays shut)
        ticks = min(5, int(t / 0.5)) + ease(seg(t % 0.5, 0.0, 0.12)) * (t < 2.5)
        ang = -ticks * 60
        th = math.radians(ticks * 60)
        hand.draw(c, clk[0] + 47 * math.sin(th), clk[1] - 47 * math.cos(th), angle=ang, shadow=False)
        # Vale's eye watches the door; one slow blink at 1.4 s
        blink = 1.3 < t < 1.5
        (eye_shut if blink else eye_open).draw(c, *eye, sx=1.0)
        # wiggle-stop: the 'not' stamp thumps onto the door and settles (never opened)
        sp = seg(t, 2.6, 3.4)
        if t >= 2.6:
            s = 1.6 - 0.6 * ease(min(1, sp * 3))
            stamp.draw(c, door_c[0] + 120, door_c[1] + 40, angle=wiggle(sp, 3, 14), scale=s,
                       alpha=min(1, sp * 4))
        stage_put(out, c, mask)
        k = int(t * 8) % 4
        if k < 3:
            bab[k].draw(out, BUB[0] + 130, by_, shadow=False)
        frames.append(out)
    return frames


# ----------------------------------------------------------------------------------------------- memory replay

def memory_replay():
    src = Image.open(os.path.join(N.ROOT, "docs", "evidence", "s000.4", "06_keyframe_view_run33.jpg")).convert("RGB")
    fw, fh = src.size  # 1278x590
    big = src.resize((fw * 2, fh * 2), Image.LANCZOS)  # zoom source
    # focus: treehouse balcony (left) to the house's left front window (design: frame coords x2)
    focus = (560, 300)
    zoom = 1.7
    # June's ghost route in frame coords: off the treehouse balcony, across the bridge line, into the window
    route = [(470, 318), (540, 326), (610, 334), (662, 344)]
    win = (662, 344)
    gj = [ghostify(june_puppet(p / 4, 210)) for p in range(4)]
    halo = soft_halo(260, hexc("EEF2FF"), 0.45)
    ring = ring_sprite(170, ORANGE, 7)
    skip = felt_disc(130, NIGHT, 230, BONE, 11, seed=111)
    icon_draw(skip, "skip", 65, 65, 64, BONE, 5)
    skip = Cut(skip)
    T = 4.4
    n = int(T * FPS)
    frames = []
    yy, xx = np.mgrid[0:OH, 0:OW]
    vig = np.clip((np.sqrt(((xx - OW / 2) / (OW * 0.62)) ** 2 + ((yy - OH / 2) / (OH * 0.62)) ** 2) - 0.55) * 2.2, 0, 1)
    cache = {}

    def view(z):
        key = round(z, 3)
        if key not in cache:
            cw, ch = fw / z, fh / z
            fx = lerp(fw / 2, focus[0], (z - 1) / (zoom - 1))
            fy = lerp(fh / 2, focus[1], (z - 1) / (zoom - 1))
            x0 = clamp(fx - cw / 2, 0, fw - cw)
            y0 = clamp(fy - ch / 2, 0, fh - ch)
            im = big.crop((int(x0 * 2), int(y0 * 2), int((x0 + cw) * 2), int((y0 + ch) * 2))).resize((OW, OH),
                                                                                                       Image.LANCZOS)
            cache[key] = (im.convert("RGBA"), x0, y0, cw, ch)
        return cache[key]

    def to_design(p, v):
        _, x0, y0, cw, ch = v
        return ((p[0] - x0) / cw * W, (p[1] - y0) / ch * H)

    tint_cache = {}
    for f in range(n):
        t = f / FPS
        # camera: glance in 0.2-0.7 s, hold, return 3.0-3.5 s
        zin, zout = ease(seg(t, 0.2, 0.55)), ease(seg(t, 3.05, 3.4))
        z = 1 + (zoom - 1) * (zin - zout)
        m = zin - zout   # memory amount (tint, bars)
        v = view(z)
        key = (round(z, 3), round(m, 2))
        if key not in tint_cache:
            im = v[0].copy()
            if m > 0:
                a = np.asarray(im.convert("RGB"), np.float32)
                lum = a.mean(2, keepdims=True)
                warm = np.concatenate([lum * 0.95 + 30, lum * 0.82 + 22, lum * 0.95 + 40], 2)  # lilac pencil-paper
                a = a * (1 - 0.55 * m) + warm * 0.55 * m
                a = a * (1 - (vig[..., None] * 0.55 * m))
                im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).convert("RGBA")
                bar = int(46 * m)
                d = ImageDraw.Draw(im)
                d.rectangle((0, 0, OW, bar), fill=INK)
                d.rectangle((0, OH - bar, OW, OH), fill=INK)
            tint_cache[key] = im
        c = tint_cache[key].copy()
        if m > 0.95:
            # ghost June: runs 0.8-2.2 s along the route, hops into the window; ring pulses 2.2-3.0 s
            u = seg(t, 0.8, 2.2)
            if 0.8 <= t < 2.25:
                p, _ = path_at(route, ease(u))
                dp = to_design(p, v)
                sc_ = 1 - 0.45 * seg(t, 1.9, 2.25)
                gy = dp[1] - 95 * sc_ - 8 * abs(math.sin(t * 14))
                halo.draw(c, dp[0], gy, scale=sc_, shadow=False)
                gj[int(t * 12) % 4].draw(c, dp[0], gy, scale=sc_, alpha=0.95, shadow=False)
            if t >= 2.15:
                rp = seg(t, 2.15, 3.0)
                dw = to_design(win, v)
                ring.draw(c, dw[0], dw[1], scale=back(min(1, rp * 4)) * (1 + 0.08 * math.sin(t * 10)), shadow=False)
        # skip disc + progress ring (tap anywhere skips)
        if m > 0.05:
            skip.draw(c, W - U.SAFE_R - 84, 150, alpha=m, shadow=False)
            pr = math.floor(seg(t, 0.2, 3.0) * 8) / 8  # stepped, so the GIF stores few changes
            d = ImageDraw.Draw(c)
            cx, cy, r = (W - U.SAFE_R - 84) * K, 150 * K, 40
            d.arc((cx - r, cy - r, cx + r, cy + r), -90, -90 + 360 * pr, fill=CANDY[:3] + (int(255 * m),), width=5)
        frames.append(c)
    # hold the normal view a little longer so the 'return' reads before the loop
    frames += [frames[-1]] * 6
    return frames


# ----------------------------------------------------------------------------------------------- notebook

def notebook():
    bg = N.inpaint(N.frame("15_notebook_run60.jpg"), (2080, 990, 2520, 1160), feather=8).convert("RGB")
    bg = bg.filter(ImageFilter.GaussianBlur(14)).convert("RGBA")
    U.gradient_scrim(bg, NIGHT, 0.55, 0.75, 0, H, axis="y")
    c = bg
    # one page, centred (carousel: other pages peek at the sides)
    for side in (-1, 1):
        peek = paper_card(380, 700, 300 + side, hexc("CFC3AA"), INK, PURPLE, 30, 3, 3, 14)
        U.place(c, peek, W / 2 + side * 1150, 560, angle=side * 2, shadow=(5, 8))
    page = paper_card(1500, 900, 321, BONE, INK, PURPLE, 34, 3.4, 3.4, 16)
    # question: June ? house (big, one row)
    U.place(c, page, W / 2, 540, angle=-0.4, shadow=(7, 10))
    badge = Sprite(90, 90, 4)
    badge.ellipse((0, 0, 90, 90), fill=ORANGE, outline=INK, width=4)
    badge.ellipse((37, 37, 53, 53), fill=INK)
    U.place(c, badge, W / 2 - 600, 210, shadow=(3, 4))
    for i, nm in enumerate(["june", "question", "house"]):
        U.place(c, N.chip(nm, 150, 30 + i * 7, bare=False), W / 2 - 430 + i * 175, 210, angle=(-1) ** i * 1.5,
                shadow=(3, 5))
    # answer: three panel slots; the comic animates when tapped
    PW, PH, GAP = 370, 330, 66
    px0 = W / 2 - (3 * PW + 2 * GAP) / 2
    py = 520
    panels = [N.comic(["treehouse"], PW, PH, 61), N.comic(["bridge"], PW, PH, 62), N.comic(["window"], PW, PH, 63)]
    for i, p in enumerate(panels):
        U.place(c, p, px0 + PW / 2 + i * (PW + GAP), py, shadow=(4, 6))
    arrows = Sprite(GAP, 40, 6)
    arrows.line([(6, 20), (GAP - 10, 20)], PURPLE, 7)
    arrows.line([(GAP - 26, 6), (GAP - 8, 20), (GAP - 26, 34)], PURPLE, 7)
    for i in range(2):
        U.place(c, arrows, px0 + PW + GAP / 2 + i * (PW + GAP), py, shadow=None)
    # clue slot (dashed) bottom-left where Bea's sticker lands; carousel dots
    slot = (W / 2 - 520, 860)
    ds = Sprite(300, 170, 10)
    ds.stitches(deckle(rrect_path(0, 0, 300, 170, 30, 3), 1.2, 4, 0.12, 0.5), GRAPHITE[:3] + (170,), 10, 8, 2.6,
                shadow=False)
    U.place(c, ds, *slot, shadow=None)
    for i in range(3):
        x = W / 2 - 40 + i * 40
        ImageDraw.Draw(c).ellipse((x - 11, 1035 - 11, x + 11, 1035 + 11), fill=PURPLE if i == 0 else None,
                                  outline=PURPLE, width=4)
    cl = felt_disc(130, NIGHT, 230, BONE, 11, seed=12)
    icon_draw(cl, "close", 65, 65, 60, BONE, 5.5)
    U.place(c, cl, W - U.SAFE_R - 84, 96, shadow=(3, 5))
    base = half(c)

    stick = Cut(N.sticker(["bea", "cape"], 135, 7))
    jr = [june_puppet(p / 4, 150) for p in range(4)]
    tap = Cut(pointer_hand(180, 225), -15)
    stamp = Cut(N.found_stamp(190))
    glow = Sprite(PW, PH, 4)
    glow.stitches(rrect_path(4, 4, PW - 4, PH - 4, 16, 2), ORANGE, 14, 9, 8, shadow=False)
    glow = Cut(glow)
    lit = glyph("june_window", int(PH * 0.84))
    T = 4.6
    n = int(T * FPS)
    frames = []
    pc = [(px0 + PW / 2 + i * (PW + GAP), py) for i in range(3)]
    for f in range(n):
        t = f / FPS
        c = base.copy()
        # 1) the found clue sticker flies in and wiggles into its slot (0.1-1.0 s)
        sp = seg(t, 0.1, 0.55)
        if t >= 0.1:
            x = lerp(W - 300, slot[0], ease(sp))
            y = lerp(-80, slot[1], ease(sp)) - 160 * math.sin(math.pi * sp)
            stick.draw(c, x, y, angle=wiggle(seg(t, 0.55, 1.1), 3, 12) + (1 - sp) * 40,
                       scale=1 + 0.25 * (1 - ease(sp)))
        # 2) ghost hand taps the answer strip (1.2-1.6 s)
        if 1.1 <= t < 1.75:
            tp = seg(t, 1.1, 1.75)
            press = math.sin(math.pi * seg(t, 1.35, 1.6))
            tap.draw(c, pc[1][0] + 40, pc[1][1] + 140 - 40 * (1 - ease(min(1, tp * 2))) + 14 * press,
                     scale=1 - 0.08 * press, alpha=min(1, tp * 4) * (1 - seg(t, 1.6, 1.75)))
        # 3) the comic plays: June runs panel to panel; the active panel gets the orange stitch (1.7-3.6 s)
        if 1.7 <= t < 3.4:
            u = seg(t, 1.7, 3.4)
            x = lerp(pc[0][0] - 60, pc[2][0], ease(u))
            i = min(2, int((x - px0) / (PW + GAP)))
            glow.draw(c, pc[i][0], pc[i][1], shadow=False)
            jr[int(t * 12) % 4].draw(c, x, py + 40 - 10 * abs(math.sin(t * 14)), scale=1 - 0.4 * seg(t, 3.1, 3.4))
        if t >= 3.3:
            glow.draw(c, pc[2][0], pc[2][1], shadow=False)
            lit.draw(c, pc[2][0], pc[2][1], scale=back(seg(t, 3.3, 3.55)) if t < 3.55 else 1.0, shadow=False)
        # 4) the green stamp thumps down (3.7 s)
        if t >= 3.7:
            s = seg(t, 3.7, 3.95)
            stamp.draw(c, W / 2 + 590, 200, scale=1.6 - 0.6 * ease(s), alpha=min(1, s * 3),
                       angle=wiggle(seg(t, 3.85, 4.4), 2, 8) - 8)
        frames.append(c)
    return frames


# ----------------------------------------------------------------------------------------------- hint tier 2

def hint():
    bg = N.frame("02_play_start_run60.jpg")
    bg = N.inpaint_rows(bg, (80, 515, 810, 690), feather=10)
    bg = N.inpaint_rows(bg, (1895, 1072, 2140, 1152), feather=6)
    goal = (600, 470)
    U.radial_soft(bg, goal[0], goal[1], 200, CANDY, 0.25)
    # radio disc lit (it was pressed) with 2 of 3 tier pips; the radio sits where the hint button lives
    rx, ry = U.NOTE_C[0] - 176, U.NOTE_C[1] + 4
    rd = felt_disc(140, ORANGE, 255, CANDY, 11, seed=82)
    icon_draw(rd, "radio", 70, 70, 76, INK, 6)
    U.place(bg, rd, rx, ry, shadow=(4, 6))
    for i in range(3):
        x = rx - 32 + i * 32
        ImageDraw.Draw(bg).ellipse((x - 10, ry + 88, x + 10, ry + 108),
                                   fill=CANDY if i < 2 else None, outline=CANDY, width=4)
    base = half(bg)
    path = [(1268, 760), (1180, 700), (1060, 650), (920, 610), (780, 575), (640, 540)]
    bhalo = soft_halo(200, CANDY, 0.55)
    bats = {}

    def bat(s, flap):
        key = (int(s), flap)
        if key not in bats:
            sp = Sprite(int(s * 1.2), int(s * 1.2), 8)
            U.paper_bat(sp, s * 0.6, s * 0.6, s, CANDY, flap=flap)
            bats[key] = Cut(sp)
        return bats[key]
    beacon = Cut(N.chip("treehouse", 170, 77, ring=ORANGE))
    NB = 9
    T = 4.0
    n = int(T * FPS)
    frames = []
    for f in range(n):
        t = f / FPS
        c = base.copy()
        # bats launch one by one from Pip (every 0.22 s), fly the route in 1.6 s, then fade at the goal
        for b in range(NB):
            t0 = 0.1 + b * 0.22
            u = (t - t0) / 1.6
            if not (0 <= u <= 1.25):
                continue
            (x, y), _ = path_at(path, ease(min(1, u)))
            persp = 0.5 + 0.5 * (y - 450) / 550
            s = max(56, 120 * persp)
            flap = (0.2, 0.0, -0.18)[int(t * 10 + b) % 3]
            a = 1.0 if u < 1 else 1 - (u - 1) / 0.25
            by = y - 30 * math.sin(math.pi * min(1, u))
            bhalo.draw(c, x, by, scale=s / 120, alpha=a, shadow=False)
            bat(round(s / 4) * 4, flap).draw(c, x, by, alpha=a * 0.97)
        # beacon pulses once the first bat arrives
        if t >= 1.6:
            p = seg(t, 1.6, 1.9)
            beacon.draw(c, goal[0], goal[1] - 80 + 6 * math.sin(t * 5), scale=back(p) * (1 + 0.04 * math.sin(t * 7)))
        frames.append(c)
    return frames


# ----------------------------------------------------------------------------------------------- output

def to_rgb(frame):
    return frame.convert("RGB") if frame.size == (OW, OH) else half(frame.convert("RGBA")).convert("RGB")


def save_gif(frames, path, colors=255):
    rgb = [to_rgb(f) for f in frames]
    # one shared palette from a montage of sample frames, no dithering: static pixels stay identical between
    # frames, so the GIF encoder only stores the changed rectangle
    picks = [rgb[i] for i in np.linspace(0, len(rgb) - 1, 6).astype(int)]
    mont = Image.new("RGB", (OW, OH * len(picks)))
    for i, p in enumerate(picks):
        mont.paste(p, (0, OH * i))
    pal = mont.quantize(colors, method=Image.Quantize.MEDIANCUT)
    q = [im.quantize(palette=pal, dither=Image.Dither.NONE) for im in rgb]
    q[0].save(path, save_all=True, append_images=q[1:], duration=int(1000 / FPS), loop=0, optimize=False,
              disposal=1)
    return rgb


def contact_sheet(rgb, path, title):
    idx = np.linspace(0, len(rgb) - 1, 6).astype(int)
    tw, th = 760, int(760 * OH / OW)
    pad, top = 24, 90
    sheet = Image.new("RGB", (3 * tw + 4 * pad, top + 2 * (th + 54) + pad), NIGHT[:3])
    d = ImageDraw.Draw(sheet)
    d.text((pad, 26), title, font=U.font(U.F_DISPLAY, 40), fill=CANDY[:3])
    f = U.font(F_BOLD, 26)
    for k, i in enumerate(idx):
        x = pad + (k % 3) * (tw + pad)
        y = top + (k // 3) * (th + 54)
        sheet.paste(rgb[i].resize((tw, th), Image.LANCZOS), (x, y))
        d.text((x + 4, y + th + 10), f"{k + 1}  ·  t = {i / FPS:.1f} s", font=f, fill=BONE[:3])
    sheet.save(path, quality=88)


JOBS = [
    ("nv2_bea_vignette", bea_vignette, "Bea speaks: animated speech bubble (loop)"),
    ("nv2_vale_vignette", vale_vignette, "Vale speaks: animated speech bubble (loop)"),
    ("nv2_memory_replay", memory_replay, "Memory replay: camera glance, June's ghost, return"),
    ("nv2_notebook", notebook, "Notebook page: sticker wiggles in, tapped comic plays"),
    ("nv2_hint", hint, "Hint tier 2: paper-bat trail to the goal"),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--copy", default=None)
    ap.add_argument("--only", default=None)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    for name, fn, title in JOBS:
        if a.only and a.only != name:
            continue
        frames = fn()
        gif = os.path.join(a.out, name + ".gif")
        rgb = save_gif(frames, gif, 128 if name == "nv2_memory_replay" else 255)
        sheet = os.path.join(a.out, name + "_sheet.jpg")
        contact_sheet(rgb, sheet, title)
        print(f"wrote {gif} ({len(frames)} frames, {os.path.getsize(gif) / 1e6:.2f} MB) + sheet")
        if a.copy:
            os.makedirs(a.copy, exist_ok=True)
            for p in (gif, sheet):
                shutil.copy(p, a.copy)


if __name__ == "__main__":
    main()
