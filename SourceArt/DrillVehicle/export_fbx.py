"""
Drillex hero drill vehicle - final game asset assembly and FBX export (Blender 5.2).

Run after build_drill_vehicle.build() and texture_bake.run():
  * saves the authoring scene as DRL_DrillVehicle_Source.blend
  * swaps authoring materials for the two baked game materials
  * merges static vehicle parts into one VehicleBody mesh (moving parts stay separate)
  * creates LOD1 meshes (~45%)
  * exports SM_DRL_DrillVehicle.fbx and SM_DRL_Drill_Standard.fbx for Unity
  * saves the game-ready scene as DRL_DrillVehicle.blend
"""
import bpy
import math
import os

HERE = r"D:\Drillex\SourceArt\DrillVehicle"
ART = r"D:\Drillex\Assets\Art\Vehicles\DRL_DrillVehicle"
TEX = os.path.join(ART, "Textures")
MESH_OUT = os.path.join(ART, "Meshes")

STATIC_PARTS = ("VehicleBody", "Suspension", "Armor", "MechanicalDetails", "Lights", "DrillMount")
LOD_RATIO = 0.45


def _sel(obs, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or obs[0]


def _game_material(name, tex_set, emission):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])

    def img(suffix, non_color):
        path = os.path.join(TEX, f"{tex_set}_{suffix}.png")
        im = bpy.data.images.load(path, check_existing=True)
        im.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = im
        return n

    nt.links.new(img("BaseColor", False).outputs[0], bsdf.inputs["Base Color"])
    mask = img("Mask", True)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(mask.outputs[0], sep.inputs[0])
    nt.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(mask.outputs[1], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(img("Normal", True).outputs[0], nmap.inputs["Color"])
    nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    if emission:
        nt.links.new(img("Emission", False).outputs[0], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.5
    return m


def _set_material(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)


def _lod(ob, ratio):
    lod = ob.copy()
    lod.data = ob.data.copy()
    lod.name = ob.name + "_L1"
    lod.data.name = lod.name
    for c in ob.users_collection:
        c.objects.link(lod)
    lod.parent = ob
    lod.matrix_parent_inverse.identity()
    lod.location = (0, 0, 0)
    lod.rotation_euler = (0, 0, 0)
    lod.scale = (1, 1, 1)
    mod = lod.modifiers.new("Decimate", 'DECIMATE')
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    _sel([lod])
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return lod


def _tris(ob):
    me = ob.data
    return sum(len(p.vertices) - 2 for p in me.polygons)


def _export(root, path):
    objs = [root] + list(root.children_recursive)
    _sel(objs, root)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='Y', axis_up='Z', bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)


def _flip_front(root):
    """Rotate a hierarchy 180 degrees about Z around the world origin, baking the rotation into
    mesh data so every node keeps a clean (unrotated) transform. Blender builds the vehicle facing
    -Y; after this it faces +Y, which Unity's Z-up conversion turns into +Z forward.
    Calling it twice restores the original state."""
    from mathutils import Matrix
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


def _depth(o):
    d = 0
    while o.parent:
        o = o.parent
        d += 1
    return d


def export_current():
    """Export both FBX files from the assembled game scene.
    Axis conversion is baked by Unity's importer (bakeAxisConversion), which keeps
    every pivot's local transform clean."""
    root = bpy.data.objects["DRL_DrillVehicle"]
    socket = bpy.data.objects["DrillSocket"]
    drill_root = bpy.data.objects["DRL_Drill_Standard"]
    os.makedirs(MESH_OUT, exist_ok=True)
    # vehicle export without the drill module
    drill_root.parent = None
    drill_root.matrix_world = socket.matrix_world.copy()
    bpy.context.view_layer.update()
    _flip_front(root)
    _export(root, os.path.join(MESH_OUT, "SM_DRL_DrillVehicle.fbx"))
    _flip_front(root)
    # drill module exported from the origin so its pivot = socket contact point
    drill_root.location = (0, 0, 0)
    drill_root.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    _flip_front(drill_root)
    _export(drill_root, os.path.join(MESH_OUT, "SM_DRL_Drill_Standard.fbx"))
    _flip_front(drill_root)
    # re-attach for the review scene
    drill_root.parent = socket
    drill_root.matrix_parent_inverse.identity()
    drill_root.location = (0, 0, 0)


def run():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for n in [n for n in bpy.data.objects.keys() if n.startswith("PV_")]:
        bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "DRL_DrillVehicle_Source.blend"), copy=True)

    m_vehicle = _game_material("M_DRL_DrillVehicle", "T_DRL_DrillVehicle", True)
    m_drill = _game_material("M_DRL_Drill_Standard", "T_DRL_Drill_Standard", False)

    root = bpy.data.objects["DRL_DrillVehicle"]
    socket = bpy.data.objects["DrillSocket"]
    mount_mesh = bpy.data.objects["DrillMount"]

    # DrillMount becomes an empty (interface pivot); its mesh merges into the body
    mount = bpy.data.objects.new("DrillMount_tmp", None)
    mount.empty_display_type = 'PLAIN_AXES'
    mount.empty_display_size = 0.2
    mount_mesh.users_collection[0].objects.link(mount)
    mount.matrix_world = mount_mesh.matrix_world.copy()
    mount.parent = root
    mount.matrix_parent_inverse = root.matrix_world.inverted()
    mw = socket.matrix_world.copy()
    socket.parent = mount
    socket.matrix_parent_inverse = mount.matrix_world.inverted()
    socket.matrix_world = mw
    mount_mesh.name = "DrillMount_mesh"
    mount.name = "DrillMount"

    # merge static parts into VehicleBody (all share origin at the root except the mount)
    body = bpy.data.objects["VehicleBody"]
    parts = [bpy.data.objects[n if n != "DrillMount" else "DrillMount_mesh"] for n in STATIC_PARTS]
    _sel(parts, body)
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "VehicleBody"
    body.data.name = "VehicleBody"

    drill_root = bpy.data.objects["DRL_Drill_Standard"]
    drill_objs = set([drill_root] + list(drill_root.children_recursive))
    vehicle_meshes = [o for o in root.children_recursive if o.type == 'MESH' and o not in drill_objs]
    for o in vehicle_meshes:
        _set_material(o, m_vehicle)
        o.data.name = o.name if not o.name.startswith("Wheel_") else "SM_Wheel"
    for o in drill_objs:
        if o.type == 'MESH':
            _set_material(o, m_drill)
            o.data.name = o.name

    # LOD1 for the large meshes (wheels are small: reused in both LODs)
    stats = {}
    for n in ("VehicleBody", "Turret", "Cannon"):
        ob = bpy.data.objects[n]
        l1 = _lod(ob, LOD_RATIO)
        stats[n] = (_tris(ob), _tris(l1))
    for n in ("DrillBase", "DrillSections", "DrillTip"):
        ob = bpy.data.objects[n]
        l1 = _lod(ob, 0.5)
        stats[n] = (_tris(ob), _tris(l1))
    stats["Wheel"] = (_tris(bpy.data.objects["Wheel_L1"]) * 8, _tris(bpy.data.objects["Wheel_L1"]) * 8)

    export_current()

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "DRL_DrillVehicle.blend"))
    total0 = sum(a for a, b in stats.values())
    total1 = sum(b for a, b in stats.values())
    return {"tris": stats, "lod0_total": total0, "lod1_total": total1}
