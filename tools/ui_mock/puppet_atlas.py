#!/usr/bin/env python3
"""Bakes the PuppetAtlas for the animated non-verbal UX (docs/nonverbal-ux.md §9, §10, §12 step 9).

Every puppet part in `PuppetParts.All` (C#) gets one cell in a 2048x2048 felt-and-paper sprite sheet, drawn by the
same procedural helpers as the approved mocks (`nonverbal_mock.py` v1 pictograms, `nonverbal_v2_mock.py` puppets),
with a soft ink drop shadow baked in so the runtime needs one Image per part. The rect table is written as
engine-free C# (`Core/Nonverbal/PuppetAtlasLayout.cs`), so the EditMode core tests can check that the atlas holds
exactly the part IDs. Extra cells (`x.*`) hold the June run flipbook frames and small chrome pieces (bubble tail,
first-launch card art) that are not vignette parts.

    python3 tools/ui_mock/puppet_atlas.py [--preview PREVIEW.jpg]

Writes Assets/SpookyStreet/Content/Resources/UI/puppet_atlas.png and the C# layout. Review the preview before use.
"""
from __future__ import annotations

import argparse
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nonverbal_mock as N  # noqa: E402
import nonverbal_v2_mock as V  # noqa: E402
import ui_mock as U  # noqa: E402
from nonverbal_mock import G, GRAPHITE, INDIGO, WOOD, WOOD_D  # noqa: E402
from ui_mock import BONE, BONE_D, CANDY, INK, ORANGE, PURPLE, Sprite  # noqa: E402

ROOT = U.ROOT
ATLAS = 2048
CELL = 200          # one cell edge in pixels (cells sit on a 204 px pitch)
GAP = 4
PITCH = CELL + GAP
OUT_PNG = os.path.join(ROOT, "Assets", "SpookyStreet", "Content", "Resources", "UI", "puppet_atlas.png")
OUT_CS = os.path.join(ROOT, "Assets", "SpookyStreet", "Core", "Nonverbal", "PuppetAtlasLayout.cs")
PARTS_CS = os.path.join(ROOT, "Assets", "SpookyStreet", "Core", "Nonverbal", "PuppetParts.cs")

STAGE = V.STAGE
DEFAULT_HEIGHT = 0.85   # a part's cell height in stage heights at scale 1


def part_ids(split=False):
    """Reads PuppetParts.cs so the atlas follows the C# contract exactly (v1 names, then the v2 and v3 constants)."""
    src = open(PARTS_CS, encoding="utf-8").read()
    v1_block = src[src.index("private static readonly string[] V1"):src.index("};", src.index("string[] V1"))]
    v1 = ["pg." + n for n in re.findall(r'"([a-z0-9_]+)"', v1_block)]
    consts = dict(re.findall(r'public const string (\w+) = "(pg\.[a-z0-9_]+)";', src))

    def group(name):
        m = re.search(name + r" = \{([^}]*)\}", src)
        return [consts[n.strip()] for n in m.group(1).split(",") if n.strip()] if m else []

    v2, v3 = group("V2"), group("V3")
    return (v1 + v2, v3) if split else v1 + v2 + v3


# ----------------------------------------------------------------------------------------------- cell art

def glyph_cell(name, cols=1, rows=1, side=0.86):
    w, h = cols * CELL, rows * CELL
    sp = Sprite(w, h, 0)
    N.GLYPHS[name](G(sp, w / 2, h / 2, min(w, h) * side))
    return sp


def wide_bridge():
    w, h = 2 * CELL, CELL
    sp = Sprite(w, h, 0)
    g = G(sp, 0, 0, 1)
    g.ow = 3.0
    x0, x1, deck = 14, w - 14, 112
    for i in range(11):
        u0, u1 = i / 11, (i + 0.78) / 11
        y0 = deck + 22 * math.sin(math.pi * u0)
        y1 = deck + 22 * math.sin(math.pi * u1)
        g.shape([(V.lerp(x0, x1, u0), y0), (V.lerp(x0, x1, u1), y1), (V.lerp(x0, x1, u1), y1 + 20),
                 (V.lerp(x0, x1, u0), y0 + 20)], WOOD)
    rope = [(V.lerp(x0, x1, i / 30), deck - 46 + 14 * math.sin(math.pi * i / 30)) for i in range(31)]
    sp.line(rope, WOOD_D, 6)
    for i in range(0, 31, 5):
        x, y = rope[i]
        sp.line([(x, y), (x, deck + 6 + 22 * math.sin(math.pi * i / 30))], WOOD_D, 4)
    for x in (x0 + 2, x1 - 2):
        sp.line([(x, deck - 60), (x, deck + 40)], WOOD_D, 7)
    return sp


def memory_frame():
    """Pencil-cloud memory border (wide): a scalloped graphite outline round a faint bone wash."""
    w, h = 3 * CELL, CELL
    sp = Sprite(w, h, 0)
    cx, cy, rx, ry = w / 2, h / 2, w / 2 - 22, h / 2 - 22
    pts = []
    n = 26
    for i in range(n * 8 + 1):
        a = 2 * math.pi * i / (n * 8)
        bump = 1 + 0.06 * abs(math.sin(a * n / 2))
        pts.append((cx + rx * math.cos(a) * bump, cy + ry * math.sin(a) * bump))
    sp.fill(pts, BONE_D[:3] + (70,), "paper", 4)
    sp.line(pts, GRAPHITE, 3.4, closed=True)
    inner = [(cx + (rx - 12) * math.cos(a), cy + (ry - 12) * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 120)]
    sp.stitches(inner, GRAPHITE[:3] + (150,), 7, 9, 2.0, shadow=False)
    for k, (dx, dy, r) in enumerate(((22, 18, 9), (10, 6, 5))):
        sp.ellipse((dx - r, h - dy - r, dx + r, h - dy + r), fill=BONE_D, outline=GRAPHITE, width=2.4)
    return sp


def stage_panel():
    """The night-felt vignette stage (9-sliced at runtime; corners keep the deckle)."""
    w, h = 3 * CELL, CELL
    sp = Sprite(w, h, 0)
    pts = U.deckle(U.rrect_path(6, 6, w - 6, h - 6, 30, 3), 1.2, 5, 0.1, 0.4)
    sp.fill(pts, STAGE, "felt", 9)
    sp.stitches(U.rrect_path(18, 18, w - 18, h - 18, 20, 2), V.STAGE_L, 9, 7, 2.6, shadow=False)
    sp.line(pts, INK, 3.2, closed=True)
    return sp


def clock_face():
    sp = Sprite(CELL, CELL, 0)
    c, r = CELL / 2, CELL * 0.44
    sp.ellipse((c - r, c - r, c + r, c + r), fill=BONE, outline=INK, width=5)
    for i in range(12):
        a = i * math.pi / 6
        r0, r1 = (r * 0.72, r * 0.88) if i % 3 == 0 else (r * 0.8, r * 0.88)
        sp.line([(c + r0 * math.cos(a), c + r0 * math.sin(a)), (c + r1 * math.cos(a), c + r1 * math.sin(a))],
                INK, 5 if i % 3 == 0 else 3)
    return sp


def clock_hand():
    """The orange hand alone, pivoting on the cell centre (same cell size as the clock face)."""
    sp = Sprite(CELL, CELL, 0)
    c = CELL / 2
    sp.line([(c, c + 6), (c, c - CELL * 0.33)], INK, 13)
    sp.line([(c, c + 6), (c, c - CELL * 0.33)], ORANGE, 8)
    sp.ellipse((c - 9, c - 9, c + 9, c + 9), fill=INK)
    return sp


def eye_shut():
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.86)
    g.line([(-0.4 + 0.8 * i / 20, 0.04 * math.sin(math.pi * i / 20)) for i in range(21)], INK, 1.6)
    for x in (-0.24, -0.08, 0.08, 0.24):
        y = 0.04 * math.sin(math.pi * (x + 0.4) / 0.8)
        g.line([(x, y), (x * 1.1, y + 0.08)], INK, 0.9)
    return sp


def puff():
    sp = Sprite(CELL, CELL, 0)
    d = CELL
    for a in range(0, 360, 45):
        r = math.radians(a)
        x, y = d / 2 + d * 0.32 * math.cos(r), d / 2 + d * 0.32 * math.sin(r)
        sp.ellipse((x - d * 0.1, y - d * 0.1, x + d * 0.1, y + d * 0.1), fill=BONE, outline=INK, width=3)
    return sp


def key_ring():
    sp = Sprite(CELL, CELL, 0)
    sp.stitches(U.circle_path(CELL / 2, CELL / 2, CELL / 2 - 14, 2), ORANGE, 15, 9, 10, shadow=False)
    return sp


def june_run(phase):
    return V.june_puppet_sprite(phase, CELL, 0)


def pip_body():
    sp = Sprite(CELL, CELL, 0)
    N.g_pip_mini(G(sp, CELL / 2, CELL / 2, CELL * 0.92))
    return sp


def bea_arm():
    """Bea's pointing arm: an orange sleeve and a bone hand pointing right (toward the clue)."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.9)
    g.shape([(-0.5, -0.1), (-0.12, -0.12), (-0.1, 0.12), (-0.5, 0.14)], ORANGE)
    g.shape([(-0.16, -0.14), (-0.08, -0.15), (-0.06, 0.15), (-0.14, 0.16)], CANDY, ow=0.7)
    hand = [(-y + 0.08, x) for x, y in N.hand_pts(0.9)]
    g.shape(hand, N.SKIN_BEA)
    return sp


def pointer_right():
    """Bea's pointing hand, turned to point right (toward the clue on the stage)."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.86)
    hand = [(-y, x) for x, y in N.hand_pts()]
    g.shape(hand, BONE)
    g.line([(0.12, 0.05), (-0.02, 0.06)], INK, 0.6)
    g.line([(0.08, 0.15), (-0.04, 0.16)], INK, 0.6)
    return sp


def house_side():
    """The side of the blue house as a wall section: indigo siding under a purple eave (the window is its own part)."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, 0, 0, 1)
    g.ow = 3.0
    w, h = CELL, CELL
    g.shape([(30, 34), (w - 30, 34), (w - 30, h - 8), (30, h - 8)], INDIGO)
    for y in range(58, h - 12, 22):
        sp.line([(34, y), (w - 34, y)], U.hexc("2C3550"), 3)
    g.shape([(14, 40), (w / 2, 6), (w - 14, 40), (w - 14, 50), (w / 2, 18), (14, 50)], PURPLE)
    return sp


def june_ghost(phase):
    """Memory ghost frame: June's run silhouette as pale translucent paper with a soft candy rim (D23.7)."""
    im = V.june_puppet_sprite(phase, CELL, 0).render()
    al = im.split()[3]
    body = Image.new("RGBA", im.size, U.hexc("EEF2FF")[:3] + (0,))
    body.putalpha(al.point(lambda v: int(v * 0.62)))
    rim = al.filter(ImageFilter.MaxFilter(7))
    rim = Image.fromarray(np.clip(np.asarray(rim, np.int16) - np.asarray(al, np.int16), 0, 255).astype(np.uint8))
    rl = Image.new("RGBA", im.size, CANDY[:3] + (0,))
    rl.putalpha(rim.point(lambda v: int(v * 0.9)))
    return Image.alpha_composite(body, rl)


def paper_bat():
    """Hint trail bat: bone paper wings with an ink outline and a candy glow, so it reads against the night."""
    sp = Sprite(CELL, CELL, 0)
    c = CELL / 2
    wing = [(-0.46, -0.06), (-0.3, -0.2), (-0.16, -0.08), (-0.06, -0.16), (0.0, -0.06), (0.06, -0.16),
            (0.16, -0.08), (0.3, -0.2), (0.46, -0.06), (0.34, 0.02), (0.26, 0.12), (0.16, 0.04), (0.08, 0.12),
            (0.0, 0.06), (-0.08, 0.12), (-0.16, 0.04), (-0.26, 0.12), (-0.34, 0.02)]
    halo = Sprite(CELL, CELL, 0)
    gh = G(halo, c, c, CELL * 0.92)
    gh.shape(wing, CANDY[:3] + (120,), outline=False)
    g = G(sp, c, c, CELL * 0.86)
    g.shape(wing, BONE, kind="paper")
    g.dot(-0.03, -0.09, 0.02, INK)
    g.dot(0.03, -0.09, 0.02, INK)
    return Image.alpha_composite(halo.render().filter(ImageFilter.GaussianBlur(5)), sp.render())


def bubble_tail():
    """Bone-paper bubble tail pointing up; inked on its two slanted sides only (the card covers the base)."""
    sp = Sprite(CELL, CELL, 0)
    tri = [(18, CELL - 4), (CELL / 2, 10), (CELL - 18, CELL - 4)]
    sp.fill(tri, BONE, "paper", 3)
    sp.line(tri[:2], INK, 4.2)
    sp.line(tri[1:], INK, 4.2)
    return sp


def wave_hand():
    """An open waving palm (hello, come on): bone paper with five spread fingers and an orange felt cuff."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.9)
    palm = [(-0.2, 0.1), (-0.36, -0.12), (-0.33, -0.17), (-0.26, -0.15), (-0.15, -0.02), (-0.2, -0.36),
            (-0.15, -0.4), (-0.1, -0.37), (-0.04, -0.12), (-0.04, -0.44), (0.02, -0.47), (0.07, -0.43),
            (0.08, -0.13), (0.13, -0.41), (0.19, -0.42), (0.21, -0.37), (0.17, -0.08), (0.26, -0.3),
            (0.32, -0.3), (0.33, -0.24), (0.22, 0.08), (0.16, 0.22), (-0.14, 0.22)]
    g.shape(palm, BONE, kind="paper")
    for x0, y0, x1, y1 in ((-0.1, -0.02, -0.12, -0.12), (0.03, -0.03, 0.02, -0.14), (0.13, 0.0, 0.15, -0.1)):
        g.line([(x0, y0), (x1, y1)], INK, 0.5)
    g.shape([(-0.17, 0.2), (0.19, 0.2), (0.2, 0.4), (-0.16, 0.4)], ORANGE)
    g.line([(-0.16, 0.3), (0.19, 0.3)], CANDY, 0.7)
    return sp


def sill_snag():
    """The attic sill clue: the base of the round window, a wooden sill and a silver cloth tuft caught on a nail."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.9)
    arc = [(-0.3, -0.02)] + [(0.3 * math.cos(a), -0.02 - 0.3 * math.sin(a)) for a in np.linspace(math.pi, 0, 24)]
    g.shape(arc + [(0.3, -0.02)], INDIGO)
    inner = [(0.2 * math.cos(a), -0.02 - 0.2 * math.sin(a)) for a in np.linspace(math.pi, 0, 20)]
    g.shape(inner, N.WARM)
    g.line([(0, -0.22), (0, -0.02)], INK, 0.7)
    g.rect(-0.44, -0.02, 0.44, 0.1, WOOD, r=0.02)
    g.rect(-0.38, 0.1, 0.38, 0.15, WOOD_D, r=0.01)
    g.dot(0.16, 0.06, 0.025, INK)
    tuft = [(0.14, 0.05), (0.24, 0.07), (0.27, 0.3), (0.21, 0.24), (0.17, 0.34), (0.12, 0.22), (0.05, 0.28)]
    g.shape(tuft, U.SILVER)
    g.line([(0.17, 0.08), (0.17, 0.24)], U.hexc("AEB8CC"), 0.6)
    N.sparkle(g, -0.27, 0.27, 0.08)
    return sp


def latch_bar():
    """The wooden latch bar of the front door with its two iron brackets (lifts as one piece)."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.92)
    g.rect(-0.44, -0.07, 0.44, 0.07, WOOD)
    g.line([(-0.38, 0.0), (0.38, -0.01)], WOOD_D, 0.6)
    for x in (-0.28, 0.28):
        g.rect(x - 0.06, -0.12, x + 0.06, 0.12, U.hexc("6A5A70"), r=0.02)
        g.dot(x, 0.0, 0.018, CANDY)
    return sp


def sparkle_part():
    """A four-point candy twinkle ("shiny", "found"), smaller and sharper than the show star."""
    sp = Sprite(CELL, CELL, 0)
    g = G(sp, CELL / 2, CELL / 2, CELL * 0.9)
    N.sparkle(g, 0.0, 0.0, 0.42)
    N.sparkle(g, 0.3, -0.3, 0.12, BONE)
    return sp


SPECIAL = {
    "pg.bridge": (wide_bridge, 2, 1, 0.7, 0),
    "pg.memory": (memory_frame, 3, 1, 0.98, 0),
    "pg.stage": (stage_panel, 3, 1, 1.0, 40),
    "pg.clock": (clock_face, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.clock_hand": (clock_hand, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.eye_shut": (eye_shut, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.puff": (puff, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.key_ring": (key_ring, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.june_run": (lambda: june_run(0.0), 1, 1, DEFAULT_HEIGHT, 0),
    "pg.pip_body": (pip_body, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.bea_arm": (bea_arm, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.pointer": (pointer_right, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.house_side": (house_side, 1, 1, 1.0, 0),
    "pg.wave_hand": (wave_hand, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.sill_snag": (sill_snag, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.latch_bar": (latch_bar, 1, 1, DEFAULT_HEIGHT, 0),
    "pg.sparkle": (sparkle_part, 1, 1, DEFAULT_HEIGHT, 0),
}

EXTRAS = [
    ("x.june_run_1", lambda: june_run(0.25), 1, 1, DEFAULT_HEIGHT),
    ("x.june_run_2", lambda: june_run(0.5), 1, 1, DEFAULT_HEIGHT),
    ("x.june_run_3", lambda: june_run(0.75), 1, 1, DEFAULT_HEIGHT),
    # The first-launch card art (x.book, x.pictures) sat here; D23.1 retired the reading choice, and the first two
    # v3 parts take their cells (bake() puts them at this point), so every other rect stays where it was.
    ("v3", None, 1, 1, 0),
    ("v3", None, 1, 1, 0),
    ("x.tail", bubble_tail, 1, 1, 1.0),
    ("x.june_ghost_0", lambda: june_ghost(0.0), 1, 1, DEFAULT_HEIGHT),
    ("x.june_ghost_1", lambda: june_ghost(0.25), 1, 1, DEFAULT_HEIGHT),
    ("x.june_ghost_2", lambda: june_ghost(0.5), 1, 1, DEFAULT_HEIGHT),
    ("x.june_ghost_3", lambda: june_ghost(0.75), 1, 1, DEFAULT_HEIGHT),
    ("x.paper_bat", paper_bat, 1, 1, DEFAULT_HEIGHT),
]

NO_SHADOW = {"pg.stage", "pg.memory", "pg.key_ring", "pg.puff", "pg.clock_hand", "x.tail", "x.june_ghost_0",
             "x.june_ghost_1", "x.june_ghost_2", "x.june_ghost_3", "x.paper_bat"}


def render_cell(sp, shadow):
    im = sp if isinstance(sp, Image.Image) else sp.render()
    if not shadow:
        return im
    a = im.split()[3]
    sh = Image.new("RGBA", im.size, INK[:3] + (0,))
    sh.putalpha(a.point(lambda v: v * 115 // 255).filter(ImageFilter.GaussianBlur(2.2)))
    lay = Image.new("RGBA", im.size, (0, 0, 0, 0))
    lay.paste(sh, (3, 5))
    return Image.alpha_composite(lay, im)


# ----------------------------------------------------------------------------------------------- packing

def part_job(pid):
    if pid in SPECIAL:
        fn, cols, rows, height, border = SPECIAL[pid]
    else:
        name = pid[3:]
        if name not in N.GLYPHS:
            raise SystemExit(f"no drawing for {pid}")
        fn, cols, rows, height, border = (lambda n=name: glyph_cell(n)), 1, 1, DEFAULT_HEIGHT, 0
    return (pid, fn, cols, rows, height, border, False)


def bake():
    """Packs first-fit in a fixed order: v1+v2 parts, the extras (the first v3 parts fill the retired first-launch
    cells), then the remaining v3 parts. Appending keeps every earlier rect stable."""
    base, v3 = part_ids(split=True)
    ids = base + v3
    jobs = [part_job(pid) for pid in base]
    pending = list(v3)
    for xid, fn, cols, rows, height in EXTRAS:
        if xid == "v3":
            if pending:
                jobs.append(part_job(pending.pop(0)))
            continue
        jobs.append((xid, fn, cols, rows, height, 0, True))
    jobs.extend(part_job(pid) for pid in pending)

    per_row = ATLAS // PITCH
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    grid = []          # occupancy per row
    rects = []
    for pid, fn, cols, rows, height, border, extra in jobs:
        placed = False
        r = 0
        while not placed:
            while len(grid) < r + rows:
                grid.append([False] * per_row)
            for c in range(per_row - cols + 1):
                if all(not grid[r + dr][c + dc] for dr in range(rows) for dc in range(cols)):
                    for dr in range(rows):
                        for dc in range(cols):
                            grid[r + dr][c + dc] = True
                    x, y = c * PITCH + GAP // 2, r * PITCH + GAP // 2
                    w, h = cols * PITCH - GAP, rows * PITCH - GAP
                    sp = fn()
                    im = render_cell(sp, pid not in NO_SHADOW)
                    if im.size != (w, h):
                        im = im.resize((w, h), Image.LANCZOS)
                    atlas.alpha_composite(im, (x, y))
                    rects.append((pid, x, ATLAS - (y + h), w, h, height, border, extra))
                    placed = True
                    break
            r += 1
            if (r + rows) * PITCH > ATLAS + PITCH:
                raise SystemExit("atlas full")
    return atlas, rects, ids


def write_cs(rects):
    def row(r):
        pid, x, y, w, h, height, border, _ = r
        return f'            new PuppetRect("{pid}", {x}, {y}, {w}, {h}, {height:.2f}f, {border}),'

    parts = "\n".join(row(r) for r in rects if not r[7])
    extras = "\n".join(row(r) for r in rects if r[7])
    cs = f"""// <auto-generated>Written by tools/ui_mock/puppet_atlas.py; re-run the script instead of editing.</auto-generated>
using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Nonverbal
{{
    /// <summary>One sprite in the baked <c>Resources/UI/puppet_atlas.png</c>.</summary>
    public readonly struct PuppetRect
    {{
        public PuppetRect(string id, int x, int y, int width, int height, float stageHeight, int border)
        {{
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            StageHeight = stageHeight;
            Border = border;
        }}

        public string Id {{ get; }}

        /// <summary>Pixel rect in the atlas, origin bottom-left (Unity texture space).</summary>
        public int X {{ get; }}
        public int Y {{ get; }}
        public int Width {{ get; }}
        public int Height {{ get; }}

        /// <summary>The sprite's height on a vignette stage at scale 1, in stage heights (width keeps the aspect).</summary>
        public float StageHeight {{ get; }}

        /// <summary>9-slice border in pixels (the stage panel), 0 for puppets.</summary>
        public int Border {{ get; }}

        public float Aspect => Height > 0 ? (float)Width / Height : 1f;
    }}

    /// <summary>
    /// The PuppetAtlas rect table (docs/nonverbal-ux.md §12 step 9). <see cref="Parts"/> holds exactly the IDs in
    /// <see cref="PuppetParts.All"/>; <see cref="Extras"/> holds the June run flipbook frames and chrome art.
    /// </summary>
    public static class PuppetAtlasLayout
    {{
        public const string ResourcePath = "UI/puppet_atlas";
        public const int AtlasSize = {ATLAS};

        /// <summary>Flipbook frames of <see cref="PuppetParts.JuneRun"/> after its own cell, in order.</summary>
        public static readonly IReadOnlyList<string> JuneRunFrames = new[] {{ "x.june_run_1", "x.june_run_2", "x.june_run_3" }};

        public const string BubbleTail = "x.tail";
        public const string PaperBat = "x.paper_bat";

        /// <summary>The memory ghost's run frames (pale translucent paper with a candy rim).</summary>
        public static readonly IReadOnlyList<string> JuneGhostFrames = new[]
        {{
            "x.june_ghost_0", "x.june_ghost_1", "x.june_ghost_2", "x.june_ghost_3",
        }};

        public static readonly IReadOnlyList<PuppetRect> Parts = new[]
        {{
{parts}
        }};

        public static readonly IReadOnlyList<PuppetRect> Extras = new[]
        {{
{extras}
        }};

        private static readonly Dictionary<string, PuppetRect> ById = Index();

        public static bool TryGet(string id, out PuppetRect rect)
        {{
            if (id != null && ById.TryGetValue(id, out rect))
            {{
                return true;
            }}

            rect = default;
            return false;
        }}

        private static Dictionary<string, PuppetRect> Index()
        {{
            var map = new Dictionary<string, PuppetRect>(StringComparer.Ordinal);
            foreach (var r in Parts)
            {{
                map[r.Id] = r;
            }}

            foreach (var r in Extras)
            {{
                map[r.Id] = r;
            }}

            return map;
        }}
    }}
}}
"""
    open(OUT_CS, "w", encoding="utf-8", newline="\n").write(cs)


def preview(atlas, path):
    bg = Image.new("RGBA", atlas.size, U.NIGHT[:3] + (255,))
    bg.alpha_composite(atlas)
    bg.convert("RGB").resize((1024, 1024), Image.LANCZOS).save(path, quality=90)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview", help="also write a JPG preview over night felt")
    ap.add_argument("--full", help="also write a full-size PNG preview over night felt")
    args = ap.parse_args()
    atlas, rects, ids = bake()
    os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
    atlas.save(OUT_PNG, optimize=True)
    write_cs(rects)
    if args.preview:
        preview(atlas, args.preview)
    if args.full:
        bg = Image.new("RGBA", atlas.size, U.NIGHT[:3] + (255,))
        bg.alpha_composite(atlas)
        bg.convert("RGB").save(args.full, quality=92)
    print(f"{len(ids)} parts + {len(rects) - len(ids)} extras -> {OUT_PNG}")


if __name__ == "__main__":
    main()
