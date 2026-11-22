#!/usr/bin/env python3
"""Offline review of the handcrafted sky (art bible §7: look at every asset before it enters the game).

Unity cannot run in cloud sessions, so this renders the sky the way the game camera sees it, in software:
  1. `dotnet run` (Program.cs) paints the textures and cuts the cloud and bat shapes with the game's own
     engine-free code (Core/Art/SkyPainter.cs, Core/World/SkyShapes.cs, SkyModel.cs);
  2. this script ray-casts the dome, moon, halo, cloud rings, bats and a shooting star from the capture cameras,
     mirroring the maths of Art/Shaders/SkyDome.shader and SkyPaper.shader in linear colour.

The scene constants below (dome, moon, palette, ring placement in Program.cs) are copies of Runtime/World values:
keep them in step when those change. Post-processing, fog and the street itself are not drawn.

Usage: python3 tools/sky_preview/sky_preview.py [out_dir]   (default ci-out/sky-preview; needs numpy, Pillow, .NET 8)
"""
import os, subprocess, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
o = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "ci-out", "sky-preview"))
W, H = 1278, 590
C = np.array([0, -3, 8.0]); M = np.array([-30.4, 19.2, 34.9]); F = np.array([0, -0.65, -2.5]); MR = 4.2
VIEW = np.array([0, 1.7, -13.0])  # MagicSky.ReferenceView
LAYERS = ((45.0, 9.5, 0.32, 40.0), (36.0, 18.0, 0.8, 0.0), (33.0, 26.5, 1.25, 200.0))  # SkyModel.Layers
MOON_LAYER, LEAD, MOON_CLOUD_HEIGHT = 1, 22.0, 3.6


def crossing(view, radius):
    """MagicSky.CrossingPoint: where the line from the view to the moon crosses a cloud ring."""
    o2 = np.array([view[0] - C[0], view[2] - C[2]]); d2 = np.array([M[0] - view[0], M[2] - view[2]])
    a = d2 @ d2; b = 2 * o2 @ d2; c = o2 @ o2 - radius * radius
    t = (-b + np.sqrt(b * b - 4 * a * c)) / (2 * a); p = o2 + d2 * t
    return np.degrees(np.arctan2(p[0], p[1])), view[1] + (M[1] - view[1]) * t


AZ, HEIGHT = crossing(VIEW, LAYERS[MOON_LAYER][0])
BASES = [layer[1] for layer in LAYERS]; BASES[MOON_LAYER] = HEIGHT - MOON_CLOUD_HEIGHT * 0.45
STARTS = [layer[3] for layer in LAYERS]; STARTS[MOON_LAYER] = AZ - LEAD
SPEEDS = [layer[2] for layer in LAYERS]
os.makedirs(o, exist_ok=True)
subprocess.run(["dotnet", "run", "--project", os.path.join(HERE, "SkyPreview.csproj"), "--", o] +
               [f"{b:.4f}" for b in BASES], check=True)

s2l = lambda c: np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
l2s = lambda c: np.where(c <= 0.0031308, c * 12.92, 1.055 * np.clip(c, 0, None) ** (1 / 2.4) - 0.055)
hexc = lambda h: np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255]) / 255.0
tex = lambda n, w, h, ch: np.frombuffer(open(f"{o}/{n}", "rb").read(), np.uint8).reshape(h, w, ch).astype(float) / 255
sky = tex("sky.rgb", 1024, 512, 3); stars = tex("stars.rg", 1024, 512, 2); moon = tex("moon.rgb", 256, 256, 3)
CS, CB, CBS, CBK, HZ = hexc(0x2B2454), hexc(0x463873), hexc(0x3B2F69), hexc(0x5A4990), hexc(0x6A4F92)
RIM, HALO, GLOW, STAR, INK = hexc(0xCFC7E8), hexc(0xC3C8F0), hexc(0x37386E), hexc(0xFFF2C8), hexc(0x1E1A26)

def sheets():
    L = open(f"{o}/rings.txt").read().strip().split("\n"); out = []
    for i in range(0, len(L), 7):
        h = L[i].split(); arr = [np.array(list(map(float, L[i + k].split()))) for k in range(1, 7)]
        out.append((int(h[0]), int(h[1]), float(h[2]), float(h[3]), float(h[4]), float(h[5]), float(h[6]), arr))
    return out
SHEETS = sheets()

def render(pos, look, fov, clock=0.0, starts=STARTS, speeds=SPEEDS, bats=None, star=None, name="out"):
    pos = np.array(pos, float); f = np.array(look, float) - pos; f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r); u = np.cross(f, r)
    foc = (H / 2) / np.tan(np.radians(fov / 2))
    px, py = np.meshgrid(np.arange(W) + 0.5 - W / 2, H / 2 - (np.arange(H) + 0.5))
    d = px[..., None] * r + py[..., None] * u + foc * f; d /= np.linalg.norm(d, axis=-1, keepdims=True)
    # dome
    oc = pos - C; b = d @ oc; t = -b + np.sqrt(b * b - (oc @ oc - 52 * 52)); P = pos + d * t[..., None] - C
    el = np.arcsin(np.clip(P[..., 1] / 52, -1, 1)); az = np.arctan2(P[..., 0], P[..., 2])
    uu = (az / (2 * np.pi) * 3) % 1.0; vv = np.clip((el - np.radians(-8) * 0.25) / (np.pi / 2), 0, 1)
    xi = np.clip((uu * 1024).astype(int), 0, 1023); yi = np.clip((vv * 512).astype(int), 0, 511)
    toMoon = (M - pos) / np.linalg.norm(M - pos); near = np.clip(d @ toMoon, 0, 1)
    col = s2l(sky[yi, xi]) + s2l(GLOW) * (near ** 6)[..., None] + s2l(STAR) * (stars[yi, xi, 0] * 0.7)[..., None]
    col = np.minimum(col, 0.95)
    col = np.where((el < np.radians(-7.9))[..., None], 0.02, col)

    def ring(col, li):
        for (l, back, rad, az0, shift, baseY, mist, (xs, bo, to, rx, ry, ins)) in SHEETS:
            if l != li: continue
            o2 = np.array([pos[0] - C[0], pos[2] - C[2]]); d2 = d[..., [0, 2]]
            a = (d2 * d2).sum(-1); bb = 2 * (d2 @ o2); cc = o2 @ o2 - rad * rad
            tt = (-bb + np.sqrt(np.maximum(bb * bb - 4 * a * cc, 0))) / (2 * a)
            Pw = pos + d * tt[..., None]; azp = np.arctan2(Pw[..., 0] - C[0], Pw[..., 2] - C[2])
            theta = np.radians(az0 + starts[li] + clock * speeds[li])
            xl = ((azp - theta + np.pi) % (2 * np.pi) - np.pi) * rad - shift; yl = Pw[..., 1] - baseY
            bot = np.interp(xl, xs, bo, left=1e9, right=1e9); top = np.interp(xl, xs, to, left=-1e9, right=-1e9)
            inside = (yl >= bot) & (yl <= top)
            lo, hi = (CBS, CBK) if back else (CS, CB); hz = mist * 0.7
            lo = lo + (HZ - lo) * hz; hi = hi + (HZ - hi) * hz
            span = to.max() - bo.min(); sh = np.clip((yl - bo.min()) / span, 0, 1)[..., None]
            body = s2l(lo + (hi - lo) * sh)
            inset = np.maximum(np.interp(xl, xs, ins), 1e-3); m = np.clip(1 - (top - yl) / inset, 0, 1)
            nx = np.interp(xl, xs, rx); ny = np.interp(xl, xs, ry)
            T = np.stack([np.cos(azp), np.zeros_like(azp), -np.sin(azp)], -1)
            n = nx[..., None] * T + ny[..., None] * np.array([0, 1, 0.0])
            tm = M - Pw; tm /= np.linalg.norm(tm, axis=-1, keepdims=True)
            facing = np.clip((n * tm).sum(-1), 0, 1) * m
            mb = np.clip(1 - (yl - bot) / inset, 0, 1) * 0.45
            edge = np.maximum(m, mb)
            rim = np.clip(facing * 0.55 + edge * near ** 32, 0, 1) * 0.85
            c2 = body + (s2l(RIM) - body) * rim[..., None]
            col = np.where(inside[..., None], c2, col)
        return col

    col = ring(col, 0)
    # moon plane
    n = (M - F) / np.linalg.norm(M - F); mr = np.cross([0, 1, 0], n); mr /= np.linalg.norm(mr); mu = np.cross(n, mr)
    tp = ((M - pos) @ n) / (d @ n); Pm = pos + d * tp[..., None] - M
    lx = Pm @ mr; ly = Pm @ mu; rr = np.sqrt(lx * lx + ly * ly) / MR
    alpha = np.interp(rr, [0.9, 1.3, 1.85, 2.7], [0.42, 0.21, 0.0714, 0], left=0.42, right=0)
    col = col + (s2l(HALO) - col) * alpha[..., None]
    mi = np.clip(((lx / MR * 0.495 + 0.5) * 256).astype(int), 0, 255); mj = np.clip(((ly / MR * 0.495 + 0.5) * 256).astype(int), 0, 255)
    col = np.where((rr <= 1)[..., None], s2l(moon[mj, mi]), col)
    if bats:
        L = open(f"{o}/shapes.txt").read().strip().split("\n"); i = L.index("bat")
        X = np.array(list(map(float, L[i + 1].split()))); Y = np.array(list(map(float, L[i + 2].split())))
        xs = X[0::2]; bo = Y[0::2]; to = Y[1::2]
        for (bx, by, sx, size) in bats:
            xl = (lx - bx * MR) / (2.0 * size * sx); yl = (ly - by * MR) / (2.0 * size)
            ins = (yl >= np.interp(xl, xs, bo, left=9, right=9)) & (yl <= np.interp(xl, xs, to, left=-9, right=-9))
            col = np.where(ins[..., None], s2l(INK), col)
    col = ring(col, 1); col = ring(col, 2)
    if star:
        azs, els, heading = star
        a, e = np.radians(azs), np.radians(els); Hd = C + 49.5 * np.array([np.sin(a) * np.cos(e), np.sin(e), np.cos(a) * np.cos(e)])
        fw = (Hd - C) / 49.5; tr = np.array(heading, float); tr -= fw * (tr @ fw); tr /= np.linalg.norm(tr); up = np.cross(fw, tr)
        ts = ((Hd - pos) @ fw) / (d @ fw); Ps = pos + d * ts[..., None] - Hd; sx = Ps @ tr; sy = Ps @ up
        w = np.interp(sx, [-9, -0.2, 0.25], [0, 0.25, 0]); al = np.interp(sx, [-9, -0.2, 0.25], [0, 0.8, 1], left=0, right=0) * (np.abs(sy) <= w)
        ast = np.sqrt(np.abs(sx) / 0.9) + np.sqrt(np.abs(sy) / 0.9); al = np.maximum(al, np.clip((1 - ast) * 3, 0, 1))
        col = col + (s2l(STAR) - col) * al[..., None]
    img = (np.clip(l2s(np.minimum(col, 0.95)), 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img).save(f"{o}/{name}.png"); return img


def textures():
    """The painted textures and cut shapes on their own."""
    flip = lambda t: Image.fromarray((t[::-1] * 255).astype(np.uint8))
    flip(sky).save(f"{o}/paint_sky.png")
    lit = np.clip(sky + stars[..., 0:1] * STAR * 0.9, 0, 0.95)
    flip(lit).save(f"{o}/paint_sky_stars.png")
    flip(moon).resize((512, 512), Image.LANCZOS).save(f"{o}/paint_moon.png")


if __name__ == "__main__":
    textures()
    # 06_keyframe_view shortly after the start, and once the first cloud has reached the moon.
    render([-0.4, 1.65, -15], [-2.5, 3.4, 8], 40, clock=8, name="view_keyframe")
    render([-0.4, 1.65, -15], [-2.5, 3.4, 8], 40, clock=30, name="view_keyframe_30s")
    # 16_sky_moon: cloud over the left half of the disc, three bats over the right half.
    cam = np.array([-13.0, 9.0, 0.0])
    az, _ = crossing(cam, LAYERS[MOON_LAYER][0])
    clock = ((az - 12.0 - STARTS[MOON_LAYER]) % 360.0) / SPEEDS[MOON_LAYER]
    render(cam, M + np.array([4.0, -3.5, 0]), 34, clock=clock,
           bats=[(0.82, 0.2, 0.9, 1), (0.44, 0.6, 0.6, 0.86), (0.07, -0.4, 1.0, 0.72)], name="view_moon")
    # 17_shooting_star: mid-streak above the roofs from the arrival porch.
    a, e = np.radians(23), np.radians(41.75)
    head = C + 49.5 * np.array([np.sin(a) * np.cos(e), np.sin(e), np.cos(a) * np.cos(e)])
    render(VIEW, head + np.array([-4.0, -10, 0]), 50, clock=60, star=(23, 41.75, (0.9, -0.4, -0.3)), name="view_star")
    render([-1.5, 5.6, -3], [0.5, 6.3, 3], 45, clock=100, name="view_glide")
    print("sky previews written to", o)
