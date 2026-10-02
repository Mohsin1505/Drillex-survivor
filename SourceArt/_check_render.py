"""Render the 6-view sheet plus close-ups of the roof/wall and corner joints for the open car."""
import bpy
import os
import runpy
from mathutils import Vector


def run(out_dir, prefix):
    pv = runpy.run_path(r"D:\Drillex\SourceArt\GunCar\scripts\preview_render.py", run_name="pv")
    pv["render"](out_dir, 480)
    scene = bpy.context.scene
    cam = scene.camera
    cam.data.type = 'PERSP'
    cam.data.lens = 45
    shots = {
        "edge": ((1.7, -0.6, 1.3), (0.45, -0.1, 0.88)),
        "corner_front": ((1.0, -2.0, 1.35), (0.35, -0.85, 0.85)),
        "corner_rear": ((-1.0, 2.0, 1.35), (-0.35, 0.85, 0.85)),
        "top_edge": ((0.0, -0.4, 2.2), (0.0, -0.4, 0.9)),
    }
    paths = []
    for name, (loc, tgt) in shots.items():
        cam.location = loc
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 640, 420
        scene.render.filepath = os.path.join(out_dir, f"{prefix}_{name}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
    for o in [o for o in bpy.data.objects if o.name.startswith(("PV_", "DBG_"))]:
        bpy.data.objects.remove(o, do_unlink=True)
    return paths
