"""Render one 3/4 review image per kit collection (EEVEE, warm sun) into _preview/."""
import bpy
import math
import os
from mathutils import Vector

OUT = r"D:\Drillex\SourceArt\DesertKit\_preview"


def _setup(scene):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.get("KitWorld") or bpy.data.worlds.new("KitWorld")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.18, 0.22, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    scene.world = w
    sun = bpy.data.objects.get("KIT_Sun")
    if sun is None:
        sun = bpy.data.objects.new("KIT_Sun", bpy.data.lights.new("KIT_Sun", 'SUN'))
        scene.collection.objects.link(sun)
    sun.data.energy = 3.5
    sun.data.color = (1.0, 0.92, 0.82)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    cam = bpy.data.objects.get("KIT_Cam")
    if cam is None:
        cam = bpy.data.objects.new("KIT_Cam", bpy.data.cameras.new("KIT_Cam"))
        scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render_all(names=None, res=(1600, 700)):
    scene = bpy.context.scene
    cam = _setup(scene)
    scene.render.resolution_x, scene.render.resolution_y = res
    kit = bpy.data.collections["DESERT_ENVIRONMENT"]
    paths = []
    for coll in kit.children:
        if names and coll.name not in names:
            continue
        obs = list(coll.objects)
        lo = Vector((1e9, 1e9, 1e9))
        hi = -lo
        for o in obs:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w))
                hi = Vector(map(max, hi, w))
        ctr = (lo + hi) / 2
        span = max(hi.x - lo.x, (hi.z - lo.z) * res[0] / res[1])
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = span * 1.08
        d = Vector((0.0, -1.0, 0.55)).normalized()
        cam.location = ctr + d * 60
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(OUT, f"kit_{coll.name}.png")
        for other in kit.children:
            other.hide_render = other is not coll
        bpy.ops.render.render(write_still=True)
        for other in kit.children:
            other.hide_render = False
        paths.append(scene.render.filepath)
    return paths
