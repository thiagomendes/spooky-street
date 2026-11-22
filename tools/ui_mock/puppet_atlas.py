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
from nonverbal_mock import G, GRAPHITE, WOOD, WOOD_D  # noqa: E402
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


def part_ids():
    """Reads PuppetParts.cs so the atlas follows the C# contract exactly (v1 names, then the v2 constants)."""
    src = open(PARTS_CS, encoding="utf-8").read()
    v1_block = src[src.index("private static readonly string[] V1"):src.index("};", src.index("string[] V1"))]
    v1 = ["pg." + n for n in re.findall(r'"([a-z0-9_]+)"', v1_block)]
    consts = dict(re.findall(r'public const string (\w+) = "(pg\.[a-z0-9_]+)";', src))
    v2_names = re.search(r"V2 = \{([^}]*)\}", src).group(1)
    v2 = [consts[n.strip()] for n in v2_names.split(",") if n.strip()]
    return v1 + v2


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


def book_card():
    """First-launch card art: Pip holding an open storybook with lines of words (reading on)."""
    sp = Sprite(CELL, CELL, 0)
    N.g_pip_mini(G(sp, CELL * 0.3, CELL * 0.52, CELL * 0.8))
    g = G(sp, CELL * 0.66, CELL * 0.55, CELL * 0.62)
    g.shape([(-0.42, -0.3), (0.0, -0.22), (0.0, 0.3), (-0.42, 0.22)], BONE, kind="paper")
    g.shape([(0.0, -0.22), (0.42, -0.3), (0.42, 0.22), (0.0, 0.3)], BONE, kind="paper")
    for i in range(4):
        y = -0.14 + i * 0.1
        g.line([(-0.34, y - 0.02), (-0.08, y + 0.01)], GRAPHITE, 0.7)
        g.line([(0.08, y + 0.01), (0.34, y - 0.02)], GRAPHITE, 0.7)
    g.shape([(-0.46, 0.22), (0.0, 0.32), (0.46, 0.22), (0.46, 0.28), (0.0, 0.38), (-0.46, 0.28)], PURPLE)
    return sp


def pictures_card():
    """First-launch card art: Pip beside three picture frames (a tiny comic, no words)."""
    sp = Sprite(CELL, CELL, 0)
    N.g_pip_mini(G(sp, CELL * 0.22, CELL * 0.55, CELL * 0.78))
    for i, name in enumerate(("treehouse", "bridge", "june_window")):
        cx = CELL * (0.46 + i * 0.19)
        cy = CELL * (0.42 + (i % 2) * 0.12)
        s = CELL * 0.17
        sp.fill(U.rrect_path(cx - s, cy - s, cx + s, cy + s, 6, 2), BONE, "paper", 3 + i)
        sp.line(U.rrect_path(cx - s, cy - s, cx + s, cy + s, 6, 2), INK, 2.6, closed=True)
        N.GLYPHS[name](G(sp, cx, cy, s * 1.7))
    return sp


def bubble_tail():
    """Bone-paper bubble tail pointing up; inked on its two slanted sides only (the card covers the base)."""
    sp = Sprite(CELL, CELL, 0)
    tri = [(18, CELL - 4), (CELL / 2, 10), (CELL - 18, CELL - 4)]
    sp.fill(tri, BONE, "paper", 3)
    sp.line(tri[:2], INK, 4.2)
    sp.line(tri[1:], INK, 4.2)
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
}

EXTRAS = [
    ("x.june_run_1", lambda: june_run(0.25), 1, 1, DEFAULT_HEIGHT),
    ("x.june_run_2", lambda: june_run(0.5), 1, 1, DEFAULT_HEIGHT),
    ("x.june_run_3", lambda: june_run(0.75), 1, 1, DEFAULT_HEIGHT),
    ("x.book", book_card, 1, 1, 1.0),
    ("x.pictures", pictures_card, 1, 1, 1.0),
    ("x.tail", bubble_tail, 1, 1, 1.0),
]

NO_SHADOW = {"pg.stage", "pg.memory", "pg.key_ring", "pg.puff", "pg.clock_hand", "x.tail"}


def render_cell(sp, shadow):
    im = sp.render()
    if not shadow:
        return im
    a = im.split()[3]
    sh = Image.new("RGBA", im.size, INK[:3] + (0,))
    sh.putalpha(a.point(lambda v: v * 115 // 255).filter(ImageFilter.GaussianBlur(2.2)))
    lay = Image.new("RGBA", im.size, (0, 0, 0, 0))
    lay.paste(sh, (3, 5))
    return Image.alpha_composite(lay, im)


# ----------------------------------------------------------------------------------------------- packing

def bake():
    ids = part_ids()
    jobs = []
    for pid in ids:
        if pid in SPECIAL:
            fn, cols, rows, height, border = SPECIAL[pid]
        else:
            name = pid[3:]
            if name not in N.GLYPHS:
                raise SystemExit(f"no drawing for {pid}")
            fn, cols, rows, height, border = (lambda n=name: glyph_cell(n)), 1, 1, DEFAULT_HEIGHT, 0
        jobs.append((pid, fn, cols, rows, height, border, False))
    for xid, fn, cols, rows, height in EXTRAS:
        jobs.append((xid, fn, cols, rows, height, 0, True))

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

        public const string Book = "x.book";
        public const string Pictures = "x.pictures";
        public const string BubbleTail = "x.tail";

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
