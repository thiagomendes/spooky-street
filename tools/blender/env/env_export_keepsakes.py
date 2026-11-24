"""Game export of the S004 seeds and keepsakes (`interior.build_troupe_photo`, `owl_house.build_crest_sign`,
`keepsakes.py`). Sibling of `env_export_interior.py`, built on `env_export.py`: the same `GameBuilder`, UV
packing, Cycles bake, pivots, `*_Glow` naming and FBX settings. Nothing that those scripts write is touched.

Textures (next to the FBX files):
  TroupePhoto_BaseColor/_Normal/_Emission.png   the troupe photo alone (per-model convention), 1024
  OwlCrest_BaseColor/_Normal/_Emission.png      the booth's crest sign alone (per-model convention), 512
  Keepsakes_BaseColor/_Normal/_Emission.png     shared atlas of PaperBatKeepsake, BatMobile and DressUpTrunk
                                                (load with `ModelKit.FallbackAtlas = "Keepsakes"`), 1024

The photo and the crest are their own models, not parts of `WorkshopMechanism` and `OwlHouse`, so that they get
the texels a close-up needs (the mechanism atlas has 214 px/m, the house atlas 69 px/m) and so that the crest can
be hung on the Players' other machines later.

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_keepsakes.py -- [out_dir] [options]
  count  samples=4  only=A,B  manifest=file.json
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy

import env_export as ex
import interior as it
import keepsakes as ks
import owl_house as oh
from env_export import Model, log

ATLASES = (("TroupePhoto", 1024), ("OwlCrest", 512), ("Keepsakes", 1024))


def _wall(half=2.0, y=0.0005):
    """The wall behind a hung model, for the visibility test only: the backs of its paper layers are never seen."""
    return [(-half, y, -half), (half, y, -half), (half, y, half), (-half, y, half)], [(0, 1, 2, 3)]


def keepsake_models(craft):
    return [
        Model("TroupePhoto", "TroupePhoto", it.build_troupe_photo, 1.0, pivots=it.photo_pivots(), ground=False, blockers=_wall()),
        Model("OwlCrest", "OwlCrest", oh.build_crest_sign, 1.0, ground=False),
        Model("PaperBatKeepsake", "Keepsakes", ks.build_keepsake_bat, 3.2, pivots=ks.bat_pivots(), ground=False),
        Model("BatMobile", "Keepsakes", ks.build_bat_mobile, 1.5, pivots=ks.mobile_pivots(), ground=False),
        Model("DressUpTrunk", "Keepsakes", ks.build_trunk, 1.0, pivots=ks.trunk_pivots()),
    ]


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    samples = int(opts.get("samples", 4))
    only = set(opts["only"].split(",")) if "only" in opts else None
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ex.install_hooks()
    it.Builder = ks.Builder = oh.Builder = ex.GameBuilder
    ex.model_list = keepsake_models
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    for atlas, size in ATLASES:
        ex.unwrap_and_pack(models, atlas, size)
        ex.bake_atlas(models, atlas, size, samples, out_dir)
    materials = {name: ex.plain_material(name) for name in [a for a, _s in ATLASES] + ["Glow"]}
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
