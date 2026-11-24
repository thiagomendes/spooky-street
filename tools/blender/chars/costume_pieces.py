"""Pip's three costume pieces (S004 plan section 2.1), fitted to her game rig. Her body FBX is not touched.

    blender -b -t 2 --python tools/blender/chars/costume_pieces.py -- export [out_dir] [samples=4]
    blender -b -t 2 --python tools/blender/chars/costume_pieces.py -- review out.png [draft|final] [dir=out_dir]
    blender -b -t 2 --python tools/blender/chars/costume_pieces.py -- icons [out_dir] [size=256]   (trunk cards)

Star-lined cape   A material variant only: `PipStarCape_BaseColor.png` is Pip's own atlas (`Pip_BaseColor.png`)
                  with the cape's berry lining repainted night violet with candy-corn paper stars; everything else
                  is the same texel for texel, so the game swaps the atlas texture and nothing else. The lining's
                  texels are found on the imported `Pip_Cape` mesh (the faces whose baked colour is berry), and the
                  baked fold shading is kept. The stars are laid out on the cape as it hangs (around her, by height).
Lantern pail      `PipLanternPail.fbx`: a round paper lantern on a wire bail, in place of the jack-o'-lantern pail
                  (hide the `Pip_Pail` renderer). Bone paper, orange ribs; its paper is the child
                  `KeepsakePail_Glow` (`GlowLevels.KeepsakeLantern`, under the bloom threshold). Rides the `Pail` bone.
Showman's hat     `PipShowmanHat.fbx`: a small felt top hat with the owl crest on its band, tilted on her head away
                  from her bat clip. Rides the `Head` bone.

The two meshes are authored in Pip's model space in her rest pose, origin at her feet like `Pip.fbx`, and exported
with the same FBX settings: under Pip's model root with an identity local transform they sit exactly where they
belong on the bind pose, and from there they are parented to their bone (`SOCKETS`) keeping the world transform.
Textures: the shared atlas `PipCostume_BaseColor/_Normal/_Emission.png` (512), next to the FBX files in
`Models/Costume/` (not under `Characters/`, so they import as static props).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export", "env"):
    if os.path.join(TOOLS, sub) not in sys.path:
        sys.path.insert(0, os.path.join(TOOLS, sub))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import env_lib as el  # noqa: E402
from env_lib import E, R, T, Builder, H, lc  # noqa: E402

PIP_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Pip")
OUT_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Costume")
ATLAS, ATLAS_SIZE = "PipCostume", 512
STAR_CAPE = "PipStarCape_BaseColor.png"
SOCKETS = {"PipShowmanHat": "Head", "PipLanternPail": "Pail"}  # piece -> the bone of Pip's rig it rides

# Pip's rest pose (tools/blender/export/pip_export.py; she faces -Y, her left is +X where the bat clip sits)
HEAD_TOP = Vector((0.0, -0.02, 1.2))  # where the hat's brim rests on her hair
HAT_AT = T(-0.045, -0.035, 1.19) @ R("Y", -17) @ R("X", 9)  # tilted to her right and a little forward
GRIP = Vector((-0.2, -0.03, 0.445))  # the handle in her right hand (the `Pail` bone's head)
LANTERN_R, LANTERN_DROP = 0.074, 0.165  # the lantern's radius, and how far under the grip its centre hangs
LINING = H(0x2F2860)  # night violet, a little lifted so the stars' field reads against the black outside
STAR = H(0xF6D365)


def log(*a):
    print("[costume_pieces]", *a, flush=True)


# --- the hat and the lantern pail -------------------------------------------------------------------------------

def showman_hat(b, craft, M=None):
    """A small felt top hat: origin at the middle of the brim's underside, up +Z, front -Y."""
    felt, band = craft.felt(E["plum"]), craft.felt(el.V["orange"])
    paint = craft.paint()
    with b.at(M if M is not None else T()):
        b.lathe(felt, [(0.0, 0.0), (0.092, 0.0), (0.098, 0.006), (0.092, 0.013), (0.062, 0.016)], 20, tint=1.0)  # brim, curled edge
        b.lathe(felt, [(0.062, 0.012), (0.058, 0.05), (0.062, 0.1), (0.068, 0.112), (0.06, 0.119), (0.0, 0.121)], 20, tint=1.0)  # crown, flared
        b.lathe(band, [(0.0625, 0.017), (0.0635, 0.019), (0.0605, 0.047), (0.059, 0.049)], 20, tint=1.0)
        b.lathe(paint, [(0.0935, 0.002), (0.1, 0.0065), (0.0935, 0.011)], 20, tint=lc(E["candy"]))  # a candy-yellow edge on the brim
        el.crest(b, craft, 0.046, T(0, -0.0615, 0.034) @ R("X", 3), step=0.0011)


def lantern_pail(b, craft, M=None):
    """A round paper lantern on a wire bail: origin at the grip (the top of the handle)."""
    paint = craft.paint()
    r, drop = LANTERN_R, LANTERN_DROP
    n = 9
    body = [(0.02, r * 0.93)] + [(r * math.sin(math.pi * (i + 1) / (n + 1)) ** 0.7, r * 0.93 * math.cos(math.pi * (i + 1) / (n + 1))) for i in range(n)] \
        + [(0.02, -r * 0.93)]
    with b.at((M if M is not None else T()) @ T(0, 0, -drop)):
        for sz in (-1, 1):
            b.lathe(paint, [(0.0, sz * (r * 0.93 + 0.012)), (0.024, sz * (r * 0.93 + 0.012)), (0.027, sz * (r * 0.93 + 0.004)), (0.024, sz * (r * 0.93 - 0.006)),
                            (0.0, sz * (r * 0.93 - 0.006))][::sz], 10, tint=lc(E["plum"]))
        for i in range(-2, 3):  # hoops under the paper
            zz = i * r * 0.3
            rr = r * math.sin(math.acos(zz / (r * 0.93))) ** 0.7 + 0.001
            b.lathe(paint, [(rr, zz - 0.0045), (rr + 0.004, zz), (rr, zz + 0.0045)], 16, tint=lc(E["orange"]))
        b.lathe(paint, [(0.0, -r - 0.004), (0.006, -r - 0.01), (0.004, -r - 0.04), (0.0, -r - 0.045)], 5, tint=lc(E["orange"]))  # tassel
        arc = [Vector((0.03 * math.cos(math.radians(a)), 0.0, r * 0.93 + 0.012 + (drop - r * 0.93 - 0.012) * math.sin(math.radians(a)))) for a in range(0, 181, 20)]
        for p, q in zip(arc, arc[1:]):  # the wire bail up to her hand
            b.rod(paint, p, q, 0.0032, 5, lc(E["ink"]))
        b.glow_tag = "KeepsakePail"
        b.lathe(craft.glow(el.blend(E["bone"], E["glow"], 0.5), 1.0), body, 16, tint=1.0)
        b.glow_tag = None


def build_hat(craft):
    b = Builder(401)
    b.part = None
    showman_hat(b, craft, HAT_AT)
    return b.finish("PipShowmanHat")


def build_lantern_pail(craft):
    b = Builder(402)
    b.part = None
    b.glow_tag = None
    lantern_pail(b, craft, T(*GRIP))
    return b.finish("PipLanternPail")


# --- the star-lined cape ----------------------------------------------------------------------------------------

def _star_mask(s, v, rng):
    """Paper stars scattered on the (around, height) plane, in metres: True inside a star."""
    hit = np.zeros(s.shape, dtype=bool)
    cell = 0.092
    for i in range(int(np.floor(s.min() / cell)) - 1, int(np.ceil(s.max() / cell)) + 1):
        for j in range(int(np.floor(v.min() / cell)) - 1, int(np.ceil(v.max() / cell)) + 1):
            cx = (i + 0.5 + rng.uniform(-0.22, 0.22)) * cell
            cz = (j + 0.5 + rng.uniform(-0.22, 0.22)) * cell
            rad, rot = rng.uniform(0.019, 0.031), rng.uniform(0, 2 * math.pi)
            dx, dz = s - cx, v - cz
            near = dx * dx + dz * dz < rad * rad
            if not near.any():
                continue
            ang = (np.arctan2(dx[near], dz[near]) + rot) % (2 * math.pi / 5)
            ang = np.abs(ang - math.pi / 5)  # 0 on a point's axis ... pi/5 on the notch
            # a five-pointed star's edge in polar form: straight lines between the tips (rad) and the notches (0.48 rad)
            tip, notch = np.array([0.0, rad]), 0.48 * rad * np.array([math.sin(math.pi / 5), math.cos(math.pi / 5)])
            edge = notch - tip
            px, pz = np.hypot(dx[near], dz[near]) * np.sin(math.pi / 5 - ang), np.hypot(dx[near], dz[near]) * np.cos(math.pi / 5 - ang)
            inside = (px - tip[0]) * edge[1] - (pz - tip[1]) * edge[0] > 0
            idx = np.nonzero(near)
            hit[tuple(a[inside] for a in idx)] = True
    return hit


def paint_star_cape(pip_dir, out_path):
    """Writes Pip's atlas with the cape lining repainted. Needs the scene to hold the imported `Pip_Cape`."""
    cape = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Pip_Cape"))
    img = bpy.data.images.load(os.path.join(pip_dir, "Pip_BaseColor.png"))
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[:, :, :3]
    me = cape.data
    me.calc_loop_triangles()
    uv = np.array([d.uv[:] for d in me.uv_layers.active.data], dtype=np.float64)
    co = np.array([(cape.matrix_world @ v.co)[:] for v in me.vertices], dtype=np.float64)

    def berry(c):  # the lining's baked colour: clearly red over green and blue
        return (c[..., 0] > 0.16) & (c[..., 0] > 1.7 * c[..., 1]) & (c[..., 0] > 1.15 * c[..., 2]) & (c[..., 2] > c[..., 1])
    target = np.zeros((h, w), dtype=bool)
    pos = np.zeros((h, w, 3), dtype=np.float32)
    lining = []
    for tri in me.loop_triangles:
        t = uv[list(tri.loops)] * (w, h)
        centre = t.mean(axis=0)
        if berry(rgb[int(min(centre[1], h - 1)), int(min(centre[0], w - 1))]):
            lining.append((t, co[list(tri.vertices)]))
    faces = len(lining)
    for gutter in (False, True):  # the triangles' own texels first, then about two texels of gutter round each
        for t, p in lining:
            d = (t[1, 1] - t[2, 1]) * (t[0, 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (t[0, 1] - t[2, 1])
            if abs(d) < 1e-9:
                continue
            x0, x1 = int(max(0, np.floor(t[:, 0].min()) - 3)), int(min(w - 1, np.ceil(t[:, 0].max()) + 3))
            y0, y1 = int(max(0, np.floor(t[:, 1].min()) - 3)), int(min(h - 1, np.ceil(t[:, 1].max()) + 3))
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            a = ((t[1, 1] - t[2, 1]) * (gx - t[2, 0]) + (t[2, 0] - t[1, 0]) * (gy - t[2, 1])) / d
            bb = ((t[2, 1] - t[0, 1]) * (gx - t[2, 0]) + (t[0, 0] - t[2, 0]) * (gy - t[2, 1])) / d
            c = 1.0 - a - bb
            eps = 2.5 / max(math.sqrt(abs(d)), 1e-6) if gutter else 0.0
            inside = (a >= -eps) & (bb >= -eps) & (c >= -eps)
            if gutter:
                inside &= ~target[y0:y1 + 1, x0:x1 + 1]
            world = a[..., None] * p[0] + bb[..., None] * p[1] + c[..., None] * p[2]
            pos[y0:y1 + 1, x0:x1 + 1][inside] = world[inside]
            target[y0:y1 + 1, x0:x1 + 1] |= inside
    target &= berry(rgb)  # only texels that were lining (the island's own texels and its dilated gutter)
    ys, xs = np.nonzero(target)
    p = pos[ys, xs]
    around = np.arctan2(p[:, 0], p[:, 1] + 0.05) * 0.34  # arc length round her back, as the cape hangs
    stars = _star_mask(around, p[:, 2], np.random.default_rng(7))
    old = rgb[ys, xs]
    lum = old @ np.array([0.5, 0.3, 0.2], dtype=np.float32)
    shade = np.clip(lum / max(float(np.median(lum)), 1e-4), 0.55, 1.3)[:, None]  # the baked folds and felt grain
    lining, star = np.array(el.lin(LINING)[:3]), np.array(el.lin(STAR)[:3])
    srgb = lambda c: np.where(c <= 0.0031308, c * 12.92, 1.055 * np.clip(c, 1e-6, None) ** (1 / 2.4) - 0.055)  # noqa: E731
    new = np.where(stars[:, None], srgb(star) * (0.85 + 0.15 * shade), srgb(lining) * shade)
    rgb[ys, xs] = np.clip(new, 0.0, 1.0)
    px[:, :, 3] = 1.0
    out = bpy.data.images.new("StarCape", w, h, alpha=False)
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw, out.file_format = out_path, "PNG"
    out.save()
    log(f"star cape: {faces} lining triangles, {len(ys)} texels ({100.0 * len(ys) / (w * h):.1f}% of the atlas), {int(stars.sum())} star texels -> {out_path}")
    return len(ys), int(stars.sum())


# --- export -----------------------------------------------------------------------------------------------------

def export(out_dir, samples):
    import env_export as ex
    import pip_review as pr
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pr.import_pip(PIP_DIR)
    os.makedirs(out_dir, exist_ok=True)
    texels, stars = paint_star_cape(PIP_DIR, os.path.join(out_dir, STAR_CAPE))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ex.install_hooks()
    global Builder
    Builder = ex.GameBuilder
    ex.model_list = lambda craft: [
        ex.Model("PipShowmanHat", ATLAS, build_hat, 1.0, ground=False),
        ex.Model("PipLanternPail", ATLAS, build_lantern_pail, 1.0, ground=False),
    ]
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft)
    ex.unwrap_and_pack(models, ATLAS, ATLAS_SIZE)
    ex.bake_atlas(models, ATLAS, ATLAS_SIZE, samples, out_dir)
    materials = {name: ex.plain_material(name) for name in (ATLAS, "Glow")}
    manifest = {"star_cape": {"file": STAR_CAPE, "lining_texels": texels, "star_texels": stars}}
    for m in models:
        manifest[m.name] = ex.export_model(m, materials, out_dir)
        manifest[m.name].update(px_per_m=round(m.density), socket=SOCKETS[m.name])
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    with open(os.path.join(HERE, "costume_manifest.json"), "w") as fh:
        json.dump(manifest, fh, indent=1)


# --- review (from the exported files, on the re-imported Pip) ----------------------------------------------------

def dress(folder):
    """Pip re-imported in her rest pose with the exported pieces riding their bones, as the game attaches them."""
    import env_game_review as gr
    import pip_review as pr
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig, meshes, _sizes = pr.import_pip(PIP_DIR)
    actions = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    star = bpy.data.images.load(os.path.join(folder, STAR_CAPE))
    plain = {}
    for o in meshes:  # every atlas mesh shares the texture in game: swap it on all of them
        if "Face" in o.name:
            continue
        for node in o.data.materials[0].node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and node.image.name.startswith("Pip_BaseColor"):
                plain[node] = node.image
    pail = next(o for o in meshes if o.name.startswith("Pip_Pail"))
    pieces = {}
    rig.animation_data_create()
    rig.animation_data.action = None  # the rest pose: where the pieces were authored
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    for name, bone in SOCKETS.items():
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, name + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        tris = 0
        for o in new:
            if o.type != "MESH":
                continue
            tris += len(o.data.polygons)
            o.data.materials.clear()
            glow = o.name.split(".")[0].endswith("_Glow")
            o.data.materials.append(gr.atlas_material(folder, ATLAS, 0.9 if glow else 0.0))
        log(f"imported {name}: {tris} tris, {sorted(o.name for o in new)}")
        root = next(o for o in new if o.parent is None)
        pieces[name] = (root, new)
        # ride the bone from the rest pose, keeping the world transform (as the game attaches it)
        world = root.matrix_world.copy()
        root.parent, root.parent_type, root.parent_bone = rig, "BONE", bone
        bpy.context.view_layer.update()
        root.matrix_world = world
    return rig, actions, star, plain, pail, pieces


def review(out, quality, folder):
    import costume as co
    rig, actions, star, plain, pail, pieces = dress(folder)
    cam = co.stage()
    scene = bpy.context.scene
    final = quality == "final"
    scene.eevee.taa_render_samples = 40 if final else 10
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]

    def set_pose(clip, frame):
        rig.animation_data_create()
        rig.animation_data.action = actions[clip] if clip else None
        if clip is None:
            for pb in rig.pose.bones:
                pb.matrix_basis.identity()
        scene.frame_set(int(actions[clip].frame_range[0]) + frame if clip else 0)

    def wear(cape, lantern, hat):
        for node, img in plain.items():
            node.image = star if cape else img
        pail.hide_render = lantern
        for name, on in (("PipLanternPail", lantern), ("PipShowmanHat", hat)):
            for o in pieces[name][1]:
                o.hide_render = not on

    def shot(clip, frame, yaw, pitch, look, dist, w, h, tag, lens=85):
        set_pose(clip, frame)
        a, e = math.radians(yaw), math.radians(pitch)
        target = Vector((0, 0, look))
        loc = target + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        return co.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{tag}.png")

    rows = []
    # each piece alone: front, side, and in the glide; then all three together; the last tile is Pip as she is today
    looks = (("cape", (True, False, False)), ("pail", (False, True, False)), ("hat", (False, False, True)), ("all", (True, True, True)))
    for name, on in looks:
        wear(*on)
        tiles = [shot(None, 0, 0, 6, 0.64, 4.0, 330, 480, name + "f"), shot(None, 0, 90, 6, 0.64, 4.0, 330, 480, name + "s"),
                 shot(None, 0, 155, 6, 0.64, 4.0, 330, 480, name + "b"), shot("Glide", 8, 0, 10, 0.75, 4.4, 480, 480, name + "g"),
                 shot("Glide", 0, 60, 14, 0.75, 4.4, 480, 480, name + "u"), shot("Fall", 0, 40, 4, 0.7, 4.4, 330, 480, name + "r")]
        rows.append(np.concatenate(tiles, axis=1))
    wear(True, True, True)
    close = [shot(None, 0, 20, 8, 1.12, 1.5, 640, 520, "c1"), shot(None, 0, -35, 0, 0.36, 1.3, 640, 520, "c2"),
             shot("Glide", 8, 0, 3, 0.8, 2.6, 640, 520, "c3")]
    wear(False, False, False)
    close.append(shot(None, 0, 0, 6, 0.64, 4.0, 358, 520, "plain"))
    rows.append(np.concatenate(close, axis=1))
    width = max(r.shape[1] for r in rows)
    co.save_sheet(np.concatenate([el.pad(r, width, r.shape[0]) for r in rows][::-1], axis=0), out)
    log("sheet ->", out)


# --- the trunk's picture cards (S004 A2): one icon per piece, from the exported files ----------------------------

ICON_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "UI", "CostumeIcons")
ICONS = ("star_cape", "lantern_pail", "showman_hat")  # file costume_icon_<name>.png, the piece ID's last part


def icons(out_dir, size, folder, cape=(10, -60, 1.7, 50)):
    """Transparent square pictures for the dress-up trunk's cards: the cape's star lining, the lantern, the hat."""
    import costume as co
    rig, actions, star, plain, pail, pieces = dress(folder)
    cam = co.stage()
    scene = bpy.context.scene
    scene.render.film_transparent = True
    scene.eevee.taa_render_samples = 48
    for o in bpy.data.objects:
        if o.name.startswith("Ground"):
            o.hide_render = True
    body = [o for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("Ground")
            and not any(o in objs for _r, objs in pieces.values())]
    os.makedirs(out_dir, exist_ok=True)

    def show(pip, lantern, hat, cape):
        for o in body:
            o.hide_render = not pip
        pail.hide_render = True if not pip else lantern
        for name, on in (("PipLanternPail", lantern), ("PipShowmanHat", hat)):
            for o in pieces[name][1]:
                o.hide_render = not on
        for node, img in plain.items():
            node.image = star if cape else img

    def centre(objs):
        pts = [o.matrix_world @ Vector(c) for o in objs if o.type == "MESH" for c in o.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        return (lo + hi) / 2, (hi - lo).length

    def shot(name, target, dist, yaw, pitch, lens):
        a, e = math.radians(yaw), math.radians(pitch)
        loc = target + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        px = co.render_view(cam, loc, target, lens, size, size, os.path.join(out_dir, "_tmp_" + name + ".png"))
        co.save_sheet(px, os.path.join(out_dir, "costume_icon_" + name + ".png"))
        log("icon ->", name)

    rig.animation_data_create()
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    scene.frame_set(0)
    bpy.context.view_layer.update()
    show(False, False, True, False)
    at, span = centre(pieces["PipShowmanHat"][1])
    shot("showman_hat", at, span * 3.2, 20, 18, 85)
    show(False, True, False, False)
    at, span = centre(pieces["PipLanternPail"][1])
    shot("lantern_pail", at, span * 2.5, 20, 10, 85)
    # The cape: Pip gliding, seen from in front and below, so the star lining under her arms fills the card.
    show(True, False, False, True)
    pail.hide_render = True  # the card is about the cape
    rig.animation_data.action = actions["Glide"]
    scene.frame_set(int(actions["Glide"].frame_range[0]) + 8)
    bpy.context.view_layer.update()
    yaw, pitch, dist, lens = cape
    shot("star_cape", Vector((0, 0, 0.8)), dist, yaw, pitch, lens)


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    if flags and flags[0] == "icons":
        cape = tuple(float(v) for v in opts["cape"].split(",")) if "cape" in opts else (10, -60, 1.7, 50)
        icons(flags[1] if len(flags) > 1 else ICON_DIR, int(opts.get("size", 256)), opts.get("dir", OUT_DIR), cape)
    elif flags and flags[0] == "review":
        review(flags[1], flags[2] if len(flags) > 2 else "draft", opts.get("dir", OUT_DIR))
    else:
        export(flags[1] if len(flags) > 1 else OUT_DIR, int(opts.get("samples", 4)))


if __name__ == "__main__":
    main()
