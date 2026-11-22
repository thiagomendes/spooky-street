#!/usr/bin/env python3
"""Mystery-first redesign mocks for Spooky Street (docs/mystery-first.md).

The owner asked that the game stop explaining its own mystery. These mocks show the proposed replacements, drawn in
the same paper-puppet style as the non-verbal v2 mocks (helpers reused from nonverbal_v2_mock.py, nonverbal_mock.py
and ui_mock.py) over the CI frames:

    mf_bea_testimony      Bea's partial testimony: a silver flash up high, never June going in.
    mf_notebook_deduction stickers placed on a question; a wrong try wobbles back; the right set reveals the comic.
    mf_hint_tiers         tier 1 nudges toward an area (a breeze), tier 2 trails to the area. No answer.
    mf_deduction_help     stuck on a question: Pip muses, the missing clue's outline points to a place, not a pair.
    mf_clue_glint         diegetic attraction: the silver cloth flutters and glints on the treehouse ramp.

    python3 tools/ui_mock/mystery_first_mock.py [--out docs/evidence/s002/mystery_first] [--copy DIR] [--only NAME]

Design only: nothing here is read by the game.
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
import nonverbal_v2_mock as V  # noqa: E402
import ui_mock as U  # noqa: E402
from nonverbal_mock import G, INDIGO, WOOD  # noqa: E402
from nonverbal_v2_mock import (BUB, FPS, NH, NW, SL, Cut, back, bubble_base, contact_sheet, ease, half,  # noqa: E402
                               june_puppet, lerp, pointer_hand, puff_sprite, save_gif, seg, stage_layer, stage_mask,
                               stage_put, wiggle)
from ui_mock import (BONE, CANDY, F_BOLD, GRAPHITE, H, INK, NIGHT, ORANGE, PURPLE, SILVER, Sprite, W,  # noqa: E402
                     deckle, felt_disc, hexc, icon_draw, paper_card, rrect_path)

OUT = os.path.join(N.FRAMES, "mystery_first")
GREEN = hexc("8DBF3F")


def chip_cut(name, d=150, seed=3, ring=None):
    return Cut(N.chip(name, d, seed, ring=ring))


def glyph_cut(name, d):
    return V.glyph(name, d)


def text_sprite(text, px, col=GRAPHITE, w=None, font=F_BOLD):
    f = U.font(font, int(px))
    tw = int(f.getlength(text)) + 20 if w is None else w
    sp = Sprite(tw, int(px * 1.5), 4)
    sp.text(tw / 2, px * 0.78, text, font, px, col, "mm")
    return sp


def caption_strip(c, text, badge, y=1040, w=1240):
    """The in-play caption strip (night felt, bone words) with the hint tier's small picture badge."""
    h = 120
    card = paper_card(w, h, 501, NIGHT, INK, CANDY, 30, 2.4, 3.0, 12, 9, 7, kind="felt")
    card.text(w / 2 + 40, h / 2 + 2, text, F_BOLD, 13 * U.PT, BONE, "mm")
    U.place(c, card, W / 2, y, shadow=(4, 6))
    bd = felt_disc(110, PURPLE, 255, CANDY, 9, seed=77)
    if badge == "bat":
        U.paper_bat(bd, 55, 55, 66, CANDY)
    else:
        G(bd, 55, 55, 70)
        N.GLYPHS[badge](G(bd, 55, 55, 70))
    U.place(c, bd, W / 2 - w / 2 + 40, y, shadow=(3, 5))


def radio(c, pips, at=None):
    rx, ry = at or (U.NOTE_C[0] - 176, U.NOTE_C[1] + 4)
    rd = felt_disc(140, ORANGE, 255, CANDY, 11, seed=82)
    icon_draw(rd, "radio", 70, 70, 76, INK, 6)
    U.place(c, rd, rx, ry, shadow=(4, 6))
    for i in range(3):
        x = rx - 32 + i * 32
        ImageDraw.Draw(c).ellipse((x - 10, ry + 88, x + 10, ry + 108), fill=CANDY if i < pips else None,
                                  outline=CANDY, width=4)


# ----------------------------------------------------------------------------------------------- Bea testimony

def bea_testimony():
    """Bea saw a piece: a silver flash up high by the side of the house. No June, no window, no pointing ring."""
    bg = N.clean_dialogue_frame()
    U.gradient_scrim(bg, NIGHT, 0.0, 0.75, 560, H, axis="y")
    base = bubble_base(bg, "bea", (1268, 560), "Something silver flashed up high, by the side of the house!", ORANGE)
    sc = Sprite(NW, NH, 0)
    g = G(sc, 0, 0, 1)
    sw, sh = sc.w, sc.h
    # the maple's autumn crown at the left (where the side yard is), the house side at the right
    g.shape([(205, 140), (232, 140), (246, sh + 4), (190, sh + 4)], WOOD)
    for x, y, rx, ry, col in ((215, 70, 150, 80, hexc("B4502A")), (120, 120, 90, 60, hexc("D9762E")),
                              (300, 110, 95, 62, hexc("D9762E")), (220, 30, 90, 50, hexc("E89A45"))):
        g.ell(x, y, rx, ry, col)
    # the blue house's side: a gable with the round attic window under the roof
    hx = sw - 600
    ax = hx + 300
    g.shape([(hx + 20, 150), (ax, 18), (sw - 20, 150), (sw - 20, sh + 4), (hx + 20, sh + 4)], INDIGO)
    for y in range(180, sh, 40):
        sc.line([(hx + 26, y), (sw - 26, y)], hexc("2C3550"), 3)
    sc.line([(hx - 10, 168), (ax, 4), (sw + 10, 168)], PURPLE, 30)
    sc.line([(hx - 10, 168), (ax, 4), (sw + 10, 168)], INK, 3)
    # the round window, dark (nobody is shown inside: Bea did not see that)
    wx, wy, wr = ax, 118, 52
    g.ell(wx, wy, wr, wr, hexc("2A2246"))
    sc.ellipse((wx - wr, wy - wr, wx + wr, wy + wr), outline=WOOD, width=10)
    sc.line([(wx - wr, wy), (wx + wr, wy)], WOOD, 6)
    sc.line([(wx, wy - wr), (wx, wy + wr)], WOOD, 6)
    # the hedge across the bottom: it hides everything low
    for i in range(10):
        g.ell(60 + i * 150, sh - 20, 105, 70, hexc("2F5A3A"))
    for i in range(10):
        g.ell(135 + i * 150, sh + 10, 95, 50, hexc("3A6B45"))
    scene = Cut(sc)
    # the flash: a silver streak with a candy sparkle head (what Bea perceived)
    fl = Sprite(460, 110, 10)
    fl.poly([(80, 56), (360, 42), (430, 54), (360, 66)], SILVER)
    fl.line([(140, 55), (420, 53)], BONE, 6)
    flash = Cut(fl)
    spark = glyph_cut("star", 150)
    eye = glyph_cut("eye", 200)
    q = glyph_cut("question", 180)
    mask = stage_mask()
    bab = [V.babble_cut(k) for k in range(3)]
    T = 4.0
    frames = []
    sx0, sy0 = SL[0], SL[1]
    for f in range(int(T * FPS)):
        t = f / FPS
        out = base.copy()
        c = stage_layer()
        scene.draw(c, (SL[0] + SL[2]) / 2, (SL[1] + SL[3]) / 2, shadow=False)
        # beat 1 (0.3-1.1 s): the flash streaks high over the hedge toward the house's side, then is gone
        u = seg(t, 0.3, 1.1)
        if 0 < u < 1:
            x = lerp(sx0 + 430, sx0 + hx + 150, ease(u))
            y = lerp(sy0 + 140, sy0 + 70, ease(u)) - 40 * math.sin(math.pi * u)
            fa = min(1, u * 5) * (1 - seg(u, 0.85, 1))
            flash.draw(c, x - 170, y + 4, alpha=fa, shadow=False)
            spark.draw(c, x + 30, y, scale=0.8 + 0.3 * math.sin(t * 30), alpha=fa, shadow=False)
        # beat 2 (1.1-1.8 s): a last twinkle where it vanished, behind the roof edge
        v = seg(t, 1.1, 1.8)
        if 0 < v < 1:
            spark.draw(c, sx0 + hx + 150, sy0 + 70, scale=0.9 * math.sin(math.pi * v), alpha=1 - v * 0.5,
                       shadow=False)
        # beat 3 (2.0-4.0 s): Bea's eye wide, a question pops: she is not sure what it was
        if t >= 2.0:
            e = back(seg(t, 2.0, 2.3))
            eye.draw(c, sx0 + 660, sy0 + 120, scale=e)
            qa = back(seg(t, 2.45, 2.75))
            if qa > 0:
                q.draw(c, sx0 + 840, sy0 + 80 + 6 * math.sin(t * 6), scale=qa, angle=8 * math.sin(t * 3))
        stage_put(out, c, mask)
        k = int(t * 8) % 4
        if k < 3 and t < 2.4:
            bab[k].draw(out, BUB[0] + 130, BUB[3] - 100, shadow=False)
        frames.append(out)
    return frames


# ----------------------------------------------------------------------------------------------- notebook deduction

PAGE_C = (W / 2, 560)
SLOT_W, SLOT_H = 330, 230
SLOT_Y = 470
TRAY_Y = 860


def notebook_page(question_chips, question_text, n_slots, pips=0, page_seed=321):
    bg = N.inpaint(N.frame("15_notebook_run60.jpg"), (2080, 990, 2520, 1160), feather=8).convert("RGB")
    bg = bg.filter(ImageFilter.GaussianBlur(14)).convert("RGBA")
    U.gradient_scrim(bg, NIGHT, 0.55, 0.75, 0, H, axis="y")
    c = bg
    page = paper_card(1760, 1000, page_seed, BONE, INK, PURPLE, 34, 3.4, 3.4, 16)
    U.place(c, page, PAGE_C[0], PAGE_C[1], angle=-0.3, shadow=(7, 10))
    x0 = PAGE_C[0] - 800
    badge = Sprite(80, 80, 4)
    badge.ellipse((0, 0, 80, 80), fill=ORANGE, outline=INK, width=4)
    badge.ellipse((32, 32, 48, 48), fill=INK)
    U.place(c, badge, x0 + 30, 150, shadow=(3, 4))
    for i, nm in enumerate(question_chips):
        U.place(c, N.chip(nm, 130, 30 + i * 7), x0 + 160 + i * 150, 150, angle=(-1) ** i * 1.5, shadow=(3, 5))
    U.place(c, text_sprite(question_text, 19 * U.PT), x0 + 160 + len(question_chips) * 150 + 420, 150,
            shadow=None)
    # the clue tray: a felt strip along the page foot, shared by every question
    tray = paper_card(1600, 230, 404, hexc("3A3270"), INK, CANDY, 30, 2.4, 3.0, 12, 9, 7, kind="felt")
    U.place(c, tray, PAGE_C[0], TRAY_Y, shadow=(3, 5))
    # carousel pips and the close disc
    for i in range(3):
        x = W / 2 - 40 + i * 40
        ImageDraw.Draw(c).ellipse((x - 11, 1105 - 11, x + 11, 1105 + 11), fill=PURPLE if i == pips else None,
                                  outline=PURPLE, width=4)
    cl = felt_disc(130, NIGHT, 230, BONE, 11, seed=12)
    icon_draw(cl, "close", 65, 65, 60, BONE, 5.5)
    U.place(c, cl, W - U.SAFE_R - 84, 96, shadow=(3, 5))
    return c


def slot_layer(n):
    """The empty slots, the same for every question: nothing says which clue goes where (drawn per frame)."""
    lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    xs = slot_xs(n)
    for i, x in enumerate(xs):
        ds = Sprite(SLOT_W, SLOT_H, 10)
        ds.stitches(deckle(rrect_path(0, 0, SLOT_W, SLOT_H, 34, 3), 1.2, 4 + i, 0.12, 0.5),
                    GRAPHITE[:3] + (170,), 12, 9, 3.0, shadow=False)
        U.place(lay, ds, x, SLOT_Y, shadow=None)
        if i < n - 1:
            plus = Sprite(60, 60, 4)
            plus.line([(30, 8), (30, 52)], PURPLE, 7)
            plus.line([(8, 30), (52, 30)], PURPLE, 7)
            U.place(lay, plus, (x + xs[i + 1]) / 2, SLOT_Y, shadow=None)
    cut = Cut.__new__(Cut)
    cut.im = half(lay)
    return cut


def slot_xs(n):
    gap = 110
    total = n * SLOT_W + (n - 1) * gap
    return [PAGE_C[0] - total / 2 + SLOT_W / 2 + i * (SLOT_W + gap) for i in range(n)]


def _blit(c, im, a):
    if a < 1:
        im = im.copy()
        im.putalpha(im.split()[3].point(lambda v: int(v * a)))
    c.alpha_composite(im)


def thought(names, d=120):
    """Pip's thought cloud: Pip's face and 1-2 pictures inside a bone-paper cloud."""
    w = d * (len(names) + 1) + 60
    h = d + 70
    sp = Sprite(w, h + 60, 12)
    for (cx, cy, r) in ((w * 0.22, h * 0.5, h * 0.42), (w * 0.5, h * 0.42, h * 0.48), (w * 0.78, h * 0.5, h * 0.42)):
        sp.ellipse((cx - r, cy - r, cx + r, cy + r), fill=BONE, outline=INK, width=3)
    sp.ellipse((w * 0.2, h * 0.18, w * 0.8, h * 0.86), fill=BONE)
    for k, (cx, cy, r) in enumerate(((w * 0.18, h + 18, 16), (w * 0.1, h + 46, 10))):
        sp.ellipse((cx - r, cy - r, cx + r, cy + r), fill=BONE, outline=INK, width=3)
    for i, n in enumerate(["pip"] + names):
        N.GLYPHS[n](G(sp, 30 + d / 2 + i * d, h / 2, d * 0.84))
    return Cut(sp)


def notebook_deduction():
    base = half(notebook_page(["june", "question", "house"], "How did June get in?", 3))
    xs = slot_xs(3)
    slots = slot_layer(3)
    specs = [("vale", ["vale", "door_shut"]), ("bea", ["bea", "star"]), ("cloth", ["cloth"]),
             ("sill", ["window", "cloth"]), ("streamer", ["bike", "streamer"])]
    stickers = {k: Cut(N.sticker(n, 120, 7 + i)) for i, (k, n) in enumerate(specs)}
    widths = {k: 120 * len(n) + 26 for k, n in specs}
    tot = sum(widths.values()) + 4 * 70
    tray = {}
    x = PAGE_C[0] - tot / 2
    for k, _ in specs:
        tray[k] = (x + widths[k] / 2, TRAY_Y)
        x += widths[k] + 70
    hand = Cut(pointer_hand(170, 235), -20)
    think = thought(["emo_think", "question"])
    puff = puff_sprite(220)
    stamp = Cut(N.found_stamp(190))
    glow = Sprite(SLOT_W, SLOT_H, 6)
    glow.stitches(rrect_path(4, 4, SLOT_W - 4, SLOT_H - 4, 30, 2), ORANGE, 14, 9, 9, shadow=False)
    glow = Cut(glow)
    PW, PH, GAP = 360, 300, 70
    px0 = PAGE_C[0] - (3 * PW + 2 * GAP) / 2
    panels = [Cut(N.comic([n], PW, PH, 61 + i)) for i, n in enumerate(["treehouse", "bridge", "window"])]
    pcs = [(px0 + PW / 2 + i * (PW + GAP), SLOT_Y) for i in range(3)]
    jr = [june_puppet(p / 4, 150) for p in range(4)]
    lit = glyph_cut("june_window", int(PH * 0.8))

    # (sticker, slot, t_start): a drag takes 0.75 s
    try1 = [("vale", 0, 0.4), ("streamer", 1, 1.25), ("bea", 2, 2.1)]
    try2 = [("vale", 0, 4.9), ("bea", 1, 5.75), ("cloth", 2, 6.6)]
    CHECK1, BACK1 = 2.95, 3.25
    CHECK2 = 7.45
    REVEAL = 8.0
    T = 11.2
    frames = []
    for f in range(int(T * FPS)):
        t = f / FPS
        c = base.copy()
        sa = 1 - seg(t, REVEAL - 0.2, REVEAL + 0.1)
        if sa > 0:
            _blit(c, slots.im, sa)
        pos = dict(tray)
        ang = {}
        scl = {}
        hand_at = None
        for moves, t_back in ((try1, BACK1), (try2, None)):
            for k, slot, t0 in moves:
                d = seg(t, t0, t0 + 0.75)
                if t < t0:
                    continue
                a, b = tray[k], (xs[slot], SLOT_Y)
                if t_back is not None and t >= t_back:
                    # wobble back to the tray (gentle, no buzzer)
                    r = seg(t, t_back + slot * 0.08, t_back + slot * 0.08 + 0.55)
                    p = (lerp(b[0], a[0], ease(r)), lerp(b[1], a[1], ease(r)) - 90 * math.sin(math.pi * r))
                    pos[k] = p
                    ang[k] = wiggle(seg(t, t_back + 0.5 + slot * 0.08, t_back + 1.2 + slot * 0.08), 3, 10)
                    continue
                if t_back is None and k not in ("vale", "bea", "cloth"):
                    continue
                e = ease(d)
                pos[k] = (lerp(a[0], b[0], e), lerp(a[1], b[1], e) - 60 * math.sin(math.pi * d))
                scl[k] = 1 + 0.15 * math.sin(math.pi * d)
                if d < 1:
                    hand_at = pos[k]
                else:
                    ang[k] = wiggle(seg(t, t0 + 0.75, t0 + 1.2), 2, 6)
        # wrong check: the filled slots give a little shake, then everything wobbles home
        if CHECK1 <= t < BACK1:
            sh = 10 * math.sin((t - CHECK1) * 60) * (1 - seg(t, CHECK1, BACK1))
            for k, slot, _ in try1:
                pos[k] = (pos[k][0] + sh, pos[k][1])
        # Pip thinks (no words, no answer): a thinking face and a question
        if BACK1 + 0.2 <= t < 4.8:
            p = back(seg(t, BACK1 + 0.2, BACK1 + 0.5)) * (1 - seg(t, 4.5, 4.8))
            think.draw(c, PAGE_C[0] + 690, 330, scale=p)
        revealed = t >= REVEAL
        # glow and press on the right set, then the slots give way to the comic
        if CHECK2 <= t < REVEAL + 0.2:
            for slot in range(3):
                glow.draw(c, xs[slot], SLOT_Y, scale=1 + 0.04 * math.sin(t * 12), shadow=False,
                          alpha=1 - seg(t, REVEAL - 0.1, REVEAL + 0.2))
        for k in pos:
            if revealed and k in ("vale", "bea", "cloth") and t >= REVEAL:
                # the three stickers fly down into the tray row as "used" (they stay the child's own)
                r = seg(t, REVEAL, REVEAL + 0.5)
                slot = {"vale": 0, "bea": 1, "cloth": 2}[k]
                p0 = (xs[slot], SLOT_Y)
                p = (lerp(p0[0], tray[k][0], ease(r)), lerp(p0[1], tray[k][1], ease(r)))
                stickers[k].draw(c, *p, scale=1.0)
                continue
            press = 1.0
            if CHECK2 <= t < REVEAL and k in ("vale", "bea", "cloth"):
                press = 1 - 0.08 * math.sin(math.pi * seg(t, CHECK2, CHECK2 + 0.3))
            stickers[k].draw(c, pos[k][0], pos[k][1], angle=ang.get(k, 0), scale=scl.get(k, 1) * press)
        if REVEAL - 0.2 <= t < REVEAL + 0.4:
            p = seg(t, REVEAL - 0.2, REVEAL + 0.4)
            puff.draw(c, PAGE_C[0], SLOT_Y, scale=0.8 + 1.4 * p, alpha=1 - p, shadow=False)
        if revealed:
            for i, (px, py) in enumerate(pcs):
                pp = back(seg(t, REVEAL + 0.15 + i * 0.12, REVEAL + 0.45 + i * 0.12))
                panels[i].draw(c, px, py, scale=pp)
            u = seg(t, REVEAL + 0.8, REVEAL + 2.3)
            if 0 < u < 1:
                x = lerp(pcs[0][0] - 60, pcs[2][0], ease(u))
                jr[int(t * 12) % 4].draw(c, x, SLOT_Y + 40 - 10 * abs(math.sin(t * 14)),
                                         scale=1 - 0.4 * seg(u, 0.85, 1))
            if t >= REVEAL + 2.2:
                lit.draw(c, pcs[2][0], pcs[2][1], scale=back(seg(t, REVEAL + 2.2, REVEAL + 2.45)), shadow=False)
            if t >= REVEAL + 2.6:
                s = seg(t, REVEAL + 2.6, REVEAL + 2.85)
                stamp.draw(c, PAGE_C[0] + 700, 160, scale=1.6 - 0.6 * ease(s), alpha=min(1, s * 3), angle=-8)
        if hand_at is not None:
            hand.draw(c, hand_at[0] + 60, hand_at[1] + 90)
        frames.append(c)
    return frames


# ----------------------------------------------------------------------------------------------- deduction help

def deduction_help():
    """Stuck on "Where are the knocks coming from?": Pip muses (reframes), and the tray shows that one clue is still
    out there, with the place to look (the hall), never which clues pair up."""
    pg = notebook_page(["knock3", "question", "house"], "Where are the knocks from?", 2, pips=1, page_seed=322)
    RADIO = (2330, 960)
    radio(pg, 0, RADIO)
    base = half(pg)
    xs = slot_xs(2)
    _blit(base, slot_layer(2).im, 1.0)
    specs = [("hum", ["floor_hum"], True), ("vale", ["vale", "door_shut"], True), ("streamer", ["bike", "streamer"],
                                                                                    True),
             ("cloth", ["cloth"], True), ("missing", ["question"], False)]
    cuts = {k: Cut(N.sticker(n, 120, 11 + i, found=fnd)) for i, (k, n, fnd) in enumerate(specs)}
    ms = Sprite(146, 144, 14)
    mp = deckle(rrect_path(0, 0, 146, 144, 28, 3), 1.4, 9, 0.12, 0.5)
    ms.stitches(mp, BONE[:3] + (200,), 10, 8, 3.0, shadow=False)
    ms.text(73, 76, "?", U.F_DISPLAY, 70, BONE[:3] + (200,), "mm")
    cuts["missing"] = Cut(ms)
    widths = {k: 120 * len(n) + 26 for k, n, _ in specs}
    tot = sum(widths.values()) + 4 * 70
    tray = {}
    x = PAGE_C[0] - tot / 2
    for k, _, _ in specs:
        tray[k] = (x + widths[k] / 2, TRAY_Y)
        x += widths[k] + 70
    hand = Cut(pointer_hand(170, 235), -20)
    think = thought(["ear", "eye"])
    place = Cut(N.chip("house_cut", 130, 41, ring=ORANGE))
    ring = V.ring_sprite(300)
    rx, ry = RADIO
    T = 7.0
    frames = []
    for f in range(int(T * FPS)):
        t = f / FPS
        c = base.copy()
        # 1) the child places the floor hum; one slot stays empty: nothing to try yet
        d = seg(t, 0.3, 1.05)
        hp = (lerp(tray["hum"][0], xs[0], ease(d)), lerp(TRAY_Y, SLOT_Y, ease(d)) - 60 * math.sin(math.pi * d))
        for k in tray:
            if k == "hum":
                continue
            a = 1.0
            if k == "missing" and t >= 3.2:
                pulse = 1 + 0.07 * math.sin((t - 3.2) * 7)
                cuts[k].draw(c, *tray[k], scale=back(seg(t, 3.2, 3.5)) * pulse if t < 3.5 else pulse)
                continue
            cuts[k].draw(c, *tray[k], alpha=a)
        cuts["hum"].draw(c, *hp, angle=wiggle(seg(t, 1.05, 1.5), 2, 6))
        # 2) the radio is pressed inside the notebook (1.7 s)
        if 1.5 <= t < 2.4:
            p = math.sin(math.pi * seg(t, 1.7, 2.1))
            hand.draw(c, rx + 40, ry + 80 - 30 * p, alpha=1 - seg(t, 2.1, 2.4))
        # 3) Pip muses: listen + look (reframes the question: where could I SEE it?)
        if t >= 2.0:
            p = back(seg(t, 2.0, 2.3))
            think.draw(c, PAGE_C[0] + 640, 340, scale=p)
        # 4) the missing clue's outline lights and carries the place badge: "still something to find in the hall"
        if t >= 3.4:
            ring.draw(c, *tray["missing"], scale=back(seg(t, 3.4, 3.7)) * 0.75, shadow=False)
        if t >= 3.9:
            place.draw(c, tray["missing"][0] + 90, TRAY_Y - 120 + 6 * math.sin(t * 5),
                       scale=back(seg(t, 3.9, 4.2)))
        frames.append(c)
    return frames


# ----------------------------------------------------------------------------------------------- hint tiers

def hint_tiers():
    """Tier 1: a breeze carries leaves toward the side yard (an area), Bea's head would turn there. Tier 2: the
    bat trail flies to the side-yard gap and circles the area. No object, no sparkle on a clue, no answer."""
    bg = N.frame("02_play_start_run60.jpg")
    bg = N.inpaint_rows(bg, (80, 515, 810, 690), feather=10)
    bg = N.inpaint_rows(bg, (1895, 1072, 2140, 1152), feather=6)
    area = (930, 740)
    b1, b2 = bg.copy(), bg.copy()
    radio(b1, 1)
    caption_strip(b1, "A breeze keeps tugging toward the side yard.", "wind")
    radio(b2, 2)
    caption_strip(b2, "Follow the bats to the side yard.", "bat")
    base1, base2 = half(b1), half(b2)
    halo = V.soft_halo(760, CANDY, 0.45)
    leaf_cols = [hexc("E07A2E"), hexc("B4502A"), hexc("D9A23A")]
    leaves = []
    for i in range(3):
        sp = Sprite(96, 64, 6)
        sp.poly([(6, 32), (36, 6), (90, 28), (38, 58)], leaf_cols[i])
        sp.line([(8, 32), (86, 28)], INK, 3)
        leaves.append(Cut(sp))
    rng = np.random.default_rng(4)
    leaf_paths = [((1700 + rng.uniform(-150, 250), 1000 + rng.uniform(-80, 40)),
                   (area[0] + rng.uniform(-120, 120), area[1] + rng.uniform(-60, 40)), rng.uniform(0, 1.6), i % 3)
                  for i in range(16)]
    path = [(1268, 800), (1180, 760), (1080, 730), (1000, 730), area]
    bats = {}

    def bat(s, flap):
        key = (int(s), flap)
        if key not in bats:
            sp = Sprite(int(s * 1.2), int(s * 1.2), 8)
            U.paper_bat(sp, s * 0.6, s * 0.6, s, CANDY, flap=flap)
            bats[key] = Cut(sp)
        return bats[key]
    T1, T = 4.6, 9.4
    frames = []
    for f in range(int(T * FPS)):
        t = f / FPS
        if t < T1:
            c = base1.copy()
            # broad, soft warm-up of the AREA (no object picked out)
            halo.draw(c, *area, alpha=math.sin(math.pi * seg(t, 0.4, T1)), shadow=False)
            for (a, b, t0, k) in leaf_paths:
                u = seg(t, 0.3 + t0, 2.3 + t0)
                if 0 < u < 1:
                    x = lerp(a[0], b[0], ease(u)) + 30 * math.sin(u * 9 + t0)
                    y = lerp(a[1], b[1], ease(u)) - 90 * math.sin(math.pi * u)
                    leaves[k].draw(c, x, y, angle=u * 540 + t0 * 90, alpha=1 - seg(u, 0.8, 1))
        else:
            c = base2.copy()
            tt = t - T1
            for b in range(9):
                t0 = 0.1 + b * 0.22
                u = (tt - t0) / 1.4
                if u < 0:
                    continue
                if u <= 1:
                    (x, y), _ = V.path_at(path, ease(u))
                else:
                    # circle the area loosely, then fade: "somewhere around here"
                    a = (u - 1) * 2.6 + b * 0.7
                    x, y = area[0] + 150 * math.cos(a), area[1] - 60 + 45 * math.sin(a)
                al = 1 - seg(u, 2.2, 2.6)
                if al <= 0:
                    continue
                persp = 0.5 + 0.5 * (y - 450) / 550
                s = max(56, 110 * persp)
                flap = (0.2, 0.0, -0.18)[int(t * 10 + b) % 3]
                bat(round(s / 4) * 4, flap).draw(c, x, y - 20 * math.sin(math.pi * min(1, u)), alpha=al)
        frames.append(c)
    return frames


# ----------------------------------------------------------------------------------------------- clue glint

def clue_glint():
    """Diegetic attraction: the silver cloth on the treehouse ramp flutters in the breeze and glints every few
    seconds. No UI arrow, no ring: the world itself is curious."""
    bg = N.frame("18_treehouse_route_run88.jpg").crop((240, 60, 1990, 869)).resize((W, H), Image.LANCZOS)
    spot = (828, 912)                                          # the cloth on the ramp's rail post
    base = half(bg)
    cloth_frames = []
    for k in range(6):
        sp = Sprite(130, 120, 8)
        fl = 10 * math.sin(k / 6 * 2 * math.pi)
        sp.poly([(20, 20), (64, 16), (110 + fl, 40 + fl / 2), (96 + fl, 70), (70, 60), (60, 96 + fl / 2), (26, 70)],
                SILVER)
        sp.line([(30, 30), (90 + fl, 46)], hexc("B9C3D3"), 2)
        sp.line([(20, 20), (64, 16)], hexc("7A5A3A"), 6)
        cloth_frames.append(Cut(sp))
    star = V.glyph("star", 150)
    halo = V.soft_halo(340, BONE, 0.85)
    T = 4.0
    frames = []
    for f in range(int(T * FPS)):
        t = f / FPS
        c = base.copy()
        cloth_frames[int(t * 9) % 6].draw(c, *spot, scale=0.9)
        for g0 in (0.5, 2.5):
            u = seg(t, g0, g0 + 0.8)
            if 0 < u < 1:
                a = math.sin(math.pi * u)
                halo.draw(c, spot[0] + 10, spot[1] - 6, alpha=a, scale=0.7, shadow=False)
                star.draw(c, spot[0] + 34, spot[1] - 24, scale=0.4 + 0.7 * a, angle=u * 60, alpha=a, shadow=False)
        frames.append(c)
    return frames


SHEET_TIMES = {
    "mf_notebook_deduction": [0.0, 2.9, 3.5, 4.3, 7.6, 11.1],
    "mf_hint_tiers": [0.8, 1.6, 2.6, 5.4, 6.6, 8.6],
    "mf_clue_glint": [0.0, 0.8, 1.6, 2.2, 2.9, 3.7],
}


def sheet_at(frames, times, path, title):
    """Contact sheet at chosen moments (the wrong try and the reveal are short beats)."""
    OW, OH = frames[0].size
    tw, th = 760, int(760 * OH / OW)
    pad, top = 24, 90
    sheet = Image.new("RGB", (3 * tw + 4 * pad, top + 2 * (th + 54) + pad), NIGHT[:3])
    d = ImageDraw.Draw(sheet)
    d.text((pad, 26), title, font=U.font(U.F_DISPLAY, 40), fill=CANDY[:3])
    f = U.font(F_BOLD, 26)
    for k, (im, tt) in enumerate(zip(frames, times)):
        x = pad + (k % 3) * (tw + pad)
        y = top + (k // 3) * (th + 54)
        sheet.paste(im.resize((tw, th), Image.LANCZOS), (x, y))
        d.text((x + 4, y + th + 10), f"{k + 1}  ·  t = {tt:.1f} s", font=f, fill=BONE[:3])
    sheet.save(path, quality=88)


JOBS = [
    ("mf_bea_testimony", bea_testimony, "Bea's testimony: only what she saw (a silver flash up high)"),
    ("mf_notebook_deduction", notebook_deduction, "Deduction: a wrong try wobbles back; the right set reveals"),
    ("mf_hint_tiers", hint_tiers, "Hints: tier 1 a breeze toward the area, tier 2 bats to the area. No answer."),
    ("mf_deduction_help", deduction_help, "Stuck on a question: Pip muses, the missing clue shows its place"),
    ("mf_clue_glint", clue_glint, "Diegetic attraction: the cloth flutters and glints, no UI arrow"),
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
        rgb = save_gif(frames, gif, 160 if name in ("mf_hint_tiers", "mf_clue_glint") else 255)
        sheet = os.path.join(a.out, name + "_sheet.jpg")
        if name in SHEET_TIMES:
            pick = [rgb[min(len(rgb) - 1, int(round(tt * FPS)))] for tt in SHEET_TIMES[name]]
            sheet_at(pick, SHEET_TIMES[name], sheet, title)
        else:
            contact_sheet(rgb, sheet, title)
        print(f"wrote {gif} ({len(frames)} frames, {os.path.getsize(gif) / 1e6:.2f} MB) + sheet")
        if a.copy:
            os.makedirs(a.copy, exist_ok=True)
            for p in (gif, sheet):
                shutil.copy(p, a.copy)


if __name__ == "__main__":
    main()
