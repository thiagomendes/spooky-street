"""Game export of the east lane's three trick-or-treat houses (`lane_houses.py`; living-street §4.1, §7, §8).
Sibling of `env_export_lanterns.py`, built on `env_export.py`: the same `GameBuilder`, hidden-face culling, UV
packing, Cycles bake, pivots, `*_Glow` naming and FBX settings. Nothing the other export scripts write is touched.

Models (next to the other environment models, `Resources/Models/Environment/`):
  LaneHouseFogg / LaneHousePearl / LaneHouseAbbotts
      empty root at the ground under the placement point, front toward -Z; children `<Model>_LOD0` (near) and
      `<Model>_LOD1` (far: Unity builds the LOD group from these names), and the lit parts shared by both levels:
      `RoomCard_Glow` (lit rooms in the windows and the hall behind the door) and `Face_Glow` (jack-o'-lanterns).
  LaneDoorFogg / LaneDoorPearl / LaneDoorAbbotts      a door leaf; origin on its hinge edge at the sill
  LanePorchLampFogg / ...Pearl / ...Abbotts           a porch lamp; origin in its glass; child `Lamp_Glow`
Textures: LaneHouses_BaseColor/_Normal/_Emission.png, one shared 2048 atlas (load with `ModelKit.FallbackAtlas =
"LaneHouses"`).

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_lane.py -- [out_dir] [options]
  count  samples=4  size=2048  only=A,B  manifest=file.json
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy

import env_export as ex
import lane_houses as lh
from env_export import SKIP, Model, glow_strength, log

ATLAS = "LaneHouses"
SIZE = 2048
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lane_manifest.json")
FAR = dict(min_size=0.03, min_bevel=0.05)
FAR_SUFFIX = "_far"


def _at_lod(fn, level):
    def build(craft):
        lh.LOD[0] = level
        try:
            fn(craft)
        finally:
            lh.LOD[0] = 0
    return build


def _no_glow(b, mat, lo, hi, pts):
    """The far mesh shares the near mesh's lit parts."""
    return SKIP if glow_strength(mat) >= 1.0 else None


def lane_models(craft):
    models = []
    for h in lh.HOUSES:
        models.append(Model(lh.MODEL[h], ATLAS, _at_lod(lh.BUILDERS[h], 0), 1.0, min_size=0.004))
        models.append(Model(lh.MODEL[h] + FAR_SUFFIX, ATLAS, _at_lod(lh.BUILDERS[h], 1), 0.3, classify=_no_glow, **FAR))
    for h in lh.HOUSES:
        models.append(Model(lh.LEAF[h], ATLAS, lh.LEAF_BUILDERS[h], 1.9, ground=False))
        models.append(Model(lh.LAMP[h], ATLAS, (lambda c, h=h: lh.build_lamp(c, h)), 2.0, ground=False, glow="Lamp"))
    return models


def merge_lods(models):
    """Each house becomes an empty root with `<Model>_LOD0` and `<Model>_LOD1` children (plus its lit parts)."""
    by_name = {m.name: m for m in models}
    out = []
    for m in models:
        if m.name.endswith(FAR_SUFFIX):
            continue
        far = by_name.get(m.name + FAR_SUFFIX)
        if far is not None:
            m.parts[m.name + lh.LOD_SUFFIX[0]] = m.parts.pop(None)
            m.parts[m.name + lh.LOD_SUFFIX[1]] = far.parts[None]
            m.tris[m.name + lh.LOD_SUFFIX[1]] = far.tris[None]
            m.far_density = getattr(far, "density", 0.0)
        out.append(m)
    return out


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    samples = int(opts.get("samples", 4))
    size = int(opts.get("size", SIZE))
    only = set(opts["only"].split(",")) if "only" in opts else None
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ex.install_hooks()
    lh.Builder = ex.GameBuilder
    ex.model_list = lane_models
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    ex.unwrap_and_pack(models, ATLAS, size)
    ex.bake_atlas(models, ATLAS, size, samples, out_dir)
    models = merge_lods(models)
    materials = {name: ex.plain_material(name) for name in (ATLAS, "Glow")}
    manifest = {}
    for m in models:
        manifest[m.name] = ex.export_model(m, materials, out_dir)
        manifest[m.name]["px_per_m"] = round(getattr(m, "density", 0.0))
        if hasattr(m, "far_density"):
            manifest[m.name]["px_per_m_lod1"] = round(m.far_density)
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    with open(opts.get("manifest", MANIFEST), "w") as fh:
        json.dump(manifest, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
