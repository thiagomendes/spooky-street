"""Adds animation takes from a fresh export to a committed character FBX, leaving everything else in it alone.

    blender -b --python tools/blender/export/fbx_add_takes.py -- <committed.fbx> <fresh.fbx> <out.fbx> Take [Take …]

Why: a clip-only change should not touch the mesh the textures were baked for. The exporters can start from a
saved scene (`--cache`), but when that scene is gone a rebuild from scratch is only the same model up to vertex
order for some characters, and not even that for Pip and Aunt Kit (their decimation does not repeat exactly, so
the rebuilt mesh has other triangles and other UVs than the committed textures fit). The skeleton, though, is
rebuilt bone for bone (pip_fbx_diff.py shows the bone table and every old clip's curves identical). So the new
clips are keyed on the rebuilt rig, and only their takes are moved over.

How: both files are read as FBX element trees (Blender's own io_scene_fbx parser). For each named take the
AnimationStack, its AnimationLayer, the AnimationCurveNodes and AnimationCurves are copied from <fresh> into
<committed>'s Objects, with their connections; the bones they drive are matched by NAME (object ids differ
between exports), and copied objects get new ids where one is already taken. The Definitions counts and the Takes
list are updated. Nothing else is edited: geometry, skin, materials, bones and the existing takes are written
back as they were read. A take that <committed> already has, or a bone it lacks, is an error.

Take names are the bare clip names ("ShadeEyes"); the rig prefix ("Bea_Rig|") is found in the file.
Check the result with pip_fbx_diff.py: <committed> against <out> (meshes identical, old clips unchanged, the
takes new) and <fresh> against <out> (the new takes' curves identical).
"""
import random
import sys

from io_scene_fbx import encode_bin, parse_fbx

ANIM = (b"AnimationStack", b"AnimationLayer", b"AnimationCurveNode", b"AnimationCurve")
SEP = b"\x00\x01"


def top(root, name):
    return next(e for e in root.elems if e.id == name)


def take_name(elem):
    """b'Kit_Rig|Idle\\x00\\x01AnimStack' -> 'Idle'."""
    return elem.props[1].split(SEP)[0].decode().split("|")[-1]


def encode(elem):
    """parse_fbx's tuples -> encode_bin's elements, property for property."""
    out = encode_bin.FBXElem(elem.id)
    add = {"B": out.add_bool, "C": out.add_char, "Z": out.add_int8, "Y": out.add_int16, "I": out.add_int32,
           "L": out.add_int64, "F": out.add_float32, "D": out.add_float64, "R": out.add_bytes, "S": out.add_string,
           "i": out.add_int32_array, "l": out.add_int64_array, "f": out.add_float32_array,
           "d": out.add_float64_array, "b": out.add_bool_array, "c": out.add_byte_array}
    for value, kind in zip(elem.props, elem.props_type):
        add[chr(kind)](value)
    out.elems.extend(encode(child) for child in elem.elems)
    return out


def main():
    committed_path, fresh_path, out_path, *takes = sys.argv[sys.argv.index("--") + 1:]
    old, version = parse_fbx.parse(committed_path)
    new, new_version = parse_fbx.parse(fresh_path)
    if version != new_version:
        raise SystemExit(f"FBX versions differ: {version} and {new_version}")
    old_objects, new_objects = top(old, b"Objects").elems, top(new, b"Objects").elems
    old_conns, new_conns = top(old, b"Connections").elems, top(new, b"Connections").elems
    used = {o.props[0] for o in old_objects}
    old_models = {o.props[1]: o.props[0] for o in old_objects if o.id == b"Model"}
    new_models = {o.props[0]: o.props[1] for o in new_objects if o.id == b"Model"}
    have = {take_name(o) for o in old_objects if o.id == b"AnimationStack"}
    stacks = {take_name(o): o for o in new_objects if o.id == b"AnimationStack"}
    by_id = {o.props[0]: o for o in new_objects}
    children = {}  # parent id -> [(connection, child id)]
    for c in new_conns:
        children.setdefault(c.props[2], []).append((c, c.props[1]))
    rng = random.Random(20261123)
    added = {kind: 0 for kind in ANIM}
    for take in takes:
        if take in have:
            raise SystemExit(f"{committed_path} already has a take {take}")
        if take not in stacks:
            raise SystemExit(f"{fresh_path} has no take {take} (it has {sorted(stacks)})")
        stack = stacks[take]
        # the take's own objects: stack <- layers <- curve nodes <- curves
        mine, frontier = [stack.props[0]], [stack.props[0]]
        while frontier:
            parent = frontier.pop()
            for _c, child in children.get(parent, ()):
                if by_id[child].id in ANIM and child not in mine:
                    mine.append(child)
                    frontier.append(child)
        remap = {}
        for i in mine:
            fresh_id = i
            while fresh_id in used:
                fresh_id = rng.randrange(1 << 20, 1 << 30)
            used.add(fresh_id)
            remap[i] = fresh_id
        for i in mine:
            src = by_id[i]
            old_objects.append(parse_fbx.FBXElem(src.id, [remap[i]] + list(src.props[1:]), src.props_type, src.elems))
            added[src.id] += 1
        driven = set()
        for c in new_conns:
            child, parent = c.props[1], c.props[2]
            if child not in remap:
                continue
            if parent in remap:
                to = remap[parent]
            elif parent in new_models:  # a curve node drives a bone: the same bone, by name, in the committed file
                name = new_models[parent]
                if name not in old_models:
                    raise SystemExit(f"take {take} drives {name.split(SEP)[0].decode()}, which {committed_path} lacks")
                to = old_models[name]
                driven.add(name)
            else:
                raise SystemExit(f"take {take}: unexpected connection {c.props}")
            old_conns.append(parse_fbx.FBXElem(c.id, [c.props[0], remap[child], to] + list(c.props[3:]),
                                               c.props_type, c.elems))
        entry = next(t for t in top(new, b"Takes").elems if t.id == b"Take" and t.props[0].decode().endswith("|" + take))
        top(old, b"Takes").elems.append(entry)
        print(f"[fbx_add_takes] {take}: {len(mine)} objects, drives {len(driven)} bones")
    total = sum(added.values())
    for d in top(old, b"Definitions").elems:
        if d.id == b"Count":
            d.props[0] += total
        elif d.id == b"ObjectType" and d.props[0] in added:
            count = next(c for c in d.elems if c.id == b"Count")
            count.props[0] += added[d.props[0]]
    root = encode_bin.FBXElem(b"")
    root.elems.extend(encode(e) for e in old.elems)
    encode_bin.write(out_path, root, version)
    print(f"[fbx_add_takes] {out_path}: added {takes}")


if __name__ == "__main__":
    main()
