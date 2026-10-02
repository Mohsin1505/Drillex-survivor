"""
Drillex hero drill vehicle - procedural build script (Blender 5.2).

Builds DRL_DrillVehicle and the separate DRL_Drill_Standard module from scratch.
Conventions:
  * meters, scale 1.0, Z up, vehicle front faces Blender -Y (exports to Unity +Z)
  * root pivot on the ground under the hull center
  * DrillSocket = attachment point for every drill module (axis = forward)
Re-running the script rebuilds everything (old DRL objects are removed first).
"""
import bpy
import bmesh
import math
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# Key dimensions (meters). Forward is -Y.
# ---------------------------------------------------------------------------
DRILL_AXIS_Z = 0.55          # drill axis height above ground
SOCKET_Y = -0.85             # front face of the brass collar = DrillSocket
TURRET_PIVOT = (0.0, 0.10, 1.04)
CANNON_PIVOT = (0.0, -0.14, 1.26)
WHEEL_R = 0.17
WHEEL_X = 0.53
WHEEL_YS = (0.55, 0.21, -0.13, -0.47)

# ---------------------------------------------------------------------------
# Scene reset
# ---------------------------------------------------------------------------

def _purge():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def _coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(c)
    return c


# ---------------------------------------------------------------------------
# Materials (authoring materials, baked down to one texture set later)
# ---------------------------------------------------------------------------
MATS = {}

PALETTE = {
    #  name          base color                metal rough  emission
    "RedPaint":    ((0.50, 0.035, 0.025, 1), 0.0, 0.50, None),
    "BlackMetal":  ((0.030, 0.028, 0.026, 1), 0.6, 0.55, None),
    "Steel":       ((0.42, 0.42, 0.43, 1), 1.0, 0.38, None),
    "DrillSteel":  ((0.62, 0.62, 0.64, 1), 1.0, 0.24, None),
    "Brass":       ((0.78, 0.50, 0.16, 1), 1.0, 0.33, None),
    "OrangePaint": ((0.80, 0.33, 0.04, 1), 0.3, 0.42, None),
    "Hazard":      ((0.85, 0.55, 0.02, 1), 0.0, 0.50, None),
    "Grille":      ((0.025, 0.025, 0.025, 1), 0.7, 0.60, None),
    "LampAmber":   ((1.00, 0.62, 0.12, 1), 0.0, 0.20, (1.00, 0.55, 0.08)),
    "LampRed":     ((1.00, 0.08, 0.04, 1), 0.0, 0.20, (1.00, 0.05, 0.02)),
    "WindowGlow":  ((1.00, 0.55, 0.15, 1), 0.0, 0.30, (1.00, 0.45, 0.10)),
    "Decal":       ((0.75, 0.75, 0.75, 1), 0.8, 0.40, None),
}


def _mat(name):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.get("A_" + name) or bpy.data.materials.new("A_" + name)
    m.use_nodes = True
    base, metal, rough, emis = PALETTE[name]
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if emis:
        bsdf.inputs["Emission Color"].default_value = (*emis, 1)
        bsdf.inputs["Emission Strength"].default_value = 6.0
    m.diffuse_color = base
    MATS[name] = m
    return m


# ---------------------------------------------------------------------------
# Geometry helpers (bmesh based, everything in world space, then origin set)
# ---------------------------------------------------------------------------
_pieces = {}   # group name -> list of objects to join


def _finish(bm, name, mat, group, coll, solid=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(_mat(mat))
    ob = bpy.data.objects.new(name, me)
    ob["solid"] = solid   # closed convex primitive, usable as an occluder
    coll.objects.link(ob)
    _pieces.setdefault(group, []).append(ob)
    return ob


def box(group, coll, mat, x0, x1, y0, y1, z0, z1, bevel=0.012, rot=None, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    if bevel > 0:
        w = min(bevel, 0.45 * min(sx, sy, sz))
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=w, segments=1,
                        affect='EDGES', clamp_overlap=True, profile=0.5)
    if rot:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=rot)
    bmesh.ops.translate(bm, vec=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), verts=bm.verts)
    return _finish(bm, name, mat, group, coll)


def _axis_matrix(axis):
    # local +Z -> requested axis
    if axis == 'X':
        return Matrix.Rotation(math.radians(90), 4, 'Y')
    if axis == '-X':
        return Matrix.Rotation(math.radians(-90), 4, 'Y')
    if axis == 'Y':
        return Matrix.Rotation(math.radians(-90), 4, 'X')
    if axis == '-Y':
        return Matrix.Rotation(math.radians(90), 4, 'X')
    return Matrix.Identity(4)


def cyl(group, coll, mat, center, r, depth, axis='Z', segs=16, r2=None, cap=True, bevel=0.0, name="cyl"):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs,
                          radius1=r, radius2=r if r2 is None else r2, depth=depth)
    if bevel > 0:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and
               any(len(f.verts) > 4 for f in e.link_faces)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
    bmesh.ops.transform(bm, matrix=_axis_matrix(axis), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return _finish(bm, name, mat, group, coll)


def lathe(group, coll, mat, profile, center, axis='-Y', segs=32, name="lathe"):
    """profile: list of (radius, axial) points, axial along local +Z."""
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0.0, a)) for r, a in profile]
    edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0, 0, 0), axis=(0, 0, 1),
                   angle=math.radians(360), steps=segs, use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.transform(bm, matrix=_axis_matrix(axis), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return _finish(bm, name, mat, group, coll, solid=False)


def lamp(group, coll, center, axis, r=0.045, color="LampAmber"):
    """Lamp = black housing + brass bezel + emissive lens, facing `axis`."""
    d = Vector({'X': (1, 0, 0), '-X': (-1, 0, 0), 'Y': (0, 1, 0), '-Y': (0, -1, 0),
                'Z': (0, 0, 1)}[axis])
    c = Vector(center)
    cyl(group, coll, "BlackMetal", c + d * 0.015, r * 1.25, 0.03, axis, 12, name="lamp_housing")
    cyl(group, coll, "Brass", c + d * 0.034, r * 1.12, 0.012, axis, 12, name="lamp_bezel")
    cyl(group, coll, color, c + d * 0.040, r, 0.012, axis, 12, r2=r * 0.8, name="lamp_lens")


def cull_hidden(groups):
    """Delete faces that are fully buried inside another solid piece of the same rigid part.

    Only pieces that never move relative to each other are tested together, so nothing
    is removed that could become visible when the turret turns or a drill is swapped.
    """
    from mathutils.bvhtree import BVHTree
    obs = [o for g in groups for o in _pieces.get(g, [])]
    solids = []
    for o in obs:
        if o.get("solid"):
            bm = bmesh.new()
            bm.from_mesh(o.data)
            lo = Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
            hi = Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
            solids.append((o, BVHTree.FromBMesh(bm), lo, hi))
            bm.free()

    def inside(pt, skip):
        for o, tree, lo, hi in solids:
            if o is skip:
                continue
            if not (lo.x - 1e-4 <= pt.x <= hi.x + 1e-4 and lo.y - 1e-4 <= pt.y <= hi.y + 1e-4
                    and lo.z - 1e-4 <= pt.z <= hi.z + 1e-4):
                continue
            loc, nrm, _i, _d = tree.find_nearest(pt)
            if loc is not None and (pt - loc).dot(nrm) < 0:
                return True
        return False

    removed = 0
    for o in obs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dead = []
        for f in bm.faces:
            c = f.calc_center_median()
            n = f.normal
            # sample the whole face (centre, corners, edge midpoints, half-way points) so a face is only
            # removed when it is fully buried, not when a few small parts happen to cover the sample points
            vs = [v.co for v in f.verts]
            pts = [c] + [v.lerp(c, 0.12) for v in vs] + [v.lerp(c, 0.5) for v in vs]
            pts += [vs[i].lerp(vs[(i + 1) % len(vs)], 0.5).lerp(c, 0.12) for i in range(len(vs))]
            probes = [p + n * 0.002 for p in pts]
            if all(inside(p, o) for p in probes):
                dead.append(f)
        if dead:
            bmesh.ops.delete(bm, geom=dead, context='FACES')
            bm.to_mesh(o.data)
            removed += len(dead)
        bm.free()
    return removed


def _join(group, name, coll, origin):
    obs = _pieces.pop(group)
    ctx = bpy.context
    for o in ctx.selected_objects:
        o.select_set(False)
    for o in obs:
        o.select_set(True)
    ctx.view_layer.objects.active = obs[0]
    if len(obs) > 1:
        bpy.ops.object.join()
    ob = ctx.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    # move origin to pivot without moving the geometry
    off = Vector(origin)
    ob.data.transform(Matrix.Translation(-off))
    ob.location = off
    # merge material slots with the same material
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    ctx.view_layer.objects.active = ob
    bpy.ops.object.material_slot_remove_unused()
    # smooth curved surfaces, keep hard edges on boxes/chamfers (normal maps are baked against this)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35), keep_sharp_edges=True)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    return ob


def _empty(name, loc, coll, parent=None, size=0.1, kind='ARROWS'):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = kind
    e.empty_display_size = size
    coll.objects.link(e)
    e.location = loc
    if parent:
        _parent(e, parent)
    return e


def _parent(child, parent):
    bpy.context.view_layer.update()
    mw = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()
    child.matrix_world = mw


# ---------------------------------------------------------------------------
# Vehicle parts
# ---------------------------------------------------------------------------

def build_body(C):
    g, c = "VehicleBody", C
    R, K, S = "RedPaint", "BlackMetal", "Steel"
    # chassis frame + lower deck
    box(g, c, K, -0.46, 0.46, -0.72, 0.80, 0.22, 0.37, 0.015, name="chassis")
    # main hull
    box(g, c, R, -0.48, 0.48, -0.46, 0.60, 0.36, 0.98, 0.03, name="hull")
    # hull panel inset (raised side plates give the layered look)
    for sx in (-1, 1):
        x0, x1 = (0.475, 0.495) if sx > 0 else (-0.495, -0.475)
        box(g, c, R, x0, x1, -0.40, 0.56, 0.47, 0.90, 0.008, name="side_plate")
    # boiler section between hull and faceplate
    cyl(g, c, R, (0, -0.56, DRILL_AXIS_Z + 0.02), 0.44, 0.24, 'Y', 32, name="boiler")
    # front faceplate: arched top + box bottom, dark inner ring
    cyl(g, c, R, (0, -0.68, DRILL_AXIS_Z), 0.50, 0.08, 'Y', 32, bevel=0.012, name="faceplate_arch")
    box(g, c, R, -0.50, 0.50, -0.72, -0.64, 0.20, DRILL_AXIS_Z, 0.012, name="faceplate_low")
    cyl(g, c, K, (0, -0.73, DRILL_AXIS_Z), 0.455, 0.03, 'Y', 32, name="faceplate_ring")
    # roof deck + front roof box
    box(g, c, K, -0.44, 0.44, -0.42, 0.58, 0.97, 1.04, 0.012, name="roof_deck")
    box(g, c, R, -0.38, 0.38, -0.38, 0.54, 1.03, 1.05, 0.006, name="roof_plate")
    box(g, c, R, -0.20, 0.20, -0.47, -0.30, 0.98, 1.11, 0.015, name="roof_lamp_box")
    # rear block with frame
    box(g, c, R, -0.50, 0.50, 0.60, 0.82, 0.24, 1.00, 0.02, name="rear_block")
    box(g, c, K, -0.52, 0.52, 0.80, 0.86, 0.22, 0.30, 0.01, name="rear_bumper")
    for sx in (-1, 1):
        lo, hi = sorted((sx * 0.46, sx * 0.52))
        box(g, c, K, lo, hi, 0.78, 0.86, 0.22, 1.03, 0.01, name="rear_post")
        box(g, c, K, lo, hi, 0.58, 0.64, 0.22, 1.03, 0.01, name="rear_post_front")
        box(g, c, K, lo, hi, 0.64, 0.78, 0.97, 1.03, 0.008, name="rear_side_rail")
    box(g, c, K, -0.46, 0.46, 0.80, 0.86, 0.97, 1.03, 0.008, name="rear_top_rail")
    # front plow (cow catcher): beams + slanted slats
    box(g, c, K, -0.44, 0.44, -0.80, -0.72, 0.20, 0.27, 0.01, name="plow_top")
    box(g, c, R, -0.46, 0.46, -1.00, -0.94, 0.00, 0.06, 0.01, name="plow_bottom")
    ang = math.atan2(0.21, 0.20)
    rx = Matrix.Rotation(ang, 3, 'X')
    for i in range(8):
        x = -0.385 + i * 0.11
        box(g, c, R, x - 0.03, x + 0.03, -1.01, -0.72, 0.115, 0.155, 0.006, rot=rx, name="plow_slat")
    for sx in (-1, 1):
        x = sx * 0.445
        box(g, c, R, x - 0.022, x + 0.022, -1.01, -0.72, 0.115, 0.155, 0.006, rot=rx, name="plow_side")


def build_suspension(C):
    g, c = "Suspension", C
    for sx in (-1, 1):
        # fuel/air tanks between the wheels
        cyl(g, c, "Steel", (sx * 0.40, 0.04, 0.20), 0.10, 1.10, 'Y', 16, bevel=0.01, name="tank")
        cyl(g, c, "BlackMetal", (sx * 0.40, -0.25, 0.20), 0.105, 0.03, 'Y', 16, name="tank_band")
        cyl(g, c, "BlackMetal", (sx * 0.40, 0.33, 0.20), 0.105, 0.03, 'Y', 16, name="tank_band")
        # bogie side frame + axle boxes
        box(g, c, "BlackMetal", sx * 0.47 - 0.015, sx * 0.47 + 0.015, -0.62, 0.70, 0.13, 0.25, 0.01, name="bogie_frame")
        for y in WHEEL_YS:
            box(g, c, "Brass", sx * 0.475 - 0.02, sx * 0.475 + 0.02, y - 0.05, y + 0.05, 0.12, 0.22, 0.008, name="axle_box")


def build_armor(C):
    g, c = "Armor", C
    for sx in (-1, 1):
        xo = 0.5 * sx
        def bx(x0, x1, *a, **k):
            lo, hi = sorted((sx * x0, sx * x1))
            return box(g, c, *a[:1], lo, hi, *a[1:], **k)
        # hazard skirt along the hull bottom, stepping up toward the front
        bx(0.48, 0.515, "Hazard", -0.30, 0.62, 0.36, 0.44, 0.008, name="skirt")
        bx(0.48, 0.515, "Hazard", -0.46, -0.30, 0.36, 0.52, 0.008, name="skirt_front")
        # vertical hazard band at the rear corner
        bx(0.49, 0.515, "Hazard", 0.60, 0.68, 0.44, 0.96, 0.008, name="hazard_band")
        # steel strap wrapping the hull near the front
        bx(0.48, 0.505, "Steel", -0.36, -0.30, 0.36, 0.99, 0.006, name="strap_side")
        # corner guard on the rear block
        bx(0.50, 0.53, "BlackMetal", 0.78, 0.86, 0.30, 0.98, 0.006, name="corner_guard")
    box(g, c, "Steel", -0.45, 0.45, -0.36, -0.30, 0.97, 1.06, 0.006, name="strap_top")
    # hazard frame on the rear face around the grille
    box(g, c, "Hazard", -0.30, 0.30, 0.82, 0.84, 0.86, 0.92, 0.006, name="rear_haz_top")
    box(g, c, "Hazard", -0.30, 0.30, 0.82, 0.84, 0.30, 0.36, 0.006, name="rear_haz_bot")
    box(g, c, "Hazard", -0.30, -0.24, 0.82, 0.84, 0.36, 0.86, 0.006, name="rear_haz_r")
    box(g, c, "Hazard", 0.24, 0.30, 0.82, 0.84, 0.36, 0.86, 0.006, name="rear_haz_l")
    # hazard beam on the plow
    box(g, c, "Hazard", -0.45, 0.45, -0.83, -0.79, 0.19, 0.28, 0.006, name="plow_haz")


def build_mechanical(C):
    g, c = "MechanicalDetails", C
    for sx in (-1, 1):
        # cabin window: dark frame, glowing pane, bars
        x0 = sx * 0.497
        lo, hi = sorted((x0, x0 + sx * 0.012))
        box(g, c, "BlackMetal", lo, hi, -0.02, 0.32, 0.54, 0.80, 0.004, name="window_frame")
        lo, hi = sorted((x0 + sx * 0.010, x0 + sx * 0.016))
        box(g, c, "WindowGlow", lo, hi, 0.01, 0.29, 0.57, 0.77, 0.0, name="window_pane")
        for y in (0.10, 0.20):
            lo, hi = sorted((x0 + sx * 0.012, x0 + sx * 0.026))
            box(g, c, "BlackMetal", lo, hi, y - 0.01, y + 0.01, 0.57, 0.77, 0.0, name="window_bar")
        lo, hi = sorted((x0 + sx * 0.012, x0 + sx * 0.026))
        box(g, c, "BlackMetal", lo, hi, 0.01, 0.29, 0.665, 0.685, 0.0, name="window_bar_h")
        # vent grille behind the window
        lo, hi = sorted((x0, x0 + sx * 0.014))
        box(g, c, "Grille", lo, hi, 0.36, 0.55, 0.58, 0.78, 0.004, name="side_grille")
        # skull roundel on the front skirt
        ax = 'X' if sx > 0 else '-X'
        cyl(g, c, "Brass", (sx * 0.52, -0.30, 0.47), 0.095, 0.02, ax, 20, name="roundel_ring")
        cyl(g, c, "Decal", (sx * 0.532, -0.30, 0.47), 0.080, 0.012, ax, 20, name="roundel_skull")
        # pipes along the upper hull edge
        cyl(g, c, "Steel", (sx * 0.495, 0.08, 0.935), 0.02, 0.96, 'Y', 8, name="pipe_long")
        cyl(g, c, "Steel", (sx * 0.495, 0.57, 0.67), 0.02, 0.52, 'Z', 8, name="pipe_vert")
        cyl(g, c, "Brass", (sx * 0.495, 0.57, 0.935), 0.03, 0.04, 'Y', 8, name="pipe_joint")
    # rear grille with louvres
    box(g, c, "Grille", -0.24, 0.24, 0.82, 0.835, 0.36, 0.86, 0.0, name="rear_grille")
    for i in range(9):
        z = 0.40 + i * 0.05
        box(g, c, "Steel", -0.23, 0.23, 0.832, 0.85, z - 0.008, z + 0.008, 0.0, name="rear_louvre")
    # roof vent grille
    box(g, c, "Grille", -0.18, 0.18, 0.36, 0.52, 1.04, 1.07, 0.006, name="roof_vent")
    # rear ladder rungs
    for i in range(4):
        z = 0.42 + i * 0.15
        box(g, c, "Steel", 0.40, 0.44, 0.86, 0.88, z - 0.01, z + 0.01, 0.0, name="ladder_rung")
        box(g, c, "Steel", -0.44, -0.40, 0.86, 0.88, z - 0.01, z + 0.01, 0.0, name="ladder_rung")


def build_lights(C):
    g, c = "Lights", C
    # front faceplate lamps
    for sx in (-1, 1):
        lamp(g, c, (sx * 0.40, -0.73, 0.93), '-Y', 0.05)
        lamp(g, c, (sx * 0.42, -0.73, 0.30), '-Y', 0.05)
        lamp(g, c, (sx * 0.10, -0.47, 1.045), '-Y', 0.045)
        # side lamps at the front top corner
        lamp(g, c, (sx * 0.50, -0.42, 0.94), 'X' if sx > 0 else '-X', 0.04)
        # rear lamps: 2 amber + 1 red per side
        lamp(g, c, (sx * 0.38, 0.86, 0.88), 'Y', 0.04)
        lamp(g, c, (sx * 0.38, 0.86, 0.66), 'Y', 0.04)
        lamp(g, c, (sx * 0.38, 0.86, 0.44), 'Y', 0.04, "LampRed")


def build_drill_mount(C):
    g, c = "DrillMount", C
    # brass/orange collar: vehicle side of the drill interface
    cyl(g, c, "OrangePaint", (0, -0.765, DRILL_AXIS_Z), 0.455, 0.11, 'Y', 32, bevel=0.012, name="collar")
    cyl(g, c, "BlackMetal", (0, -0.765, DRILL_AXIS_Z), 0.462, 0.025, 'Y', 32, name="collar_band")
    cyl(g, c, "Brass", (0, -0.833, DRILL_AXIS_Z), 0.425, 0.034, 'Y', 32, bevel=0.006, name="flange")
    for i in range(6):
        a = math.radians(30 + i * 60)
        cyl(g, c, "Steel", (math.cos(a) * 0.39, -0.85, DRILL_AXIS_Z + math.sin(a) * 0.39),
            0.022, 0.02, 'Y', 6, name="flange_bolt")


def build_turret(C):
    g, c = "Turret", C
    px, py, pz = TURRET_PIVOT
    cyl(g, c, "BlackMetal", (px, py, pz + 0.035), 0.27, 0.07, 'Z', 24, bevel=0.008, name="ring")
    cyl(g, c, "Brass", (px, py, pz + 0.08), 0.235, 0.03, 'Z', 24, name="ring_brass")
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        cyl(g, c, "Steel", (px + math.cos(a) * 0.255, py + math.sin(a) * 0.255, pz + 0.072), 0.016, 0.012, 'Z', 6, name="ring_bolt")
    box(g, c, "RedPaint", -0.23, 0.23, py - 0.20, py + 0.27, pz + 0.09, pz + 0.34, 0.025, name="turret_body")
    box(g, c, "BlackMetal", -0.245, 0.245, py - 0.21, py + 0.28, pz + 0.085, pz + 0.12, 0.008, name="turret_skirt")
    box(g, c, "BlackMetal", -0.245, 0.245, py - 0.21, py + 0.28, pz + 0.30, pz + 0.33, 0.008, name="turret_band")
    box(g, c, "RedPaint", -0.17, 0.17, py - 0.12, py + 0.20, pz + 0.34, pz + 0.37, 0.01, name="turret_top")
    cyl(g, c, "BlackMetal", (px, py + 0.06, pz + 0.365), 0.07, 0.03, 'Z', 12, name="hatch")
    for sx in (-1, 1):
        lo, hi = sorted((sx * 0.228, sx * 0.242))
        box(g, c, "BlackMetal", lo, hi, py - 0.07, py + 0.12, pz + 0.135, pz + 0.285, 0.004, name="turret_grille_frame")
        lo, hi = sorted((sx * 0.236, sx * 0.246))
        box(g, c, "Grille", lo, hi, py - 0.055, py + 0.105, pz + 0.15, pz + 0.27, 0.0, name="turret_grille")
        lo, hi = sorted((sx * 0.228, sx * 0.240))
        box(g, c, "Decal", lo, hi, py + 0.15, py + 0.24, pz + 0.165, pz + 0.255, 0.0, name="turret_emblem")
        lamp(g, c, (sx * 0.17, py - 0.205, pz + 0.22), '-Y', 0.034)
        lo, hi = sorted((sx * 0.235, sx * 0.27))
        box(g, c, "Brass", lo, hi, py - 0.12, py + 0.18, pz + 0.09, pz + 0.13, 0.006, name="side_bracket")
    box(g, c, "BlackMetal", -0.14, 0.14, py + 0.268, py + 0.282, pz + 0.135, pz + 0.285, 0.004, name="turret_rear_frame")
    box(g, c, "Grille", -0.125, 0.125, py + 0.276, py + 0.286, pz + 0.15, pz + 0.27, 0.0, name="turret_rear_grille")
    # mantlet: brass drum holding the cannon
    cyl(g, c, "Brass", (0, py - 0.23, CANNON_PIVOT[2]), 0.09, 0.07, 'Y', 16, bevel=0.006, name="mantlet")


def build_cannon(C):
    g, c = "Cannon", C
    _, py, pz = CANNON_PIVOT
    cyl(g, c, "Brass", (0, py - 0.06, pz), 0.065, 0.10, 'Y', 16, bevel=0.006, name="breech")
    cyl(g, c, "BlackMetal", (0, py - 0.33, pz), 0.042, 0.46, 'Y', 16, name="barrel")
    cyl(g, c, "Steel", (0, py - 0.30, pz), 0.050, 0.04, 'Y', 16, name="barrel_band")
    lathe(g, c, "RedPaint",
          [(0.030, 0.0), (0.056, 0.0), (0.056, 0.085), (0.044, 0.09), (0.030, 0.09), (0.030, 0.0)],
          (0, py - 0.55, pz), '-Y', 16, name="muzzle")


def build_wheel(C):
    """One wheel mesh (origin at hub, axle along X). Instanced 8x."""
    g, c = "Wheel", C
    w = 0.07
    lathe(g, c, "BlackMetal",
          [(0.125, -w / 2), (0.17, -w / 2), (0.17, w / 2 - 0.02), (0.19, w / 2 - 0.01),
           (0.19, w / 2), (0.125, w / 2), (0.125, -w / 2)],
          (0, 0, 0), 'X', 20, name="wheel_rim")
    cyl(g, c, "Brass", (0, 0, 0), 0.05, w + 0.03, 'X', 12, bevel=0.006, name="wheel_hub")
    for i in range(6):
        rot = Matrix.Rotation(math.radians(i * 60), 3, 'X')
        box(g, c, "Steel", -0.015, 0.015, -0.017, 0.017, 0.04, 0.13, 0.0, name="wheel_spoke")
        ob = _pieces[g][-1]
        ob.data.transform(rot.to_4x4())


def build_drill(C):
    """Standard drill module. Origin = DrillSocket (back face of base plate)."""
    sock = (0, SOCKET_Y, DRILL_AXIS_Z)
    # base plate (does not spin)
    lathe("DrillBase", C, "Steel",
          [(0.0, 0.0), (0.40, 0.0), (0.41, 0.01), (0.41, 0.045), (0.395, 0.055), (0.0, 0.055)],
          sock, '-Y', 32, name="drill_base")
    for i in range(8):
        a = math.radians(22.5 + i * 45)
        cyl("DrillBase", C, "Brass", (math.cos(a) * 0.36, SOCKET_Y - 0.06, DRILL_AXIS_Z + math.sin(a) * 0.36),
            0.02, 0.012, '-Y', 6, name="base_bolt")
    # stepped cone sections (spin)
    radii = (0.39, 0.345, 0.30, 0.255, 0.21, 0.165, 0.12)
    L = 0.085
    a = 0.055
    prof = [(0.0, a), (radii[0] - 0.01, a)]
    for i, R in enumerate(radii):
        prof += [(R, a + 0.008), (R - 0.028, a + L - 0.004)]
        a += L
        nxt = radii[i + 1] - 0.01 if i + 1 < len(radii) else 0.085
        prof += [(R - 0.032, a), (nxt, a)]
    prof.append((0.0, a))
    lathe("DrillSections", C, "DrillSteel", prof, sock, '-Y', 32, name="drill_sections")
    tip_start = a
    lathe("DrillTip", C, "DrillSteel",
          [(0.0, tip_start), (0.085, tip_start), (0.06, tip_start + 0.07), (0.0, 0.85)],
          sock, '-Y', 24, name="drill_tip")
    return tip_start


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def build():
    _purge()
    _pieces.clear()
    MATS.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0

    veh = _coll("DRL_DrillVehicle")
    cols = {n: _coll(n, veh) for n in ("VehicleBody", "DrillMount", "Turret", "Wheels", "Suspension",
                                       "Armor", "Lights", "MechanicalDetails", "VFX_Sockets")}
    drill_c = _coll("DRL_Drill_Standard")
    work = _coll("_build_tmp")

    build_body(work)
    build_suspension(work)
    build_armor(work)
    build_mechanical(work)
    build_lights(work)
    build_drill_mount(work)
    build_turret(work)
    build_cannon(work)
    build_wheel(work)
    tip_a = build_drill(work)
    culled = cull_hidden(("VehicleBody", "Suspension", "Armor", "MechanicalDetails", "Lights", "DrillMount"))
    culled += cull_hidden(("Turret",)) + cull_hidden(("Cannon",)) + cull_hidden(("Wheel",))
    print("culled hidden faces:", culled)

    root = _empty("DRL_DrillVehicle", (0, 0, 0), veh, size=0.3, kind='PLAIN_AXES')
    parts = {}
    for grp in ("VehicleBody", "Suspension", "Armor", "MechanicalDetails", "Lights"):
        parts[grp] = _join(grp, grp, cols[grp], (0, 0, 0))
        _parent(parts[grp], root)

    mount = _join("DrillMount", "DrillMount", cols["DrillMount"], (0, SOCKET_Y + 0.11, DRILL_AXIS_Z))
    _parent(mount, root)
    socket = _empty("DrillSocket", (0, SOCKET_Y, DRILL_AXIS_Z), cols["DrillMount"], mount, 0.25)

    turret = _join("Turret", "Turret", cols["Turret"], TURRET_PIVOT)
    _parent(turret, root)
    cannon = _join("Cannon", "Cannon", cols["Turret"], CANNON_PIVOT)
    _parent(cannon, turret)
    _empty("VFX_CannonMuzzle", (0, CANNON_PIVOT[1] - 0.64, CANNON_PIVOT[2]), cols["Turret"], cannon, 0.1)

    wheel_src = _join("Wheel", "Wheel_L1", cols["Wheels"], (0, 0, 0))
    wheels_root = _empty("Wheels", (0, 0, 0), cols["Wheels"], root, 0.05, 'PLAIN_AXES')
    wheel_mesh = wheel_src.data
    wheel_mesh.name = "SM_Wheel"
    bpy.data.objects.remove(wheel_src, do_unlink=True)
    for side, sx in (("L", 1), ("R", -1)):
        for i, y in enumerate(WHEEL_YS, 1):
            w = bpy.data.objects.new(f"Wheel_{side}{i}", wheel_mesh)
            cols["Wheels"].objects.link(w)
            w.location = (sx * WHEEL_X, y, WHEEL_R)
            if sx < 0:
                w.rotation_euler = (0, 0, math.pi)  # flange faces outward on both sides
            _parent(w, wheels_root)

    sockets = _empty("VFX_Sockets", (0, 0, 0), cols["VFX_Sockets"], root, 0.05, 'PLAIN_AXES')
    for name, loc in (("Socket_Headlight", (0, -0.75, 0.95)),
                      ("Socket_CameraTarget", (0, 0.0, 0.60)),
                      ("Socket_HealthBar", (0, 0.10, 1.65)),
                      ("Socket_TrapDrop", (0, 0.98, 0.30)),
                      ("Socket_RearCoupler", (0, 0.87, 0.26))):
        _empty(name, loc, cols["VFX_Sockets"], sockets, 0.1)

    # drill module (own root, origin = socket)
    droot = _empty("DRL_Drill_Standard", (0, SOCKET_Y, DRILL_AXIS_Z), drill_c, size=0.2, kind='PLAIN_AXES')
    dbase = _join("DrillBase", "DrillBase", drill_c, (0, SOCKET_Y, DRILL_AXIS_Z))
    _parent(dbase, droot)
    rotor = _empty("DrillRotor", (0, SOCKET_Y, DRILL_AXIS_Z), drill_c, droot, 0.15, 'SINGLE_ARROW')
    rotor.rotation_euler = (0, 0, 0)
    dsec = _join("DrillSections", "DrillSections", drill_c, (0, SOCKET_Y, DRILL_AXIS_Z))
    _parent(dsec, rotor)
    dtip = _join("DrillTip", "DrillTip", drill_c, (0, SOCKET_Y, DRILL_AXIS_Z))
    _parent(dtip, rotor)
    _empty("VFX_DrillTip", (0, SOCKET_Y - 0.85, DRILL_AXIS_Z), drill_c, rotor, 0.1)
    # attach module to the socket (kept as a parent link in Blender only)
    _parent(droot, socket)

    bpy.data.collections.remove(work)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    return root


if __name__ == "__main__":
    build()
