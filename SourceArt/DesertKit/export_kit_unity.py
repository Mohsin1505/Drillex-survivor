"""Export every DESERT_ENVIRONMENT asset as its own FBX for Unity (run headless on DRL_DesertKit.blend).

blender -b DRL_DesertKit.blend --python export_kit_unity.py
* one FBX per asset: Assets/Art/Environment/DesertKit/Meshes/<COLLECTION>/SM_<NAME>.fbx
* asset sits at the origin, pivot = base centre (as authored), front -Y flipped 180 deg Z (project convention:
  Unity importer bakeAxisConversion -> +Z forward, clean transforms)
* palette texture copied to Textures/T_DesertKit_Palette.png
The .blend is not saved.
"""
import bmesh
import bpy
import json
import math
import os
import shutil
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ART = r"D:\Drillex\Assets\Art\Environment\DesertKit"


def main():
    os.makedirs(os.path.join(ART, "Textures"), exist_ok=True)
    shutil.copyfile(os.path.join(HERE, "T_DesertKit_Palette.png"), os.path.join(ART, "Textures", "T_DesertKit_Palette.png"))
    kit = bpy.data.collections["DESERT_ENVIRONMENT"]
    flip = Matrix.Rotation(math.pi, 4, 'Z')
    report = []
    for coll in kit.children:
        out_dir = os.path.join(ART, "Meshes", coll.name)
        os.makedirs(out_dir, exist_ok=True)
        for ob in list(coll.objects):
            if ob.type != 'MESH':
                continue
            ob.location = (0, 0, 0)
            ob.data = ob.data.copy() if ob.data.users > 1 else ob.data
            ob.data.transform(flip)
            ob.data.name = "SM_" + ob.name
            # triangulate here (beauty) so Unity never has to split concave n-gons itself
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
            bm.to_mesh(ob.data)
            bm.free()
            bpy.ops.object.select_all(action='DESELECT')
            ob.select_set(True)
            bpy.context.view_layer.objects.active = ob
            path = os.path.join(out_dir, f"SM_{ob.name}.fbx")
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, object_types={'MESH'},
                apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                axis_forward='Y', axis_up='Z', bake_space_transform=False,
                use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
                add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
            tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
            report.append({"name": ob.name, "coll": coll.name, "tris": tris,
                           "size": [round(v, 2) for v in ob.dimensions]})
    json.dump(report, open(os.path.join(HERE, "_export_report.json"), "w"), indent=1)
    print("EXPORTED", len(report))


main()
