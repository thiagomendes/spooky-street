#!/usr/bin/env python3
"""Spooky Street hint sprites (docs/nonverbal-ux.md §6, S002 hints close).

Three small baked UI sprites, drawn with the same paper and ink helpers as ui_mock.py:

  ghost_hand.png  256x256  the tier-3 "show me" hand: bone paper, index finger up, ink outline, the wrist fading
                           out like a ghost. Its fingertip is at (0.5, 0.94) of the sprite (pivot for presses).
                           Drawn opaque; the game shows it translucent.
  hint_star.png    64x64   one paper star fleck for the tier-1 sparkle; white paper, tinted candy corn in game.
  glyph_key.png   128x128  a bone key cap (9-sliced, border 40) with a soft lower lip; the key's name is text.

    python3 tools/ui_mock/hint_sprites.py [--out DIR] [--sheet SHEET.png]

Every sprite is reviewed by rendering the sheet and looking at it before it goes into the game.
"""
from __future__ import annotations

import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_mock as um  # noqa: E402  (shared paper, felt and ink helpers)

SS = um.SS
OUT = um.UNITY_UI
GHOST_FINGERTIP = (0.5, 0.94)  # normalized, origin bottom-left (Unity pivot)


def _capsule(draw, sp, a, b, width):
    """A rounded stroke from a to b (sprite pixels) into a supersampled mask."""
    draw.line([sp.S(*a), sp.S(*b)], fill=255, width=int(width * SS))
    r = width * SS / 2
    for x, y in (sp.S(*a), sp.S(*b)):
        draw.ellipse((x - r, y - r, x + r, y + r), fill=255)


def _outline(sp, mask, color, width):
    """Ink edge: the mask dilated minus the mask eroded, so the line sits on the cut."""
    k = int(width * SS) | 1
    outer = mask.filter(ImageFilter.MaxFilter(k))
    inner = mask.filter(ImageFilter.MinFilter(k))
    edge = ImageChops.subtract(outer, inner)
    lay = Image.new("RGBA", sp.img.size, color[:3] + (0,))
    lay.putalpha(edge.filter(ImageFilter.GaussianBlur(0.6 * SS)))
    sp.comp(lay)


def ghost_hand():
    """Bone paper pointing hand, index finger up; the wrist fades out (a ghost, not a glove)."""
    sp = um.Sprite(232, 232, 12)
    m = Image.new("L", sp.img.size, 0)
    d = ImageDraw.Draw(m)
    # Index finger: a long capsule straight up; its tip lands at the sprite's (0.5, 0.94) = local (116, ~3).
    _capsule(d, sp, (116, 22), (116, 130), 40)
    # Palm, a soft rounded block under the fingers.
    d.polygon(sp.Sp(um.rrect_path(88, 118, 194, 206, 30, 2)), fill=255)
    # Three folded fingers side by side right of the index (their tips tucked toward the palm), the little one lowest.
    folded = [um.rrect_path(134, 98, 162, 150, 14, 2), um.rrect_path(158, 106, 184, 154, 13, 2),
              um.rrect_path(180, 118, 202, 160, 11, 2)]
    for f in folded:
        d.polygon(sp.Sp(f), fill=255)
    # The thumb crosses in front of the folded fingers, from the left of the palm.
    thumb_a, thumb_b = (92, 176), (150, 150)
    _capsule(d, sp, thumb_a, thumb_b, 32)
    # Wrist, running off the bottom edge (faded below).
    d.polygon(sp.Sp([(108, 196), (176, 196), (172, 244), (112, 244)]), fill=255)
    m = m.filter(ImageFilter.GaussianBlur(0.8 * SS)).point(lambda v: 255 if v > 127 else 0)

    sp.fill(m, um.BONE, "paper", 7)
    # A cool lilac rim inside the edge: the ghost's glow, so it reads as "not a real hand" on felt and in the world.
    rim = ImageChops.subtract(m, m.filter(ImageFilter.MinFilter(int(14 * SS) | 1)))
    rim = rim.filter(ImageFilter.GaussianBlur(4 * SS))
    glow = Image.new("RGBA", sp.img.size, (190, 170, 235, 0))
    glow.putalpha(ImageChops.multiply(rim, m).point(lambda v: int(v * 0.55)))
    sp.comp(glow)
    # Inner ink lines: the folds between the folded fingers (their upper halves) and the thumb's edge over them.
    seam = um.INK[:3] + (220,)
    sp.line([(160, 114), (160, 140)], seam, 3.2)
    sp.line([(182, 126), (182, 146)], seam, 3.2)
    sp.line([(136, 106), (136, 132)], seam, 3.2)  # the index finger's side over the first folded finger
    tm = Image.new("L", sp.img.size, 0)
    _capsule(ImageDraw.Draw(tm), sp, thumb_a, thumb_b, 32)
    _outline(sp, ImageChops.multiply(tm, m), um.INK, 3.6)
    _outline(sp, m, um.INK, 5.0)

    im = sp.render()
    # Fade the wrist into nothing over its last 46 px.
    a = np.asarray(im.split()[3], np.float32)
    h = a.shape[0]
    ramp = np.clip((h - 8 - np.arange(h, dtype=np.float32)) / 46.0, 0, 1) ** 1.4
    a *= ramp[:, None]
    im.putalpha(Image.fromarray(a.astype(np.uint8), "L"))
    return im


def hint_star():
    """A white paper five-point star with a soft fuzzed edge (tinted candy corn in game)."""
    sp = um.Sprite(56, 56, 4)
    import math
    pts = []
    for i in range(10):
        r = 27 if i % 2 == 0 else 12
        a = math.radians(-90 + 36 * i)
        pts.append((28 + r * math.cos(a), 29 + r * math.sin(a)))
    pts = um.deckle(um.dense(pts, 1.5), 0.6, 5, 0.3, 0.5)
    sp.fill(pts, (255, 255, 255, 255), "paper", 3, fuzz=True)
    # A soft lighter core so the fleck has a little lift.
    core = Image.new("L", sp.img.size, 0)
    x, y = sp.S(28, 29)
    ImageDraw.Draw(core).ellipse((x - 8 * SS, y - 8 * SS, x + 8 * SS, y + 8 * SS), fill=110)
    lay = Image.new("RGBA", sp.img.size, (255, 255, 255, 0))
    lay.putalpha(core.filter(ImageFilter.GaussianBlur(4 * SS)))
    sp.comp(lay)
    return sp.render()


def glyph_key():
    """Bone paper key cap (9-sliced, border 40): rounded top face, a darker lip below, ink outline."""
    w = h = 120
    pad = 4
    sp = um.Sprite(w, h, pad)
    body = um.rrect_path(2, 2, w - 2, h - 2, 26, 2)
    sp.fill(body, um.BONE_D, "paper", 21)
    face = um.rrect_path(8, 6, w - 8, h - 18, 20, 2)
    sp.fill(face, um.BONE, "paper", 22)
    # Soft shade at the face's lower edge (it sits above the lip).
    shade = Image.new("L", sp.img.size, 0)
    ImageDraw.Draw(shade).line(sp.Sp(face[len(face) // 4: len(face) // 2]), fill=255, width=int(5 * SS))
    lay = Image.new("RGBA", sp.img.size, (90, 60, 40, 0))
    lay.putalpha(shade.filter(ImageFilter.GaussianBlur(3 * SS)).point(lambda v: v * 0.3))
    sp.comp(lay)
    sp.line(body, um.INK, 4.0, closed=True)
    return sp.render()


SPRITES = {
    "ghost_hand": ghost_hand,
    "hint_star": hint_star,
    "glyph_key": glyph_key,
}


def review_sheet(images, path):
    """The sprites on night felt and on bone paper, at size and at phone scale, for review by eye."""
    sheet = Image.new("RGBA", (1100, 560), um.NIGHT)
    paper = Image.new("RGBA", (1100, 280), um.BONE)
    sheet.paste(paper, (0, 280))
    x = 30
    for name, im in images.items():
        for row, y in ((0, 20), (1, 300)):
            shown = im
            if name == "ghost_hand":
                a = np.asarray(im.split()[3], np.float32) * 0.82
                shown = im.copy()
                shown.putalpha(Image.fromarray(a.astype(np.uint8), "L"))
            if name == "hint_star":
                tint = Image.new("RGBA", im.size, um.CANDY)
                tint.putalpha(im.split()[3])
                shown = ImageChops.multiply(tint, im)
                shown.putalpha(im.split()[3])
            sheet.alpha_composite(shown, (x, y))
            small = shown.resize((max(1, shown.size[0] // 3), max(1, shown.size[1] // 3)), Image.LANCZOS)
            sheet.alpha_composite(small, (x, y + shown.size[1] + 4) if shown.size[1] < 200 else (x + shown.size[0] + 6, y))
        x += im.size[0] + (110 if name == "ghost_hand" else 60)
    # A key cap stretched wide (Space), with its label, to check the 9-slice.
    key = images["glyph_key"]
    b = 40
    wide = Image.new("RGBA", (300, 128), (0, 0, 0, 0))
    wide.paste(key.crop((0, 0, b, 128)), (0, 0))
    wide.paste(key.crop((b, 0, 128 - b, 128)).resize((300 - 2 * b, 128)), (b, 0))
    wide.paste(key.crop((128 - b, 0, 128, 128)), (300 - b, 0))
    for y in (20, 300):
        sheet.alpha_composite(wide, (760, y + 130))
        d = ImageDraw.Draw(sheet)
        d.text((910, y + 182), "Space", font=um.font(um.F_BOLD, 40), fill=um.INK, anchor="mm")
        d.text((x - 60 - 64, y + 52 + 20), "E", font=um.font(um.F_BOLD, 54), fill=um.INK, anchor="mm")
    sheet.convert("RGB").save(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=OUT, help="folder for the PNGs (default: the game's Resources/UI)")
    ap.add_argument("--sheet", default=None, help="also write a review sheet here")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    images = {}
    for name, make in SPRITES.items():
        im = make()
        assert im.size[0] % 4 == 0 and im.size[1] % 4 == 0, (name, im.size)
        im.save(os.path.join(args.out, name + ".png"), optimize=True)
        images[name] = im
        print(name, im.size)
    if args.sheet:
        review_sheet(images, args.sheet)
        print("sheet", args.sheet)


if __name__ == "__main__":
    main()
