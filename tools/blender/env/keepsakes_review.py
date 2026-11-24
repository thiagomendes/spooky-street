"""Review sheet of the S004 seeds and keepsakes as built (the approved-look pass, before the game export):
the troupe photo (close, and at the size it has on a phone from the hall's play camera), the owl crest (close
and beside Pip for scale), the collectible paper bat closed and open, the bat mobile and the dress-up trunk.

The game versions are reviewed from the re-imported FBX files by `keepsakes_game_review.py`.

Run: blender -b -t 2 --python tools/blender/env/keepsakes_review.py -- out.png [draft|final] [view ...]"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import env_lib as el
import interior as it
import keepsakes as ks
import owl_house as oh
from env_lib import H, T, R, grouped

GAP = 6.0


def lights(warm=1.0):
    el.night_world(0.5, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 1.2, (0.72, 0.76, 1.0), 6.0, 60.0)
    el.sun_light("Lamp", (0.6, 1.6, -0.9), 2.4 * warm, (1.0, 0.82, 0.6), 8.0, 60.0)
    el.sun_light("Fill", (-1.4, 1.0, -0.3), 0.7, (0.75, 0.7, 1.0), 6.0, 60.0)


def wall(craft, w=3.0, h=2.2, rgb=None):
    b = el.Builder(1)
    b.box(craft.tinted(rgb or el.blend(el.E["purple"], el.E["bone"], 0.2), 0.9, 0.16, 2.5), (w, 0.05, h), (0, 0.025, 0), bevel=0, tint=1.0)
    b.finish("Wall")


def main():
    out, quality, rest = el.args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    groups = {}

    def add(name, build, x):
        root, _ = grouped(name, build)
        root.location.x = x
        groups[name] = x
        print(f"TRIS {name}: {el.triangles(root)}", flush=True)

    def photo():
        wall(craft)
        it.build_troupe_photo(craft)

    def crest():
        wall(craft, 1.2, 1.2, el.E["indigo"])
        oh.build_crest_sign(craft)

    add("Photo", photo, 0.0)
    add("Crest", crest, GAP)
    for i, (name, build) in enumerate(ks.REVIEW):
        add(name, lambda build=build: build(craft), GAP * (i + 2))
    final = quality == "final"
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0, bloom=0.04)
    lights()
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    # name -> (group, eye offset, target offset, lens, width, height, ortho scale)
    views = {
        "photo": ("Photo", (0.0, -3.0, 0.0), (0.0, 0.0, 0.01), 50, 1600, 1060, 0.8),
        "photo_owl": ("Photo", (-0.2, -3.0, 0.03), (-0.2, 0.0, 0.03), 50, 800, 800, 0.24),
        "photo_moon": ("Photo", (0.17, -3.0, 0.03), (0.17, 0.0, 0.03), 50, 800, 800, 0.24),
        "photo_phone": ("Photo", (0.9, -3.4, -0.5), (0.0, 0.0, -0.2), 28, 844, 390, None),  # as the hall's play camera frames it
        "crest": ("Crest", (0.0, -3.0, 0.0), (0.0, 0.0, 0.0), 50, 800, 800, 0.5),
        "crest_side": ("Crest", (-0.55, -0.75, 0.2), (0.0, 0.0, 0.0), 50, 800, 800, None),
    }
    views.update(ks.VIEWS)
    todo = [v for v in rest if v in views] or list(views)
    tiles = {}
    for name in todo:
        group, eye, target, lens, w, h, ortho = views[name]
        x = groups[group]
        el.solo([group], keep=())
        tiles[name] = el.render_view(cam, (eye[0] + x, eye[1], eye[2]), (target[0] + x, target[1], target[2]), lens, int(w * k), int(h * k),
                                     f"{tmp}_{name}.png", ortho)
        print("rendered", name, flush=True)
    width = max(t.shape[1] for t in tiles.values())
    rows, row = [], []
    for name in todo:  # pack tiles into rows of the widest tile's width
        if row and sum(t.shape[1] for t in row) + tiles[name].shape[1] > max(width, 1700 * k):
            rows.append(row)
            row = []
        row.append(tiles[name])
    rows.append(row)
    full = max(sum(t.shape[1] for t in r) for r in rows)
    strips = []
    for r in rows:
        hh = max(t.shape[0] for t in r)
        strips.append(el.pad(np.concatenate([el.pad(t, t.shape[1], hh) for t in r], axis=1), full, hh))
    el.co.save_sheet(np.concatenate(strips[::-1], axis=0), out)
    print("keepsakes sheet ->", out)


if __name__ == "__main__":
    main()
