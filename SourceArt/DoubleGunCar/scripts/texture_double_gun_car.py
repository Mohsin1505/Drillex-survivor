"""Materials, UVs and texture bake for the gun car (mobile: 1024 set + 512 emission),
then swaps to the single game material M_DRL_GunCar."""
import bpy
import importlib.util
import os

HERE = r"D:\Drillex\SourceArt\DoubleGunCar"
TEX = os.path.join(HERE, "Textures")
SET = "T_DRL_DoubleGunCar"
OBJS = ("Body", "Turret", "Cannon_R", "Axle_1")


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, "scripts", name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def game_material():
    m = bpy.data.materials.get("M_DRL_DoubleGunCar") or bpy.data.materials.new("M_DRL_DoubleGunCar")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])

    def img(suffix, non_color):
        im = bpy.data.images.load(os.path.join(TEX, f"{SET}_{suffix}.png"), check_existing=True)
        im.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
        im.reload()
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
    nt.links.new(img("Emission", False).outputs[0], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.5
    return m


def run():
    bk = _load("bake_lib")
    # tight bake margin (3 px) so dilated pixels never reach a neighbouring island at 1024
    _bake = bk.bake
    bk.bake = lambda obs, kind, img, margin=3, **kw: _bake(obs, kind, img, margin=3, **kw)
    bk.SPEC["YellowPaint"] = dict(col=(0.82, 0.52, 0.03), metal=0.0, rough=0.45, paint=True)
    bk.SPEC["Camo"] = dict(col=(0.46, 0.03, 0.022), metal=0.0, rough=0.48, paint=True, rivets=True, camo=True)
    obs = bk.unwrap(OBJS, margin=0.006)
    px_per_m = bk.texel_scale(obs) * 1024
    rivet = 1.0 / max(bk.texel_scale(obs) * 0.06, 1e-6)
    for m in {s.material for o in obs for s in o.material_slots if s.material}:
        bk.build_material(m.name[2:], rivet)
    maps = bk.bake_set(SET, OBJS, 1024, 1024, TEX)
    gm = game_material()
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data.materials.clear()
            o.data.materials.append(gm)
    return {"px_per_m": round(px_per_m), "maps": maps}
