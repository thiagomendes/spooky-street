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
Textures: one atlas per house, shared by the house's two levels of detail, its door leaf and its lamp, named after
the house: `LaneHouseFogg_BaseColor.png` (2048), `_Normal.png` (1024) and `_Emission.png` (512). The house finds them
by its own name (ModelKit's convention); its leaf and lamp load with `ModelKit.FallbackAtlas = "LaneHouseFogg"`.
One 2048 atlas for all three houses gave about 35 px/m; one per house gives about the owl house's 69.

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_lane.py -- [out_dir] [options]
  count  samples=4  size=2048  only=A,B  manifest=file.json
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import math

import bpy

import env_export as ex
import lane_houses as lh
from env_export import SKIP, Model, glow_strength, log

SIZE = 2048
NORMAL_SIZE, EMISSION_SIZE = 1024, 512
MANIFEST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lane_manifest.json")
FAR = dict(min_size=0.03, min_bevel=0.05)
FAR_SUFFIX = "_far"
ROOF = "Roof"
ROOF_WEIGHT = 0.55  # roofs are seen from below and from afar: fewer texels than the walls and porches


def _at_lod(fn, level):
    def build(craft):
        lh.LOD[0] = level
        lh.ROOF_PART = ROOF if level == 0 else None
        try:
            fn(craft)
        finally:
            lh.LOD[0] = 0
            lh.ROOF_PART = None
    return build


def _no_glow(b, mat, lo, hi, pts):
    """The far mesh shares the near mesh's lit parts."""
    return SKIP if glow_strength(mat) >= 1.0 else None


def hall_blockers(house):
    """The lit hall behind the door, closed for the visibility test only: its walls are lit cards (not occluders),
    and without them every inside face of the house would count as seen through the doorway."""
    dx, wy = lh.DOORS[house]
    x0, x1 = dx - lh.OPEN_W / 2 - 0.05, dx + lh.OPEN_W / 2 + 0.05
    y0, y1 = wy + 0.1, wy + lh.HALL + 0.02
    z0, z1 = lh.SILL - 0.01, lh.SILL + lh.OPEN_H + 0.05
    quads = [[(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)],
             [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)],
             [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
             [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)],
             [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)]]
    return [p for q in quads for p in q], [tuple(range(4 * i, 4 * i + 4)) for i in range(len(quads))]


def lane_models(craft):
    models = []
    for h in lh.HOUSES:
        # the roof is its own static part while UVs are made (its own texel weight), joined back before export
        atlas = lh.MODEL[h]
        models.append(Model(lh.MODEL[h], atlas, _at_lod(lh.BUILDERS[h], 0), 1.0, min_size=0.004, finish_parts={"~": ROOF},
                            blockers=hall_blockers(h)))
        models.append(Model(lh.MODEL[h] + FAR_SUFFIX, atlas, _at_lod(lh.BUILDERS[h], 1), 0.16, classify=_no_glow,
                            blockers=hall_blockers(h), **FAR))
    for h in lh.HOUSES:
        models.append(Model(lh.LEAF[h], lh.MODEL[h], lh.LEAF_BUILDERS[h], 1.9, ground=False))
        models.append(Model(lh.LAMP[h], lh.MODEL[h], (lambda c, h=h: lh.build_lamp(c, h)), 2.0, ground=False, glow="Lamp"))
    return models


def unwrap_and_pack(models, size, atlas):
    """`env_export.unwrap_and_pack` with a texel weight per part (the roofs' `ROOF_WEIGHT`) and concave packing."""
    objs = [(m, p, o) for m in models if m.atlas == atlas for p, o in m.parts.items()]
    if not objs:
        return
    for m, part, o in objs:
        ex._select([o])
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.0, area_weight=0.0, correct_aspect=True,
                                 scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        uv, a3, a2 = ex._uv_and_area(o.data)
        w = m.weight * (ROOF_WEIGHT if part == ROOF else 1.0)
        k = w * math.sqrt(max(a3.sum(), 1e-9) / max(a2.sum(), 1e-12)) * 0.01
        o.data.uv_layers.active.data.foreach_set("uv", (uv * k).ravel())
    ex._select([o for _m, _p, o in objs])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(udim_source="CLOSEST_UDIM", rotate=True, rotate_method="CARDINAL", scale=True, merge_overlap=False,
                            margin_method="FRACTION", margin=1.0 / size, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    used = area = 0.0
    for m, part, o in objs:
        _uv, a3, a2 = ex._uv_and_area(o.data)
        used += a2.sum()
        area += a3.sum()
        d = math.sqrt(a2.sum() / max(a3.sum(), 1e-9)) * size
        if part in (None, ROOF):
            setattr(m, "density" if part is None else "roof_density", d)
        log(f"  {m.name}/{part or 'main'}: {d:.0f} px/m, {a3.sum():.0f} m2")
    log(f"{atlas}: fill {used * 100:.0f}%, surface {area:.0f} m2")


def merge_lods(models):
    """Each house becomes an empty root with `<Model>_LOD0` and `<Model>_LOD1` children (plus its lit parts)."""
    by_name = {m.name: m for m in models}
    out = []
    for m in models:
        if m.name.endswith(FAR_SUFFIX):
            continue
        if ROOF in m.parts:  # the roof back into the near mesh
            roof = m.parts.pop(ROOF)
            ex._select([m.parts[None], roof])
            bpy.context.view_layer.objects.active = m.parts[None]
            bpy.ops.object.join()
            m.tris[None] = m.tris.get(None, 0) + m.tris.pop(ROOF, 0)
        far = by_name.get(m.name + FAR_SUFFIX)
        if far is not None:
            m.parts[m.name + lh.LOD_SUFFIX[0]] = m.parts.pop(None)
            m.parts[m.name + lh.LOD_SUFFIX[1]] = far.parts[None]
            m.tris[m.name + lh.LOD_SUFFIX[1]] = far.tris[None]
            m.far_density = getattr(far, "density", 0.0)
        out.append(m)
    return out


def shrink(out_dir, atlas, size):
    """The normal map at half size and the emission mask (lit windows and lamps only) at a quarter."""
    for kind, target in (("Normal", NORMAL_SIZE), ("Emission", EMISSION_SIZE)):
        target = min(target, size)
        path = os.path.join(out_dir, f"{atlas}_{kind}.png")
        img = bpy.data.images.load(path)
        if kind == "Normal":
            img.colorspace_settings.name = "Non-Color"
        if img.size[0] != target:
            img.scale(target, target)
            img.filepath_raw = path
            img.file_format = "PNG"
            img.save()
        bpy.data.images.remove(img)


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
    atlases = sorted({m.atlas for m in models})
    for atlas in atlases:
        unwrap_and_pack(models, size, atlas)
        ex.bake_atlas(models, atlas, size, samples, out_dir)
        shrink(out_dir, atlas, size)
    models = merge_lods(models)
    materials = {name: ex.plain_material(name) for name in atlases + ["Glow"]}
    manifest = {}
    for m in models:
        manifest[m.name] = ex.export_model(m, materials, out_dir)
        manifest[m.name]["px_per_m"] = round(getattr(m, "density", 0.0))
        if hasattr(m, "far_density"):
            manifest[m.name]["px_per_m_roof"] = round(getattr(m, "roof_density", 0.0))
            manifest[m.name]["px_per_m_lod1"] = round(m.far_density)
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    with open(opts.get("manifest", MANIFEST), "w") as fh:
        json.dump(manifest, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
