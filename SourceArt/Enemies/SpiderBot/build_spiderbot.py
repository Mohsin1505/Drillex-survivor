"""
Drillex enemy "Spider Robot" - mobile-first build, atlas, rig, animation and FBX export (Blender 5.2).

  blender -b --factory-startup --python build_spiderbot.py -- [--render]

Reference: ref_concept_sheet.png (cute, chunky, eight short legs, one big glowing eye, red/orange camo).
Budget: ~3.3k tris, one skinned mesh, 1 material per colour variant sharing one 512 atlas layout
(per-variant base colour atlas, shared emission + mask). Every part is rigid-skinned to one bone
(1 influence per vertex). Legs are animated with IK during authoring and baked to plain FK keys.
Front faces +Y (Unity +Z after export), Z up, 1 unit = 1 m. Body ~1.2 m tall, ~1.9 m across.

Tile layout of the 512 atlas (4x4 tiles of 128 px, tile index = row * 4 + col, row 0 at the bottom):
  0 armour (camo, seams, bolts, wear)   1 gunmetal   2 dark metal   3 eye lens
  4 vent slits (emissive)   5 small lens   6 leg armour   7 accent
"""
import bpy
import bmesh
import math
import os
import sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = r"D:\Drillex\Assets\Art\Enemies\DRL_SpiderBot"
OUT_FBX = os.path.join(UNITY, "Meshes", "SK_DRL_SpiderBot.fbx")
TEX_DIR = os.path.join(UNITY, "Textures")
PREVIEW = os.path.join(HERE, "_preview")
REF = os.path.join(HERE, "ref_concept_sheet.png")

ARMOR, GUN, DARK, EYE, VENT, LENS, LEGARMOR, ACCENT = range(8)
ATLAS = 512
# atlas regions in pixels (x, y, size); the body armour gets a 256 px block since it fills the top view
REGION = {ARMOR: (0, 0, 256), GUN: (256, 0, 128), DARK: (384, 0, 128), EYE: (256, 128, 128),
          VENT: (384, 128, 128), LENS: (0, 256, 128), LEGARMOR: (128, 256, 128), ACCENT: (256, 256, 128)}
TILE = 128  # size of the region being painted

VARIANTS = {
    #          primary   secondary  glow       role
    "Red":    ("D8261C", "FF8A1A", "FFB020"),   # standard
    "Blue":   ("1F6FE6", "38D8FF", "3FD0FF"),   # fast
    "Green":  ("39B51C", "B8FF2E", "66FF3A"),   # tough
    "Purple": ("8B2BE2", "FF3FD2", "E85BFF"),   # elite
}
GUNMETAL, GUN_LIGHT, DARKMETAL = "3A3D42", "8C9299", "1E1F22"


def rgb(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


# ====================================================================== textures
def _noise(n, cells, seed):
    """Smooth value noise on an n x n grid (bilinear-upsampled random lattice)."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells + 1, cells + 1)).astype(np.float32)
    x = np.linspace(0, cells, n, endpoint=False)
    i = x.astype(int)
    f = x - i
    f = f * f * (3 - 2 * f)
    a = g[np.ix_(i, i)]
    b = g[np.ix_(i, i + 1)]
    c = g[np.ix_(i + 1, i)]
    d = g[np.ix_(i + 1, i + 1)]
    fx = f[None, :]
    fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def _fbm(n, seed):
    return 0.55 * _noise(n, 4, seed) + 0.3 * _noise(n, 9, seed + 1) + 0.15 * _noise(n, 23, seed + 2)


def _tile_coords():
    v, u = np.mgrid[0:TILE, 0:TILE].astype(np.float32)
    return (u + 0.5) / TILE, (v + 0.5) / TILE


def _seams_bolts(u, v, seams_u, seams_v):
    """Returns (seam darkness 0..1, bolt mask, bolt centre mask)."""
    seam = np.zeros_like(u)
    for s in seams_u:
        seam = np.maximum(seam, np.clip(1.0 - np.abs(u - s) * TILE / 1.3, 0, 1))
    for s in seams_v:
        seam = np.maximum(seam, np.clip(1.0 - np.abs(v - s) * TILE / 1.3, 0, 1))
    bolt = np.zeros_like(u)
    centre = np.zeros_like(u)
    pts = [(a + da, b + db) for a in seams_u for b in seams_v for da in (-0.07, 0.07) for db in (-0.07, 0.07)]
    for (bu, bv) in pts:
        r = np.sqrt((u - bu) ** 2 + (v - bv) ** 2) * TILE
        bolt = np.maximum(bolt, np.clip(3.2 - r, 0, 1))
        centre = np.maximum(centre, np.clip(1.6 - r, 0, 1))
    return seam, bolt, centre


def _scratches(seed, count=14):
    rng = np.random.default_rng(seed)
    u, v = _tile_coords()
    m = np.zeros_like(u)
    for _ in range(count):
        x0, y0 = rng.random(2)
        ang = rng.uniform(-0.6, 0.6) + (math.pi / 4 if rng.random() < 0.5 else -math.pi / 4)
        L = rng.uniform(0.06, 0.18)
        dx, dy = math.cos(ang), math.sin(ang)
        t = np.clip((u - x0) * dx + (v - y0) * dy, 0, L)
        d = np.sqrt((u - x0 - t * dx) ** 2 + (v - y0 - t * dy) ** 2) * TILE
        m = np.maximum(m, np.clip(0.9 - d, 0, 1) * 0.8)
    return m


def build_tiles(primary, secondary, glow):
    """Returns dict tile -> (rgb HxWx3, emission HxW, metallic HxW, smooth HxW)."""
    global TILE
    tiles = {}
    for t, (_, _, size) in REGION.items():
        TILE = size
        tiles[t] = _paint_region(t, primary, secondary, glow)
    return tiles


def _paint_region(which, primary, secondary, glow):
    P, S, G = rgb(primary), rgb(secondary), rgb(glow)
    gm, gl, dk = rgb(GUNMETAL), rgb(GUN_LIGHT), rgb(DARKMETAL)
    u, v = _tile_coords()
    tiles = {}

    def paint(base_col, camo, seams_u, seams_v, seed, wear_amt):
        col = np.broadcast_to(base_col, (TILE, TILE, 3)).copy()
        # soft gradient: lighter towards the top of each region
        col *= (0.82 + 0.25 * v)[..., None]
        if camo:
            n = 0.75 * _noise(TILE, 3, seed) + 0.25 * _noise(TILE, 7, seed + 5)
            patch = np.clip((n - 0.5) * 16, 0, 1)[..., None]
            col = col * (1 - patch) + S * (0.85 + 0.2 * v[..., None]) * patch
        shade = 0.94 + 0.1 * _fbm(TILE, seed + 7)[..., None]
        col *= shade
        seam, bolt, centre = _seams_bolts(u, v, seams_u, seams_v)
        col *= (1 - 0.55 * seam)[..., None]
        col = col * (1 - bolt[..., None]) + (gl * 0.9) * bolt[..., None]
        col *= (1 - 0.6 * centre)[..., None]
        wear = np.clip((_fbm(TILE, seed + 13) - (1 - wear_amt)) * 8, 0, 1)
        sc = _scratches(seed + 21)
        exposed = np.maximum(wear, sc)
        col = col * (1 - exposed[..., None]) + gl * exposed[..., None]
        metal = 0.15 + 0.75 * exposed + 0.6 * bolt
        smooth = 0.42 + 0.15 * exposed
        return np.clip(col, 0, 1), np.zeros_like(u), np.clip(metal, 0, 1), smooth

    tiles[ARMOR] = paint(P, True, [0.34, 0.68], [0.5], 3, 0.26)
    tiles[LEGARMOR] = paint(P, True, [0.5], [0.33, 0.7], 11, 0.3)
    tiles[ACCENT] = paint(S, False, [0.5], [0.5], 17, 0.22)
    g = paint(gm, False, [0.25, 0.75], [0.25, 0.75], 29, 0.2)
    tiles[GUN] = (g[0], g[1], np.clip(g[2] + 0.55, 0, 1), g[3] + 0.05)
    d = paint(dk, False, [], [], 31, 0.12)
    tiles[DARK] = (d[0], d[1], np.clip(d[2] + 0.5, 0, 1), d[3])

    # eye: bright core, glow colour mid, dark iris ring and a small highlight
    r = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2) * 2
    core = np.clip(1.0 - r * 3.2, 0, 1) ** 1.5
    col = G[None, None, :] * (0.45 + 0.55 * np.clip(1 - r, 0, 1))[..., None]
    col = col * (1 - core[..., None]) + (0.55 * G + 0.45) * core[..., None]
    ring = np.clip(1 - np.abs(r - 0.82) * 10, 0, 1)
    col *= (1 - 0.7 * ring)[..., None]
    hl = np.clip(1 - np.sqrt((u - 0.36) ** 2 + (v - 0.66) ** 2) * 14, 0, 1)
    col = col * (1 - hl[..., None]) + hl[..., None]
    emi = np.clip(1.05 - r * 0.85, 0, 1) * (1 - 0.7 * ring)
    tiles[EYE] = (np.clip(col, 0, 1), emi, np.zeros_like(u), np.full_like(u, 0.9))

    # vent: gunmetal frame with four glowing slits
    slit = ((np.abs(((v * 4) % 1) - 0.5) < 0.22) & (u > 0.12) & (u < 0.88)).astype(np.float32)
    col = gm * 0.7 * (1 - slit[..., None]) + G * slit[..., None]
    tiles[VENT] = (col, slit, np.full_like(u, 0.6) * (1 - slit), np.full_like(u, 0.5))

    # small side lens
    r2 = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2) * 2
    lens = np.clip(1.2 - r2, 0, 1)
    col = G * (0.5 + 0.5 * lens[..., None]) + 0.25 * (lens ** 3)[..., None]
    tiles[LENS] = (np.clip(col, 0, 1), lens, np.zeros_like(u), np.full_like(u, 0.9))
    return tiles[which]


def write_png(path, arr):
    """arr: HxWx4 float 0..1 (row 0 = bottom, Blender convention)."""
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.pixels = arr.astype(np.float32).ravel()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


def build_atlases():
    os.makedirs(TEX_DIR, exist_ok=True)
    out = {}
    emission = mask = None
    for name, (p, s, g) in VARIANTS.items():
        tiles = build_tiles(p, s, g)
        base = np.zeros((ATLAS, ATLAS, 4), np.float32)
        base[..., 3] = 1
        emi = np.zeros((ATLAS, ATLAS, 4), np.float32)
        emi[..., 3] = 1
        msk = np.zeros((ATLAS, ATLAS, 4), np.float32)
        msk[..., 3] = 0.4
        for t, (col, e, m, sm) in tiles.items():
            c0, r0, n = REGION[t]
            base[r0:r0 + n, c0:c0 + n, :3] = col
            emi[r0:r0 + n, c0:c0 + n, :3] = e[..., None]
            msk[r0:r0 + n, c0:c0 + n, 0] = m
            msk[r0:r0 + n, c0:c0 + n, 3] = sm
        # textures are authored in display (sRGB) values; Blender stores float pixels linear-free for PNG
        out[name] = write_png(os.path.join(TEX_DIR, f"T_SpiderBot_{name}_BaseColor.png"), base)
        if emission is None:
            emission = write_png(os.path.join(TEX_DIR, "T_SpiderBot_Emission.png"), emi)
            mask = write_png(os.path.join(TEX_DIR, "T_SpiderBot_Mask.png"), msk)
    for img in list(out.values()) + [emission, mask]:
        img.colorspace_settings.name = 'sRGB'
    mask.colorspace_settings.name = 'Non-Color'
    return out, emission, mask


def make_materials(atlases, emission, mask):
    mats = {}
    for name, img in atlases.items():
        m = bpy.data.materials.new(f"M_SpiderBot_{name}")
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img
        te = nt.nodes.new("ShaderNodeTexImage"); te.image = emission
        tm = nt.nodes.new("ShaderNodeTexImage"); tm.image = mask
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
        nt.links.new(tm.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
        nt.links.new(tm.outputs[1], inv.inputs[1])
        nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
        mul = nt.nodes.new("ShaderNodeMixRGB"); mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1
        nt.links.new(te.outputs[0], mul.inputs[1])
        g = rgb(VARIANTS[name][2])
        mul.inputs[2].default_value = (*[float(x) ** 2.2 for x in g], 1)
        nt.links.new(mul.outputs[0], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 4.0
        mats[name] = m
    return mats


# ====================================================================== geometry
COLL = {}
PARTS = []


def coll(name):
    if name not in COLL:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
        COLL[name] = c
    return COLL[name]


def frame(zdir, xhint=Vector((1, 0, 0))):
    z = zdir.normalized()
    x = xhint - z * xhint.dot(z)
    if x.length < 1e-4:
        x = Vector((0, 1, 0)) - z * z.y
    x.normalize()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()


def _uv_tile(me, tile, mode):
    """Fit the part into its atlas tile. mode: 'box' (dominant-axis projection) or 'front' (XZ planar)."""
    px, py, n = REGION[tile]
    inset = 4.0 / ATLAS
    u0, v0 = px / ATLAS + inset, py / ATLAS + inset
    span = n / ATLAS - 2 * inset
    co = [v.co for v in me.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    size = mx - mn
    size = Vector((max(size.x, 1e-4), max(size.y, 1e-4), max(size.z, 1e-4)))
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i])) if mode == 'box' else 1
        a, b = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[ax]
        for li in poly.loop_indices:
            c = me.vertices[me.loops[li].vertex_index].co
            s = (c[a] - mn[a]) / size[a]
            t = (c[b] - mn[b]) / size[b]
            uv.data[li].uv = (u0 + s * span, v0 + t * span)


def _finish(bm, tile, bone, collection, mat, name, uvmode='box'):
    bm.transform(mat)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    coll(collection).objects.link(ob)
    _uv_tile(me, tile, uvmode)
    ob["bone"] = bone
    PARTS.append(ob)
    return ob


def sphere(center, radii, tile, bone, collection, name, segs=12, rings=8, rot=None, cut=None, uvmode='box'):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    if cut is not None:
        g = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, cut), plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    m = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4))
    return _finish(bm, tile, bone, collection, m, name, uvmode)


def tube(a, b, r1, r2, tile, bone, collection, name, segs=8, cap=True, xhint=Vector((1, 0, 0)), sxy=(1, 1), uvmode='box'):
    a, b = Vector(a), Vector(b)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=(b - a).length)
    bmesh.ops.scale(bm, vec=Vector((sxy[0], sxy[1], 1)), verts=bm.verts)
    m = Matrix.Translation((a + b) / 2) @ frame(b - a, xhint)
    return _finish(bm, tile, bone, collection, m, name, uvmode)


def box(center, size, tile, bone, collection, name, bevel=0.0, segs=1, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segs, affect='EDGES', profile=0.5)
    m = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4))
    return _finish(bm, tile, bone, collection, m, name)


def taper_box(a, b, w1, w2, t1, t2, tile, bone, collection, name, up=Vector((0, 0, 1))):
    """Tapered armour plate along a->b (width w, thickness t), slightly rounded via a 1-seg bevel."""
    a, b = Vector(a), Vector(b)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for vtx in bm.verts:
        k = vtx.co.z + 0.5
        w = w1 + (w2 - w1) * k
        t = t1 + (t2 - t1) * k
        vtx.co.x *= w
        vtx.co.y *= t
        vtx.co.z *= (b - a).length
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(w1, w2, t1, t2) * 0.25, segments=1, affect='EDGES', profile=0.5)
    m = Matrix.Translation((a + b) / 2) @ frame(b - a, up)
    return _finish(bm, tile, bone, collection, m, name)


# ---------------------------------------------------------------------- skeleton
BODY_Z = 0.6
LEG_ANGLES = (35.0, 72.0, 108.0, 145.0)       # yaw from +Y (front) toward the side
COXA_R, HIP_R, FOOT_R = 0.42, 0.52, 1.0
COXA_Z, HIP_Z = 0.52, 0.55
FEMUR, TIBIA = 0.45, 0.75
LEGS = []                                       # (name, side, yaw_deg)
for side, s in (("L", -1), ("R", 1)):
    for i, a in enumerate(LEG_ANGLES):
        LEGS.append((f"{side}{i + 1}", s, a))


def leg_dir(s, a):
    r = math.radians(a)
    return Vector((s * math.sin(r), math.cos(r), 0))


def solve_knee(H, F, s, a):
    """2-bone IK in the vertical leg plane, knee up."""
    d = F - H
    dist = d.length
    alpha = math.acos(max(-1, min(1, (FEMUR ** 2 + dist ** 2 - TIBIA ** 2) / (2 * FEMUR * dist))))
    dirn = d.normalized()
    side_axis = leg_dir(s, a).cross(Vector((0, 0, 1))).normalized()   # horizontal, perpendicular to the plane
    # rotate dirn upward by alpha around side_axis (pick the sign that raises the knee)
    for sign in (1, -1):
        k = H + (Quaternion(side_axis, sign * alpha) @ dirn) * FEMUR
        if k.z > (H + dirn * FEMUR).z:
            return k
    return H + dirn * FEMUR


BONES = {}


def define_bones():
    B = BONES
    B["Root"] = (Vector((0, 0, 0)), Vector((0, 0.3, 0)), None, True)
    B["Body"] = (Vector((0, 0, BODY_Z)), Vector((0, 0, BODY_Z + 0.3)), "Root", True)
    B["Eye"] = (Vector((0, 0.42, 0.68)), Vector((0, 0.62, 0.68)), "Body", True)
    for side, s in (("L", -1), ("R", 1)):
        B["Antenna" + side] = (Vector((s * 0.24, -0.04, 1.0)), Vector((s * 0.26, -0.04, 1.2)), "Body", True)
    for name, s, a in LEGS:
        dvec = leg_dir(s, a)
        C = Vector((0, 0, COXA_Z)) + dvec * COXA_R
        H = Vector((0, 0, HIP_Z)) + dvec * HIP_R
        F = dvec * FOOT_R
        K = solve_knee(H, F, s, a)
        B["Coxa_" + name] = (C, H, "Body", True)
        B["Femur_" + name] = (H, K, "Coxa_" + name, True)
        B["Tibia_" + name] = (K, F, "Femur_" + name, True)
        B["FootIK_" + name] = (F, F + Vector((0, 0, 0.15)), "Root", False)


def build_parts():
    Bc, Lc, Sc = "Body", "Legs", "Sensor"
    # ---- body shell (chunky, rounded)
    sphere((0, 0, 0.62), (0.6, 0.6, 0.44), ARMOR, "Body", Bc, "BodyCore", segs=16, rings=10)
    sphere((0, -0.03, 0.66), (0.64, 0.62, 0.42), ARMOR, "Body", Bc, "TopShell", segs=16, rings=10, cut=0.1)
    tube((0, 0, 0.6), (0, 0, 0.68), 0.64, 0.66, GUN, "Body", Bc, "WaistBand", segs=16, cap=False)
    tube((0, -0.12, 1.06), (0, -0.12, 1.11), 0.15, 0.13, GUN, "Body", Bc, "TopHatch", segs=10)
    box((0, -0.58, 0.64), (0.34, 0.1, 0.2), GUN, "Body", Bc, "VentFrame", bevel=0.02)
    box((0, -0.635, 0.64), (0.26, 0.02, 0.13), VENT, "Body", Bc, "VentSlits")
    for s in (-1, 1):
        # side sensor pods
        tube((s * 0.5, 0.16, 0.74), (s * 0.6, 0.18, 0.74), 0.11, 0.1, GUN, "Body", Bc, "SidePod", segs=10, xhint=Vector((0, 1, 0)))
        tube((s * 0.6, 0.18, 0.74), (s * 0.615, 0.18, 0.74), 0.065, 0.065, LENS, "Body", Sc, "SideLens", segs=10, xhint=Vector((0, 1, 0)), uvmode='box')
        # armour plates over the leg roots
        sphere((s * 0.47, -0.02, 0.58), (0.16, 0.34, 0.12), ACCENT, "Body", Bc, "HipPlate", segs=10, rings=6)
        # antennae (pop off on death)
        taper_box((s * 0.24, -0.04, 0.98), (s * 0.27, -0.06, 1.22), 0.1, 0.05, 0.08, 0.04, ACCENT, "Antenna" + ("L" if s < 0 else "R"), Bc, "Antenna")
        sphere((s * 0.27, -0.06, 1.24), (0.04, 0.04, 0.04), LENS, "Antenna" + ("L" if s < 0 else "R"), Sc, "AntennaTip", segs=8, rings=5)

    # ---- big central eye
    tube((0, 0.42, 0.68), (0, 0.6, 0.68), 0.32, 0.31, GUN, "Body", Sc, "EyeSocket", segs=20, xhint=Vector((1, 0, 0)))
    tube((0, 0.6, 0.68), (0, 0.625, 0.68), 0.31, 0.29, ACCENT, "Body", Sc, "EyeRim", segs=20, cap=False)
    sphere((0, 0.57, 0.68), (0.25, 0.25, 0.12), EYE, "Eye", Sc, "EyeLens", segs=16, rings=8,
           rot=Matrix.Rotation(math.radians(-90), 4, 'X'), cut=0.0, uvmode='front')
    for s in (-1, 1):
        box((s * 0.24, 0.62, 0.88), (0.1, 0.06, 0.05), GUN, "Body", Sc, "EyeBrow", bevel=0.012, rot=Matrix.Rotation(math.radians(s * -18), 4, 'Y'))

    # ---- legs (gunmetal joints, armoured shins, dark claws)
    for name, s, a in LEGS:
        C, H, _, _ = BONES["Coxa_" + name]
        _, K, _, _ = BONES["Femur_" + name]
        _, F, _, _ = BONES["Tibia_" + name]
        sphere(C + leg_dir(s, a) * 0.04, (0.085, 0.085, 0.085), GUN, "Coxa_" + name, Lc, "Hip", segs=8, rings=6)
        tube(H, K, 0.06, 0.055, GUN, "Femur_" + name, Lc, "Femur", segs=6)
        up = (K - H).normalized()
        taper_box(H + up * 0.08, K - up * 0.04, 0.11, 0.13, 0.07, 0.09, LEGARMOR, "Femur_" + name, Lc, "ThighPlate", up=Vector((0, 0, 1)))
        sphere(K, (0.085, 0.085, 0.085), GUN, "Tibia_" + name, Lc, "Knee", segs=8, rings=6)
        down = (F - K).normalized()
        taper_box(K + down * 0.03, F - down * 0.15, 0.19, 0.11, 0.16, 0.09, LEGARMOR, "Tibia_" + name, Lc, "Shin", up=leg_dir(s, a))
        tube(F - down * 0.17, F, 0.05, 0.012, DARK, "Tibia_" + name, Lc, "Claw", segs=6)


# ---------------------------------------------------------------------- armature
def build_armature():
    data = bpy.data.armatures.new("SpiderBot_Rig")
    arm = bpy.data.objects.new("SpiderBot_Rig", data)
    coll("Rig").objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    ebs = {}
    for n, (h, t, p, deform) in BONES.items():
        eb = data.edit_bones.new(n)
        eb.head, eb.tail = h, t
        eb.use_deform = deform
        ebs[n] = eb
    for n, (h, t, p, deform) in BONES.items():
        if p:
            ebs[n].parent = ebs[p]
            ebs[n].use_connect = (ebs[p].tail - h).length < 1e-5
        ebs[n].align_roll(Vector((0, 0, 1)) if abs((t - h).normalized().z) < 0.95 else Vector((0, 1, 0)))
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def add_ik(arm):
    for name, s, a in LEGS:
        pb = arm.pose.bones["Tibia_" + name]
        c = pb.constraints.new('IK')
        c.target = arm
        c.subtarget = "FootIK_" + name
        c.chain_count = 3
        c.use_tail = True
        c.use_stretch = False
        cx = arm.pose.bones["Coxa_" + name]
        cx.lock_ik_x = True; cx.lock_ik_y = True
        for b in ("Femur_", "Tibia_"):
            p = arm.pose.bones[b + name]
            p.lock_ik_y = True; p.lock_ik_z = True
            p.ik_stretch = 0.0


def remove_ik(arm):
    for pb in arm.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)


# ---------------------------------------------------------------------- skin + join
def build_export_mesh(arm, mat):
    copies = []
    for ob in PARTS:
        c = ob.copy()
        c.data = ob.data.copy()
        coll("Export").objects.link(c)
        vg = c.vertex_groups.new(name=ob["bone"])
        vg.add(list(range(len(c.data.vertices))), 1.0, 'REPLACE')
        copies.append(c)
        ob.hide_render = True
        ob.hide_set(True)
    bpy.ops.object.select_all(action='DESELECT')
    for c in copies:
        c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    me_ob = bpy.context.view_layer.objects.active
    me_ob.name = me_ob.data.name = "SpiderBot"
    if "bone" in me_ob:
        del me_ob["bone"]
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    me_ob.data.materials.clear()
    me_ob.data.materials.append(mat)
    me_ob.parent = arm
    mod = me_ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    return me_ob


# ---------------------------------------------------------------------- animation
REST = {}


def to_local(bone, world_vec):
    return REST[bone].inverted() @ world_vec


def key_bone(arm, bone, f, loc=None, rot=None, scale=None):
    """loc: armature-space offset; rot: list of (axis, deg) in armature space."""
    pb = arm.pose.bones[bone]
    if loc is not None:
        pb.location = to_local(bone, Vector(loc))
        pb.keyframe_insert("location", frame=f)
    if rot is not None:
        q = Quaternion()
        for ax, deg in rot:
            R = Quaternion(Vector(ax), math.radians(deg))
            q = q @ (REST[bone].inverted() @ R @ REST[bone])
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=f)
    if scale is not None:
        pb.scale = Vector((scale, scale, scale))
        pb.keyframe_insert("scale", frame=f)


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
GROUP_A = {"L1", "R2", "L3", "R4"}


def gait(arm, T, stride, lift, bob, look=0.0):
    """Alternating-tetrapod walk in place. Body travels +Y at stride / (T/2) frames."""
    for f in range(1, T + 2):
        t = (f - 1) / T
        for name, s, a in LEGS:
            p = (t + (0.0 if name in GROUP_A else 0.5)) % 1.0
            if p < 0.5:
                y = stride / 2 - stride * (p / 0.5)
                z = 0.0
            else:
                q = (p - 0.5) / 0.5
                e = q * q * (3 - 2 * q)
                y = -stride / 2 + stride * e
                z = lift * math.sin(math.pi * q)
            key_bone(arm, "FootIK_" + name, f, loc=(0, y, z))
        key_bone(arm, "Body", f, loc=(0, 0, -bob + bob * math.cos(4 * math.pi * t)),
                 rot=[(Y, 2.5 * math.sin(2 * math.pi * t)), (X, -2.0)])
        key_bone(arm, "Eye", f, rot=[(Z, look * math.sin(2 * math.pi * t))])
    _linear(arm)


def _linear(arm):
    act = arm.animation_data.action
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    for k in fc.keyframe_points:
                        k.interpolation = 'LINEAR'


def anim_idle(arm):
    T = 48
    for f in range(1, T + 2, 4):
        t = (f - 1) / T
        key_bone(arm, "Body", f, loc=(0, 0, -0.02 + 0.02 * math.cos(2 * math.pi * t)), rot=[(Z, 3 * math.sin(2 * math.pi * t))])
        key_bone(arm, "Eye", f, rot=[(Z, 16 * math.sin(2 * math.pi * t)), (X, 5 * math.sin(4 * math.pi * t))])
        for name, s, a in LEGS:
            z = 0.0
            if name == "R1":
                z = 0.06 * max(0.0, math.sin(2 * math.pi * (t * 2 - 0.1))) if t < 0.5 else 0.0
            if name == "L2":
                z = 0.05 * max(0.0, math.sin(2 * math.pi * (t * 2 - 1.1))) if t >= 0.5 else 0.0
            key_bone(arm, "FootIK_" + name, f, loc=(0, 0, z))
        for side in ("L", "R"):
            key_bone(arm, "Antenna" + side, f, rot=[(Y, (4 if side == "R" else -4) * math.sin(2 * math.pi * t * 2))])


def anim_attack(arm):
    # crouch + aim (charge), fire with recoil, recover. Fire frame = 13 (0.4 s at 30 fps)
    keys = [
        (1, 0.00, 0.00, 0), (9, -0.13, -0.06, -10), (12, -0.15, -0.08, -12),
        (13, -0.06, -0.16, -4), (16, -0.04, -0.12, 2), (24, -0.01, -0.02, 0), (34, 0.0, 0.0, 0)]
    for f, dz, dy, pitch in keys:
        key_bone(arm, "Body", f, loc=(0, dy, dz), rot=[(X, pitch)])
        key_bone(arm, "Eye", f, rot=[(X, pitch * 0.4)], scale=1.12 if f in (9, 12) else 1.0)
        for name, s, a in LEGS:
            key_bone(arm, "FootIK_" + name, f, loc=(0, 0, 0))
        for side in ("L", "R"):
            key_bone(arm, "Antenna" + side, f, rot=[(X, -pitch * 1.5)])


def anim_death(arm):
    # recoil, sparks/flash (script), antennae pop off, legs splay, cartoon collapse with a bounce
    for f, dz, pitch, roll in [(1, 0, 0, 0), (5, 0.16, 18, 0), (10, 0.05, 8, -6), (18, -0.36, -6, 12), (22, -0.30, -4, 9), (26, -0.38, -5, 13), (40, -0.38, -5, 13)]:
        key_bone(arm, "Body", f, loc=(0, -0.05 if f > 1 else 0, dz), rot=[(X, pitch), (Y, roll)])
    for f, sc, tilt in [(1, 1.0, 0), (5, 1.15, 10), (18, 0.85, -25), (40, 0.85, -25)]:
        key_bone(arm, "Eye", f, rot=[(X, tilt)], scale=sc)
    for side, s in (("L", -1), ("R", 1)):
        b = "Antenna" + side
        key_bone(arm, b, 1, loc=(0, 0, 0), rot=[], scale=1.0)
        key_bone(arm, b, 5, loc=(0, 0, 0), rot=[], scale=1.0)
        key_bone(arm, b, 12, loc=(s * 0.35, -0.1, 0.55), rot=[(Y, s * 140)], scale=1.0)
        key_bone(arm, b, 20, loc=(s * 0.6, -0.2, 0.25), rot=[(Y, s * 260)], scale=0.6)
        key_bone(arm, b, 24, loc=(s * 0.65, -0.2, 0.0), rot=[(Y, s * 300)], scale=0.01)
        key_bone(arm, b, 40, loc=(s * 0.65, -0.2, 0.0), rot=[(Y, s * 300)], scale=0.01)
    for name, s, a in LEGS:
        out = leg_dir(s, a)
        for f, k, z in [(1, 0, 0), (5, 0.05, 0.12), (18, 0.38, 0.0), (22, 0.34, 0.03), (26, 0.4, 0.0), (40, 0.4, 0.0)]:
            key_bone(arm, "FootIK_" + name, f, loc=tuple(out * k + Vector((0, 0, z))))


def bake(arm, src, name, f0, f1):
    arm.animation_data.action = src
    bpy.context.scene.frame_set(f0)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.nla.bake(frame_start=f0, frame_end=f1, step=1, only_selected=True, visual_keying=True,
                     clear_constraints=False, clear_parents=False, use_current_action=False,
                     clean_curves=False, bake_types={'POSE'})
    bpy.ops.object.mode_set(mode='OBJECT')
    out = arm.animation_data.action
    out.name = name
    out.use_fake_user = True
    return out


def make_actions(arm):
    for pb in arm.pose.bones:
        REST[pb.name] = pb.bone.matrix_local.to_quaternion()
    arm.animation_data_create()
    sc = bpy.context.scene
    sc.render.fps = 30
    plan = [
        ("Idle", 1, 49, anim_idle),
        ("Roam", 1, 25, lambda a: gait(a, 24, 0.42, 0.12, 0.012, look=12)),
        ("Walk", 1, 17, lambda a: gait(a, 16, 0.80, 0.18, 0.025)),
        ("Attack", 1, 34, anim_attack),
        ("Death", 1, 40, anim_death),
    ]
    srcs, final = [], {}
    for name, f0, f1, fn in plan:
        src = bpy.data.actions.new(name + "_src")
        arm.animation_data.action = src
        # full rest key first so channels an action doesn't animate never leak in from another action
        for pb in arm.pose.bones:
            key_bone(arm, pb.name, f0, loc=(0, 0, 0), rot=[], scale=1.0)
        fn(arm)
        srcs.append((name, src, f0, f1))
    for name, src, f0, f1 in srcs:
        final[name] = bake(arm, src, name, f0, f1)
    for _, src, _, _ in srcs:
        bpy.data.actions.remove(src)
    remove_ik(arm)
    # rest pose for the file
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)
    arm.animation_data.action = final["Idle"]
    return final


# ---------------------------------------------------------------------- reference, export, preview
def add_reference():
    c = coll("Reference")
    if os.path.exists(REF):
        img = bpy.data.images.load(REF, check_existing=True)
        e = bpy.data.objects.new("Ref_ConceptSheet", None)
        e.empty_display_type = 'IMAGE'
        e.data = img
        e.empty_display_size = 4
        e.location = (0, 3, 1.5)
        e.rotation_euler = (math.radians(90), 0, math.radians(180))
        c.objects.link(e)
    # scale references: 1.8 m person, 1.2 m spider height mark, a train car block
    me = bpy.data.meshes.new("Ref_Person")
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1); bmesh.ops.scale(bm, vec=(0.45, 0.3, 1.8), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(-2.2, 0, 0.9), verts=bm.verts); bm.to_mesh(me); bm.free()
    p = bpy.data.objects.new("Ref_Person_1.8m", me); c.objects.link(p); p.display_type = 'WIRE'; p.hide_render = True


def export(arm, mesh):
    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='Y', axis_up='Z', bake_space_transform=False,
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        armature_nodetype='NULL', use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)


def render_previews(arm, mesh, mats, acts):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.24, 0.15, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sun.data.energy = 3.2
    sun.data.color = (1, 0.8, 0.62); sun.rotation_euler = (math.radians(48), 0, math.radians(200))
    sc.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=40)
    fl = bpy.context.active_object
    fm = bpy.data.materials.new("Sand"); fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0.62, 0.3, 0.16, 1)
    fl.data.materials.append(fm)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam

    # four variants side by side (+ a small crowd for the gameplay view)
    rigs = [(arm, mesh)]
    names = list(mats)
    mesh.data.materials[0] = mats[names[0]]
    for i in range(1, 12):
        a2 = arm.copy(); sc.collection.objects.link(a2)
        a2.animation_data.action = arm.animation_data.action
        m2 = mesh.copy(); m2.data = mesh.data.copy(); sc.collection.objects.link(m2)
        m2.parent = a2; m2.modifiers["Armature"].object = a2
        m2.data.materials[0] = mats[names[i % 4]]
        rigs.append((a2, m2))

    def place(n, spacing=2.3, rows=False):
        for i, (a, m) in enumerate(rigs):
            show = i < n
            a.hide_render = m.hide_render = not show
            if rows:
                a.location = ((i % 4) * spacing - 1.5 * spacing, (i // 4) * -2.6, 0)
                a.rotation_euler = (0, 0, math.radians(((i * 37) % 60) - 30))
            else:
                a.location = (i * spacing - (n - 1) * spacing / 2, 0, 0)
                a.rotation_euler = (0, 0, 0)

    def shot(name, loc, look):
        cam.location = loc
        cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(PREVIEW, name + ".png")
        bpy.ops.render.render(write_still=True)

    def set_action(name, frame):
        for a, _ in rigs:
            a.animation_data.action = acts[name]
        sc.frame_set(frame)

    cam.data.lens = 50
    place(4)
    set_action("Idle", 1)
    shot("spider_variants_front", (1.5, 7.5, 2.4), (0, 0, 0.55))
    shot("spider_variants_threequarter", (6.5, 5.5, 3.6), (0, 0, 0.5))
    place(1)
    for nm, fr in (("Walk", 5), ("Attack", 12), ("Death", 24)):
        set_action(nm, fr)
        shot(f"spider_{nm.lower()}", (2.4, 3.2, 1.6), (0, 0, 0.5))
    # gameplay camera: tilted top-down from far away, 12 spiders mid-walk
    place(12, spacing=3.0, rows=True)
    set_action("Walk", 7)
    cam.data.lens = 35
    for a, _ in rigs:
        a.rotation_euler.z += math.radians(180)
    shot("spider_gameplay_crowd", (0, 22, 24), (0, -2.5, 0))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    atlases, emission, mask = build_atlases()
    mats = make_materials(atlases, emission, mask)
    define_bones()
    build_parts()
    arm = build_armature()
    add_ik(arm)
    mesh = build_export_mesh(arm, mats["Red"])
    acts = make_actions(arm)
    add_reference()
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print("TRIS", tris, "VERTS", len(mesh.data.vertices), "BONES", len([b for b in arm.data.bones if b.use_deform]))
    print("ACTIONS", {a.name: tuple(a.frame_range) for a in bpy.data.actions})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "DRL_SpiderBot.blend"))
    export(arm, mesh)
    print("EXPORTED", OUT_FBX)
    if "--render" in sys.argv:
        render_previews(arm, mesh, mats, acts)
    print("DONE")


main()
