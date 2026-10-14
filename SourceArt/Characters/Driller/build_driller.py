"""
Drillex crew character "The Driller" - mobile-first build, rig, animation and FBX export (Blender 5.2).

Run headless:
  blender -b --factory-startup --python build_driller.py -- [--render]

Art direction: chunky stylised miner matching the drill train (red / hazard yellow / gunmetal).
Budget: one material, one 64x64 palette (base colour generated at runtime in Unity per colourway),
~6-8k tris for body + one headgear. Front faces +Y (Unity +Z after export), Z up, 1 unit = 1 m.

Every face UV sits on the centre of one palette cell (8x8 px cells in a 64x64 texture, rows 0-3),
except hazard-stripe parts which are projected onto the stripe band (rows 4-7). The cell table is
mirrored in Assets/Script/Crew/CrewPalette.cs - keep the indices in sync.
"""
import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_FBX = r"D:\Drillex\Assets\Art\Characters\DRL_Driller\Meshes\SK_DRL_Driller.fbx"
PREVIEW = os.path.join(HERE, "_preview")

# ------------------------------------------------------------------ palette cells
SUIT, SUIT_DARK, ACCENT, HELMET, METAL, METAL_DARK, RUBBER, LEATHER = range(0, 8)
SKIN, SKIN_DARK, HAIR, EYE_WHITE, EYE_DARK, LENS, VISOR, BRASS = range(8, 16)
TANK, CLOTH, GLASS = 16, 17, 18
STRIPE = -1  # projected onto the hazard stripe band

# default "Crimson" colourway, only used for Blender previews (runtime colours live in CrewPalette.cs)
PREVIEW_COLORS = {
    SUIT: "B3261E", SUIT_DARK: "5E1712", ACCENT: "F2B10F", HELMET: "F2B10F", METAL: "9AA0A6",
    METAL_DARK: "3A3D42", RUBBER: "232427", LEATHER: "6B4126", SKIN: "D9A07A", SKIN_DARK: "B87A57",
    HAIR: "5A3A22", EYE_WHITE: "F2EEE6", EYE_DARK: "1A1410", LENS: "FFE7A0", VISOR: "FF7A1E",
    BRASS: "C9962E", TANK: "D0CEC4", CLOTH: "3E4A3D", GLASS: "5BCBFF",
}
EMISSIVE = {LENS, VISOR, GLASS}
STRIPE_A, STRIPE_B = "F2B10F", "1C1C1E"


def cell_uv(c):
    return ((c % 8) + 0.5) / 8.0, ((c // 8) + 0.5) / 8.0


# ------------------------------------------------------------------ skeleton (A-pose, front +Y, character left = -X)
SHOULDER_Z = 1.44
ARM_DIR = Vector((math.sin(math.radians(48)), 0, -math.cos(math.radians(48))))


def arm_points(s):
    sh = Vector((s * 0.27, 0.0, SHOULDER_Z))
    d = Vector((s * ARM_DIR.x, 0, ARM_DIR.z))
    el = sh + d * 0.28
    wr = el + d * 0.26
    hd = wr + d * 0.15
    return sh, el, wr, hd


BONES = {}  # name -> (head, tail, parent)


def define_bones():
    B = BONES
    B["Root"] = (Vector((0, 0, 0)), Vector((0, 0.2, 0)), None)
    B["Hips"] = (Vector((0, 0, 0.93)), Vector((0, 0, 1.05)), "Root")
    B["Spine"] = (Vector((0, 0, 1.05)), Vector((0, 0, 1.20)), "Hips")
    B["Chest"] = (Vector((0, 0, 1.20)), Vector((0, 0, 1.46)), "Spine")
    B["Neck"] = (Vector((0, 0.01, 1.46)), Vector((0, 0.01, 1.58)), "Chest")
    B["Head"] = (Vector((0, 0.01, 1.58)), Vector((0, 0.01, 1.92)), "Neck")
    for side, s in (("Left", -1), ("Right", 1)):
        sh, el, wr, hd = arm_points(s)
        B[side + "Shoulder"] = (Vector((s * 0.06, 0, 1.42)), sh.copy(), "Chest")
        B[side + "UpperArm"] = (sh, el, side + "Shoulder")
        B[side + "LowerArm"] = (el, wr, side + "UpperArm")
        B[side + "Hand"] = (wr, hd, side + "LowerArm")
        hip = Vector((s * 0.11, 0, 0.92))
        knee = Vector((s * 0.12, 0.012, 0.50))
        ankle = Vector((s * 0.13, 0, 0.12))
        ball = Vector((s * 0.13, 0.14, 0.03))
        tip = Vector((s * 0.13, 0.25, 0.03))
        B[side + "UpperLeg"] = (hip, knee, "Hips")
        B[side + "LowerLeg"] = (knee, ankle, side + "UpperLeg")
        B[side + "Foot"] = (ankle, ball, side + "LowerLeg")
        B[side + "Toes"] = (ball, tip, side + "Foot")


# ------------------------------------------------------------------ geometry helpers
PARTS = {"Body": [], "HG_HardHat": [], "HG_Welder": [], "HG_Goggles": []}


def frame(zdir, xhint=Vector((1, 0, 0))):
    """Rotation matrix whose local Z points along zdir."""
    z = zdir.normalized()
    x = xhint - z * xhint.dot(z)
    if x.length < 1e-4:
        x = Vector((0, 1, 0)) - z * z.y
    x.normalize()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()


def _finish(bm, group, cell, weights, mat=Matrix.Identity(4), name="part"):
    bm.transform(mat)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    uv = me.uv_layers.new(name="UVMap")
    if cell == STRIPE:
        # project on the dominant front plane: 64 px per 0.4 m, band rows 4-7
        xs = [v.co.x for v in me.vertices]
        zs = [v.co.z for v in me.vertices]
        x0, z0 = min(xs), min(zs)
        for li, loop in enumerate(me.loops):
            co = me.vertices[loop.vertex_index].co
            u = 0.02 + (co.x - x0) / 0.4
            v = 0.56 + (co.z - z0) / 0.4
            uv.data[li].uv = (u, min(v, 0.98))
    else:
        u, v = cell_uv(cell)
        for d in uv.data:
            d.uv = (u, v)
    ob["weights"] = repr(weights)
    PARTS[group].append(ob)
    return ob


def box(group, center, size, cell, weights, bevel=0.0, segs=2, rot=None, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segs, affect='EDGES', profile=0.5)
    m = Matrix.Translation(Vector(center)) @ (rot if rot is not None else Matrix.Identity(4))
    return _finish(bm, group, cell, weights, m, name)


def tube(group, a, b, r1, r2, cell, weights, segs=12, cap=True, name="tube", xhint=Vector((1, 0, 0)), scale_xy=(1, 1)):
    a, b = Vector(a), Vector(b)
    L = (b - a).length
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=L)
    bmesh.ops.scale(bm, vec=Vector((scale_xy[0], scale_xy[1], 1)), verts=bm.verts)
    m = Matrix.Translation((a + b) / 2) @ frame(b - a, xhint)
    return _finish(bm, group, cell, weights, m, name)


def blob(group, center, radii, cell, weights, segs=12, rings=8, name="blob", rot=None, cut_below=None, fill=True):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    if cut_below is not None:
        # keep the upper part (local z >= cut_below), fill the hole
        geom = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, cut_below), plane_no=(0, 0, 1), clear_inner=True)
        edges = [e for e in geom["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges and fill:
            bmesh.ops.edgeloop_fill(bm, edges=edges)
    bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
    m = Matrix.Translation(Vector(center)) @ (rot if rot is not None else Matrix.Identity(4))
    return _finish(bm, group, cell, weights, m, name)


def Rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'X')


def Ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Y')


def Rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Z')


# ------------------------------------------------------------------ the character
def build_body():
    G = "Body"
    TORSO = ("zband", [("Hips", 0.0, 1.03), ("Spine", 1.07, 1.18), ("Chest", 1.24, 9.0)])

    for side, s in (("Left", -1), ("Right", 1)):
        x = s * 0.13
        F, LL, UL = side + "Foot", side + "LowerLeg", side + "UpperLeg"
        # boots: chunky, toe-capped, thick sole
        box(G, (x, 0.045, 0.085), (0.17, 0.30, 0.13), RUBBER, [F], bevel=0.035, name="boot")
        box(G, (x, 0.045, 0.018), (0.185, 0.32, 0.036), METAL_DARK, [F], bevel=0.01, segs=1, name="sole")
        box(G, (x, 0.165, 0.075), (0.165, 0.085, 0.105), METAL, [F], bevel=0.03, name="toecap")
        box(G, (x, -0.02, 0.15), (0.175, 0.04, 0.03), LEATHER, [F], bevel=0.008, segs=1, name="bootstrap")
        tube(G, (x, 0, 0.12), (x, 0, 0.27), 0.088, 0.085, RUBBER, [F, LL], name="bootcuff")
        # trousers
        tube(G, (x, 0.004, 0.25), (s * 0.12 + 0.0, 0.012, 0.52), 0.074, 0.083, SUIT, [LL, UL], name="shin")
        box(G, (s * 0.122, 0.085, 0.50), (0.13, 0.05, 0.15), METAL, [LL], bevel=0.02, name="kneepad")
        box(G, (s * 0.122, 0.11, 0.50), (0.06, 0.012, 0.06), BRASS, [LL], bevel=0.005, segs=1, name="kneebolt")
        tube(G, (s * 0.12, 0.012, 0.48), (s * 0.11, 0, 0.95), 0.088, 0.112, SUIT, [UL, "Hips", LL], name="thigh")
        box(G, (s * 0.205, 0.0, 0.72), (0.045, 0.13, 0.15), SUIT_DARK, [UL], bevel=0.012, segs=1, name="pocket")

    # pelvis, belt, pouches
    box(G, (0, 0, 0.96), (0.36, 0.24, 0.20), SUIT, [TORSO], bevel=0.06, segs=2, name="pelvis")
    tube(G, (0, 0, 0.965), (0, 0, 1.045), 0.205, 0.205, LEATHER, ["Hips"], segs=16, scale_xy=(1, 0.72), name="belt")
    box(G, (0, 0.152, 1.005), (0.09, 0.022, 0.07), BRASS, ["Hips"], bevel=0.01, segs=1, name="buckle")
    for s in (-1, 1):
        box(G, (s * 0.15, 0.10, 0.975), (0.085, 0.06, 0.10), LEATHER, ["Hips"], bevel=0.015, segs=1, name="pouch")
        box(G, (s * 0.15, 0.132, 1.012), (0.088, 0.012, 0.035), METAL_DARK, ["Hips"], bevel=0.004, segs=1, name="pouchflap")
    # wrench hanging from the belt (right hip)
    box(G, (0.205, 0.04, 0.88), (0.025, 0.035, 0.20), METAL, ["Hips"], bevel=0.006, segs=1, rot=Ry(-8), name="wrench")
    box(G, (0.215, 0.04, 0.775), (0.03, 0.07, 0.05), METAL, ["Hips"], bevel=0.008, segs=1, name="wrenchhead")

    # torso: barrel chest jumpsuit
    box(G, (0, 0, 1.24), (0.46, 0.29, 0.46), SUIT, [TORSO], bevel=0.11, segs=3, name="torso")
    box(G, (0, 0.005, 1.10), (0.40, 0.25, 0.10), SUIT_DARK, [TORSO], bevel=0.04, segs=2, name="waist")
    # harness: straps, hazard band, badge
    for s in (-1, 1):
        box(G, (s * 0.115, 0.146, 1.27), (0.065, 0.02, 0.36), ACCENT, [TORSO], bevel=0.008, segs=1, name="strap")
        box(G, (s * 0.115, -0.146, 1.27), (0.065, 0.02, 0.36), ACCENT, [TORSO], bevel=0.008, segs=1, name="strapback")
        box(G, (s * 0.115, 0.158, 1.39), (0.04, 0.012, 0.04), BRASS, [TORSO], bevel=0.005, segs=1, name="clasp")
    box(G, (0, 0.152, 1.19), (0.40, 0.022, 0.065), STRIPE, [TORSO], bevel=0.006, segs=1, name="hazardband")
    tube(G, (-0.05, 0.15, 1.33), (-0.05, 0.168, 1.33), 0.04, 0.04, METAL, [TORSO], segs=12, name="badge")
    tube(G, (-0.05, 0.166, 1.33), (-0.05, 0.172, 1.33), 0.026, 0.026, SUIT, [TORSO], segs=12, name="badgecore")
    # collar
    tube(G, (0, 0.005, 1.43), (0, 0.008, 1.50), 0.13, 0.105, SUIT_DARK, [("zband", [("Chest", 0, 1.47), ("Neck", 1.50, 9)])], segs=14, name="collar")

    # backpack: frame + twin tanks + exhaust
    box(G, (0, -0.19, 1.26), (0.34, 0.08, 0.40), METAL_DARK, ["Chest"], bevel=0.02, segs=1, name="packframe")
    for s in (-1, 1):
        tube(G, (s * 0.085, -0.265, 1.08), (s * 0.085, -0.265, 1.44), 0.07, 0.07, TANK, ["Chest"], segs=12, name="tank")
        blob(G, (s * 0.085, -0.265, 1.44), (0.07, 0.07, 0.04), TANK, ["Chest"], segs=12, rings=6, cut_below=0.0, name="tankcap")
        tube(G, (s * 0.085, -0.265, 1.10), (s * 0.085, -0.265, 1.14), 0.073, 0.073, ACCENT, ["Chest"], segs=12, name="tankband")
        tube(G, (s * 0.085, -0.265, 1.36), (s * 0.085, -0.265, 1.40), 0.073, 0.073, ACCENT, ["Chest"], segs=12, name="tankband")
    tube(G, (0.0, -0.265, 1.47), (0.0, -0.265, 1.56), 0.022, 0.026, METAL, ["Chest"], segs=8, name="valve")
    tube(G, (0.15, -0.24, 1.40), (0.17, -0.25, 1.58), 0.022, 0.026, METAL, ["Chest"], segs=8, name="exhaust")
    tube(G, (0.17, -0.25, 1.58), (0.172, -0.25, 1.61), 0.03, 0.03, METAL_DARK, ["Chest"], segs=8, name="exhaustcap")

    # neck + head
    tube(G, (0, 0.01, 1.46), (0, 0.01, 1.62), 0.062, 0.06, SKIN, ["Neck", ("near", "Head")], segs=10, name="neck")
    blob(G, (0, 0.01, 1.72), (0.15, 0.158, 0.172), SKIN, ["Head"], segs=16, rings=10, name="head")
    blob(G, (0, 0.085, 1.632), (0.138, 0.092, 0.085), HAIR, ["Head"], segs=14, rings=8, name="beard")
    for s in (-1, 1):
        blob(G, (s * 0.05, 0.148, 1.677), (0.055, 0.03, 0.026), HAIR, ["Head"], segs=10, rings=6, rot=Ry(s * 14), name="moustache")
        blob(G, (s * 0.056, 0.150, 1.742), (0.034, 0.016, 0.036), EYE_WHITE, ["Head"], segs=10, rings=6, name="eyewhite")
        blob(G, (s * 0.054, 0.163, 1.740), (0.017, 0.008, 0.021), EYE_DARK, ["Head"], segs=8, rings=6, name="pupil")
        box(G, (s * 0.06, 0.158, 1.776), (0.066, 0.024, 0.022), HAIR, ["Head"], bevel=0.007, segs=1, rot=Ry(s * -10), name="brow")
        blob(G, (s * 0.15, 0.0, 1.72), (0.022, 0.035, 0.045), SKIN_DARK, ["Head"], segs=8, rings=6, name="ear")
    blob(G, (0, 0.168, 1.715), (0.036, 0.034, 0.034), SKIN_DARK, ["Head"], segs=10, rings=6, name="nose")

    # arms
    for side, s in (("Left", -1), ("Right", 1)):
        sh, el, wr, hd = arm_points(s)
        d = (el - sh).normalized()
        UA, LA, H, SH = side + "UpperArm", side + "LowerArm", side + "Hand", side + "Shoulder"
        out = Vector((s, 0, 0))
        # shoulder pad (rigid with the upper arm so it follows raised arms)
        blob(G, sh + Vector((s * 0.025, 0, 0.02)), (0.105, 0.115, 0.07), ACCENT, [UA], segs=12, rings=8, rot=Ry(s * 40), cut_below=-0.35, fill=False, name="shoulderpad")
        tube(G, sh - d * 0.03, el, 0.078, 0.066, SUIT, [UA, SH, ("near", LA)], name="upperarm")
        tube(G, el - d * 0.02, el + d * 0.05, 0.074, 0.074, SUIT_DARK, [UA, LA], name="sleevecuff")
        tube(G, el, wr, 0.062, 0.057, SKIN, [LA, ("near", UA)], name="forearm")
        # gauntlet glove + mitten hand
        tube(G, wr - d * 0.08, wr + d * 0.01, 0.072, 0.082, RUBBER, [LA, ("near", H)], name="gauntlet")
        fr = frame(d, Vector((0, 1, 0)))  # local x = forward(+Y) so palm faces the body
        box(G, wr + d * 0.065, (0.11, 0.07, 0.12), RUBBER, [H], bevel=0.025, segs=2, rot=fr, name="palm")
        box(G, wr + d * 0.15, (0.10, 0.065, 0.075), RUBBER, [H], bevel=0.025, segs=2, rot=fr @ Rx(s * -12), name="fingers")
        thumb_c = wr + d * 0.07 + Vector((0, 0.06, 0)) - out * 0.02
        box(G, thumb_c, (0.035, 0.035, 0.07), RUBBER, [H], bevel=0.012, segs=1, rot=fr @ Ry(25), name="thumb")
        box(G, wr + d * 0.04 - out * 0.0, (0.115, 0.075, 0.03), ACCENT, [H], bevel=0.006, segs=1, rot=fr, name="gloveband")


def build_headgear():
    H = ["Head"]
    # 1. hard hat with head lamp
    G = "HG_HardHat"
    blob(G, (0, 0.0, 1.785), (0.182, 0.198, 0.155), HELMET, H, segs=16, rings=10, cut_below=0.0, name="dome")
    tube(G, (0, 0.02, 1.778), (0, 0.02, 1.798), 0.215, 0.215, HELMET, H, segs=18, scale_xy=(0.97, 1.08), name="brim")
    box(G, (0, -0.01, 1.935), (0.045, 0.30, 0.03), HELMET, H, bevel=0.012, segs=1, name="ridge")
    box(G, (0.0, 0.17, 1.86), (0.075, 0.03, 0.06), METAL_DARK, H, bevel=0.01, segs=1, rot=Rx(-20), name="lampmount")
    tube(G, (0, 0.17, 1.875), (0, 0.235, 1.88), 0.042, 0.046, METAL, H, segs=12, name="lamp")
    tube(G, (0, 0.232, 1.88), (0, 0.24, 1.88), 0.036, 0.036, LENS, H, segs=12, name="lens")
    for s in (-1, 1):
        box(G, (s * 0.172, 0.0, 1.835), (0.02, 0.12, 0.04), STRIPE, H, rot=Ry(s * -25), name="hatstripe")

    # 2. welder helmet with glowing visor slit
    G = "HG_Welder"
    box(G, (0, 0.015, 1.735), (0.37, 0.39, 0.42), HELMET, H, bevel=0.12, segs=3, name="shell")
    box(G, (0, 0.19, 1.71), (0.30, 0.05, 0.27), METAL_DARK, H, bevel=0.035, segs=2, name="faceplate")
    box(G, (0, 0.214, 1.755), (0.23, 0.02, 0.055), VISOR, H, bevel=0.008, segs=1, name="visor")
    for s in (-1, 1):
        tube(G, (s * 0.18, 0.02, 1.74), (s * 0.215, 0.02, 1.74), 0.05, 0.05, METAL, H, segs=12, xhint=Vector((0, 1, 0)), name="hinge")
        box(G, (s * 0.07, 0.205, 1.62), (0.05, 0.03, 0.04), METAL, H, bevel=0.008, segs=1, name="filter")
    box(G, (0, 0.02, 1.95), (0.05, 0.30, 0.03), STRIPE, H, bevel=0.006, segs=1, rot=Rz(90), name="topstripe")

    # 3. beanie with goggles
    G = "HG_Goggles"
    blob(G, (0, 0.0, 1.79), (0.168, 0.176, 0.16), CLOTH, H, segs=16, rings=10, cut_below=0.0, name="beanie")
    tube(G, (0, 0.0, 1.785), (0, 0.0, 1.83), 0.172, 0.172, SUIT_DARK, H, segs=16, scale_xy=(1, 1.05), name="fold")
    blob(G, (0, -0.02, 1.945), (0.04, 0.04, 0.035), ACCENT, H, segs=8, rings=6, name="pompom")
    tube(G, (0, 0.0, 1.83), (0, 0.0, 1.865), 0.178, 0.178, RUBBER, H, segs=16, scale_xy=(1, 1.06), name="gogglestrap")
    for s in (-1, 1):
        c = Vector((s * 0.058, 0.158, 1.85))
        tube(G, c - Vector((0, 0.02, 0)), c + Vector((0, 0.035, 0)), 0.05, 0.048, BRASS, H, segs=12, name="goggle")
        tube(G, c + Vector((0, 0.033, 0)), c + Vector((0, 0.04, 0)), 0.038, 0.038, GLASS, H, segs=12, name="gogglelens")
    box(G, (0, 0.178, 1.85), (0.04, 0.02, 0.02), BRASS, H, bevel=0.005, segs=1, name="bridge")


# ------------------------------------------------------------------ skinning
def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length


def _weights_for(p, spec):
    """spec items: bone name | ("near", bone) (low-priority neighbour) | ("zband", [(bone, z0, z1), ...])."""
    out = {}
    plain = []
    for it in spec:
        if isinstance(it, tuple) and it[0] == "zband":
            bands = it[1]
            z = p.z
            ws = []
            for i, (bn, z0, z1) in enumerate(bands):
                if z0 <= z <= z1:
                    ws = [(bn, 1.0)]
                    break
                if i + 1 < len(bands) and z1 < z < bands[i + 1][1]:
                    t = (z - z1) / (bands[i + 1][1] - z1)
                    ws = [(bn, 1 - t), (bands[i + 1][0], t)]
                    break
            if not ws:
                ws = [(bands[0][0], 1.0)] if z < bands[0][1] else [(bands[-1][0], 1.0)]
            for bn, w in ws:
                out[bn] = out.get(bn, 0) + w
            return out
        plain.append(it)
    if len(plain) == 1:
        return {plain[0] if isinstance(plain[0], str) else plain[0][1]: 1.0}
    raw = {}
    for it in plain:
        near = isinstance(it, tuple)
        bn = it[1] if near else it
        h, t, _ = BONES[bn]
        d = _seg_dist(p, h, t) + 0.01
        raw[bn] = (1.0 / d ** 6) * (0.6 if near else 1.0)
    tot = sum(raw.values())
    out = {k: v / tot for k, v in raw.items() if v / tot > 0.03}
    tot = sum(out.values())
    return {k: v / tot for k, v in out.items()}


def skin_part(ob):
    spec = eval(ob["weights"])
    groups = {}
    for v in ob.data.vertices:
        p = ob.matrix_world @ v.co
        for bn, w in _weights_for(p, spec).items():
            if bn not in groups:
                groups[bn] = ob.vertex_groups.new(name=bn)
            groups[bn].add([v.index], w, 'REPLACE')


# ------------------------------------------------------------------ assembly
def make_material():
    import struct
    size = 64
    img = bpy.data.images.get("T_Driller_Preview") or bpy.data.images.new("T_Driller_Preview", size, size)
    emi = bpy.data.images.get("T_Driller_PreviewE") or bpy.data.images.new("T_Driller_PreviewE", size, size)
    px, pe = [0.0] * (size * size * 4), [0.0] * (size * size * 4)

    def lin(hexs):
        c = [int(hexs[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
        return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]

    for y in range(size):
        for x in range(size):
            i = (y * size + x) * 4
            if y < 32:
                c = (y // 8) * 8 + x // 8
                col = lin(PREVIEW_COLORS.get(c, "FF00FF"))
                px[i:i + 4] = col + [1.0]
                pe[i:i + 4] = (col if c in EMISSIVE else [0, 0, 0]) + [1.0]
            else:
                col = lin(STRIPE_A if ((x + y) // 8) % 2 == 0 else STRIPE_B)
                px[i:i + 4] = col + [1.0]
                pe[i:i + 4] = [0, 0, 0, 1]
    img.colorspace_settings.name = 'Linear Rec.709'
    emi.colorspace_settings.name = 'Linear Rec.709'
    img.pixels = px
    emi.pixels = pe

    m = bpy.data.materials.get("M_DRL_Driller") or bpy.data.materials.new("M_DRL_Driller")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.interpolation = 'Closest'
    te = nt.nodes.new("ShaderNodeTexImage"); te.image = emi; te.interpolation = 'Closest'
    nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(te.outputs[0], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 3.0
    bsdf.inputs["Roughness"].default_value = 0.62
    return m


def build_armature():
    arm_data = bpy.data.armatures.new("Driller_Rig")
    arm = bpy.data.objects.new("Driller_Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    ebs = {}
    for name, (h, t, parent) in BONES.items():
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail = h, t
        ebs[name] = eb
    for name, (h, t, parent) in BONES.items():
        if parent:
            ebs[name].parent = ebs[parent]
            ebs[name].use_connect = (ebs[parent].tail - Vector(h)).length < 1e-4
        # consistent rolls: limbs roll so local X points forward-ish
        ebs[name].align_roll(Vector((0, 1, 0)) if abs((Vector(t) - Vector(h)).normalized().y) < 0.9 else Vector((0, 0, 1)))
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def join_group(group, mat, arm):
    obs = PARTS[group]
    for ob in obs:
        skin_part(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = group
    if "weights" in ob:
        del ob["weights"]
    # weld coincident verts inside each part only (parts don't share verts), smooth-by-angle shading
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0005)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(42))
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    ob.parent = arm
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    # limit to 4 influences, normalised (Unity mobile skin weights: 2-4)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    return ob


# ------------------------------------------------------------------ animation
REST_ROT = {}


def _key(arm, frame_no, pose):
    """pose: {bone: (axis, degrees) list} rotations expressed in rest armature space; '_loc' = hips offset."""
    for pb in arm.pose.bones:
        q = Quaternion()
        for axis, deg in pose.get(pb.name, []):
            R = Quaternion(Vector(axis), math.radians(deg))
            rest = REST_ROT[pb.name]
            q = q @ (rest.inverted() @ R @ rest)
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=frame_no)
        if pb.name == "Hips":
            # pose-bone location is in bone-local space; hips bone points +Z so local Y = world Z
            loc = Vector(pose.get("_loc", (0, 0, 0)))
            pb.location = REST_ROT[pb.name].inverted() @ loc
            pb.keyframe_insert("location", frame=frame_no)


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def arms_down(extra=None):
    """Relax the A-pose to arms-at-sides (A-pose is only the bind pose)."""
    p = {"LeftUpperArm": [(Y, -26)], "RightUpperArm": [(Y, 26)],
         "LeftLowerArm": [(X, 14)], "RightLowerArm": [(X, 14)]}
    if extra:
        for k, v in extra.items():
            p[k] = v if k == "_loc" else p.get(k, []) + v
    return p


def make_actions(arm):
    for pb in arm.pose.bones:
        REST_ROT[pb.name] = pb.bone.matrix_local.to_quaternion()
    arm.animation_data_create()
    scene = bpy.context.scene
    scene.render.fps = 30
    acts = {}

    def new_action(name, keys):
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        arm.animation_data.action = act
        for f, pose in keys:
            _key(arm, f, pose)
        acts[name] = act
        return act

    # Idle: breathing, weight shift, look around (60 f loop)
    def idle(t):
        b = math.sin(t * 2 * math.pi)
        b2 = math.sin(t * 2 * math.pi + 1.2)
        return arms_down({
            "Spine": [(X, -1.5 * b)], "Chest": [(X, -2.0 * b)], "Neck": [(Z, 4 * b2)], "Head": [(Z, 5 * b2), (X, 2 * b)],
            "LeftUpperArm": [(Y, -2 * b)], "RightUpperArm": [(Y, 2 * b)],
            "LeftUpperLeg": [], "_loc": (0, 0, -0.006 + 0.006 * b),
        })
    new_action("Idle", [(1 + i * 10, idle(i / 6)) for i in range(7)])

    # Wave: right arm up, forearm swings (60 f loop)
    def wave(t, amp):
        w = math.sin(t * 2 * math.pi * 2) * amp
        return {
            "LeftUpperArm": [(Y, -26)], "LeftLowerArm": [(X, 14)],
            "RightShoulder": [(Y, -12 * amp)],
            "RightUpperArm": [(Y, -40 * amp), (X, 14 * amp)], "RightLowerArm": [(Y, (-78 + w * 22) * amp)],
            "RightHand": [(Y, -10 * amp)],
            "Chest": [(Z, -4 * amp), (Y, 3 * amp)], "Head": [(Z, -6 * amp), (Y, 4 * amp)], "Spine": [(Y, -2 * amp)],
        }
    keys = [(1, arms_down())]
    for i in range(1, 9):
        keys.append((1 + i * 5 + 3, wave(i / 8, 1.0)))
    keys.append((60, arms_down()))
    new_action("Wave", keys)

    # Cheer: double fist pump with a hop (45 f)
    def cheer(up, hop):
        return {
            "LeftShoulder": [(Y, 15 * up)], "RightShoulder": [(Y, -15 * up)],
            "LeftUpperArm": [(Y, 112 * up - 26 * (1 - up))], "RightUpperArm": [(Y, -112 * up + 26 * (1 - up))],
            "LeftLowerArm": [(Y, 30 * up), (X, 14 * (1 - up))], "RightLowerArm": [(Y, -30 * up), (X, 14 * (1 - up))],
            "Chest": [(X, 8 * up)], "Head": [(X, 10 * up)], "Spine": [(X, 3 * up)],
            "LeftUpperLeg": [(X, 10 * hop)], "RightUpperLeg": [(X, 10 * hop)],
            "LeftLowerLeg": [(X, -20 * hop)], "RightLowerLeg": [(X, -20 * hop)],
            "_loc": (0, 0, 0.09 * hop - 0.03 * (1 - hop) * up),
        }
    new_action("Cheer", [(1, arms_down()), (7, cheer(0.55, 0)), (13, cheer(1.0, 1.0)), (19, cheer(0.8, 0.2)),
                         (25, cheer(1.0, 0.9)), (31, cheer(0.85, 0)), (45, arms_down())])

    # Seated: driving pose for the train cab (single frame hold, 2 keys)
    seated = {"LeftUpperLeg": [(X, 82)], "RightUpperLeg": [(X, 82)], "LeftLowerLeg": [(X, -80)], "RightLowerLeg": [(X, -80)],
              "LeftUpperArm": [(Y, -30), (X, 45)], "RightUpperArm": [(Y, 30), (X, 45)],
              "LeftLowerArm": [(X, 50)], "RightLowerArm": [(X, 50)], "Chest": [(X, 4)], "_loc": (0, 0, -0.42)}
    new_action("Drive", [(1, seated), (30, seated)])

    arm.animation_data.action = acts["Idle"]
    return acts


# ------------------------------------------------------------------ export / preview
def export(arm, meshes):
    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for m in meshes:
        m.hide_set(False)
        m.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='Y', axis_up='Z', bake_space_transform=False,
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        armature_nodetype='NULL', use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
    return OUT_FBX


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def render_previews(arm, meshes, acts):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 900, 1100
    sc.render.film_transparent = False
    world = sc.world or bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.32, 0.22, 0.17, 1)
    bg.inputs[1].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.5
    sun.data.color = (1.0, 0.82, 0.65)
    sun.rotation_euler = (math.radians(50), 0, math.radians(150))
    sc.collection.objects.link(sun)
    rim = bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", 'SUN'))
    rim.data.energy = 2.0
    rim.data.color = (0.6, 0.75, 1.0)
    rim.rotation_euler = (math.radians(60), 0, math.radians(-30))
    sc.collection.objects.link(rim)
    bpy.ops.mesh.primitive_plane_add(size=6)
    fl = bpy.context.active_object
    fm = bpy.data.materials.new("Floor"); fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0.45, 0.28, 0.18, 1)
    fl.data.materials.append(fm)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 60
    sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, angle, action, frame, headgear, dist=4.4, h=1.15, look=1.0):
        arm.animation_data.action = acts[action]
        sc.frame_set(frame)
        for m in meshes:
            if m.name.startswith("HG_"):
                m.hide_render = m.name != headgear
        a = math.radians(angle)
        cam.location = (math.sin(a) * dist, math.cos(a) * dist, h)
        d = Vector((0, 0, look)) - cam.location
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(PREVIEW, name + ".png")
        bpy.ops.render.render(write_still=True)

    shot("driller_front_hardhat", 25, "Idle", 1, "HG_HardHat")
    shot("driller_back_hardhat", 200, "Idle", 1, "HG_HardHat")
    shot("driller_wave_welder", -30, "Wave", 16, "HG_Welder")
    shot("driller_cheer_goggles", 15, "Cheer", 13, "HG_Goggles")
    shot("driller_face_goggles", 10, "Idle", 1, "HG_Goggles", dist=1.6, h=1.72, look=1.72)
    shot("driller_bindpose", 0, "Idle", 1, "HG_HardHat")


def main():
    render = "--render" in sys.argv
    bpy.ops.wm.read_factory_settings(use_empty=True)
    define_bones()
    build_body()
    build_headgear()
    mat = make_material()
    arm = build_armature()
    meshes = [join_group(g, mat, arm) for g in PARTS]
    acts = make_actions(arm)
    stats = {m.name: tri_count(m) for m in meshes}
    print("TRIS", stats, "body+hat", stats["Body"] + max(v for k, v in stats.items() if k.startswith("HG_")))
    print("VERTS", {m.name: len(m.data.vertices) for m in meshes})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "DRL_Driller.blend"))
    print("EXPORTED", export(arm, meshes))
    if render:
        render_previews(arm, meshes, acts)
    print("DONE")


main()
