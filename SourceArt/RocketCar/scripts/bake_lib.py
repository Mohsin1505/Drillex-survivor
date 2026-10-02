"""
Drillex hero drill vehicle - materials, UVs and texture bake (Blender 5.2, Cycles).

1. Rebuilds the authoring materials (A_*) as procedural Cycles shaders:
   painted metal with edge chips, cavity grime, rivet rows, hazard stripes,
   decals, emissive lamps.
2. Unwraps and packs one UV atlas for the vehicle and one for the drill module.
3. Bakes BaseColor, Normal, Mask (R metallic, G AO, A smoothness) and Emission
   into texture sets that Unity's URP/Lit uses directly.
"""
import bpy
import bmesh
import math
import os
import numpy as np

HERE = r"D:\Drillex\SourceArt\DrillVehicle"
TEX_OUT = r"D:\Drillex\Assets\Art\Vehicles\DRL_DrillVehicle\Textures"

VEHICLE_OBJS = ("VehicleBody", "Suspension", "Armor", "MechanicalDetails", "Lights",
                "DrillMount", "Turret", "Cannon", "Wheel_L1")
DRILL_OBJS = ("DrillBase", "DrillSections", "DrillTip")

# texture sets: (name, objects, size, emission size or None)
SETS = (
    ("T_DRL_DrillVehicle", VEHICLE_OBJS, 2048, 1024),
    ("T_DRL_Drill_Standard", DRILL_OBJS, 1024, None),
)


# ---------------------------------------------------------------------------
# Node helpers
# ---------------------------------------------------------------------------
class NB:
    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.x = 0

    def n(self, kind, **inputs):
        node = self.nt.nodes.new(kind)
        node.location = (self.x, 0)
        self.x += 40
        for k, v in inputs.items():
            if k.startswith("_"):
                setattr(node, k[1:], v)
            else:
                self.set(node.inputs[k], v)
        return node

    def set(self, sock, v):
        if hasattr(v, "bl_idname") or hasattr(v, "is_output"):
            self.nt.links.new(v if hasattr(v, "is_output") else v.outputs[0], sock)
        else:
            sock.default_value = v

    def math(self, op, a, b=0.0, clamp=False):
        m = self.n("ShaderNodeMath", _operation=op, _use_clamp=clamp)
        self.set(m.inputs[0], a)
        self.set(m.inputs[1], b)
        return m.outputs[0]

    def mix(self, fac, a, b):
        m = self.n("ShaderNodeMix", _data_type='RGBA')
        self.set(m.inputs["Factor"], fac)
        self.set(m.inputs[6], a)
        self.set(m.inputs[7], b)
        return m.outputs[2]

    def mixf(self, fac, a, b):
        m = self.n("ShaderNodeMix", _data_type='FLOAT')
        self.set(m.inputs["Factor"], fac)
        self.set(m.inputs[2], a)
        self.set(m.inputs[3], b)
        return m.outputs[0]

    def maprange(self, v, a, b, c=0.0, d=1.0):
        m = self.n("ShaderNodeMapRange", _clamp=True)
        self.set(m.inputs[0], v)
        m.inputs[1].default_value = a
        m.inputs[2].default_value = b
        m.inputs[3].default_value = c
        m.inputs[4].default_value = d
        return m.outputs[0]


# per material: base rgb, metallic, roughness, flags
SPEC = {
    "RedPaint":    dict(col=(0.46, 0.03, 0.022), metal=0.0, rough=0.48, paint=True, rivets=True),
    "BlackMetal":  dict(col=(0.028, 0.026, 0.025), metal=0.35, rough=0.55, paint=True, rivets=True),
    "OrangePaint": dict(col=(0.78, 0.30, 0.035), metal=0.0, rough=0.42, paint=True, rivets=True),
    "Hazard":      dict(col=(0.82, 0.50, 0.02), metal=0.0, rough=0.5, paint=True, stripes=True),
    "Steel":       dict(col=(0.40, 0.40, 0.41), metal=1.0, rough=0.38, scratches=True),
    "DrillSteel":  dict(col=(0.60, 0.60, 0.62), metal=1.0, rough=0.26, scratches=True, rings=True),
    "Brass":       dict(col=(0.72, 0.45, 0.13), metal=1.0, rough=0.32, scratches=True),
    "Grille":      dict(col=(0.02, 0.02, 0.02), metal=0.6, rough=0.6, slots=True),
    "LampAmber":   dict(col=(1.0, 0.50, 0.08), metal=0.0, rough=0.15, emit=(1.0, 0.40, 0.04), lens=True),
    "LampRed":     dict(col=(1.0, 0.10, 0.05), metal=0.0, rough=0.15, emit=(1.0, 0.04, 0.02), lens=True),
    "WindowGlow":  dict(col=(0.9, 0.40, 0.08), metal=0.0, rough=0.3, emit=(1.0, 0.33, 0.04), window=True),
    "Decal":       dict(col=(0.7, 0.7, 0.7), metal=0.2, rough=0.45, decal=True),
}


def build_material(name, rivet_scale):
    sp = SPEC[name]
    mat = bpy.data.materials["A_" + name]
    mat.use_nodes = True
    b = NB(mat)
    out = b.n("ShaderNodeOutputMaterial")
    bsdf = b.n("ShaderNodeBsdfPrincipled")
    b.nt.links.new(bsdf.outputs[0], out.inputs[0])
    geo = b.n("ShaderNodeNewGeometry")
    tc = b.n("ShaderNodeTexCoord")
    pos = geo.outputs["Position"]

    # edge mask from a Bevel node (Cycles): 1 near sharp edges
    bev = b.n("ShaderNodeBevel", _samples=8, Radius=0.006)
    dot = b.n("ShaderNodeVectorMath", _operation='DOT_PRODUCT')
    b.set(dot.inputs[0], bev.outputs[0])
    b.set(dot.inputs[1], geo.outputs["Normal"])
    edge = b.maprange(b.math('SUBTRACT', 1.0, dot.outputs["Value"]), 0.002, 0.03)
    # wide edge band for rivet rows
    bev2 = b.n("ShaderNodeBevel", _samples=8, Radius=0.035)
    dot2 = b.n("ShaderNodeVectorMath", _operation='DOT_PRODUCT')
    b.set(dot2.inputs[0], bev2.outputs[0])
    b.set(dot2.inputs[1], geo.outputs["Normal"])
    edge_wide = b.maprange(b.math('SUBTRACT', 1.0, dot2.outputs["Value"]), 0.004, 0.02)

    # cavity / ambient grime
    ao = b.n("ShaderNodeAmbientOcclusion", _samples=16, Distance=0.12)
    grime = b.maprange(ao.outputs["AO"], 0.95, 0.35)
    # ground dirt: stronger toward the bottom
    sep = b.n("ShaderNodeSeparateXYZ")
    b.set(sep.inputs[0], pos)
    low = b.maprange(sep.outputs["Z"], 0.55, 0.05)

    noise = b.n("ShaderNodeTexNoise", Scale=22.0, Detail=6.0, Roughness=0.6)
    b.set(noise.inputs["Vector"], pos)
    noise_big = b.n("ShaderNodeTexNoise", Scale=4.0, Detail=3.0)
    b.set(noise_big.inputs["Vector"], pos)

    col = sp["col"] + (1.0,)
    base = b.mix(b.maprange(noise_big.outputs["Fac"], 0.3, 0.7, 0.0, 0.25), col,
                 tuple(c * 0.7 for c in sp["col"]) + (1.0,))
    metal = sp["metal"]
    rough = sp["rough"]
    height = 0.0

    if sp.get("stripes"):
        # diagonal hazard stripes in world space (consistent on every face)
        diag = b.math('ADD', b.math('ADD', sep.outputs["X"], sep.outputs["Y"]), sep.outputs["Z"])
        wave = b.math('FRACT', b.math('MULTIPLY', diag, 9.0))
        stripe = b.maprange(wave, 0.48, 0.52)
        base = b.mix(stripe, base, (0.02, 0.02, 0.02, 1.0))

    if sp.get("decal"):
        img_s = bpy.data.images.load(os.path.join(HERE, "decal_skull.png"), check_existing=True)
        img_e = bpy.data.images.load(os.path.join(HERE, "decal_emblem.png"), check_existing=True)
        sgn = b.math('SIGN', sep.outputs["X"])
        # skull roundels on the hull sides
        u = b.math('ADD', 0.5, b.math('MULTIPLY', b.math('MULTIPLY', sgn, -1.0),
                                    b.math('DIVIDE', b.math('ADD', sep.outputs["Y"], 0.30), 0.165)))
        v = b.math('ADD', 0.5, b.math('DIVIDE', b.math('SUBTRACT', sep.outputs["Z"], 0.47), 0.165))
        uv = b.n("ShaderNodeCombineXYZ")
        b.set(uv.inputs[0], u)
        b.set(uv.inputs[1], v)
        ts = b.n("ShaderNodeTexImage", _image=img_s, _extension='CLIP')
        b.set(ts.inputs[0], uv.outputs[0])
        # hazard emblems on the turret sides
        u2 = b.math('ADD', 0.5, b.math('MULTIPLY', b.math('MULTIPLY', sgn, -1.0),
                                     b.math('DIVIDE', b.math('SUBTRACT', sep.outputs["Y"], 0.295), 0.095)))
        v2 = b.math('ADD', 0.5, b.math('DIVIDE', b.math('SUBTRACT', sep.outputs["Z"], 1.25), 0.095))
        uv2 = b.n("ShaderNodeCombineXYZ")
        b.set(uv2.inputs[0], u2)
        b.set(uv2.inputs[1], v2)
        te = b.n("ShaderNodeTexImage", _image=img_e, _extension='CLIP')
        b.set(te.inputs[0], uv2.outputs[0])
        is_turret = b.maprange(sep.outputs["Z"], 0.95, 1.0)
        base = b.mix(is_turret, ts.outputs[0], te.outputs[0])

    if sp.get("camo"):
        cn = b.n("ShaderNodeTexNoise", Scale=3.2, Detail=4.0, Roughness=0.55)
        b.set(cn.inputs["Vector"], pos)
        yellow = b.maprange(cn.outputs["Fac"], 0.53, 0.56)
        dark = b.maprange(cn.outputs["Fac"], 0.41, 0.38)
        base = b.mix(yellow, base, (0.82, 0.50, 0.03, 1.0))
        base = b.mix(dark, base, (0.03, 0.028, 0.026, 1.0))

    chips = None
    if sp.get("paint"):
        chip_src = b.math('ADD', b.math('MULTIPLY', edge, 1.2),
                          b.math('MULTIPLY', b.math('SUBTRACT', noise.outputs["Fac"], 0.5), 1.4))
        chips = b.maprange(chip_src, 0.62, 0.72)
        base = b.mix(chips, base, (0.16, 0.15, 0.145, 1.0))
        metal = b.mixf(chips, sp["metal"], 1.0)
        rough = b.mixf(chips, sp["rough"], 0.42)
        height = b.math('MULTIPLY', chips, -0.4)

    if sp.get("rivets"):
        vor = b.n("ShaderNodeTexVoronoi", _voronoi_dimensions='2D', Scale=rivet_scale, Randomness=0.0)
        b.set(vor.inputs["Vector"], tc.outputs["UV"])
        dots = b.maprange(vor.outputs["Distance"], 0.20, 0.13)
        rivets = b.math('MULTIPLY', dots, edge_wide)
        height = b.math('ADD', height, b.math('MULTIPLY', rivets, 1.0))
        base = b.mix(b.math('MULTIPLY', rivets, 0.25), base, (0.25, 0.24, 0.23, 1.0))

    if sp.get("scratches"):
        sc = b.n("ShaderNodeTexNoise", Scale=60.0, Detail=10.0)
        b.set(sc.inputs["Vector"], pos)
        rough = b.math('ADD', rough, b.math('MULTIPLY', b.math('SUBTRACT', sc.outputs["Fac"], 0.5), 0.25))
        height = b.math('ADD', height, b.math('MULTIPLY', sc.outputs["Fac"], 0.05))

    if sp.get("rings"):
        # machined rings on the drill
        sep_ring = b.math('FRACT', b.math('MULTIPLY', sep.outputs["Y"], 120.0))
        ring = b.maprange(b.math('ABSOLUTE', b.math('SUBTRACT', sep_ring, 0.5)), 0.42, 0.5)
        height = b.math('ADD', height, b.math('MULTIPLY', ring, 0.15))
        base = b.mix(b.math('MULTIPLY', ring, 0.35), base, (0.3, 0.3, 0.31, 1.0))

    if sp.get("slots"):
        slot = b.math('FRACT', b.math('MULTIPLY', sep.outputs["Z"], 55.0))
        bar = b.maprange(b.math('ABSOLUTE', b.math('SUBTRACT', slot, 0.5)), 0.22, 0.30)
        base = b.mix(bar, (0.005, 0.005, 0.005, 1.0), (0.18, 0.18, 0.19, 1.0))
        metal = b.mixf(bar, 0.0, 0.9)
        height = b.math('ADD', height, b.math('MULTIPLY', bar, 0.8))

    emit_col = None
    if sp.get("lens"):
        # bright core, faceted rim
        emit_col = sp["emit"] + (1.0,)
    if sp.get("window"):
        bars = b.math('FRACT', b.math('MULTIPLY', sep.outputs["Z"], 18.0))
        frame = b.maprange(b.math('ABSOLUTE', b.math('SUBTRACT', bars, 0.5)), 0.40, 0.46)
        emit_col = b.mix(frame, sp["emit"] + (1.0,), (0.05, 0.02, 0.0, 1.0))
        base = b.mix(frame, base, (0.03, 0.03, 0.03, 1.0))

    # grime / dirt on everything except emissives
    if not sp.get("emit"):
        dirt = b.math('MINIMUM', b.math('ADD', b.math('MULTIPLY', grime, 0.75), b.math('MULTIPLY', low, 0.35)), 1.0)
        dirt = b.math('MULTIPLY', dirt, b.maprange(noise_big.outputs["Fac"], 0.25, 0.65, 0.5, 1.0))
        base = b.mix(dirt, base, (0.07, 0.055, 0.045, 1.0))
        rough = b.math('ADD', rough, b.math('MULTIPLY', dirt, 0.3), clamp=True)
        metal = b.mixf(b.math('MULTIPLY', dirt, 0.7), metal, 0.0)

    b.set(bsdf.inputs["Base Color"], base)
    b.set(bsdf.inputs["Metallic"], metal)
    b.set(bsdf.inputs["Roughness"], rough)
    if emit_col is not None:
        b.set(bsdf.inputs["Emission Color"], emit_col)
        bsdf.inputs["Emission Strength"].default_value = 1.0

    bump = b.n("ShaderNodeBump", Strength=0.6, Distance=0.002)
    b.set(bump.inputs["Height"], height)
    b.set(bump.inputs["Normal"], bev.outputs[0])
    b.set(bsdf.inputs["Normal"], bump.outputs[0])

    # named output used for the metallic bake
    mo = b.n("ShaderNodeMath", _operation='ADD', _name="METAL_OUT")
    b.set(mo.inputs[0], metal)
    mo.inputs[1].default_value = 0.0
    bsdf.name = "BSDF"
    return mat


# ---------------------------------------------------------------------------
# UVs
# ---------------------------------------------------------------------------

def _select(names):
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    obs = [bpy.data.objects[n] for n in names]
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    return obs


def _shrink_underside(o, factor=0.25):
    """Faces pointing down are never seen from the gameplay camera: give them less texture space."""
    bm = bmesh.from_edit_mesh(o.data)
    uvl = bm.loops.layers.uv.active
    down = {f for f in bm.faces if f.normal.z < -0.6}
    seen = set()
    for f in down:
        if f in seen:
            continue
        group, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            group.append(g)
            for e in g.edges:
                for nf in e.link_faces:
                    if nf in down and nf not in seen:
                        seen.add(nf)
                        stack.append(nf)
        loops = [l for g in group for l in g.loops]
        cx = sum(l[uvl].uv.x for l in loops) / len(loops)
        cy = sum(l[uvl].uv.y for l in loops) / len(loops)
        for l in loops:
            l[uvl].uv.x = cx + (l[uvl].uv.x - cx) * factor
            l[uvl].uv.y = cy + (l[uvl].uv.y - cy) * factor
    bmesh.update_edit_mesh(o.data)


def unwrap(names, margin):
    obs = _select(names)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    for o in obs:
        _shrink_underside(o)
    bpy.ops.uv.pack_islands(udim_source='CLOSEST_UDIM', rotate=True, margin_method='FRACTION',
                            margin=margin, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')
    return obs


def texel_scale(obs):
    """UV units per meter (sqrt of uv area / surface area)."""
    uv_a = 0.0
    a = 0.0
    seen = set()
    for o in obs:
        if o.data.name in seen:
            continue
        seen.add(o.data.name)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        uvl = bm.loops.layers.uv.active
        for f in bm.faces:
            a += f.calc_area()
            pts = [l[uvl].uv for l in f.loops]
            s = 0.0
            for i in range(len(pts)):
                x1, y1 = pts[i]
                x2, y2 = pts[(i + 1) % len(pts)]
                s += x1 * y2 - x2 * y1
            uv_a += abs(s) / 2
        bm.free()
    return math.sqrt(uv_a / max(a, 1e-9))


# ---------------------------------------------------------------------------
# Baking
# ---------------------------------------------------------------------------

def _bake_targets(obs, img):
    mats = set()
    for o in obs:
        for s in o.material_slots:
            if s.material:
                mats.add(s.material)
    for m in mats:
        nt = m.node_tree
        node = nt.nodes.get("BAKE_TARGET") or nt.nodes.new("ShaderNodeTexImage")
        node.name = "BAKE_TARGET"
        node.image = img
        node.select = True
        nt.nodes.active = node
    return mats


def _new_img(name, size, non_color):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, size, size, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    return img


def bake(obs, kind, img, margin=8, **kw):
    _select([o.name for o in obs])
    _bake_targets(obs, img)
    bpy.ops.object.bake(type=kind, margin=margin, use_clear=True, **kw)


def _pixels(img):
    a = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(-1, 4)


def _route_to_emission(mats, bsdf_input):
    """Temporarily feed a BSDF input (or METAL_OUT when None) into Emission for an EMIT bake."""
    saved = []
    for m in mats:
        nt = m.node_tree
        bsdf = nt.nodes["BSDF"]
        ec, es = bsdf.inputs["Emission Color"], bsdf.inputs["Emission Strength"]
        old_link = ec.links[0].from_socket if ec.links else None
        saved.append((m, old_link, tuple(ec.default_value), es.default_value))
        for l in list(ec.links):
            nt.links.remove(l)
        if bsdf_input is None:
            nt.links.new(nt.nodes["METAL_OUT"].outputs[0], ec)
        else:
            src = bsdf.inputs[bsdf_input]
            if src.links:
                nt.links.new(src.links[0].from_socket, ec)
            else:
                ec.default_value = tuple(src.default_value)
        es.default_value = 1.0
    return saved


def _restore_emission(saved):
    for m, old_link, dv, strength in saved:
        nt = m.node_tree
        bsdf = nt.nodes["BSDF"]
        ec = bsdf.inputs["Emission Color"]
        for l in list(ec.links):
            nt.links.remove(l)
        if old_link:
            nt.links.new(old_link, ec)
        ec.default_value = dv
        bsdf.inputs["Emission Strength"].default_value = strength


def bake_set(set_name, names, size, emit_size, out_dir):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    cp = bpy.context.preferences.addons['cycles'].preferences
    for backend in ('OPTIX', 'CUDA'):
        try:
            cp.compute_device_type = backend
            cp.get_devices()
            if any(d.type == backend for d in cp.devices):
                break
        except TypeError:
            continue
    for d in cp.devices:
        d.use = d.type != 'CPU'
    scene.cycles.device = 'GPU'
    scene.cycles.samples = 8
    scene.render.bake.margin_type = 'EXTEND'
    obs = [bpy.data.objects[n] for n in names]
    os.makedirs(out_dir, exist_ok=True)
    results = {}

    mats = {s.material for o in obs for s in o.material_slots if s.material}
    # base color: routed through emission (a DIFFUSE bake would return black for metals)
    img = _new_img(set_name + "_BaseColor", size, False)
    saved = _route_to_emission(mats, "Base Color")
    bake(obs, 'EMIT', img)
    _restore_emission(saved)
    img.filepath_raw = os.path.join(out_dir, img.name + ".png")
    img.file_format = 'PNG'
    img.save()
    results["BaseColor"] = img.filepath_raw

    # normal (tangent space, OpenGL / Unity convention: +Y)
    img = _new_img(set_name + "_Normal", size, True)
    bake(obs, 'NORMAL', img, normal_space='TANGENT')
    img.filepath_raw = os.path.join(out_dir, img.name + ".png")
    img.file_format = 'PNG'
    img.save()
    results["Normal"] = img.filepath_raw

    # roughness
    rough = _new_img(set_name + "_tmpRough", size, True)
    bake(obs, 'ROUGHNESS', rough)
    # ambient occlusion
    scene.cycles.samples = 64
    ao = _new_img(set_name + "_tmpAO", size, True)
    bake(obs, 'AO', ao)
    scene.cycles.samples = 8
    # metallic: METAL_OUT routed through emission
    saved = _route_to_emission(mats, None)
    metal = _new_img(set_name + "_tmpMetal", size, True)
    bake(obs, 'EMIT', metal)
    _restore_emission(saved)

    # pack mask: R metallic, G AO, B 0, A smoothness
    r = _pixels(rough)
    a = _pixels(ao)
    mt = _pixels(metal)
    mask = np.zeros_like(r)
    mask[:, 0] = np.clip(mt[:, 0], 0, 1)
    mask[:, 1] = np.clip(a[:, 0], 0, 1)
    mask[:, 3] = np.clip(1.0 - r[:, 0], 0, 1)
    img = _new_img(set_name + "_Mask", size, True)
    img.pixels.foreach_set(mask.ravel())
    img.filepath_raw = os.path.join(out_dir, img.name + ".png")
    img.file_format = 'PNG'
    img.save()
    results["Mask"] = img.filepath_raw
    for t in (rough, ao, metal):
        bpy.data.images.remove(t)

    if emit_size:
        img = _new_img(set_name + "_Emission", emit_size, False)
        bake(obs, 'EMIT', img)
        img.filepath_raw = os.path.join(out_dir, img.name + ".png")
        img.file_format = 'PNG'
        img.save()
        results["Emission"] = img.filepath_raw
    return results


def run(do_bake=True):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    report = {}
    scales = {}
    for set_name, names, size, emit in SETS:
        obs = unwrap(names, margin=0.004 if size >= 2048 else 0.006)
        scales[set_name] = texel_scale(obs)
        report[set_name + "_px_per_m"] = round(scales[set_name] * size)
    # rivet spacing ~6 cm everywhere: voronoi scale in UV units
    for name in SPEC:
        if "A_" + name in bpy.data.materials:
            sc = scales["T_DRL_DrillVehicle"] * 0.06
            build_material(name, 1.0 / max(sc, 1e-6))
    if do_bake:
        for set_name, names, size, emit in SETS:
            report[set_name] = bake_set(set_name, names, size, emit, TEX_OUT)
    return report
