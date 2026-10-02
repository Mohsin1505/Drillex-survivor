"""Export the gun car to the Drillex Unity project (same convention as the drill head:
Z-up FBX, Unity bakes the axis conversion, model flipped to face +Y so Unity gets +Z forward)."""
import bpy
import math
import os
import shutil
from mathutils import Matrix

HERE = r"D:\Drillex\SourceArt\DoubleGunCar"
DST = r"D:\Drillex\Assets\Art\Vehicles\DRL_DoubleGunCar"


def _depth(o):
    d = 0
    while o.parent:
        o = o.parent
        d += 1
    return d


def _flip(root):
    rot = Matrix.Rotation(math.pi, 4, 'Z')
    objs = [root] + list(root.children_recursive)
    new_world = {}
    for o in objs:
        loc, q, sc = o.matrix_world.decompose()
        new_world[o] = Matrix.LocRotScale(rot @ loc, q, sc)
    for me in {o.data for o in objs if o.type == 'MESH'}:
        me.transform(rot)
    for o in sorted(objs, key=_depth):
        o.matrix_world = new_world[o]


def run():
    os.makedirs(os.path.join(DST, "Meshes"), exist_ok=True)
    os.makedirs(os.path.join(DST, "Textures"), exist_ok=True)
    root = bpy.data.objects["DRL_DoubleGunCar"]
    bpy.context.view_layer.update()
    _flip(root)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(DST, "Meshes", "SM_DRL_DoubleGunCar.fbx"), use_selection=True,
        object_types={'EMPTY', 'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='Y', axis_up='Z', bake_space_transform=False, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False,
        path_mode='STRIP', embed_textures=False)
    _flip(root)
    bpy.ops.object.select_all(action='DESELECT')
    for f in os.listdir(os.path.join(HERE, "Textures")):
        if f.endswith(".png"):
            shutil.copy2(os.path.join(HERE, "Textures", f), os.path.join(DST, "Textures", f))
    return sorted(os.listdir(DST + r"\Textures"))
