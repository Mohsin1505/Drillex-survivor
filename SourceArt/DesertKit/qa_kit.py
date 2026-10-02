"""QA report for the desert kit: names, collections, ground contact, transforms, islands, materials, cliff seams."""
import bpy
import bmesh

EXPECTED = {
    "ROCK_BLOCKS": [f"ROCK_BLOCK_{i:02d}" for i in range(1, 11)],
    "ROCK_VARIATIONS": [f"ROCK_S_{i:02d}" for i in range(1, 13)],
    "BOULDERS": [f"BOULDER_{i:02d}" for i in range(1, 6)],
    "ROCK_FORMATIONS": ["NATURAL_ARCH_01", "ROCK_FORMATION_01", "ROCK_PILLAR_01", "ROCK_PILLAR_02", "ROCK_WALL_01"],
    "CLIFFS": ["CLIFF_STRAIGHT_01", "CLIFF_STRAIGHT_02", "CLIFF_CORNER_01", "CLIFF_CORNER_02", "CLIFF_END_01", "CLIFF_TOWER_01", "CLIFF_TOWER_02"],
    "GROUND": ["GROUND_FLAT_01", "GROUND_SLOPE_01", "GROUND_RAISED_01", "GROUND_ROCKY_01"],
    "VEGETATION": ["CACTUS_TALL_01", "CACTUS_TALL_02", "CACTUS_MED_01", "CACTUS_ARM_01", "CACTUS_SMALL_01", "GRASS_BUSH_01",
                   "GRASS_BUSH_02", "GRASS_BUSH_03", "YUCCA_PLANT_01", "DESERT_SHRUB_01", "DESERT_SHRUB_02", "AGAVE_PLANT_01",
                   "FLOWER_PLANT_01", "DRY_BUSH_01"],
    "PROPS": ["BONE_RIB_01", "BONE_RIB_02", "BONE_RIB_03", "WOOD_FENCE_01", "WOOD_FENCE_02", "DEAD_TREE_01", "SIGN_POST_01",
              "CRATE_01", "BARREL_01", "BONE_PILE_01"],
    "EXTRAS": ["ROCK_DEBRIS_01", "ROCK_STACK_01", "ROCK_DEBRIS_02"],
}
# assets built from several deliberately separate pieces (clusters, leaves, planks, bones)
MULTI_PIECE_OK = {"ROCK_S_09", "ROCK_S_10", "NATURAL_ARCH_01", "ROCK_PILLAR_02", "CACTUS_SMALL_01", "GRASS_BUSH_01", "GRASS_BUSH_02",
                  "GRASS_BUSH_03", "YUCCA_PLANT_01", "DESERT_SHRUB_01", "DESERT_SHRUB_02", "AGAVE_PLANT_01", "FLOWER_PLANT_01",
                  "DRY_BUSH_01", "BONE_RIB_03", "WOOD_FENCE_01", "WOOD_FENCE_02", "DEAD_TREE_01", "SIGN_POST_01", "CRATE_01",
                  "BARREL_01", "BONE_PILE_01", "ROCK_DEBRIS_01", "ROCK_DEBRIS_02"}


def islands(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    seen = set()
    groups = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        zmin = v.co.z
        while stack:
            a = stack.pop()
            zmin = min(zmin, a.co.z)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        groups.append(zmin)
    nonmanifold = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()
    return groups, nonmanifold


def end_profile(ob, axis, value):
    pts = set()
    for v in ob.data.vertices:
        c = v.co
        if abs((c.x if axis == 'x' else c.y) - value) < 1e-3:
            pts.add((round(c.y if axis == 'x' else c.x, 3), round(c.z, 3)))
    return pts


def run():
    rep = {"missing": [], "wrong_collection": [], "not_grounded": [], "transform": [], "modifiers": [],
           "materials": [], "floating_islands": [], "multi_piece_unexpected": [], "nonmanifold_edges": {}}
    kit = bpy.data.collections["DESERT_ENVIRONMENT"]
    all_names = set()
    for coll_name, names in EXPECTED.items():
        coll = kit.children.get(coll_name)
        for n in names:
            all_names.add(n)
            ob = bpy.data.objects.get(n)
            if ob is None:
                rep["missing"].append(n)
                continue
            if coll is None or ob.name not in coll.objects:
                rep["wrong_collection"].append(n)
            zmin = min(v.co.z for v in ob.data.vertices)
            if abs(zmin) > 1e-3:
                rep["not_grounded"].append((n, round(zmin, 3)))
            if any(abs(r) > 1e-6 for r in ob.rotation_euler) or any(abs(sc - 1) > 1e-6 for sc in ob.scale) or abs(ob.location.z) > 1e-6:
                rep["transform"].append(n)
            if ob.modifiers:
                rep["modifiers"].append(n)
            if len(ob.data.materials) != 1 or ob.data.materials[0].name != "M_DesertKit":
                rep["materials"].append(n)
            groups, nm = islands(ob.data)
            floating = [round(z, 3) for z in groups if z > 0.05]
            if len(groups) > 1 and n not in MULTI_PIECE_OK:
                rep["multi_piece_unexpected"].append((n, len(groups)))
            if floating and n not in ("BONE_RIB_03",):
                rep["floating_islands"].append((n, floating[:5]))
            if nm:
                rep["nonmanifold_edges"][n] = nm
    extra = [o.name for o in kit.all_objects if o.name not in all_names]
    rep["unexpected_objects"] = extra
    # cliff module interfaces: every open end must have the identical (y,z) / (x,z) profile
    s1 = bpy.data.objects["CLIFF_STRAIGHT_01"]
    ref = end_profile(s1, 'x', -2.0)
    checks = {
        "STRAIGHT_01 +x": end_profile(s1, 'x', 2.0),
        "STRAIGHT_02 -x": end_profile(bpy.data.objects["CLIFF_STRAIGHT_02"], 'x', -2.0),
        "STRAIGHT_02 +x": end_profile(bpy.data.objects["CLIFF_STRAIGHT_02"], 'x', 2.0),
        "CORNER_01 -x": end_profile(bpy.data.objects["CLIFF_CORNER_01"], 'x', -2.0),
        "CORNER_01 +y": end_profile(bpy.data.objects["CLIFF_CORNER_01"], 'y', 2.0),
        "CORNER_02 -x": end_profile(bpy.data.objects["CLIFF_CORNER_02"], 'x', -2.0),
        "CORNER_02 +y": end_profile(bpy.data.objects["CLIFF_CORNER_02"], 'y', 2.0),
        "END_01 -x": end_profile(bpy.data.objects["CLIFF_END_01"], 'x', -2.0),
    }
    def bounds(p):
        if not p:
            return None
        return (min(a for a, b in p), max(a for a, b in p), min(b for a, b in p), max(b for a, b in p))
    rep["cliff_ref_profile"] = bounds(ref)
    rep["cliff_profiles"] = {k: bounds(v) for k, v in checks.items()}
    return rep
