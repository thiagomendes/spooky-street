"""Review renders of the east lane's three porch houses, from the EXPORTED FBX files and the committed atlas (the
files the game loads), in an approximation of the game's night light: the trilight ambient (sky, equator and ground
colours of `Atmosphere.cs`), the cool moon from the north at the game's intensity, no porch point lights (the lane
has none), glow parts at the game's levels (`GlowLevels`, the runtime's porch lamp 0.9 and Nana Pearl's warm 1.7),
and the lane laid out as `LivingStreetLayout` places it (Unity x, z is Blender x, y).

    blender -b -t 2 --python tools/blender/env/lane_review.py -- <out_dir> [models=DIR] [crowd=DIR] [pip=DIR]
                                                                [shots=a,b] [samples=24] [scale=1.0]

Writes PNG tiles into <out_dir> (compose them with tools/blender/env/lane_sheet.py):
  lane.png, lane_grey.png        the lane from the hedge gap with the gameplay camera's 50 degrees (CI frame 94's view)
  door_<House>_closed/open.png   the gameplay camera behind Pip at the house's bell, door shut, then at the gag's
                                 peak swing with the neighbour(s) in the doorway
  lod_<House>.png                LOD0 and LOD1 side by side at 22 m
  leaf_<House>.png, lamps.png    the door leaves open and the three lamps close
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd", "env"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import env_export as ex  # noqa: E402
import env_lib as el  # noqa: E402
import lane_houses as lh  # noqa: E402
from env_lib import H, lin  # noqa: E402

REPO = os.path.dirname(TOOLS)
REPO = os.path.dirname(REPO)
CROWD = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Crowd")
PIPDIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Pip")

# LivingStreetLayout.Doors: house placement (Unity x, z), yaw; gag: open degrees, bounce, lamp boost
PLACE = {"Fogg": (28.6, 9.6, 0.0), "Pearl": (39.2, 10.6, 0.0), "Abbotts": (32.6, -8.6, 180.0)}
GAG = {"Fogg": (115.0, 0.18, 0.0), "Pearl": (70.0, 0.0, 0.8), "Abbotts": (95.0, 0.06, 0.0)}
# who answers (recipe parts): the neighbours' own art when committed, else the slice's placeholders
NEIGHBOURS = {"Fogg": ["Neighbour_Fogg"], "Pearl": ["Neighbour_Pearl"], "Abbotts": ["Neighbour_AbbottA", "Neighbour_AbbottB"]}
OLD_RECIPES = {
    "Neighbour_Fogg": ("CrowdAdult", ["Body_King", "Hands_T6", "Shoes_Ink", "Head_King", "Hair_Crop"]),
    "Neighbour_Pearl": ("CrowdAdult", ["Body_CatMum", "Hands_T2", "Shoes_Bone", "Head_CatMum", "Hair_Bun"]),
    "Neighbour_AbbottA": ("CrowdAdult", ["Body_Skeleton", "Hands_T4", "Shoes_Orange", "Head_King", "Hair_Short"]),
    "Neighbour_AbbottB": ("CrowdAdult", ["Body_King", "Hands_T2", "Shoes_Bone", "Head_King", "Hair_Crop"]),
}
GLOW = {"RoomCard": 1.2, "Face": 1.8, "Lamp": 0.9, "NeighbourWindow": 0.9}
AMBIENT = {"sky": H(0x4B4A86), "equator": H(0x3A2E5C), "ground": H(0x221C30)}
MOON = (H(0x8FA8FF), 0.6)
FOV = 50.0


def args():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    return flags[0], opts


def world():
    """Camera rays see the painted sky's colours; everything else is lit by the game's trilight ambient."""
    w = bpy.data.worlds.new("LaneNight")
    w.use_nodes = True
    nodes, links = w.node_tree.nodes, w.node_tree.links
    bg = nodes["Background"]
    coord = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Generated"], split.inputs[0])
    amb = nodes.new("ShaderNodeValToRGB")
    amb.color_ramp.elements[0].position, amb.color_ramp.elements[0].color = 0.0, lin(AMBIENT["ground"])
    amb.color_ramp.elements[1].position, amb.color_ramp.elements[1].color = 1.0, lin(AMBIENT["sky"])
    mid = amb.color_ramp.elements.new(0.5)
    mid.color = lin(AMBIENT["equator"])
    norm = nodes.new("ShaderNodeMapRange")  # z -1..1 -> 0..1
    norm.inputs["From Min"].default_value, norm.inputs["From Max"].default_value = -1.0, 1.0
    links.new(split.outputs["Z"], norm.inputs["Value"])
    links.new(norm.outputs["Result"], amb.inputs[0])
    sky = nodes.new("ShaderNodeValToRGB")
    sky.color_ramp.elements[0].position, sky.color_ramp.elements[0].color = 0.45, lin(H(0x7A4E98))
    sky.color_ramp.elements[1].position, sky.color_ramp.elements[1].color = 0.85, lin(H(0x3A2C66))
    links.new(norm.outputs["Result"], sky.inputs[0])
    path = nodes.new("ShaderNodeLightPath")
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    links.new(amb.outputs[0], mix.inputs[6])
    links.new(sky.outputs[0], mix.inputs[7])
    links.new(mix.outputs[2], bg.inputs[0])
    bg.inputs[1].default_value = 1.0
    bpy.context.scene.world = w


def setup(samples):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    ee = scene.eevee
    ee.taa_render_samples = samples
    ee.use_gtao, ee.gtao_distance, ee.gtao_factor = True, 0.6, 1.0
    ee.use_soft_shadows = True
    ee.shadow_cube_size, ee.shadow_cascade_size = "1024", "4096"
    ee.use_bloom, ee.bloom_intensity, ee.bloom_threshold, ee.bloom_radius = True, 0.05, 1.0, 6.0
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 0.15
    scene.render.film_transparent = False
    world()
    # the moon: Atmosphere.cs Euler(32, 160, 0) -> Unity forward (0.29, -0.53, -0.80) -> Blender (0.29, -0.80, -0.53)
    el.sun_light("Moon", (0.29, -0.80, -0.53), math.pi * MOON[1], MOON[0], 3.0, 70.0)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.clip_end = 400.0
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(FOV)
    el.pm.link(cam)
    scene.camera = cam
    return cam


def flat(name, rgb, rough=0.95):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(rgb)
    b.inputs["Roughness"].default_value = rough
    return mat


def ground():
    """Lawn, the north sidewalk, the lane's hedges (just enough of the street to judge the houses in)."""
    def box(name, mat, c, s):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=c)
        o = bpy.context.active_object
        o.name = name
        o.scale = s
        o.data.materials.append(mat)
        return o
    box("Lawn", flat("Lawn", H(0x24433F)), (34.0, 2.0, -0.05), (60, 50, 0.1))
    box("Sidewalk", flat("Slab", H(0x5A5070)), (34.0, 3.5, 0.02), (26, 2, 0.04))
    hedge = flat("Hedge", H(0x244F3A))
    box("HedgeN", hedge, (34.0, 17.5, 0.65), (26, 0.8, 1.3))
    box("HedgeS", hedge, (34.0, -13.5, 0.65), (26, 0.8, 1.3))
    box("HedgeE", hedge, (45.5, 2.0, 0.65), (0.8, 31, 1.3))


class Lib:
    def __init__(self, folder):
        self.folder, self.mats = folder, {}

    def material(self, atlas, glow=0.0):
        if (atlas, glow) not in self.mats:
            self.mats[(atlas, glow)] = atlas_material(self.folder, atlas, glow)
        return self.mats[(atlas, glow)]

    def load(self, model, parent, atlas):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(self.folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        parts = {}
        for o in new:
            if o.parent is None:
                o.parent = parent
            base = o.name.split(".")[0]
            parts[base] = o
            if o.type != "MESH":
                continue
            o.data.materials.clear()
            if base.endswith("_Glow"):
                o.data.materials.append(self.material(atlas, GLOW.get(base[:-5], 1.5)))
            else:
                o.data.materials.append(self.material(atlas))
        return parts


def atlas_material(folder, atlas, glow=0.0):
    mat = bpy.data.materials.new(f"{atlas}_{glow}")
    mat.use_nodes = True
    mat.use_backface_culling = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    base = nodes.new("ShaderNodeTexImage")
    base.image = bpy.data.images.load(os.path.join(folder, atlas + "_BaseColor.png"), check_existing=True)
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    nimg = bpy.data.images.load(os.path.join(folder, atlas + "_Normal.png"), check_existing=True)
    nimg.colorspace_settings.name = "Non-Color"
    tn = nodes.new("ShaderNodeTexImage")
    tn.image = nimg
    nm = nodes.new("ShaderNodeNormalMap")
    links.new(tn.outputs["Color"], nm.inputs["Color"])
    links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if glow:
        emit = nodes.new("ShaderNodeTexImage")
        emit.image = bpy.data.images.load(os.path.join(folder, atlas + "_Emission.png"), check_existing=True)
        links.new(emit.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = glow
    return mat


def house_frame(house):
    x, z, yaw = PLACE[house]
    e = bpy.data.objects.new("House_" + house, None)
    el.pm.link(e)
    e.location = (x, z, 0.0)
    e.rotation_euler = (0, 0, math.radians(-yaw))
    return e


def to_world(house, local):
    x, z, yaw = PLACE[house]
    a = math.radians(-yaw)
    lx, ly, lz = local
    return Vector((x + lx * math.cos(a) - ly * math.sin(a), z + lx * math.sin(a) + ly * math.cos(a), lz))


def swing_peak(house):
    open_deg, bounce, _ = GAG[house]
    return max(open_deg * ((1 - (1 - t) ** 2) + bounce * math.sin(math.pi * t)) for t in [i / 100 for i in range(101)])


def neighbour(crowd, rid):
    import witness_review as wr
    import crowd_kit as kit
    rec = getattr(kit, "NEIGHBOURS", {}).get(rid)
    body, parts = rec if rec and os.path.exists(os.path.join(crowd, rec[0] + ".fbx")) else OLD_RECIPES[rid]
    m = wr.import_model(crowd, body, leaf_bones=True)
    atlas = body if os.path.exists(os.path.join(crowd, body + "_BaseColor.png")) else "Crowd"
    mat = bpy.data.materials.get("Crowd_" + atlas)
    if mat is None:
        mat = bpy.data.materials.new("Crowd_" + atlas)
        mat.use_nodes = True
        mat.use_backface_culling = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        t = nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(os.path.join(crowd, atlas + "_BaseColor.png"))
        links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.9
    keep = []
    for o in m["meshes"]:
        if o.name.split(".")[0] in parts:
            o.data.materials.clear()
            o.data.materials.append(mat)
            keep.append(o)
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    m["meshes"] = keep
    mover = bpy.data.objects.new("Mover_" + rid, None)
    el.pm.link(mover)
    top = m["rig"]
    while top.parent is not None:
        top = top.parent
    top.parent = mover
    m["mover"] = mover
    return m


def pose(m, clip, frame):
    rig = m["rig"]
    rig.animation_data_create()
    act = m["actions"].get(clip)
    if act is not None:
        rig.animation_data.action = act
    return int(act.frame_range[0]) + frame if act else 0


def render(cam, loc, target, w, h, path, lens=None):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = w, h
    if lens:
        cam.data.sensor_fit = "AUTO"
        cam.data.lens = lens
    else:
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle_y = math.radians(FOV)
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendered", path, flush=True)


def main():
    out, opts = args()
    os.makedirs(out, exist_ok=True)
    models = opts.get("models", ex.OUT_DIR)
    crowd = opts.get("crowd", CROWD)
    shots = set(opts["shots"].split(",")) if "shots" in opts else None
    k = float(opts.get("scale", 1.0))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = setup(int(opts.get("samples", 24)))
    ground()
    lib = Lib(models)
    frames, leaves, lamps, figures = {}, {}, {}, {}
    for h in lh.HOUSES:
        frames[h] = house_frame(h)
        parts = lib.load(lh.MODEL[h], frames[h], lh.MODEL[h])
        frames[h + "_lod1"] = parts.get(lh.MODEL[h] + "_LOD1")
        frames[h + "_lod0"] = parts.get(lh.MODEL[h] + "_LOD0")
        dx, wy = lh.DOORS[h]
        hinge = bpy.data.objects.new("Hinge_" + h, None)
        el.pm.link(hinge)
        hinge.parent = frames[h]
        hinge.location = (dx - lh.DOOR_W / 2, wy - lh.HINGE_IN, lh.SILL)
        lib.load(lh.LEAF[h], hinge, lh.MODEL[h])
        leaves[h] = hinge
        lamp = bpy.data.objects.new("Lamp_" + h, None)
        el.pm.link(lamp)
        lamp.parent = frames[h]
        lamp.location = (dx + lh.LAMP_AT[0], wy + lh.LAMP_AT[2], lh.LAMP_AT[1])
        lamps[h] = lib.load(lh.LAMP[h], lamp, lh.MODEL[h])
        if frames[h + "_lod1"] is not None:
            frames[h + "_lod1"].hide_render = True
    bpy.context.view_layer.update()
    pip = None
    try:
        import witness_review as wr
        pip = wr.import_model(opts.get("pip", PIPDIR), "Pip", leaf_bones=True)
        mover = bpy.data.objects.new("Mover_Pip", None)
        el.pm.link(mover)
        top = pip["rig"]
        while top.parent is not None:
            top = top.parent
        top.parent = mover
        pip["mover"] = mover
        mover.location = (0, 0, -50)
    except Exception as exc:  # Pip is only there for scale
        print("no Pip:", exc, flush=True)

    def want(name):
        return shots is None or name in shots
    W, Hh = int(1278 * k), int(590 * k)
    if want("lane"):  # CI frame 94's camera: Unity eye (21.2, 2.4, -2.2), target (34, 1.4, 1.2)
        render(cam, (21.2, -2.2, 2.4), (34.0, 1.2, 1.4), W, Hh, os.path.join(out, "lane.png"))
    if want("lane2"):  # down the lane from the Green's corner, looking back west
        render(cam, (44.0, 1.0, 2.6), (30.0, 0.0, 2.6), W, Hh, os.path.join(out, "lane2.png"))
    for h in lh.HOUSES:
        dx, wy = lh.DOORS[h]
        stand = to_world(h, (dx, lh.STAND[h] + 0.1, 0.0))
        door = to_world(h, (dx, wy, 1.6))
        away = (stand - door)
        away.z = 0
        away.normalize()
        side = Vector((-away.y, away.x, 0))
        eye = stand + away * 3.4 + side * 1.3 + Vector((0, 0, 2.15))
        if pip is not None:
            pip["mover"].location = stand
            pip["mover"].rotation_euler = (0, 0, math.atan2(-away.y, -away.x) + math.pi / 2)
            pose(pip, "Idle", 0)
        if want("door_" + h):
            leaves[h].rotation_euler = (0, 0, 0)
            render(cam, eye, door, int(844 * k), int(390 * k), os.path.join(out, f"door_{h}_closed.png"))
        if want("open_" + h):
            leaves[h].rotation_euler = (0, 0, math.radians(-swing_peak(h)))
            boost = GAG[h][2]
            if boost:
                for o in lamps[h].values():
                    if o.type == "MESH" and o.name.startswith("Lamp_Glow"):
                        o.data.materials[0] = lib.material(lh.MODEL[h], 0.9 + boost)
            ms = []
            ids = NEIGHBOURS[h]
            for i, rid in enumerate(ids):
                m = neighbour(crowd, rid)
                across = 0.18 if len(ids) == 1 else (-0.22 if i == 0 else 0.38)
                m["mover"].location = to_world(h, (dx + across, wy - 0.3, lh.SILL))
                m["mover"].rotation_euler = (0, 0, math.radians(-PLACE[h][2]))
                f = pose(m, "Wave" if h == "Fogg" else "Talk" if h == "Pearl" else "Idle", 12 + 6 * i)
                if h == "Abbotts":
                    m["mover"].location.z += 0.1 + 0.04 * i  # the hop
                bpy.context.scene.frame_set(f)
                ms.append(m)
            render(cam, eye, door, int(844 * k), int(390 * k), os.path.join(out, f"door_{h}_open.png"))
            close = to_world(h, (dx + 0.9, wy - 2.4, 1.7))
            render(cam, close, to_world(h, (dx - 0.2, wy, 1.4)), int(560 * k), int(390 * k), os.path.join(out, f"door_{h}_near.png"))
            for m in ms:
                for o in m["meshes"] + [m["rig"]]:
                    o.hide_render = True
            leaves[h].rotation_euler = (0, 0, 0)
    if pip is not None:
        pip["mover"].location = (0, 0, -50)
    for h in lh.HOUSES:
        if not want("lod_" + h):
            continue
        x, z, yaw = PLACE[h]
        front = to_world(h, (6.0, -21.0, 3.2))
        target = to_world(h, (0.0, 0.0, 3.2))
        tiles = []

        def top(o):
            while o.parent is not None:
                o = o.parent
            return o
        others = [o for o in bpy.data.objects if top(o).name.startswith("House_") and top(o) is not frames[h]]
        hidden = [o for o in others if not o.hide_render]
        for o in hidden:  # the house alone, so no neighbour stands between it and the camera
            o.hide_render = True
        for level in (0, 1):
            frames[h + "_lod0"].hide_render = level == 1
            frames[h + "_lod1"].hide_render = level == 0
            p = os.path.join(out, f"lod_{h}_{level}.png")
            render(cam, front, target, int(520 * k), int(420 * k), p, lens=85)
            tiles.append(p)
        frames[h + "_lod0"].hide_render = False
        frames[h + "_lod1"].hide_render = True
        for o in hidden:
            o.hide_render = False
    if want("leaves"):  # the open leaf's face, from the hinge side
        for h in lh.HOUSES:
            leaves[h].rotation_euler = (0, 0, math.radians(-GAG[h][0] * 0.6))
            dx, wy = lh.DOORS[h]
            render(cam, to_world(h, (dx - 1.9, wy - 2.3, 1.45)), to_world(h, (dx - 0.3, wy - 0.6, 1.2)), int(420 * k), int(520 * k),
                   os.path.join(out, f"leaf_{h}.png"), lens=55)
            leaves[h].rotation_euler = (0, 0, 0)
    print("done", flush=True)


if __name__ == "__main__":
    main()
