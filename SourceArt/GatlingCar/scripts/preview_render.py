"""Render a turnaround contact sheet (front, left, right, top, rear, 3/4) for review."""
import bpy
import math
import os
from mathutils import Vector

VIEWS = {
    # name: (camera location, look-at, ortho)
    "front": ((0, -6, 0.62), (0, 0, 0.62), True),
    "left":  ((6, -0.05, 0.62), (0, -0.05, 0.62), True),
    "right": ((-6, -0.05, 0.62), (0, -0.05, 0.62), True),
    "top":   ((0, -0.05, 6), (0, -0.05, 0), True),
    "rear":  ((0, 6, 0.62), (0, 0, 0.62), True),
    "persp": ((3.2, -3.6, 2.2), (0, -0.25, 0.55), False),
}


def _setup_world(scene):
    world = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.32, 0.32, 0.33, 1)
    bg.inputs[1].default_value = 0.9
    scene.world = world
    for name, rot, energy in (("Key", (math.radians(50), 0, math.radians(-35)), 4.0),
                              ("Fill", (math.radians(60), 0, math.radians(140)), 1.5),
                              ("Top", (0, 0, 0), 1.5)):
        lt = bpy.data.objects.get("PV_" + name)
        if lt is None:
            ld = bpy.data.lights.new("PV_" + name, 'SUN')
            lt = bpy.data.objects.new("PV_" + name, ld)
            scene.collection.objects.link(lt)
        lt.data.energy = energy
        lt.rotation_euler = rot
        lt.hide_select = True


def _camera(scene):
    cam = bpy.data.objects.get("PV_Camera")
    if cam is None:
        cam = bpy.data.objects.new("PV_Camera", bpy.data.cameras.new("PV_Camera"))
        scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render(out_dir, res=640, views=None):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    _setup_world(scene)
    cam = _camera(scene)
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for name in (views or VIEWS):
        loc, target, ortho = VIEWS[name]
        cam.location = loc
        d = Vector(target) - Vector(loc)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y' if name != "top" else 'Y').to_euler()
        if name == "top":
            cam.rotation_euler = (0, 0, math.radians(90))  # front points left like the sheet's top view
        cam.data.type = 'ORTHO' if ortho else 'PERSP'
        cam.data.ortho_scale = 2.9
        cam.data.lens = 50
        scene.render.filepath = os.path.join(out_dir, f"view_{name}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
    return paths
