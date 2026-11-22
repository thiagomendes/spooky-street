#!/usr/bin/env python3
"""Renders every case 01 vignette offline, the way `VignettePlayer` draws it, for review (docs/nonverbal-ux.md).

The vignette data is sampled by the engine-free C# core itself (`tools/vignette-dump`, `VignetteSampler`), so the
frames show exactly what the game plays; the parts come from the baked `puppet_atlas.png` with the rects in
`PuppetAtlasLayout.cs`. Overlays (ring, puff, knock) follow `VignettePlayer.RenderOverlays`.

    python3 tools/ui_mock/vignette_sheet.py [--sheet OUT.jpg] [--gif-dir DIR] [--only vg.id,...] [--json DUMP.json]

The contact sheet has one row per vignette: its ID, the line keys that use it and three frames (the end of each
beat, or mid-loop for one-beat vignettes), over the night-felt stage at the bubble's stage size (992x215 units).
"""
from __future__ import annotations

import argparse
import json
import math
import os
import re
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_mock as U  # noqa: E402

ROOT = U.ROOT
ATLAS_PNG = os.path.join(ROOT, "Assets", "SpookyStreet", "Content", "Resources", "UI", "puppet_atlas.png")
LAYOUT_CS = os.path.join(ROOT, "Assets", "SpookyStreet", "Core", "Nonverbal", "PuppetAtlasLayout.cs")
STAGE_W, STAGE_H = 992, 215          # DialogueView inner stage in canvas units
STAGE_COL = U.hexc("332D62")
FLIP_FPS = 12


def load_layout():
    src = open(LAYOUT_CS, encoding="utf-8").read()
    rects = {}
    for m in re.finditer(r'new PuppetRect\("([^"]+)", (\d+), (\d+), (\d+), (\d+), ([0-9.]+)f, (\d+)\)', src):
        pid, x, y, w, h, sh, _ = m.groups()
        rects[pid] = (int(x), int(y), int(w), int(h), float(sh))
    frames = re.search(r"JuneRunFrames = new\[\] \{([^}]*)\}", src).group(1)
    return rects, [f.strip().strip('"') for f in frames.split(",") if f.strip()]


class Atlas:
    def __init__(self):
        self.im = Image.open(ATLAS_PNG).convert("RGBA")
        self.rects, self.run_frames = load_layout()
        self.cache = {}

    def sprite(self, pid):
        if pid not in self.cache:
            x, y, w, h, _ = self.rects[pid]
            top = self.im.size[1] - (y + h)
            self.cache[pid] = self.im.crop((x, top, x + w, top + h))
        return self.cache[pid]

    def nominal(self, pid, stage_h):
        x, y, w, h, sh = self.rects.get(pid, (0, 0, 1, 1, 0.85))
        hh = sh * stage_h
        return hh * w / h, hh

    def frame_of(self, pid, frame):
        if pid != "pg.june_run":
            return pid
        n = len(self.run_frames) + 1
        f = frame % n
        return pid if f == 0 else self.run_frames[f - 1]


def dump(path):
    subprocess.run(["dotnet", "run", "--project", os.path.join(ROOT, "tools", "vignette-dump"), "--", path],
                   check=True, stdout=subprocess.DEVNULL)
    return json.load(open(path, encoding="utf-8"))


def stage_bg(k):
    w, h = int(STAGE_W * k), int(STAGE_H * k)
    sp = U.Sprite(w, h, 0)
    pts = U.rrect_path(2, 2, w - 2, h - 2, 18 * k + 4, 3)
    sp.fill(pts, STAGE_COL, "felt", 9)
    sp.stitches(U.rrect_path(10, 10, w - 10, h - 10, 12, 2), U.hexc("413A78"), 9, 7, 2.2, shadow=False)
    sp.line(pts, U.INK, 2.4, closed=True)
    return sp.render()


def put(canvas, im, cx, cy, w, h, rot, alpha):
    if w < 1 or h < 1:
        return
    s = im.resize((max(1, int(round(w))), max(1, int(round(h)))), Image.LANCZOS)
    if alpha < 0.999:
        a = s.split()[3].point(lambda v: int(v * max(0.0, alpha)))
        s.putalpha(a)
    if abs(rot) > 0.01:
        s = s.rotate(rot, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(s, (int(round(cx - s.size[0] / 2)), int(round(cy - s.size[1] / 2))))


def render(atlas, frame, prev, k=0.5, index=0):
    """One frame of a vignette at scale k (1 = canvas units). `prev` is the previous frame (moving parts flip)."""
    W, H = STAGE_W * k, STAGE_H * k
    canvas = stage_bg(k)
    pos = {}
    for i, p in enumerate(frame["parts"]):
        if not p["visible"]:
            continue
        cx, cy = p["x"] * W, H - p["y"] * H
        pos[p["track"]] = (cx, cy, p)
        moving = prev is not None and prev["parts"][i]["visible"] and (
            abs(prev["parts"][i]["x"] - p["x"]) * W > 0.05 or abs(prev["parts"][i]["y"] - p["y"]) * H > 0.05)
        pid = atlas.frame_of(p["part"], index * FLIP_FPS // 15) if moving else p["part"]
        w, h = atlas.nominal(p["part"], H)
        put(canvas, atlas.sprite(pid), cx, cy, w * p["scale"], h * p["scale"], p["rot"], p["alpha"])
    for c in frame["cues"][:3]:
        kind = c["kind"]
        if kind not in ("Ring", "Puff", "Knock") or c["target"] not in pos:
            continue
        cx, cy, p = pos[c["target"]]
        tw, th = atlas.nominal(p["part"], H)
        d = max(tw, th) * max(0.3, p["scale"])
        prog = max(0.0, min(1.0, c["progress"]))
        alpha = 1.0
        if kind == "Ring":
            u = min(1.0, prog * 4)
            v = u - 1
            pop = 1 + v * v * (2.70158 * v + 1.70158)
            scale = 1.15 * pop * (1 + 0.08 * math.sin(c["elapsed"] * 2 * math.pi * 1.6))
            sid = "pg.key_ring"
        elif kind == "Puff":
            scale, alpha, sid = 0.6 + 0.8 * prog, 1 - prog, "pg.puff"
        else:
            scale, alpha, sid = 0.5 + 0.9 * prog, 1 - prog * prog, "pg.knock_ring"
            cx, cy = cx + d * 0.25, cy - d * 0.1
        put(canvas, atlas.sprite(sid), cx, cy, d * scale, d * scale, 0, alpha)
    return canvas


def pick_times(v):
    beats = v["beats"]
    if len(beats) == 1:
        b0, b1 = beats[0]
        return [b0 + (b1 - b0) * 0.35, b0 + (b1 - b0) * 0.7, b1]
    ts = [b[1] - 0.05 for b in beats]
    if len(ts) == 2:
        ts = [beats[0][0] + (beats[0][1] - beats[0][0]) * 0.5, ts[0], ts[1]]
    return ts


def frame_at(v, t):
    frames = v["frames"]
    i = min(range(len(frames)), key=lambda j: abs(frames[j]["t"] - t))
    return i, frames[i], frames[i - 1] if i > 0 else None


def sheet(data, atlas, out, only=None):
    users = {}
    for row in data["lines"]:
        if row["vignette"]:
            users.setdefault(row["vignette"], []).append(row["key"].replace("c01.", ""))
    vs = [v for v in data["vignettes"] if not only or v["id"] in only]
    k = 0.5
    fw, fh = int(STAGE_W * k), int(STAGE_H * k)
    label_w, gap, row_h, top = 300, 12, fh + 22, 92
    W = label_w + 3 * (fw + gap) + 12
    H = top + row_h * len(vs) + 20
    bg = Image.new("RGBA", (W, H), U.NIGHT[:3] + (255,))
    d = ImageDraw.Draw(bg)
    title = U.font(U.F_DISPLAY, 40)
    bold = U.font(U.F_BOLD, 19)
    body = U.font(U.F_BODY, 15)
    small = U.font(U.F_BODY, 14)
    d.text((20, 18), f"Case 01 vignettes: all {len(vs)}, three frames each (beat ends)", font=title, fill=U.CANDY)
    d.text((20, 66), "Rendered offline from the C# data (VignetteSampler) with the baked puppet atlas.", font=small,
           fill=U.BONE_D)
    for r, v in enumerate(vs):
        y = top + r * row_h
        d.text((16, y + 4), v["id"], font=bold, fill=U.BONE)
        d.text((16, y + 28), f'{v["length"]:.1f} s · {len(v["beats"])} beat(s) · ≤{v["moving"]} moving', font=small,
               fill=U.BONE_D)
        keys = users.get(v["id"], ["notebook answer strip"])
        ly = y + 48
        for key in keys[:4]:
            d.text((16, ly), key, font=body, fill=U.CANDY)
            ly += 18
        for c, t in enumerate(pick_times(v)):
            i, f, prev = frame_at(v, t)
            im = render(atlas, f, prev, k, i)
            x = label_w + c * (fw + gap)
            bg.alpha_composite(im, (x, y))
            d.text((x + 8, y + fh + 1), f"t = {f['t']:.1f} s", font=small, fill=U.BONE_D)
    bg.convert("RGB").save(out, quality=88)
    print(f"{len(vs)} vignettes -> {out}")


def gifs(data, atlas, out_dir, only=None):
    os.makedirs(out_dir, exist_ok=True)
    for v in data["vignettes"]:
        if only and v["id"] not in only:
            continue
        frames = []
        for i, f in enumerate(v["frames"]):
            frames.append(render(atlas, f, v["frames"][i - 1] if i else None, 0.6, i).convert("RGB"))
        frames[0].save(os.path.join(out_dir, v["id"] + ".gif"), save_all=True, append_images=frames[1:],
                       duration=int(1000 / 15), loop=0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="contact sheet JPG")
    ap.add_argument("--gif-dir", help="also write one GIF per vignette here")
    ap.add_argument("--only", help="comma-separated vignette IDs")
    ap.add_argument("--json", help="use this dump instead of running tools/vignette-dump")
    args = ap.parse_args()
    path = args.json or os.path.join(tempfile.mkdtemp(), "vignettes.json")
    data = json.load(open(path, encoding="utf-8")) if args.json else dump(path)
    only = set(args.only.split(",")) if args.only else None
    atlas = Atlas()
    if args.sheet:
        sheet(data, atlas, args.sheet, only)
    if args.gif_dir:
        gifs(data, atlas, args.gif_dir, only)


if __name__ == "__main__":
    main()
