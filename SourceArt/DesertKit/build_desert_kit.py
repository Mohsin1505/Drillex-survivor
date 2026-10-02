"""
Drillex stylized desert environment kit - Blender 5.2 procedural build.

Every asset is its own object, origin at the base centre (Z=0 = ground contact), clean transforms,
flat-shaded stylized facets, ONE shared material (M_DesertKit) that samples a small colour palette
texture (T_DesertKit_Palette, 256 px). Each face's UVs point at one palette swatch, chosen from the
face's role (sandstone, sand, cactus, grass, wood, bone, accent) and its orientation/height, which
gives the hand-painted gradient look (lighter tops, darker undersides/crevices) with no unique
textures - ideal for mobile batching.

Run build() inside Blender. Re-running rebuilds the whole kit.
"""
import bpy
import bmesh
import math
import os
import random
from mathutils import Matrix, Vector

HERE = r"D:\Drillex\SourceArt\DesertKit"
PALETTE_PATH = os.path.join(HERE, "T_DesertKit_Palette.png")

# ---------------------------------------------------------------------------
# Palette: 16 x 16 grid of 16 px swatches. Row = material role, column = shade (dark -> light).
# ---------------------------------------------------------------------------
CELL = 16
GRID = 16
ROLE = {"rock": 0, "sand": 1, "cactus": 2, "grass": 3, "wood": 4, "bone": 5, "accent": 6, "dry": 7}
PALETTE = {
    0: [(0.30, 0.09, 0.05), (0.42, 0.13, 0.07), (0.53, 0.18, 0.09), (0.63, 0.23, 0.11),
        (0.72, 0.29, 0.14), (0.80, 0.36, 0.18), (0.87, 0.45, 0.24), (0.93, 0.56, 0.32)],
    1: [(0.52, 0.27, 0.13), (0.62, 0.34, 0.17), (0.71, 0.41, 0.21), (0.78, 0.48, 0.26),
        (0.84, 0.55, 0.31), (0.89, 0.62, 0.37), (0.93, 0.69, 0.44), (0.96, 0.76, 0.52)],
    2: [(0.09, 0.22, 0.08), (0.13, 0.30, 0.10), (0.17, 0.38, 0.12), (0.22, 0.46, 0.14),
        (0.28, 0.53, 0.16), (0.35, 0.60, 0.19), (0.43, 0.66, 0.23), (0.52, 0.72, 0.28)],
    3: [(0.30, 0.32, 0.10), (0.39, 0.42, 0.13), (0.48, 0.50, 0.16), (0.57, 0.57, 0.19),
        (0.66, 0.62, 0.22), (0.74, 0.67, 0.27), (0.81, 0.72, 0.33), (0.87, 0.77, 0.40)],
    4: [(0.18, 0.10, 0.05), (0.24, 0.13, 0.07), (0.30, 0.17, 0.09), (0.37, 0.21, 0.11),
        (0.44, 0.26, 0.13), (0.51, 0.31, 0.16), (0.58, 0.37, 0.20), (0.65, 0.44, 0.25)],
    5: [(0.55, 0.47, 0.36), (0.63, 0.55, 0.43), (0.70, 0.62, 0.50), (0.77, 0.69, 0.56),
        (0.83, 0.76, 0.63), (0.88, 0.82, 0.69), (0.92, 0.87, 0.75), (0.96, 0.92, 0.82)],
    # accents: flower orange, flower yellow, cactus flower red, dark socket, iron, sign paint, white, black
    6: [(0.95, 0.52, 0.08), (0.98, 0.78, 0.15), (0.85, 0.20, 0.12), (0.10, 0.07, 0.05),
        (0.25, 0.25, 0.26), (0.72, 0.62, 0.45), (0.95, 0.93, 0.88), (0.05, 0.05, 0.05)],
    7: [(0.25, 0.15, 0.08), (0.33, 0.21, 0.11), (0.42, 0.28, 0.15), (0.50, 0.35, 0.19),
        (0.58, 0.42, 0.24), (0.65, 0.49, 0.29), (0.72, 0.56, 0.35), (0.78, 0.63, 0.42)],
}
ACC = {"orange": 0, "yellow": 1, "red": 2, "socket": 3, "iron": 4, "paint": 5, "white": 6, "black": 7}


def make_palette():
    size = CELL * GRID
    img = bpy.data.images.get("T_DesertKit_Palette")
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new("T_DesertKit_Palette", size, size, alpha=False)
    px = [0.0] * (size * size * 4)
    for row, shades in PALETTE.items():
        for col, rgb in enumerate(shades):
            for y in range(row * CELL, row * CELL + CELL):
                for x in range(col * CELL, col * CELL + CELL):
                    i = (y * size + x) * 4
                    px[i:i + 4] = [rgb[0], rgb[1], rgb[2], 1.0]
    img.pixels = px
    img.filepath_raw = PALETTE_PATH
    img.file_format = 'PNG'
    img.save()
    return img


def make_material(img):
    m = bpy.data.materials.get("M_DesertKit") or bpy.data.materials.new("M_DesertKit")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    return m


# ---------------------------------------------------------------------------
# Geometry helpers. Every part is a bmesh with a face int layer "role".
# ---------------------------------------------------------------------------

def _bm():
    bm = bmesh.new()
    bm.faces.layers.int.new("role")
    return bm


def _set_role(bm, faces, role):
    lay = bm.faces.layers.int["role"]
    for f in faces:
        f[lay] = role


def _to_obj(bm, name, coll):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


def part_box(coll, role, size, center, rng=None, jitter=0.0, bevel=0.0, cuts=0, flat_top=True,
             rot_z=0.0, keep_x=None, keep_y=None):
    """Chunky jittered box. keep_x/keep_y: |coord| beyond which verts are not jittered (clean module ends)."""
    bm = _bm()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    sx, sy, sz = size
    if rng and jitter:
        for v in bm.verts:
            top = v.co.z > sz / 2 - 1e-4
            bottom = v.co.z < -sz / 2 + 1e-4
            j = Vector((rng.uniform(-1, 1) * sx, rng.uniform(-1, 1) * sy, rng.uniform(-1, 1) * sz)) * jitter
            if bottom or (top and flat_top):
                j.z = 0.0
            # verts on a module end plane stay exactly in place so modules connect seamlessly
            if (keep_x is not None and abs(center[0] + v.co.x) > keep_x) or \
               (keep_y is not None and abs(center[1] + v.co.y) > keep_y):
                j = Vector((0, 0, 0))
            v.co += j
    if bevel:
        w = min(bevel, 0.3 * min(size))
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=w, segments=1, affect='EDGES', clamp_overlap=True)
    if rot_z:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rot_z, 3, 'Z'))
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


def part_lump(coll, role, radius, center, rng, squash=(1, 1, 1), subdiv=1, noise=0.18, flatten_bottom=True):
    """Faceted rock lump from an icosphere."""
    bm = _bm()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in bm.verts:
        n = v.co.normalized()
        v.co = v.co * (1 + rng.uniform(-noise, noise))
        v.co.x *= squash[0]
        v.co.y *= squash[1]
        v.co.z *= squash[2]
    if flatten_bottom:
        zmin = min(v.co.z for v in bm.verts)
        cut = zmin + (radius * squash[2]) * 0.35
        for v in bm.verts:
            if v.co.z < cut:
                v.co.z = cut
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bmesh.ops.translate(bm, vec=(0, 0, -cut), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


def part_cyl(coll, role, r0, r1, p0, p1, segs=6, cap=True):
    """Tapered cylinder between two points."""
    bm = _bm()
    a, b = Vector(p0), Vector(p1)
    d = b - a
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=d.length)
    q = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=q, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


def part_lathe(coll, role, profile, center, segs=8, ridged=0.0):
    """Revolve (r, z) profile around Z. ridged > 0 alternates radius for cactus ribs."""
    bm = _bm()
    rings = []
    for r, z in profile:
        ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            rr = r * (1 - ridged if (k % 2 and r > 1e-4) else 1)
            ring.append(bm.verts.new((center[0] + math.cos(a) * rr, center[1] + math.sin(a) * rr, center[2] + z)))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        for k in range(segs):
            q = [rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]]
            try:
                faces.append(bm.faces.new(q))
            except ValueError:
                pass
    if profile[0][0] > 1e-4:
        faces.append(bm.faces.new(list(reversed(rings[0]))))
    if profile[-1][0] > 1e-4:
        faces.append(bm.faces.new(rings[-1]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


def part_blade(coll, role, base, direction, length, width, bend=0.25, thick=0.012):
    """Thin 3-sided tapered leaf/blade (visible from every side, no backface issues)."""
    bm = _bm()
    b = Vector(base)
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(d).normalized()
    mid = b + d * length * 0.5 + Vector((0, 0, -1)) * length * bend * 0.3
    tip = b + d * length + Vector((0, 0, -1)) * length * bend
    ring0 = [bm.verts.new(b + side * width), bm.verts.new(b - side * width), bm.verts.new(b + up * thick)]
    ring1 = [bm.verts.new(mid + side * width * 0.6), bm.verts.new(mid - side * width * 0.6), bm.verts.new(mid + up * thick)]
    t = bm.verts.new(tip)
    for i in range(3):
        j = (i + 1) % 3
        bm.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]])
        bm.faces.new([ring1[i], ring1[j], t])
    bm.faces.new(list(reversed(ring0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


def part_tube_arc(coll, role, center, radius, a0, a1, r_base, r_tip, segs=10, sides=6, plane_rot=0.0, lift=0.0):
    """Tapered tube along an arc in the XZ plane (bone ribs)."""
    bm = _bm()
    rings = []
    for i in range(segs + 1):
        t = i / segs
        a = a0 + (a1 - a0) * t
        p = Vector((math.cos(a) * radius, 0, math.sin(a) * radius + lift))
        tan = Vector((-math.sin(a), 0, math.cos(a)))
        n1 = Vector((0, 1, 0))
        n2 = tan.cross(n1).normalized()
        r = r_base + (r_tip - r_base) * t
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(p + (n1 * math.cos(ang) + n2 * math.sin(ang)) * r))
        rings.append(ring)
    for i in range(segs):
        for k in range(sides):
            bm.faces.new([rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(plane_rot, 3, 'Z'))
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    _set_role(bm, bm.faces, role)
    return _to_obj(bm, "part", coll)


BOOL_FALLBACKS = []


def _bbox(ob):
    vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))


def _apply_bool(base, other, op='UNION'):
    """Exact boolean with a safety check: if the solver drops geometry (result smaller than the
    union of both bounding boxes), restore the base mesh and join the part instead."""
    backup = base.data.copy()
    if op == 'UNION':
        lo1, hi1 = _bbox(base)
        lo2, hi2 = _bbox(other)
        exp_lo = Vector(map(min, lo1, lo2))
        exp_hi = Vector(map(max, hi1, hi2))
    mod = base.modifiers.new("u", 'BOOLEAN')
    mod.operation = op
    mod.solver = 'EXACT'
    mod.object = other
    ctx = bpy.context
    with ctx.temp_override(object=base, active_object=base, selected_objects=[base], selected_editable_objects=[base]):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ok = len(base.data.vertices) > 0
    if ok and op == 'UNION':
        lo, hi = _bbox(base)
        ok = all(abs(a - b) < 0.03 for a, b in zip(lo, exp_lo)) and all(abs(a - b) < 0.03 for a, b in zip(hi, exp_hi))
    if ok:
        bpy.data.meshes.remove(backup)
        bpy.data.objects.remove(other, do_unlink=True)
        return
    BOOL_FALLBACKS.append(base.name)
    broken = base.data
    base.data = backup
    bpy.data.meshes.remove(broken)
    if op == 'UNION':
        join([base, other])
    else:
        bpy.data.objects.remove(other, do_unlink=True)


def union(parts):
    """Boolean-union solid parts into one clean connected mesh (no hidden interior faces)."""
    base = parts[0]
    for p in parts[1:]:
        _apply_bool(base, p)
    return base


def join(parts):
    """Join parts that are intentionally separate pieces (leaves, clusters, props)."""
    base = parts[0]
    if len(parts) > 1:
        ctx = bpy.context
        with ctx.temp_override(active_object=base, selected_editable_objects=parts, selected_objects=parts):
            bpy.ops.object.join()
    return base


# ---------------------------------------------------------------------------
# Finishing: name, origin at base centre, palette UVs, material
# ---------------------------------------------------------------------------
MAT = None


def _remove_slivers(me, max_verts=40, max_size=0.08):
    """Delete tiny loose shards the boolean solver can leave behind (a few verts, a few cm)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    seen = set()
    dead = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, group = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop()
            group.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        if len(group) <= max_verts:
            ext = max(max(g.co[i] for g in group) - min(g.co[i] for g in group) for i in range(3))
            if ext < max_size:
                dead += group
    if dead:
        bmesh.ops.delete(bm, geom=dead, context='VERTS')
        bm.to_mesh(me)
    bm.free()


def finish(ob, name, coll, rng, shade_bias=0, ground=True, recenter=True):
    ob.name = name
    ob.data.name = name
    me = ob.data
    _remove_slivers(me)
    # origin at base centre, ground contact at Z=0
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    off = Vector(((min(xs) + max(xs)) / 2 if recenter else 0.0, (min(ys) + max(ys)) / 2 if recenter else 0.0,
                  min(zs) if ground else 0.0))
    me.transform(Matrix.Translation(-off))
    ob.location = (0, 0, 0)
    h = max(max(zs) - min(zs), 1e-3)
    # palette UVs
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    role_attr = me.attributes.get("role")
    size = CELL * GRID
    for poly in me.polygons:
        role = role_attr.data[poly.index].value if role_attr else 0
        acc_col = None
        if role >= 100:
            role, acc_col = 6, role - 100
        nz = poly.normal.z
        hz = (poly.center.z) / h
        if role == 1:
            col = 4.6 + nz * 0.8 + rng.uniform(-0.25, 0.25) + shade_bias   # calm sand, no checker patches
        elif role in (0, 7):
            col = 3.0 + nz * 2.2 + hz * 2.0 + rng.uniform(-0.9, 0.9) + shade_bias
            if hz < 0.12 and nz < 0.5:
                col -= 1.2     # darker near the ground / in crevices
        elif role in (2, 3):
            col = 2.5 + hz * 3.0 + nz * 1.0 + rng.uniform(-0.8, 0.8) + shade_bias
        elif role in (4, 5):
            col = 3.5 + nz * 1.5 + rng.uniform(-1.0, 1.0) + shade_bias
        else:
            col = acc_col if acc_col is not None else 0
        c = int(max(0, min(7, round(col))))
        u = (c * CELL + CELL / 2) / size
        v = (role * CELL + CELL / 2) / size
        for li in poly.loop_indices:
            uv[li].uv = (u, v)
    for poly in me.polygons:
        poly.material_index = 0
        poly.use_smooth = False
    me.materials.clear()
    me.materials.append(MAT)
    if role_attr:
        me.attributes.remove(role_attr)
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    return ob


def accent(ob, col_name):
    """Mark every face of an accent part: role value 100 + palette column (row 6 = accents)."""
    attr = ob.data.attributes["role"]
    for d in attr.data:
        d.value = 100 + ACC[col_name]
    return ob


# ---------------------------------------------------------------------------
# Asset builders
# ---------------------------------------------------------------------------
R = ROLE


def rock_block(tmp, rng, w, d, h, nx, ny, nz, step=0.06):
    """Modular block made of nx*ny*nz chunky sub-blocks with seams/steps (flat top/bottom)."""
    parts = []
    sx, sy, sz = w / nx, d / ny, h / nz
    ov = 0.06   # sub-blocks overlap so the union is one solid piece; the bevels still read as seams
    for i in range(nx):
        for j in range(ny):
            for k in range(nz):
                cx = -w / 2 + sx * (i + 0.5)
                cy = -d / 2 + sy * (j + 0.5)
                top = k == nz - 1
                hz = sz * rng.uniform(0.96, 1.0) if top else sz + ov
                size = (sx + ov - rng.uniform(0.0, step * 0.4), sy + ov - rng.uniform(0.0, step * 0.4), hz)
                parts.append(part_box(tmp, R["rock"], size, (cx + rng.uniform(-0.015, 0.015), cy + rng.uniform(-0.015, 0.015), sz * k + hz / 2),
                                      rng, jitter=0.03, bevel=0.05, cuts=1))
    return union(parts)


def boulder(tmp, rng, r, squash, subdiv=2, noise=0.16, chips=2):
    base = part_lump(tmp, R["rock"], r, (0, 0, 0), rng, squash=squash, subdiv=subdiv, noise=noise)
    for _ in range(chips):
        a = rng.uniform(0, 2 * math.pi)
        cutter = part_box(tmp, R["rock"], (r * 0.7, r * 0.7, r * 0.7),
                          (math.cos(a) * r * 1.05 * squash[0], math.sin(a) * r * 1.05 * squash[1], r * rng.uniform(0.8, 1.3) * squash[2]),
                          rot_z=rng.uniform(0, 3))
        cutter.rotation_euler = (rng.uniform(0, 1), rng.uniform(0, 1), 0)
        _apply_bool(base, cutter, 'DIFFERENCE')
    return base


def cliff_straight(tmp, rng, width=4.0, depth=1.6, height=4.0, crown=0.6):
    hw, hd = width / 2, depth / 2
    parts = [part_box(tmp, R["rock"], (width, depth, height), (0, 0, height / 2), rng, jitter=0.03, cuts=2,
                      keep_x=hw - 0.05)]
    # front block rows (layered sandstone), kept inside the module ends
    rows = 3
    for k in range(rows):
        x = -hw + 0.1
        z0 = height * k / rows
        rh = height / rows
        while x < hw - 0.3:
            bw = min(rng.uniform(0.8, 1.6), hw - 0.1 - x)
            if bw < 0.35:
                break
            proud = rng.uniform(0.08, 0.2)
            parts.append(part_box(tmp, R["rock"], (bw - 0.05, 0.3, rh - 0.06),
                                  (x + bw / 2, -hd - proud + 0.15, z0 + rh / 2), rng, jitter=0.06, bevel=0.05, cuts=1))
            x += bw
    # crown blocks give the top silhouette some variation (away from the ends)
    x = -hw + 0.6
    while x < hw - 0.9:
        bw = rng.uniform(0.6, 1.2)
        ch = rng.uniform(0.2, crown)
        parts.append(part_box(tmp, R["rock"], (bw, depth * rng.uniform(0.5, 0.85), ch),
                              (x + bw / 2, rng.uniform(-0.15, 0.15), height + ch / 2 - 0.02), rng, jitter=0.08, bevel=0.05, cuts=1))
        x += bw + rng.uniform(0.1, 0.5)
    return union(parts)


def cliff_corner(tmp, rng, depth=1.6, height=4.0, tower=0.0):
    """90-degree outer corner: one open end at x=-2 (profile y in [-0.8,0.8]), the other at y=+2."""
    hd = depth / 2
    parts = [
        part_box(tmp, R["rock"], (2.0 + hd, depth, height), ((-2.0 + hd) / 2, 0, height / 2), rng, jitter=0.03, cuts=2, keep_x=1.95, keep_y=1.95),
        part_box(tmp, R["rock"], (depth, 2.0 + hd, height), (0, (2.0 - hd) / 2, height / 2), rng, jitter=0.03, cuts=2, keep_x=1.95, keep_y=1.95),
    ]
    # front blocks along both outer faces
    for k in range(3):
        rh = height / 3
        z = rh * (k + 0.5)
        parts.append(part_box(tmp, R["rock"], (1.1, 0.3, rh - 0.06), (-1.2, -hd - 0.05, z), rng, jitter=0.06, bevel=0.05, cuts=1))
        parts.append(part_box(tmp, R["rock"], (0.3, 1.1, rh - 0.06), (hd + 0.05, 1.2, z), rng, jitter=0.06, bevel=0.05, cuts=1))
        parts.append(part_box(tmp, R["rock"], (0.9, 0.9, rh - 0.05), (0.1, -0.1, z), rng, jitter=0.06, bevel=0.05, cuts=1))
    if tower:
        parts.append(part_box(tmp, R["rock"], (1.4, 1.4, tower), (0.0, 0.0, height + tower / 2 - 0.05), rng, jitter=0.07, bevel=0.06, cuts=1))
        parts.append(part_box(tmp, R["rock"], (1.0, 1.0, tower * 0.5), (0.05, 0.05, height + tower + tower * 0.25 - 0.1), rng, jitter=0.08, bevel=0.05, cuts=1))
    return union(parts)


def cliff_end(tmp, rng, depth=1.6, height=4.0):
    """Open end at x=-2 matching the straight profile, stepping down to the ground by x=+1.4."""
    hd = depth / 2
    parts = [part_box(tmp, R["rock"], (1.6, depth, height), (-1.2, 0, height / 2), rng, jitter=0.03, cuts=2, keep_x=1.95)]
    steps = [(0.4, 0.8 * height, 0.95), (1.2, 0.55 * height, 0.85), (2.0, 0.3 * height, 0.7), (2.7, 0.12 * height, 0.55)]
    for x, h, dd in steps:
        parts.append(part_box(tmp, R["rock"], (0.9, depth * dd, h), (-2.0 + x + 0.35, rng.uniform(-0.1, 0.1), h / 2), rng, jitter=0.07, bevel=0.05, cuts=1))
    return union(parts)


def stacked(tmp, rng, base_w, levels, shrink=0.85, lean=0.05, jitter=0.07, round_top=False):
    parts = []
    z = 0.0
    w = base_w
    ox = oy = 0.0
    for i, h in enumerate(levels):
        d = w * rng.uniform(0.8, 1.0)
        parts.append(part_box(tmp, R["rock"], (w, d, h), (ox, oy, z + h / 2), rng, jitter=jitter, bevel=0.06, cuts=1, rot_z=rng.uniform(-0.15, 0.15)))
        z += h - 0.04
        w *= shrink * rng.uniform(0.92, 1.05)
        ox += rng.uniform(-lean, lean)
        oy += rng.uniform(-lean, lean)
    return union(parts), z


def debris(tmp, rng, n, spread, size, role=R["rock"]):
    parts = []
    for i in range(n):
        a = rng.uniform(0, 2 * math.pi)
        rr = rng.uniform(0.0, spread) if i else 0.0
        s = size * rng.uniform(0.5, 1.0) * (1.5 if i == 0 else 1.0)
        if rng.random() < 0.5:
            parts.append(part_lump(tmp, role, s, (math.cos(a) * rr, math.sin(a) * rr, 0), rng, squash=(1.2, 1.0, 0.7), subdiv=1, noise=0.25))
        else:
            parts.append(part_box(tmp, role, (s * 1.6, s * 1.2, s * 0.9), (math.cos(a) * rr, math.sin(a) * rr, s * 0.45), rng,
                                  jitter=0.12, bevel=0.03, rot_z=rng.uniform(0, 3)))
    return join(parts)


# ---- vegetation ----

def cactus_column(tmp, base, height, radius, segs=8):
    prof = [(radius * 0.9, 0.0), (radius, height * 0.15), (radius, height - radius * 0.9), (radius * 0.75, height - radius * 0.35),
            (radius * 0.35, height - radius * 0.05), (0.0, height)]
    return part_lathe(tmp, R["cactus"], prof, base, segs=segs, ridged=0.14)


def cactus_arm(tmp, trunk_r, side, z, out, up, r):
    """Elbow arm: horizontal stub out of the trunk, then a vertical column (unioned to the trunk)."""
    sx, sy = side
    p0 = Vector((sx * trunk_r * 0.4, sy * trunk_r * 0.4, z))
    p1 = Vector((sx * (trunk_r + out), sy * (trunk_r + out), z + r * 0.4))
    stub = part_cyl(tmp, R["cactus"], r, r, p0, p1, segs=8)
    elbow = part_lump(tmp, R["cactus"], r * 1.02, (p1.x, p1.y, p1.z - r), random.Random(1), subdiv=1, noise=0.0, flatten_bottom=False)
    col = cactus_column(tmp, (p1.x, p1.y, p1.z - r * 0.2), up, r)
    return [stub, elbow, col]


def cactus(tmp, rng, height, radius, arms):
    parts = [cactus_column(tmp, (0, 0, 0), height, radius)]
    for (side, zf, out, upf) in arms:
        parts += cactus_arm(tmp, radius, side, height * zf, out, height * upf, radius * 0.62)
    return union(parts)


def grass(tmp, rng, n, h, spread, role=R["grass"], width=0.035, bend=0.3):
    parts = []
    for i in range(n):
        a = rng.uniform(0, 2 * math.pi)
        tilt = rng.uniform(0.25, 0.75)
        d = (math.cos(a) * tilt, math.sin(a) * tilt, 1.0)
        b = (math.cos(a) * rng.uniform(0, spread), math.sin(a) * rng.uniform(0, spread), -0.02)
        parts.append(part_blade(tmp, role, b, d, h * rng.uniform(0.6, 1.0), width, bend=bend))
    return parts


def branch_tree(tmp, rng, p, d, length, r, depth, out, role=R["dry"], segs=5):
    d = d.normalized()
    q = p + d * length
    out.append(part_cyl(tmp, role, r, r * 0.65, p, q, segs=segs))
    if depth <= 0:
        return
    for _ in range(rng.choice([2, 2, 3])):
        nd = d + Vector((rng.uniform(-0.9, 0.9), rng.uniform(-0.9, 0.9), rng.uniform(-0.1, 0.5)))
        branch_tree(tmp, rng, q - d * r * 0.5, nd, length * rng.uniform(0.55, 0.75), r * 0.62, depth - 1, out, role, segs)


# ---- props ----

def bone(tmp, rng, length, r, center, rot):
    a = Vector((-length / 2, 0, 0))
    b = Vector((length / 2, 0, 0))
    parts = [part_cyl(tmp, R["bone"], r, r * 0.85, a, b, segs=6)]
    for e in (a, b):
        for s in (-1, 1):
            parts.append(part_lump(tmp, R["bone"], r * 1.35, (e.x, s * r * 0.7, -r * 0.3), rng, subdiv=1, noise=0.05, flatten_bottom=False))
    ob = join(parts)
    ob.data.transform(Matrix.Rotation(rot, 4, 'Z'))
    ob.data.transform(Matrix.Translation(center))
    return ob


def skull(tmp, rng, center):
    parts = [part_lump(tmp, R["bone"], 0.16, (0, 0, 0.16), rng, squash=(1.0, 1.15, 0.95), subdiv=2, noise=0.04, flatten_bottom=False),
             part_box(tmp, R["bone"], (0.18, 0.14, 0.09), (0, -0.16, 0.07), rng, jitter=0.05, bevel=0.02)]
    for s in (-1, 1):
        parts.append(accent(part_cyl(tmp, R["accent"], 0.04, 0.035, (s * 0.06, -0.14, 0.2), (s * 0.06, -0.19, 0.2), segs=6), "socket"))
        # small horn
        parts.append(part_cyl(tmp, R["bone"], 0.035, 0.008, (s * 0.12, 0.02, 0.26), (s * 0.3, 0.06, 0.36), segs=5))
    ob = join(parts)
    ob.data.transform(Matrix.Translation(center))
    return ob


# ---------------------------------------------------------------------------
# Kit
# ---------------------------------------------------------------------------

def build():
    global MAT
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for blk in (bpy.data.meshes, bpy.data.materials):
        for item in list(blk):
            if item.users == 0:
                blk.remove(item)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0

    img = make_palette()
    MAT = make_material(img)

    root = bpy.data.collections.new("DESERT_ENVIRONMENT")
    scene.collection.children.link(root)
    cols = {}
    for n in ("ROCK_BLOCKS", "ROCK_VARIATIONS", "BOULDERS", "ROCK_FORMATIONS", "CLIFFS", "GROUND", "VEGETATION", "PROPS", "EXTRAS"):
        cols[n] = bpy.data.collections.new(n)
        root.children.link(cols[n])
    tmp = bpy.data.collections.new("_tmp")
    scene.collection.children.link(tmp)

    made = []

    def add(name, coll, ob, rng, **kw):
        made.append(finish(ob, name, cols[coll], rng, **kw))

    # 1. ROCK BLOCKS -------------------------------------------------------
    specs = [(1.6, 1.2, 0.9, 2, 2, 1), (1.8, 1.4, 0.7, 2, 2, 1), (1.4, 1.2, 1.1, 2, 1, 2), (1.3, 1.3, 0.9, 2, 2, 2),
             (1.5, 1.3, 1.4, 2, 2, 2), (1.6, 1.2, 0.9, 3, 1, 2), (1.2, 1.1, 1.6, 2, 1, 3), (1.6, 1.2, 1.0, 2, 1, 1),
             (2.0, 1.2, 0.8, 3, 2, 1), (1.6, 1.3, 1.1, 2, 2, 2)]
    for i, (w, d, h, nx, ny, nz) in enumerate(specs, 1):
        rng = random.Random(100 + i)
        if i == 8:
            # tall pointed slab leaning on lower blocks
            parts = [rock_block(tmp, rng, w, d, 0.55, 2, 1, 1)]
            spike = part_box(tmp, R["rock"], (0.5, 0.55, 1.8), (0.0, 0.0, 0.9), rng, jitter=0.08, bevel=0.05, cuts=1, flat_top=False)
            for v in spike.data.vertices:
                if v.co.z > 1.4:
                    v.co.x *= 0.45
                    v.co.y *= 0.6
            parts.append(spike)
            ob = union(parts)
        else:
            ob = rock_block(tmp, rng, w, d, h, nx, ny, nz)
        add(f"ROCK_BLOCK_{i:02d}", "ROCK_BLOCKS", ob, rng)

    # 2. SMALL / MEDIUM ROCKS ---------------------------------------------
    for i in range(1, 13):
        rng = random.Random(200 + i)
        if i in (1, 2, 4):        # angular rocks
            ob = part_lump(tmp, R["rock"], rng.uniform(0.35, 0.5), (0, 0, 0), rng, squash=(1.1, 1.0, 0.85), subdiv=1, noise=0.22)
        elif i in (3, 7):         # flat / low rocks
            ob = part_box(tmp, R["rock"], (rng.uniform(0.8, 1.1), rng.uniform(0.6, 0.8), rng.uniform(0.25, 0.35)), (0, 0, 0.15), rng, jitter=0.12, bevel=0.04, cuts=1)
        elif i in (5, 6, 12):     # pointed rocks
            ob = part_cyl(tmp, R["rock"], rng.uniform(0.3, 0.42), rng.uniform(0.03, 0.08), (0, 0, 0), (rng.uniform(-0.1, 0.1), 0, rng.uniform(0.7, 1.1)), segs=5)
            if i == 6:
                ob = union([ob, part_lump(tmp, R["rock"], 0.18, (0.35, 0.1, 0), rng, subdiv=1, noise=0.25)])
        elif i in (8, 11):        # broken medium blocks
            ob = rock_block(tmp, rng, rng.uniform(1.1, 1.4), 0.8, rng.uniform(0.6, 0.75), 2, 1, 1)
        else:                     # small clusters
            ob = debris(tmp, rng, 5 if i == 9 else 6, 0.45, 0.17)
        add(f"ROCK_S_{i:02d}", "ROCK_VARIATIONS", ob, rng)

    # 3. BOULDERS ------------------------------------------------------------
    bspec = [(1.2, (1.25, 1.0, 0.75)), (1.1, (0.95, 0.9, 1.15)), (1.25, (1.05, 1.0, 1.0)), (1.0, (1.1, 0.95, 0.85)), (1.15, (0.9, 0.95, 1.2))]
    for i, (r, sq) in enumerate(bspec, 1):
        rng = random.Random(300 + i)
        add(f"BOULDER_{i:02d}", "BOULDERS", boulder(tmp, rng, r, sq, noise=0.12 + 0.03 * i, chips=1 + i % 3), rng)

    # 4. FORMATIONS ---------------------------------------------------------
    rng = random.Random(401)
    parts = []
    for side in (-1, 1):                      # legs
        z = 0.0
        for k, (w, h) in enumerate([(2.2, 1.4), (1.9, 1.3), (1.7, 1.2), (1.6, 1.0)]):
            parts.append(part_box(tmp, R["rock"], (w, 1.8 - k * 0.1, h), (side * (3.6 - k * 0.15), rng.uniform(-0.1, 0.1), z + h / 2), rng, jitter=0.06, bevel=0.07, cuts=1))
            z += h - 0.05
    n = 9                                    # span: blocks along an arc
    for k in range(n):
        a = math.pi * (0.16 + 0.68 * k / (n - 1))
        x = -math.cos(a) * 3.5
        z = 4.6 + math.sin(a) * 1.6
        blk = part_box(tmp, R["rock"], (1.15, 1.5, 1.0), (0, 0, 0), rng, jitter=0.07, bevel=0.07, cuts=1)
        blk.data.transform(Matrix.Rotation(math.pi / 2 - a, 4, 'Y'))
        blk.data.transform(Matrix.Translation((x, rng.uniform(-0.08, 0.08), z)))
        parts.append(blk)
    arch = union(parts)
    rubble = debris(tmp, rng, 7, 1.2, 0.25)
    rubble.data.transform(Matrix.Translation((-3.2, -1.0, 0)))
    rubble2 = debris(tmp, rng, 6, 1.0, 0.22)
    rubble2.data.transform(Matrix.Translation((3.4, 0.9, 0)))
    add("NATURAL_ARCH_01", "ROCK_FORMATIONS", join([arch, rubble, rubble2]), rng)

    rng = random.Random(402)
    ob, _ = stacked(tmp, rng, 2.6, [1.4, 1.3, 1.2, 1.0, 0.8], shrink=0.88, lean=0.08)
    add("ROCK_FORMATION_01", "ROCK_FORMATIONS", ob, rng)
    rng = random.Random(403)
    ob, _ = stacked(tmp, rng, 1.6, [1.6, 1.5, 1.4, 1.3, 1.1, 0.8], shrink=0.9, lean=0.06)
    add("ROCK_PILLAR_01", "ROCK_FORMATIONS", ob, rng)
    rng = random.Random(404)
    ob, top = stacked(tmp, rng, 1.2, [1.2, 1.5, 1.5, 1.4, 1.2, 1.0, 0.7], shrink=0.95, lean=0.05)
    cap = part_box(tmp, R["rock"], (1.15, 1.1, 0.35), (0, 0, top + 0.12), rng, jitter=0.1, bevel=0.05, cuts=1)
    ob = union([ob, cap])
    base_r = debris(tmp, rng, 6, 0.9, 0.22)
    add("ROCK_PILLAR_02", "ROCK_FORMATIONS", join([ob, base_r]), rng)
    rng = random.Random(405)
    add("ROCK_WALL_01", "ROCK_FORMATIONS", rock_block(tmp, rng, 4.0, 1.0, 1.3, 4, 1, 2), rng)

    # 5. CLIFFS (module: 4 m wide, 1.6 m deep, 4 m tall at the connecting ends) -------------
    rng = random.Random(501)
    add("CLIFF_STRAIGHT_01", "CLIFFS", cliff_straight(tmp, rng), rng, recenter=False)
    rng = random.Random(502)
    add("CLIFF_STRAIGHT_02", "CLIFFS", cliff_straight(tmp, rng, crown=0.9), rng, recenter=False)
    rng = random.Random(503)
    add("CLIFF_CORNER_01", "CLIFFS", cliff_corner(tmp, rng), rng, recenter=False)
    rng = random.Random(504)
    add("CLIFF_CORNER_02", "CLIFFS", cliff_corner(tmp, rng, tower=1.6), rng, recenter=False)
    rng = random.Random(505)
    add("CLIFF_END_01", "CLIFFS", cliff_end(tmp, rng), rng, recenter=False)
    rng = random.Random(506)
    ob, _ = stacked(tmp, rng, 3.0, [2.0, 1.8, 1.6, 1.4, 1.0], shrink=0.88, lean=0.1)
    add("CLIFF_TOWER_01", "CLIFFS", ob, rng)
    rng = random.Random(507)
    ob, top = stacked(tmp, rng, 3.2, [1.8, 1.7, 1.6, 1.5, 1.3, 1.0], shrink=0.84, lean=0.14)
    add("CLIFF_TOWER_02", "CLIFFS", ob, rng)

    # 6. GROUND (4 x 4 m tiles, 0.25 m thick; top surface flat for gameplay) ------------------
    rng = random.Random(601)
    g = part_box(tmp, R["sand"], (4.0, 4.0, 0.25), (0, 0, 0.125), rng, jitter=0.0, bevel=0.04, cuts=3)
    for v in g.data.vertices:
        if v.co.z > 0.2 and abs(v.co.x) < 1.9 and abs(v.co.y) < 1.9:
            v.co.z += rng.uniform(-0.03, 0.03)
    add("GROUND_FLAT_01", "GROUND", g, rng)
    rng = random.Random(602)
    g = part_box(tmp, R["sand"], (4.0, 4.0, 0.25), (0, 0, 0.125), rng, bevel=0.04, cuts=3)
    for v in g.data.vertices:
        if v.co.z > 0.2:
            v.co.z += (v.co.y + 2.0) / 4.0 * 0.8 + rng.uniform(-0.02, 0.02) * (abs(v.co.x) < 1.9)
    add("GROUND_SLOPE_01", "GROUND", g, rng)
    rng = random.Random(603)
    plat = part_box(tmp, R["rock"], (4.0, 4.0, 0.9), (0, 0, 0.45), rng, jitter=0.0, bevel=0.06, cuts=2)
    role = plat.data.attributes["role"]
    for poly in plat.data.polygons:
        if poly.normal.z > 0.9:
            role.data[poly.index].value = R["sand"]
    lip = []
    for k in range(5):
        lip.append(part_box(tmp, R["rock"], (0.78, 0.3, 0.82), (-1.6 + k * 0.8, -1.98, 0.41), rng, jitter=0.06, bevel=0.05, cuts=1))
    add("GROUND_RAISED_01", "GROUND", union([plat] + lip), rng)
    rng = random.Random(604)
    g = part_box(tmp, R["sand"], (4.0, 4.0, 0.25), (0, 0, 0.125), rng, bevel=0.04, cuts=3)
    for v in g.data.vertices:
        if v.co.z > 0.2 and abs(v.co.x) < 1.9 and abs(v.co.y) < 1.9:
            v.co.z += rng.uniform(-0.03, 0.03)
    rocks = []
    for k in range(6):
        p = (rng.uniform(-1.5, 1.5), rng.uniform(-1.5, 1.5), 0.18)
        rocks.append(part_lump(tmp, R["rock"], rng.uniform(0.12, 0.3), p, rng, squash=(1.2, 1.0, 0.8), subdiv=1, noise=0.25))
    add("GROUND_ROCKY_01", "GROUND", union([g] + rocks), rng)

    # 7. VEGETATION -----------------------------------------------------------
    rng = random.Random(701)
    add("CACTUS_TALL_01", "VEGETATION", cactus(tmp, rng, 2.2, 0.2, [((1, 0), 0.38, 0.22, 0.32), ((-1, 0), 0.52, 0.2, 0.26)]), rng)
    rng = random.Random(702)
    add("CACTUS_TALL_02", "VEGETATION", cactus(tmp, rng, 2.0, 0.19, [((0, 1), 0.45, 0.22, 0.3), ((0, -1), 0.3, 0.18, 0.22)]), rng)
    rng = random.Random(703)
    add("CACTUS_MED_01", "VEGETATION", cactus(tmp, rng, 1.3, 0.17, [((1, 0), 0.35, 0.16, 0.3)]), rng)
    rng = random.Random(704)
    c = cactus(tmp, rng, 1.6, 0.17, [((1, 0), 0.3, 0.2, 0.42), ((-1, 0), 0.45, 0.2, 0.32), ((0, -1), 0.6, 0.16, 0.2)])
    add("CACTUS_ARM_01", "VEGETATION", c, rng)
    rng = random.Random(705)
    ball = part_lathe(tmp, R["cactus"], [(0.22, 0.0), (0.3, 0.12), (0.3, 0.3), (0.22, 0.45), (0.1, 0.52), (0.0, 0.53)], (0, 0, 0), segs=10, ridged=0.12)
    flowers = []
    for k in range(3):
        a = k * 2.1
        fp = (math.cos(a) * 0.09, math.sin(a) * 0.09, 0.52)
        flowers.append(accent(part_lathe(tmp, R["accent"], [(0.0, 0.0), (0.06, 0.03), (0.07, 0.06), (0.0, 0.05)], fp, segs=5), "orange"))
    add("CACTUS_SMALL_01", "VEGETATION", join([ball] + flowers), rng)

    for i, (n, h, sp, w) in enumerate([(22, 0.7, 0.18, 0.035), (16, 0.55, 0.15, 0.03), (12, 0.42, 0.12, 0.03)], 1):
        rng = random.Random(710 + i)
        add(f"GRASS_BUSH_{i:02d}", "VEGETATION", join(grass(tmp, rng, n, h, sp, width=w)), rng)

    rng = random.Random(720)
    trunk = part_cyl(tmp, R["dry"], 0.09, 0.07, (0, 0, 0), (0, 0, 0.35), segs=6)
    blades = []
    for k in range(18):
        a = rng.uniform(0, 2 * math.pi)
        up = rng.uniform(0.6, 1.6)
        blades.append(part_blade(tmp, R["grass"], (0, 0, 0.3), (math.cos(a), math.sin(a), up), rng.uniform(0.6, 0.85), 0.045, bend=0.1))
    add("YUCCA_PLANT_01", "VEGETATION", join([trunk] + blades), rng)

    for i in (1, 2):
        rng = random.Random(730 + i)
        parts = []
        for k in range(5 if i == 1 else 7):
            a = rng.uniform(0, 2 * math.pi)
            rr = rng.uniform(0, 0.35)
            parts.append(part_lump(tmp, R["grass"], rng.uniform(0.16, 0.26), (math.cos(a) * rr, math.sin(a) * rr, 0.0), rng,
                                   squash=(1.0, 1.0, 0.8), subdiv=1, noise=0.25))
        if i == 1:
            twigs = []
            branch_tree(tmp, rng, Vector((0, 0, 0)), Vector((0, 0, 1)), 0.25, 0.03, 2, twigs)
            parts += twigs
        else:
            for k in range(4):
                a = rng.uniform(0, 6.28)
                parts.append(accent(part_lump(tmp, R["accent"], 0.04, (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.32), rng, subdiv=1, noise=0.1, flatten_bottom=False), "red"))
        add(f"DESERT_SHRUB_{i:02d}", "VEGETATION", join(parts), rng)

    rng = random.Random(740)
    leaves = []
    for ring, (n, ln, up) in enumerate([(7, 0.6, 0.35), (6, 0.45, 0.9), (4, 0.3, 2.0)]):
        for k in range(n):
            a = 2 * math.pi * k / n + ring * 0.4
            leaves.append(part_blade(tmp, R["cactus"], (0, 0, 0.05), (math.cos(a), math.sin(a), up), ln, 0.1, bend=-0.05, thick=0.05))
    add("AGAVE_PLANT_01", "VEGETATION", join(leaves), rng)

    rng = random.Random(750)
    parts = grass(tmp, rng, 14, 0.45, 0.2, width=0.03)
    for k in range(5):
        a = rng.uniform(0, 6.28)
        rr = rng.uniform(0.05, 0.25)
        stem_top = Vector((math.cos(a) * rr, math.sin(a) * rr, rng.uniform(0.4, 0.55)))
        parts.append(part_cyl(tmp, R["grass"], 0.012, 0.01, (stem_top.x * 0.5, stem_top.y * 0.5, 0), stem_top, segs=4))
        parts.append(accent(part_lathe(tmp, R["accent"], [(0.0, 0.0), (0.07, 0.02), (0.08, 0.04), (0.0, 0.035)], stem_top, segs=6), "orange"))
        parts.append(accent(part_lump(tmp, R["accent"], 0.025, stem_top + Vector((0, 0, 0.04)), rng, subdiv=1, noise=0.0, flatten_bottom=False), "yellow"))
    add("FLOWER_PLANT_01", "VEGETATION", join(parts), rng)

    rng = random.Random(760)
    twigs = []
    for k in range(4):
        a = k * 1.6 + rng.uniform(-0.3, 0.3)
        branch_tree(tmp, rng, Vector((0, 0, 0)), Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1)), 0.35, 0.03, 2, twigs)
    add("DRY_BUSH_01", "VEGETATION", join(twigs), rng)

    # 8. PROPS ----------------------------------------------------------------
    rib_specs = [(1.4, 0.13, 0.05, 0.0, 1), (1.7, 0.15, 0.05, 0.25, 1), (1.5, 0.14, 0.05, 0.0, 2)]
    for i, (rad, rb, rt, twist, count) in enumerate(rib_specs, 1):
        rng = random.Random(800 + i)
        parts = []
        for k in range(count):
            parts.append(part_tube_arc(tmp, R["bone"], (k * 0.55, k * 0.35, 0), rad, math.radians(5), math.radians(150), rb, rt,
                                       segs=12, sides=6, plane_rot=twist + k * 0.15, lift=-math.sin(math.radians(5)) * rad))
        add(f"BONE_RIB_{i:02d}", "PROPS", join(parts), rng)

    for i in (1, 2):
        rng = random.Random(820 + i)
        parts = []
        posts = [-1.0, 0.0, 1.0] if i == 1 else [-1.0, 1.0]
        for k, x in enumerate(posts):
            lean = (0.25 if (i == 2 and k == 1) else rng.uniform(-0.05, 0.05))
            p = part_box(tmp, R["wood"], (0.14, 0.14, 1.0), (0, 0, 0.5), rng, jitter=0.04, bevel=0.02)
            p.data.transform(Matrix.Rotation(lean, 4, 'Y'))
            p.data.transform(Matrix.Translation((x, 0, 0)))
            parts.append(p)
        rails = [(0.75, 0.0), (0.4, 0.0)] if i == 1 else [(0.75, 0.0), (0.35, -0.35)]
        for z, tilt in rails:
            r_ = part_box(tmp, R["wood"], (2.2 if tilt == 0 else 1.4, 0.06, 0.12), (0, -0.09, 0), rng, jitter=0.03, bevel=0.015)
            r_.data.transform(Matrix.Rotation(tilt, 4, 'Y'))
            r_.data.transform(Matrix.Translation((0.0 if tilt == 0 else -0.35, 0, z)))
            parts.append(r_)
        add(f"WOOD_FENCE_{i:02d}", "PROPS", join(parts), rng)

    rng = random.Random(830)
    tree = []
    branch_tree(tmp, rng, Vector((0, 0, 0)), Vector((0.15, 0.05, 1)), 1.4, 0.16, 3, tree, segs=6)
    roots = [part_cyl(tmp, R["dry"], 0.1, 0.03, (0, 0, 0.15), (math.cos(a) * 0.45, math.sin(a) * 0.45, 0.0), segs=5) for a in (0.3, 2.4, 4.3)]
    add("DEAD_TREE_01", "PROPS", join(tree + roots), rng)

    rng = random.Random(840)
    post = part_box(tmp, R["wood"], (0.14, 0.14, 1.7), (0, 0, 0.85), rng, jitter=0.03, bevel=0.02)
    signs = []
    for z, dirx, rot in ((1.45, 1, 0.08), (1.1, -1, -0.12)):
        bd = part_box(tmp, R["wood"], (0.8, 0.05, 0.22), (dirx * 0.38, -0.1, z), rng, jitter=0.03, bevel=0.015)
        tip = part_cyl(tmp, R["wood"], 0.16, 0.0, (dirx * 0.78, -0.1, z), (dirx * 0.98, -0.1, z), segs=4)
        paint = accent(part_box(tmp, R["accent"], (0.5, 0.02, 0.06), (dirx * 0.35, -0.13, z), rng), "paint")
        signs += [bd, tip, paint]
    add("SIGN_POST_01", "PROPS", join([post] + signs), rng)

    rng = random.Random(850)
    parts = [part_box(tmp, R["wood"], (0.66, 0.66, 0.66), (0, 0, 0.33), rng, bevel=0.02)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(part_box(tmp, R["wood"], (0.08, 0.08, 0.7), (sx * 0.31, sy * 0.31, 0.35), rng, bevel=0.01))
    for face_y in (-1, 1):
        dg = part_box(tmp, R["wood"], (0.85, 0.04, 0.08), (0, 0, 0), rng, bevel=0.01)
        dg.data.transform(Matrix.Rotation(math.radians(45 * face_y), 4, 'Y'))
        dg.data.transform(Matrix.Translation((0, face_y * 0.335, 0.33)))
        parts.append(dg)
    for sx in (-1, 1):
        dg = part_box(tmp, R["wood"], (0.04, 0.85, 0.08), (0, 0, 0), rng, bevel=0.01)
        dg.data.transform(Matrix.Rotation(math.radians(45 * sx), 4, 'X'))
        dg.data.transform(Matrix.Translation((sx * 0.335, 0, 0.33)))
        parts.append(dg)
    add("CRATE_01", "PROPS", join(parts), rng)

    rng = random.Random(860)
    body = part_lathe(tmp, R["wood"], [(0.27, 0.0), (0.31, 0.2), (0.33, 0.42), (0.31, 0.64), (0.27, 0.84)], (0, 0, 0), segs=12)
    hoops = [accent(part_lathe(tmp, R["accent"], [(r + 0.012, z - 0.03), (r + 0.012, z + 0.03)], (0, 0, 0), segs=12), "iron")
             for r, z in ((0.28, 0.08), (0.325, 0.32), (0.325, 0.52), (0.28, 0.76))]
    lid = part_lathe(tmp, R["wood"], [(0.25, 0.84), (0.25, 0.86), (0.0, 0.86)], (0, 0, 0), segs=12)
    add("BARREL_01", "PROPS", join([body, lid] + hoops), rng)

    rng = random.Random(870)
    bones = [bone(tmp, rng, rng.uniform(0.5, 0.75), 0.04, (rng.uniform(-0.6, 0.6), rng.uniform(-0.4, 0.4), 0.05), rng.uniform(0, 3.14))
             for _ in range(5)]
    add("BONE_PILE_01", "PROPS", join(bones + [skull(tmp, rng, (0.15, 0.0, 0.0))]), rng)

    # 9. EXTRAS ----------------------------------------------------------------
    rng = random.Random(901)
    add("ROCK_DEBRIS_01", "EXTRAS", debris(tmp, rng, 6, 0.6, 0.2), rng)
    rng = random.Random(902)
    st, _ = stacked(tmp, rng, 0.7, [0.25, 0.22, 0.2, 0.18, 0.15], shrink=0.8, lean=0.03, jitter=0.12)
    add("ROCK_STACK_01", "EXTRAS", st, rng)
    rng = random.Random(903)
    add("ROCK_DEBRIS_02", "EXTRAS", debris(tmp, rng, 8, 0.8, 0.16), rng)

    bpy.data.collections.remove(tmp)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)

    # lay out every collection in its own row for inspection (objects keep clean transforms otherwise)
    y = 0.0
    for name in ("ROCK_BLOCKS", "ROCK_VARIATIONS", "BOULDERS", "ROCK_FORMATIONS", "CLIFFS", "GROUND", "VEGETATION", "PROPS", "EXTRAS"):
        x = 0.0
        depth = 0.0
        for ob in cols[name].objects:
            dims = ob.dimensions
            ob.location = (x + dims.x / 2, y, 0.0)
            x += dims.x + 0.8
            depth = max(depth, dims.y)
        y -= depth + 2.0
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in made}
    tris["_bool_fallbacks"] = len(BOOL_FALLBACKS)
    tris["_fallback_assets"] = 0
    return tris
