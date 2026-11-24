"""Game export of the first case's inside spaces and routes (`interior.py`, `routes.py`). Sibling of
`env_export.py` / `env_export_street.py` and built on them: the same `GameBuilder`, UV packing, Cycles bake,
pivots (base centre, facing -Y), `*_Glow` naming and FBX settings. Nothing that those scripts write is touched.

Textures (next to the FBX files):
  OwlHouseInterior_BaseColor/_Normal/_Emission.png    the interior shell alone (per-model convention)
  WorkshopMechanism_BaseColor/_Normal/_Emission.png   the mechanism alone (per-model convention)
  Routes_BaseColor/_Normal/_Emission.png              shared atlas of ShedRamp, SillBridge, TreehouseCabin, ClothSnag
                                                      (load with `ModelKit.FallbackAtlas = "Routes"`)

Hidden-face culling. The exterior rule ("can any outside viewpoint see the face?") would delete a closed room, so
the interior shell uses the same ray test with a short reach: a face stays when some ray leaves it for
`ROOM_REACH` metres without hitting the static shell (or the solid ground / loft slab behind the single-sided
walls). The other models use the fixed exterior rule unchanged.

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export_interior.py -- [out_dir] [options]
  count  size=2048 (interior and routes atlases)  mech=1024  samples=4  only=A,B  manifest=file.json
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import env_export as ex
import interior as it
import routes as rt
from env_export import Model, log

SHELL = "OwlHouseInterior"
SHELL_STATIC = (None, "Attic", "NookWall")  # parts that never move: they occlude and are culled
ROOM_REACH = 0.3
ATLASES = (("OwlHouseInterior", "size"), ("WorkshopMechanism", "mech"), ("Routes", "size"))


def interior_models(craft):
    return [
        Model(SHELL, SHELL, it.build_interior, 1.0, origin=ex.HOUSE_ORIGIN, pivots=it.interior_pivots(), ground=False),
        Model("WorkshopMechanism", "WorkshopMechanism", lambda c: it.build_mechanism(c, photo=False), 1.0, pivots=it.mechanism_pivots()),
        Model("ShedRamp", "Routes", rt.build_shed_ramp, 1.0),
        Model("SillBridge", "Routes", rt.build_sill_bridge, 1.5, ground=False),
        Model("TreehouseCabin", "Routes", rt.build_cabin, 1.3, ground=False, eyes={None: rt.CABIN_EYES}),
        Model("ClothSnag", "Routes", rt.build_cloth_snag, 5.0, ground=False),
    ]


def _solids():
    """What lies behind the single-sided shell: the ground under the hall, the nook pit, the loft slab."""
    quads = it.solid_quads()
    verts = [p for q in quads for p in q]
    return verts, [tuple(range(4 * i, 4 * i + 4)) for i in range(len(quads))]


def room_cull(builders):
    """Drop the faces of the static shell that no viewpoint inside the rooms can see (see module docstring)."""
    verts, tris = _solids()
    tris = [t for q in tris for t in ((q[0], q[1], q[2]), (q[0], q[2], q[3]))]
    for b in builders:
        base = len(verts)
        verts += b.verts
        for f, part, sheet in zip(b.faces, b.parts, b.sheets):
            if part in SHELL_STATIC and not sheet:
                tris += [tuple(base + i for i in t) for t in ex._triangles(b.verts, f)]
    bvh = BVHTree.FromPolygons(verts, tris, all_triangles=True, epsilon=0.0)
    total = dropped = 0
    for b in builders:
        keep = []
        for f, part, card in zip(b.faces, b.parts, b.cards):
            seen = card or part not in SHELL_STATIC
            if not seen:
                for tri in ex._triangles(b.verts, f):
                    p0, p1, p2 = (Vector(b.verts[i]) for i in tri)
                    n = (p1 - p0).cross(p2 - p0)
                    if n.length < 1e-10:
                        continue
                    n.normalize()
                    samples = [p + n * 0.003 for p in ex._tri_grid(p0, p1, p2)]
                    seen = any(bvh.ray_cast(o, d, ROOM_REACH)[0] is None for d in ex.DIRS if d.dot(n) >= 0.08 for o in samples)
                    if seen:
                        break
            keep.append(seen)
        total += len(keep)
        dropped += len(keep) - sum(keep)
        for name in ("faces", "cols", "mat_ids", "smooth", "parts", "cards", "sheets"):
            setattr(b, name, [x for x, k in zip(getattr(b, name), keep) if k])
    log(f"  {SHELL}: {dropped} of {total} faces never visible from inside, dropped")


def install():
    ex.install_hooks()
    it.Builder = rt.Builder = ex.GameBuilder
    ex.model_list = interior_models
    visible, meshes, done = ex.visible, ex.mesh_from_builders, set()

    def shell_visible(verts, faces, cards, occluder=None, ground=True, sheets=None):
        if ex.CTX.model == SHELL:
            return [True] * len(faces)  # already culled by `room_cull`
        return visible(verts, faces, cards, occluder, ground, sheets)

    def shell_meshes(name, builders, part, model=None):
        if ex.CTX.model == SHELL and id(builders) not in done:
            done.add(id(builders))
            room_cull(builders)
        return meshes(name, builders, part, model)
    ex.visible, ex.mesh_from_builders = shell_visible, shell_meshes


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    sizes = {"size": int(opts.get("size", 2048)), "mech": int(opts.get("mech", 1024))}
    samples = int(opts.get("samples", 4))
    only = set(opts["only"].split(",")) if "only" in opts else None
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    install()
    craft = ex.GameCraft(1.2)
    models = ex.build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    for atlas, key in ATLASES:
        ex.unwrap_and_pack(models, atlas, sizes[key])
        ex.bake_atlas(models, atlas, sizes[key], samples, out_dir)
    materials = {name: ex.plain_material(name) for name in [a for a, _k in ATLASES] + ["Glow"]}
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
