"""Compose the lane houses' review tiles (lane_review.py) into labelled review sheets (plain Python with Pillow, no Blender).

    python3 tools/blender/env/lane_sheet.py <tiles_dir> <out_dir>

Writes lane_houses_review.jpg (the lane both ways and its greyscale, each door shut and answering at the gameplay
camera, the doorways close) and lane_houses_lod.jpg (LOD0 beside LOD1 at 22 m, the leaves open).
"""
import os
import sys

from PIL import Image, ImageDraw, ImageOps

BG = (18, 14, 30)
INK = (244, 230, 200)
HOUSES = ("Fogg", "Pearl", "Abbotts")


def tile(path, w, label):
    im = Image.open(path).convert("RGB")
    im = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    d.rectangle((0, im.height - 22, im.width, im.height), fill=BG)
    d.text((8, im.height - 18), label, fill=INK)
    return im


def row(tiles, gap=6):
    h = max(t.height for t in tiles)
    out = Image.new("RGB", (sum(t.width for t in tiles) + gap * (len(tiles) - 1), h), BG)
    x = 0
    for t in tiles:
        out.paste(t, (x, 0))
        x += t.width + gap
    return out


def stack(rows, gap=6):
    w = max(r.width for r in rows)
    out = Image.new("RGB", (w, sum(r.height for r in rows) + gap * (len(rows) - 1)), BG)
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + gap
    return out


def main():
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    p = lambda n: os.path.join(src, n)  # noqa: E731
    lane = tile(p("lane.png"), 900, "the lane from the hedge gap (CI frame 94's camera): Fogg left, Pearl beyond, the Abbotts right")
    grey = ImageOps.grayscale(Image.open(p("lane.png"))).convert("RGB")
    grey.save(p("lane_grey.png"))
    rows = [row([lane, tile(p("lane2.png"), 900, "from the Green's corner, looking back west")]),
            row([tile(p("lane_grey.png"), 900, "greyscale: trim, doors and lit windows separate; no lit attic or round window"),
                 tile(p(f"door_{HOUSES[0]}_near.png"), 440, "Mr. Fogg's doorway, the hall lit behind him")])]
    for h in HOUSES:
        rows.append(row([tile(p(f"door_{h}_closed.png"), 600, f"{h}: the gameplay camera at the bell, door shut"),
                         tile(p(f"door_{h}_open.png"), 600, f"{h}: the gag at its peak, the neighbours answering"),
                         tile(p(f"door_{h}_near.png"), 400, f"{h}: the doorway")]))
    stack(rows).save(os.path.join(out, "lane_houses_review.jpg"), quality=88)
    rows = [row([tile(p(f"lod_{h}_0.png"), 400, f"{h} LOD0 at 22 m"), tile(p(f"lod_{h}_1.png"), 400, f"{h} LOD1 at 22 m")])
            for h in HOUSES]
    rows.append(row([tile(p(f"leaf_{h}.png"), 330, f"{h}'s leaf, open") for h in HOUSES]))
    stack(rows).save(os.path.join(out, "lane_houses_lod.jpg"), quality=88)
    print("sheets ->", out)


if __name__ == "__main__":
    main()
