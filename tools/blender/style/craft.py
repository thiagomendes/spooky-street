"""Handcrafted surface library for Spooky Street's art direction (spec §6: "a handmade neighborhood after
sunset": painted timber, stitched costumes, paper decorations with curled edges).

Every look is procedural Blender shading so it is original and reproducible; for the game these are baked
to textures. `intensity` scales how strongly the craft reads (0.5 = subtle, 1.0 = pronounced).
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin(rgb):
    return tuple(srgb_to_linear(c) for c in rgb) + (1.0,)


def hex_rgb(value):
    return ((value >> 16) & 255) / 255.0, ((value >> 8) & 255) / 255.0, (value & 255) / 255.0


class Craft:
    """Factory for craft materials and stitch geometry at a given intensity."""

    def __init__(self, intensity=1.0):
        self.k = intensity
        self._cache = {}

    # --- node helpers -----------------------------------------------------------------------------------
    @staticmethod
    def _base(name):
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        bsdf = nodes["Principled BSDF"]
        return mat, nodes, mat.node_tree.links, bsdf

    @staticmethod
    def _coords(nodes, links, scale=(1.0, 1.0, 1.0)):
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = scale
        links.new(coord.outputs["Object"], mapping.inputs["Vector"])
        return mapping.outputs["Vector"]

    @staticmethod
    def _noise(nodes, links, vector, scale, detail=6.0, roughness=0.6):
        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = scale
        noise.inputs["Detail"].default_value = detail
        noise.inputs["Roughness"].default_value = roughness
        links.new(vector, noise.inputs["Vector"])
        return noise.outputs["Fac"]

    @staticmethod
    def _mix_colors(nodes, links, factor, a, b):
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        links.new(factor, mix.inputs[0])
        mix.inputs[6].default_value = a
        mix.inputs[7].default_value = b
        return mix.outputs[2]

    @staticmethod
    def _bump(nodes, links, height, strength, distance):
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = strength
        bump.inputs["Distance"].default_value = distance
        links.new(height, bump.inputs["Height"])
        return bump.outputs["Normal"]

    @staticmethod
    def _shade(rgb, factor):
        return tuple(min(1.0, c * factor) for c in rgb)

    def _cached(self, key, build):
        if key not in self._cache:
            self._cache[key] = build()
        return self._cache[key]

    # --- surfaces -----------------------------------------------------------------------------------------
    def felt(self, rgb, name="Felt"):
        """Pressed wool felt: soft sheen, mottled fibres, fuzzy micro-bump."""
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}")
            vec = self._coords(nodes, links)
            mottle = self._noise(nodes, links, vec, 9.0, 5.0)
            fibres = self._noise(nodes, links, vec, 140.0, 12.0, 0.8)
            k = self.k
            color = self._mix_colors(nodes, links, mottle, lin(self._shade(rgb, 1 - 0.22 * k)),
                                     lin(self._shade(rgb, 1 + 0.08 * k)))
            links.new(color, bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, fibres, min(1.0, 0.7 * k), 0.006), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = 1.0
            bsdf.inputs["Sheen Weight"].default_value = 0.35 + 0.5 * k
            bsdf.inputs["Sheen Roughness"].default_value = 0.35
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("felt", rgb), build)

    def knit(self, rgb, name="Knit"):
        """Knitted wool: vertical rib bands plus fibre fuzz."""
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}")
            vec = self._coords(nodes, links, (1.0, 1.0, 1.0))
            wave = nodes.new("ShaderNodeTexWave")
            wave.wave_type = "BANDS"
            wave.bands_direction = "X"
            wave.inputs["Scale"].default_value = 55.0
            wave.inputs["Distortion"].default_value = 1.5
            links.new(vec, wave.inputs["Vector"])
            fibres = self._noise(nodes, links, vec, 380.0, 8.0, 0.7)
            add = nodes.new("ShaderNodeMath")
            add.operation = "ADD"
            links.new(wave.outputs["Fac"], add.inputs[0])
            links.new(fibres, add.inputs[1])
            k = self.k
            links.new(self._bump(nodes, links, add.outputs[0], min(1.0, 0.7 * k), 0.005), bsdf.inputs["Normal"])
            color = self._mix_colors(nodes, links, wave.outputs["Fac"], lin(self._shade(rgb, 1 - 0.22 * k)), lin(rgb))
            links.new(color, bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.95
            bsdf.inputs["Sheen Weight"].default_value = 0.3 + 0.4 * k
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("knit", rgb), build)

    def waxed(self, rgb, name="Waxed"):
        """Waxed rain-cape cotton: soft satin highlight, faint weave."""
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}")
            vec = self._coords(nodes, links)
            weave = self._noise(nodes, links, vec, 600.0, 2.0, 0.5)
            mottle = self._noise(nodes, links, vec, 9.0, 3.0)
            k = self.k
            color = self._mix_colors(nodes, links, mottle, lin(self._shade(rgb, 1 - 0.08 * k)), lin(rgb))
            links.new(color, bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, weave, 0.15 * k, 0.002), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = 0.42
            bsdf.inputs["Coat Weight"].default_value = 0.15 * k
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("waxed", rgb), build)

    def paper(self, rgb, glow=0.0, name="Paper"):
        """Cut paper: matte, fibrous, a little translucent; optional inner glow for lanterns."""
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}_{glow}")
            vec = self._coords(nodes, links)
            fibres = self._noise(nodes, links, vec, 160.0, 12.0, 0.8)
            blotch = self._noise(nodes, links, vec, 6.0, 3.0)
            k = self.k
            color = self._mix_colors(nodes, links, blotch, lin(self._shade(rgb, 1 - 0.15 * k)), lin(rgb))
            links.new(color, bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, fibres, min(1.0, 0.4 * k), 0.003), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = 0.92
            bsdf.inputs["Subsurface Weight"].default_value = 0.15
            if glow > 0:
                links.new(color, bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = glow
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("paper", rgb, glow), build)

    def painted_wood(self, rgb, name="PaintedWood"):
        """Timber painted by hand: brush-stroke streaks along the grain, grain bump, slightly worn tone."""
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}")
            strokes_vec = self._coords(nodes, links, (1.5, 30.0, 30.0))
            strokes = self._noise(nodes, links, strokes_vec, 3.0, 6.0, 0.65)
            grain_vec = self._coords(nodes, links, (1.0, 1.0, 1.0))
            grain = nodes.new("ShaderNodeTexWave")
            grain.wave_type = "RINGS"
            grain.inputs["Scale"].default_value = 6.0
            grain.inputs["Distortion"].default_value = 6.0
            grain.inputs["Detail"].default_value = 3.0
            links.new(grain_vec, grain.inputs["Vector"])
            k = self.k
            color = self._mix_colors(nodes, links, strokes, lin(self._shade(rgb, 1 - 0.32 * k)),
                                     lin(self._shade(rgb, 1 + 0.14 * k)))
            links.new(color, bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, grain.outputs["Fac"], min(1.0, 0.35 * k), 0.006), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = 0.68
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("wood", rgb), build)

    def smooth(self, rgb, roughness=0.5, emission=0.0, subsurface=0.0, name="Smooth"):
        def build():
            mat, nodes, links, bsdf = self._base(f"{name}_{rgb}_{roughness}")
            bsdf.inputs["Base Color"].default_value = lin(rgb)
            bsdf.inputs["Roughness"].default_value = roughness
            bsdf.inputs["Subsurface Weight"].default_value = subsurface
            if emission > 0:
                bsdf.inputs["Emission Color"].default_value = lin(rgb)
                bsdf.inputs["Emission Strength"].default_value = emission
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("smooth", rgb, roughness, emission, subsurface), build)

    # --- stitches -----------------------------------------------------------------------------------------
    def stitches(self, name, points, rgb, spacing=0.016, dash=0.009, radius=0.0022, offset=None, closed=False):
        """Running stitch along a polyline: short thread dashes sitting on the surface."""
        if self.k <= 0:
            return None
        pts = [Vector(p) for p in points]
        if closed:
            pts.append(pts[0])
        bm = bmesh.new()
        carry = 0.0
        for a, b in zip(pts, pts[1:]):
            seg = b - a
            length = seg.length
            if length < 1e-6:
                continue
            direction = seg / length
            t = carry
            while t < length:
                centre = a + direction * min(t + dash * 0.5, length)
                if offset is not None:
                    centre += Vector(offset)
                quat = direction.to_track_quat("Z", "Y")
                mat = Matrix.Translation(centre) @ quat.to_matrix().to_4x4()
                bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=radius, radius2=radius,
                                      depth=dash * (0.6 + 0.4 * self.k), matrix=mat)
                t += spacing
            carry = t - length
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        obj = bpy.data.objects.new(name, me)
        obj.data.materials.append(self.smooth(rgb, 0.8, name="Thread"))
        bpy.context.scene.collection.objects.link(obj)
        return obj
