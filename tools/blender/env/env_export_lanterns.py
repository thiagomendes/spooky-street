"""Game export of the paper lantern table's art (`lanterns.py`; living-street §4.2, D34, D35). Sibling of
`env_export_keepsakes.py`, built on `env_export.py`: the same `GameBuilder`, UV packing, Cycles bake, pivots,
`*_Glow` naming and FBX settings. Nothing those scripts write is touched.

Models (next to the other environment models, `Resources/Models/Environment/`):
  PaperLanternBat / PaperLanternCat / PaperLanternGhost / PaperLanternLeaf   root = ink, wire, tassel;
      child `PaperLantern_Glow` = the neutral lit paper the game tints to the chosen colour
  LanternTable    the folding craft table; child `PaperLanternSamples_Glow` = its string of finished lanterns
  LanternHook     Pip's porch hook; children `LanternHang` (pivot: where a lantern's ring hangs) and `BellButton`
Textures: Lanterns_BaseColor/_Normal/_Emission.png, one shared 1024 atlas (load with `ModelKit.FallbackAtlas =
"Lanterns"`).

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_lanterns.py -- [out_dir] [options]
  count  samples=4  size=1024  only=A,B  manifest=file.json
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy

import env_export as ex
import lanterns as ln
from env_export import Model, log

ATLAS = "Lanterns"
SIZE = 1024
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lanterns_manifest.json")


def lantern_models(craft):
    models = [Model(ln.MODELS[s], ATLAS, (lambda c, s=s: ln.build_lantern(c, s)), 2.2, ground=False, glow=ln.GLOW_TAG)
              for s in ln.SHAPES]
    models.append(Model(ln.TABLE, ATLAS, ln.build_table, 1.0, glow=ln.SAMPLES_TAG))
    models.append(Model(ln.HOOK, ATLAS, ln.build_hook, 1.1, pivots=ln.hook_pivots()))
    return models


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
    ln.Builder = ex.GameBuilder
    ex.model_list = lantern_models
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    ex.unwrap_and_pack(models, ATLAS, size, shape_method="CONCAVE")
    ex.bake_atlas(models, ATLAS, size, samples, out_dir)
    materials = {name: ex.plain_material(name) for name in (ATLAS, "Glow")}
    manifest = {}
    for m in models:
        manifest[m.name] = ex.export_model(m, materials, out_dir)
        manifest[m.name]["px_per_m"] = round(getattr(m, "density", 0.0))
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    with open(opts.get("manifest", MANIFEST), "w") as fh:
        json.dump(manifest, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
