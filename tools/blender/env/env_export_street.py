"""Game export of the street dressing (`street.py`): the ground set, the long boundary hedge and the three
skyline neighbour houses. Sibling of `env_export.py` and built on it: the same `GameBuilder`, hidden-face
culling, UV packing, Cycles bake, pivots (base centre, facing -Y) and FBX settings.

Textures (next to the FBX files; nothing that `env_export.py` writes is touched):
  Lawn_BaseColor/_Normal/_Emission.png    the lawn tile alone, one square UV island so the texture itself tiles
  Street_BaseColor/_Normal/_Emission.png  shared atlas for every other model here

Glow parts: `NeighbourWindow_Glow` (lit windows, baked as dim amber) and `Face_Glow` (jack-o'-lantern faces).

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_street.py -- [out_dir] [options]
  count        only build and print triangle counts
  size=2048    Street atlas size       lawn=1024  Lawn texture size
  samples=4    Cycles bake samples     only=A,B   build a subset (drafts only)
  manifest=f   write triangle counts, pivots and texel densities to a JSON file
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import env_export as ex
import street as st
from env_export import Model, log

FAR = dict(min_size=0.03, min_bevel=0.05)  # skyline houses: no bevels, no trim under 3 cm


def street_models(craft):
    return [
        Model("Lawn", "Lawn", st.build_lawn, 1.0),
        Model("PathStones", "Street", st.build_path, 3.6),
        Model("Sidewalk", "Street", st.build_sidewalk, 3.2),
        Model("LeafScatterA", "Street", lambda c: st.build_leaf_scatter(c, "A"), 4.0),
        Model("LeafScatterB", "Street", lambda c: st.build_leaf_scatter(c, "B"), 4.0),
        Model("LeafScatterC", "Street", lambda c: st.build_leaf_scatter(c, "C"), 4.0),
        Model("BoundaryHedgeLong", "Street", st.build_hedge_long, 1.1),
        Model("NeighbourHouseA", "Street", st.build_neighbour_a, 0.72, **FAR),
        Model("NeighbourHouseB", "Street", st.build_neighbour_b, 0.72, **FAR),
        Model("NeighbourHouseC", "Street", st.build_neighbour_c, 0.72, **FAR),
    ]


def lawn_uvs(model, size):
    """One planar island over the whole square, and the exact normals of the height field: the neighbouring
    tile continues both the texture and the shading."""
    me = model.parts[None].data
    co = np.zeros(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    vi = np.zeros(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", vi)
    layer = me.uv_layers.new(name="UVMap") if not me.uv_layers else me.uv_layers.active
    layer.data.foreach_set("uv", (co[vi][:, :2] / st.LAWN + 0.5).ravel())
    me.normals_split_custom_set_from_vertices([st.lawn_normal(float(x), float(y))[:] for x, y, _z in co])
    model.density = size / st.LAWN


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    size, lawn, samples = int(opts.get("size", 2048)), int(opts.get("lawn", 1024)), int(opts.get("samples", 4))
    only = set(opts["only"].split(",")) if "only" in opts else None
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ex.install_hooks()
    st.Builder = ex.GameBuilder
    ex.model_list = street_models
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    for m in models:
        if m.atlas == "Lawn":
            lawn_uvs(m, lawn)
            ex.bake_atlas(models, "Lawn", lawn, samples, out_dir)
    ex.unwrap_and_pack(models, "Street", size)
    ex.bake_atlas(models, "Street", size, samples, out_dir)
    materials = {name: ex.plain_material(name) for name in ("Lawn", "Street", "Glow")}
    manifest = {}
    for m in models:
        manifest[m.name] = ex.export_model(m, materials, out_dir)
        manifest[m.name]["px_per_m"] = round(getattr(m, "density", 0.0))
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    side = opts.get("manifest")
    if side:
        with open(side, "w") as fh:
            json.dump(manifest, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
