"""Game export of the maple (`maple.py`). Sibling of `env_export.py` and built on it: the same `GameBuilder`,
hidden-face culling, UV packing, Cycles bake, pivot (trunk base, facing -Y) and FBX settings.

Textures (next to the FBX; nothing that `env_export.py` writes is touched):
  Maple_BaseColor/_Normal/_Emission.png  the bark and the leaf kit (ModelKit's per-model convention)

The crown is alpha-free cut geometry. Its leaves are instances of the 24 baked kit leaves (8 colours x 3 folds):
each instance copies a kit leaf's triangles and UVs, so the crown shares a small patch of the texture at a
high texel density. Leaves are paper cards and get real back faces like every other card. One mesh, one material.

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_maple.py -- [out_dir] [options]
  count        only build and print triangle counts
  size=2048    texture size            samples=4  Cycles bake samples
  manifest=f   write triangle counts and texel densities to a JSON file
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import env_export as ex
import env_lib as el
import maple as mp
from env_export import Model, log

TWIG_WEIGHT, LEAF_WEIGHT = 0.4, 1.25  # texel density relative to the trunk and limbs


def _wood(part):
    def build(craft):
        b = ex.GameBuilder(81)
        mp.add_wood(b, craft, part=part)
        b.finish("Maple")
    return build


def maple_models(craft):
    return [Model("Maple", "Maple", _wood("thick"), 1.0), Model("MapleTwigs", "Maple", _wood("thin"), TWIG_WEIGHT, ground=False),
            Model("MapleLeafKit", "Maple", mp.build_leaf_kit, LEAF_WEIGHT, ground=False)]


def instance_leaves(kit, tree):
    """The crown as one mesh: every placed leaf is a copy of its kit variant's baked triangles and UVs."""
    me = kit.data
    co = np.zeros(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    vi = np.zeros(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", vi)
    uv = np.zeros(len(me.loops) * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    tri = co.reshape(-1, 3)[vi].reshape(-1, 3, 3)  # the working meshes are triangulated
    uv = uv.reshape(-1, 3, 2)
    variant = np.rint(tri[:, :, 0].mean(axis=1) / mp.KIT_STEP).astype(int)
    tri = tri - np.array([0.0, 0.0, 1.0], dtype=np.float32)
    tri[:, :, 0] -= (variant * mp.KIT_STEP)[:, None]
    # shading normals: each half of a leaf is one curled sheet, creased on the midrib (half the vertices of
    # faceted leaves once Unity welds them)
    face = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    half = tri[:, :, 0].mean(axis=1) > 0
    nrm = np.zeros_like(tri)
    for v in np.unique(variant):
        for h in (False, True):
            sel = np.nonzero((variant == v) & (half == h))[0]
            acc = {}
            for t in sel:
                for c in range(3):
                    key = tuple(np.round(tri[t, c], 4))
                    acc[key] = acc.get(key, 0) + face[t]
            for t in sel:
                for c in range(3):
                    n = acc[tuple(np.round(tri[t, c], 4))]
                    nrm[t, c] = n / np.linalg.norm(n)
    pos, uvs, nrms = [], [], []
    for shape, colour, M in tree.leaves:
        sel = variant == colour * mp.N_SHAPES + shape
        m = np.array(M @ el.S(1.0 / mp.KIT_SIZE), dtype=np.float32)
        rot = m[:3, :3] / np.linalg.norm(m[:3, 0])
        pos.append(tri[sel] @ m[:3, :3].T + m[:3, 3])
        nrms.append(nrm[sel] @ rot.T)
        uvs.append(uv[sel])
    pos, uvs, nrms = np.concatenate(pos), np.concatenate(uvs), np.concatenate(nrms)
    n = len(pos)
    out = bpy.data.meshes.new("MapleLeaves")
    out.from_pydata(pos.reshape(-1, 3).tolist(), [], np.arange(n * 3).reshape(-1, 3).tolist())
    out.uv_layers.new(name="UVMap").data.foreach_set("uv", uvs.ravel())
    out.polygons.foreach_set("use_smooth", np.ones(n, dtype=bool))
    out.use_auto_smooth = True
    out.normals_split_custom_set(nrms.reshape(-1, 3).tolist())
    out.attributes.new("card", "INT", "FACE").data.foreach_set("value", np.ones(n, dtype=np.int32))
    out.update()
    obj = bpy.data.objects.new("MapleLeaves", out)
    el.pm.link(obj)
    return obj, n


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    size, samples = int(opts.get("size", 2048)), int(opts.get("samples", 4))
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ex.install_hooks()
    mp.Builder = ex.GameBuilder
    ex.model_list = maple_models
    craft = ex.GameCraft(1.2)
    tree = mp.grow()
    log(f"maple: {mp.stats(tree)}")
    maple, twigs, kit = ex.build_models(craft)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    # twigs unwrap to hundreds of slanted slivers: pack them by their true outlines, at any angle
    ex.unwrap_and_pack([maple, twigs, kit], "Maple", size, rotate_method="ANY", shape_method="CONCAVE", margin=2.0 / size)
    ex.bake_atlas([maple, twigs, kit], "Maple", size, samples, out_dir)
    leaves, n = instance_leaves(kit.parts[None], tree)
    maple.merge = [twigs.parts[None], leaves]
    materials = {name: ex.plain_material(name) for name in ("Maple", "Glow")}
    report = ex.export_model(maple, materials, out_dir)
    report.update(px_per_m=round(maple.density), leaf_px_per_m=round(kit.density), leaves=len(tree.leaves), leaf_triangles=n * 2)
    log(f"exported Maple.fbx: {report['triangles']} tris ({n * 2} in {len(tree.leaves)} leaves with their backs)")
    side = opts.get("manifest")
    if side:
        with open(side, "w") as fh:
            json.dump({"Maple": report}, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
