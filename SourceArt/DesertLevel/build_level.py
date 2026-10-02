"""
Drillex desert canyon level - assembled from the desert kit (Blender 5.2).

* Kit assets are appended once into a hidden KIT_SOURCE collection; every placement is a linked
  duplicate (shared mesh + shared material) -> instancing, tiny memory, mobile-friendly.
* Terrain: one sculpted-by-script ground mesh (canyon floor rising toward the walls, sandy dunes,
  small depressions) with a flattened, winding train corridor.
* Composition: foreground framing rocks, mid-ground canyon walls / pillars / boulders, background
  arch + huge silhouettes; vegetation in clusters with open sand between them.
* Sunset: low warm sun from behind the canyon (rim light + long shadows), warm sky gradient,
  volumetric haze for atmospheric depth.
Everything is deterministic (seeded), so re-running rebuilds the same level.
"""
import bpy
import bmesh
import math
import random
from mathutils import Vector, Matrix, noise

KIT = r"D:\Drillex\SourceArt\DesertKit\DRL_DesertKit.blend"
TRAIN = [
    (r"D:\Drillex\SourceArt\DrillVehicle\DRL_DrillVehicle.blend", "DRL_DrillVehicle"),
    (r"D:\Drillex\SourceArt\GunCar\DRL_GunCar.blend", "DRL_GunCar"),
    (r"D:\Drillex_StorageBlock\DRL_StorageBlock.blend", "DRL_StorageBlock"),
    (r"D:\Drillex\SourceArt\RocketCar\DRL_RocketCar.blend", "DRL_RocketCar"),
    (r"D:\Drillex\SourceArt\GatlingCar\DRL_GatlingCar.blend", "DRL_GatlingCar"),
    (r"D:\Drillex\SourceArt\FlameCar\DRL_FlameCar.blend", "DRL_FlameCar"),
]

CORRIDOR = 5.5        # half-width kept completely clear for the train
SHOULDER = 9.0        # beyond this, big rocks are allowed


# ---------------------------------------------------------------------------
# Path + terrain
# ---------------------------------------------------------------------------

def path_x(y):
    """Winding train route centre line (runs from the camera (y<0) into the distance (+y))."""
    return 6.0 * math.sin(y / 26.0) + 2.5 * math.sin(y / 11.0 + 1.3) - 0.03 * y


def path_dir(y):
    d = Vector((path_x(y + 0.5) - path_x(y - 0.5), 1.0, 0.0))
    return d.normalized()


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def height(x, y):
    dx = abs(x - path_x(y))
    dunes = noise.fractal(Vector((x * 0.045, y * 0.045, 0.3)), 0.6, 2.0, 4) * 0.9
    ripples = noise.noise(Vector((x * 0.25, y * 0.12, 1.7))) * 0.06
    floor = dunes + ripples
    # canyon floor rises toward the walls on both sides
    rise = smooth(10.0, 34.0, dx) * 3.2
    # flat, slightly sunken corridor for the train, blended into the dunes
    corridor = smooth(CORRIDOR + 0.5, CORRIDOR + 6.0, dx)
    return (floor + rise) * corridor - 0.05 * (1 - corridor)


def build_terrain(coll, mat, step=1.5):
    x0, x1, y0, y1 = -80.0, 80.0, -45.0, 230.0
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    bm = bmesh.new()
    grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x, y = x0 + i * step, y0 + j * step
            row.append(bm.verts.new((x, y, height(x, y))))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]])
    me = bpy.data.meshes.new("TERRAIN_DesertCanyon")
    bm.to_mesh(me)
    bm.free()
    # palette UVs: sand row (1); corridor lighter & calmer, slopes darker, rocky rim -> sandstone row (0)
    uv = me.uv_layers.new(name="UVMap").data
    rng = random.Random(7)
    size = 256.0
    for p in me.polygons:
        c = p.center
        dx = abs(c.x - path_x(c.y))
        slope = 1.0 - p.normal.z
        n = noise.noise(Vector((c.x * 0.08, c.y * 0.08, 4.0)))
        if dx < CORRIDOR + 0.6:
            row, col = 1, 5
        elif slope > 0.22 and dx > 14:
            row, col = 0, 4 + (1 if n > 0 else 0)
        else:
            row = 1
            col = 3.4 + n * 1.2 - slope * 5 + rng.uniform(-0.15, 0.15)
            col = int(max(1, min(6, round(col))))
        u = (col * 16 + 8) / size
        v = (row * 16 + 8) / size
        for li in p.loop_indices:
            uv[li].uv = (u, v)
    me.materials.append(mat)
    ob = bpy.data.objects.new("TERRAIN_DesertCanyon", me)
    coll.objects.link(ob)
    return ob


# ---------------------------------------------------------------------------
# Kit + placement
# ---------------------------------------------------------------------------

def load_kit():
    with bpy.data.libraries.load(KIT, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n.isupper() and not n.startswith("KIT_")]
    src_coll = bpy.data.collections.new("KIT_SOURCE")
    bpy.context.scene.collection.children.link(src_coll)
    kit = {}
    for ob in dst.objects:
        if ob is None or ob.type != 'MESH':
            continue
        src_coll.objects.link(ob)
        ob.location = (0, 0, 0)
        kit[ob.name] = ob
    lc = bpy.context.view_layer.layer_collection.children["KIT_SOURCE"]
    lc.exclude = True
    return kit


class Placer:
    def __init__(self, kit, colls):
        self.kit = kit
        self.colls = colls
        self.placed = []        # (x, y, radius) footprint for overlap checks
        self.count = 0

    def free(self, x, y, r, min_dx=CORRIDOR + 0.8):
        if abs(x - path_x(y)) - r < min_dx:
            return False
        for (px, py, pr) in self.placed:
            if (px - x) ** 2 + (py - y) ** 2 < (pr + r) ** 2 * 0.8:
                return False
        return True

    def put(self, name, x, y, coll, rot=0.0, scale=1.0, sink=0.08, check=True, min_dx=CORRIDOR + 0.8, tilt=0.0):
        src = self.kit[name]
        r = max(src.dimensions.x, src.dimensions.y) * 0.5 * scale
        if check and not self.free(x, y, r * 0.85, min_dx):
            return None
        ob = bpy.data.objects.new(f"{name}.{self.count:03d}", src.data)   # linked duplicate (instancing)
        self.count += 1
        self.colls[coll].objects.link(ob)
        # sit on the lowest terrain point under the footprint, sunk a little so nothing floats
        hs = [height(x + dx * r * 0.7, y + dy * r * 0.7) for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1))]
        ob.location = (x, y, min(hs) - sink * scale)
        ob.rotation_euler = (tilt, 0.0, rot)
        ob.scale = (scale, scale, scale)
        self.placed.append((x, y, r * 0.85))
        return ob


def build_level():
    scene = bpy.context.scene
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights, bpy.data.worlds):
        for it in list(blk):
            if it.users == 0:
                blk.remove(it)

    kit = load_kit()
    mat = bpy.data.materials["M_DesertKit"]
    root = bpy.data.collections.new("DESERT_LEVEL")
    scene.collection.children.link(root)
    colls = {}
    for n in ("TERRAIN", "CANYON_WALLS", "LANDMARKS", "BACKGROUND", "ROCKS", "VEGETATION", "PROPS", "FOREGROUND", "TRAIN", "LIGHTING"):
        colls[n] = bpy.data.collections.new(n)
        root.children.link(colls[n])
    build_terrain(colls["TERRAIN"], mat)
    P = Placer(kit, colls)
    rng = random.Random(42)
    deg = math.radians

    # --- canyon wall, LEFT side (faces the track: cliff front -Y rotated to +X) -----------------
    y = -30.0
    while y < 150:
        s = rng.uniform(1.6, 2.3)
        px = path_x(y) - rng.uniform(19.0, 23.0) - (y > 60) * rng.uniform(0, 6)
        name = rng.choice(["CLIFF_STRAIGHT_01", "CLIFF_STRAIGHT_02", "CLIFF_STRAIGHT_01", "CLIFF_TOWER_01"])
        P.put(name, px, y, "CANYON_WALLS", rot=deg(90) + rng.uniform(-0.25, 0.25), scale=s, check=False)
        if rng.random() < 0.45:   # second, taller layer behind for a stepped canyon silhouette
            P.put(rng.choice(["CLIFF_TOWER_02", "ROCK_FORMATION_01", "CLIFF_CORNER_02"]), px - rng.uniform(5, 9), y + rng.uniform(-2, 2),
                  "CANYON_WALLS", rot=rng.uniform(0, 6.28), scale=s * rng.uniform(1.1, 1.5), check=False)
        y += 4.0 * s * rng.uniform(0.85, 1.0)

    # --- RIGHT side: broken mesa + pillars (more open than the left) ------------------------------
    y = -20.0
    while y < 150:
        s = rng.uniform(1.4, 2.2)
        px = path_x(y) + rng.uniform(24.0, 30.0)
        name = rng.choice(["CLIFF_STRAIGHT_02", "ROCK_WALL_01", "CLIFF_END_01", "CLIFF_STRAIGHT_01"])
        P.put(name, px, y, "CANYON_WALLS", rot=deg(-90) + rng.uniform(-0.3, 0.3), scale=s, check=False)
        y += rng.uniform(9.0, 15.0)

    # far-right mesa line: closes the open horizon on the right of the gameplay view
    y = 20.0
    while y < 170:
        P.put(rng.choice(["CLIFF_TOWER_01", "ROCK_FORMATION_01", "CLIFF_STRAIGHT_02", "CLIFF_TOWER_02"]),
              path_x(y) + rng.uniform(38.0, 50.0), y, "BACKGROUND", rot=deg(-90) + rng.uniform(-0.4, 0.4),
              scale=rng.uniform(2.6, 3.8), check=False)
        y += rng.uniform(10.0, 16.0)

    # --- landmarks ------------------------------------------------------------------------------
    P.put("NATURAL_ARCH_01", path_x(112) - 5, 112, "LANDMARKS", rot=deg(8), scale=3.4, check=False)
    for (dy, ox, nm, s) in ((40, 15, "ROCK_PILLAR_02", 2.1), (46, 19, "ROCK_PILLAR_01", 2.4), (52, 14.5, "ROCK_PILLAR_02", 1.6),
                            (78, -15, "ROCK_PILLAR_01", 2.0), (96, 17, "CLIFF_TOWER_02", 2.2), (24, -14, "ROCK_FORMATION_01", 1.5)):
        P.put(nm, path_x(dy) + ox, dy, "LANDMARKS", rot=rng.uniform(0, 6.28), scale=s, check=False)

    # --- background silhouettes (huge, far, haze makes them read as distant) -----------------------
    for k in range(16):
        yb = rng.uniform(150, 215)
        xb = rng.uniform(-70, 70)
        if abs(xb - path_x(yb)) < 12:
            xb += 16 * (1 if xb >= path_x(yb) else -1)
        nm = rng.choice(["CLIFF_TOWER_01", "CLIFF_TOWER_02", "ROCK_PILLAR_01", "ROCK_PILLAR_02", "ROCK_FORMATION_01", "CLIFF_STRAIGHT_02"])
        P.put(nm, xb, yb, "BACKGROUND", rot=rng.uniform(0, 6.28), scale=rng.uniform(3.0, 5.0), check=False)

    # --- boulders / blocks framing the route (mid-ground, varied scale) ---------------------------
    big = ["BOULDER_01", "BOULDER_02", "BOULDER_03", "BOULDER_04", "BOULDER_05", "ROCK_BLOCK_02", "ROCK_BLOCK_05", "ROCK_BLOCK_09",
           "ROCK_BLOCK_10", "ROCK_BLOCK_08", "ROCK_BLOCK_06"]
    for k in range(70):
        yy = rng.uniform(-10, 135)
        side = rng.choice((-1, 1))
        xx = path_x(yy) + side * rng.uniform(SHOULDER, 18)
        P.put(rng.choice(big), xx, yy, "ROCKS", rot=rng.uniform(0, 6.28), scale=rng.uniform(1.0, 2.2), min_dx=SHOULDER - 1)

    for (off, yy, nm, sc) in ((-10, 20, "ROCK_BLOCK_04", 1.6), (10.5, 26, "BOULDER_03", 1.5), (-11, 44, "ROCK_BLOCK_03", 1.8),
                              (12, 64, "BOULDER_05", 1.7), (-10.5, 70, "ROCK_BLOCK_07", 1.5), (11, 82, "ROCK_BLOCK_01", 1.7)):
        P.put(nm, path_x(yy) + off, yy, "ROCKS", rot=rng.uniform(0, 6.28), scale=sc, check=False)

    # --- small rocks near the route (gameplay readability: low and small) --------------------------
    small = [f"ROCK_S_{i:02d}" for i in range(1, 13)] + ["ROCK_DEBRIS_01", "ROCK_DEBRIS_02", "ROCK_STACK_01"]
    for k in range(120):
        yy = rng.uniform(-15, 140)
        side = rng.choice((-1, 1))
        xx = path_x(yy) + side * rng.uniform(CORRIDOR + 1.2, 16)
        P.put(rng.choice(small), xx, yy, "ROCKS", rot=rng.uniform(0, 6.28), scale=rng.uniform(0.9, 1.6))

    # --- vegetation clusters (dense pockets, open sand between) ------------------------------------
    clusters = [
        ("cactus", 8, -10), ("cactus", -9, 6), ("grass", 10, 14), ("agave", -11, 22), ("cactus", 12, 30),
        ("sparse", -10, 38), ("grass", -8, 50), ("cactus", 11, 58), ("agave", 9, 70), ("sparse", -12, 76),
        ("cactus", -9, 88), ("grass", 12, 95), ("cactus", 10, 108), ("sparse", -10, 112), ("grass", -9, 126),
        ("cactus", 14, 124), ("agave", 13, 3), ("grass", -13, -4),
        ("grass", 7.5, 22), ("cactus", -7.5, 28), ("grass", 8, 42), ("agave", -8, 62), ("cactus", 7.5, 84),
        ("grass", -7.5, 100), ("sparse", 8, 18),
    ]
    for kind, off, cy in clusters:
        cx = path_x(cy) + off
        crng = random.Random(int(cy * 13 + off))
        if kind == "cactus":
            members = ["CACTUS_TALL_01", "CACTUS_MED_01", "CACTUS_SMALL_01", "DRY_BUSH_01", "ROCK_S_09", "GRASS_BUSH_02"]
            if crng.random() < 0.5:
                members[0] = crng.choice(["CACTUS_TALL_02", "CACTUS_ARM_01"])
        elif kind == "grass":
            members = ["GRASS_BUSH_01", "GRASS_BUSH_02", "GRASS_BUSH_03", "GRASS_BUSH_01", "FLOWER_PLANT_01", "DESERT_SHRUB_01", "ROCK_S_10"]
        elif kind == "agave":
            members = ["AGAVE_PLANT_01", "YUCCA_PLANT_01", "GRASS_BUSH_02", "DESERT_SHRUB_02", "ROCK_S_03", "GRASS_BUSH_03"]
        else:
            members = ["CACTUS_ARM_01", "DRY_BUSH_01", "BONE_PILE_01", "ROCK_S_07"]
        for i, m in enumerate(members):
            a = crng.uniform(0, 6.28)
            rr = 0.0 if i == 0 else crng.uniform(0.9, 3.0)
            P.put(m, cx + math.cos(a) * rr, cy + math.sin(a) * rr, "VEGETATION", rot=crng.uniform(0, 6.28),
                  scale=crng.uniform(1.0, 1.5) if m.startswith("CACTUS") else crng.uniform(1.6, 2.4), sink=0.02)
    # single sparse plants across the open sand
    for k in range(45):
        yy = rng.uniform(-15, 140)
        xx = path_x(yy) + rng.choice((-1, 1)) * rng.uniform(CORRIDOR + 1.5, 20)
        P.put(rng.choice(["GRASS_BUSH_03", "GRASS_BUSH_02", "DRY_BUSH_01", "CACTUS_SMALL_01", "DESERT_SHRUB_01"]), xx, yy,
              "VEGETATION", rot=rng.uniform(0, 6.28), scale=rng.uniform(1.5, 2.2), sink=0.02)

    # --- storytelling props ---------------------------------------------------------------------
    # giant ribcage of some huge beast beside the route
    for k in range(5):
        yy = 34 + k * 2.2
        P.put("BONE_RIB_02" if k % 2 else "BONE_RIB_01", path_x(yy) + 10.5, yy, "PROPS", rot=deg(0) + k * 0.05, scale=2.6, sink=0.3, check=False)
    P.put("BONE_PILE_01", path_x(46) + 8.3, 46, "PROPS", rot=deg(30), scale=1.6, check=False)
    # old broken fence line along the right shoulder
    for k in range(9):
        yy = 60 + k * 2.6
        nm = "WOOD_FENCE_02" if k in (3, 6) else "WOOD_FENCE_01"
        d = path_dir(yy)
        P.put(nm, path_x(yy) + 7.2, yy, "PROPS", rot=math.atan2(d.y, d.x) + rng.uniform(-0.08, 0.08), scale=1.1, sink=0.15, check=False)
    # abandoned supply stop
    sx, sy = path_x(16) - 8.0, 16.0
    P.put("SIGN_POST_01", sx + 0.8, sy - 1.5, "PROPS", rot=deg(200), scale=1.3, check=False)
    P.put("CRATE_01", sx - 0.6, sy + 0.4, "PROPS", rot=deg(15), scale=1.4, check=False)
    P.put("CRATE_01", sx - 1.4, sy + 1.3, "PROPS", rot=deg(-20), scale=1.2, check=False)
    P.put("BARREL_01", sx + 0.4, sy + 1.6, "PROPS", rot=0.0, scale=1.3, check=False)
    P.put("BARREL_01", sx - 0.3, sy - 0.9, "PROPS", rot=deg(90), scale=1.3, check=False, tilt=deg(90), sink=-0.33)
    P.put("DEAD_TREE_01", path_x(70) - 10, 70, "PROPS", rot=deg(40), scale=2.2, check=False)
    P.put("DEAD_TREE_01", path_x(102) + 12, 102, "PROPS", rot=deg(160), scale=1.8, check=False)
    P.put("BONE_RIB_03", path_x(88) - 9, 88, "PROPS", rot=deg(70), scale=1.8, sink=0.25, check=False)
    P.put("SIGN_POST_01", path_x(54) + 6.6, 54, "PROPS", rot=deg(250), scale=1.2, check=False)

    # --- foreground framing (big, close to camera) ------------------------------------------------
    P.put("ROCK_BLOCK_05", path_x(-14) - 9.5, -14, "FOREGROUND", rot=deg(25), scale=3.0, check=False)
    P.put("BOULDER_02", path_x(-12) - 13, -9, "FOREGROUND", rot=deg(80), scale=2.6, check=False)
    P.put("ROCK_BLOCK_09", path_x(-16) + 11, -16, "FOREGROUND", rot=deg(-30), scale=2.4, check=False)
    P.put("BOULDER_04", path_x(-6) + 12.5, -6, "FOREGROUND", rot=deg(140), scale=2.2, check=False)
    P.put("CACTUS_TALL_02", path_x(-8) - 7.5, -8, "FOREGROUND", rot=deg(30), scale=1.5, check=False, sink=0.02)
    P.put("GRASS_BUSH_01", path_x(-11) - 6.8, -11, "FOREGROUND", rot=0.0, scale=1.8, check=False, sink=0.02)
    P.put("ROCK_DEBRIS_02", path_x(-9) + 8.0, -9, "FOREGROUND", rot=deg(10), scale=1.8, check=False)

    P.put("ROCK_BLOCK_02", path_x(-2) - 10.5, -2, "FOREGROUND", rot=deg(12), scale=2.6, check=False)
    P.put("BOULDER_01", path_x(4) - 12.5, 4, "FOREGROUND", rot=deg(60), scale=2.0, check=False)
    P.put("ROCK_BLOCK_10", path_x(-3) + 11.0, -3, "FOREGROUND", rot=deg(-20), scale=2.0, check=False)
    for k, (dx, dy) in enumerate(((-7.0, -1), (-8.2, 1.5), (-6.4, 3.5), (7.2, 2), (8.0, 5), (-7.4, 8))):
        P.put(("GRASS_BUSH_01", "GRASS_BUSH_03", "GRASS_BUSH_02")[k % 3], path_x(dy) + dx, dy, "FOREGROUND",
              rot=k * 1.7, scale=2.2, check=False, sink=0.02)
    for k in range(10):
        yy = 4 + k * 2.6
        d = path_dir(yy)
        P.put("WOOD_FENCE_02" if k in (2, 7) else "WOOD_FENCE_01", path_x(yy) - 6.6, yy, "PROPS",
              rot=math.atan2(d.y, d.x) + rng.uniform(-0.08, 0.08), scale=1.1, sink=0.15, check=False)

    # --- the train on its route ---------------------------------------------------------------------
    place_train(colls["TRAIN"])
    setup_lighting(scene, colls["LIGHTING"])
    setup_camera(scene, colls["LIGHTING"])
    return {"instances": P.count, "placed": len(P.placed)}


def place_train(coll):
    """Append the Drillex train (head + cars) and lay it along the route, heading toward the camera."""
    y_head = 9.0
    spacing = 2.05
    for i, (path, root_name) in enumerate(TRAIN):
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.objects = list(src.objects)
        root = None
        for ob in dst.objects:
            if ob is None or ob.type not in ('MESH', 'EMPTY'):
                continue
            coll.objects.link(ob)
            if ob.name.startswith(root_name) and ob.parent is None and ob.type == 'EMPTY':
                root = ob
        for ob in list(bpy.data.objects):
            if ob.name.startswith(("PV_", "DBG_", "KIT_")):
                bpy.data.objects.remove(ob, do_unlink=True)
        if root is None:
            continue
        y = y_head + i * (spacing + (0.95 if i == 0 else 0.0))
        d = path_dir(y)
        root.location = (path_x(y), y, height(path_x(y), y) + 0.0)
        # vehicles are modelled facing -Y; face them back along the route (toward the camera)
        root.rotation_euler = (0, 0, math.atan2(d.y, d.x) - math.pi / 2)


SUN_DIR = Vector((0.45, -0.80, -0.34)).normalized()   # light travel: low sun from the back-left, lights the train's camera-facing sides
GLOW_DIR = Vector((-0.05, 1.0, 0.07)).normalized()     # where the sunset glow sits on the horizon (behind the arch)


def setup_lighting(scene, coll):
    # low sunset sun straight down the canyon (behind the arch): rim light on the train, long shadows toward camera
    sun = bpy.data.objects.new("SUN_Sunset", bpy.data.lights.new("SUN_Sunset", 'SUN'))
    coll.objects.link(sun)
    sun.data.energy = 6.0
    sun.data.color = (1.0, 0.60, 0.30)
    sun.data.angle = math.radians(2.5)
    sun.rotation_euler = SUN_DIR.to_track_quat('-Z', 'Y').to_euler()
    # cool sky fill from the camera side: shadows read blue-violet instead of black, no shadow cost
    fill = bpy.data.objects.new("SUN_Fill", bpy.data.lights.new("SUN_Fill", 'SUN'))
    coll.objects.link(fill)
    fill.data.energy = 0.55
    fill.data.color = (0.55, 0.62, 0.95)
    fill.data.angle = math.radians(20)
    fill.data.use_shadow = False
    fill.rotation_euler = Vector((-0.2, 0.8, -0.55)).normalized().to_track_quat('-Z', 'Y').to_euler()

    # sky: deep dusk gradient + bright sun glow on the horizon behind the arch
    w = bpy.data.worlds.new("WORLD_DesertSunset")
    scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputWorld")
    bg = N("ShaderNodeBackground")
    tc = N("ShaderNodeTexCoord")
    sep = N("ShaderNodeSeparateXYZ")
    ramp = N("ShaderNodeValToRGB")
    L(tc.outputs["Generated"], sep.inputs[0])
    L(sep.outputs["Z"], ramp.inputs[0])
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.47, (0.55, 0.22, 0.10, 1)
    els[1].position, els[1].color = 0.80, (0.22, 0.20, 0.32, 1)
    els.new(0.505).color = (1.0, 0.55, 0.22, 1)
    els.new(0.60).color = (0.80, 0.42, 0.28, 1)
    dot = N("ShaderNodeVectorMath")
    dot.operation = 'DOT_PRODUCT'
    L(tc.outputs["Generated"], dot.inputs[0])
    dot.inputs[1].default_value = tuple(GLOW_DIR)
    mr = N("ShaderNodeMapRange")
    mr.inputs[1].default_value, mr.inputs[2].default_value = 0.55, 1.0
    L(dot.outputs["Value"], mr.inputs[0])
    pw = N("ShaderNodeMath")
    pw.operation = 'POWER'
    pw.inputs[1].default_value = 4.0
    L(mr.outputs[0], pw.inputs[0])
    glow = N("ShaderNodeMixRGB")
    glow.blend_type = 'ADD'
    L(pw.outputs[0], glow.inputs[0])
    L(ramp.outputs[0], glow.inputs[1])
    glow.inputs[2].default_value = (2.2, 1.0, 0.35, 1)
    L(glow.outputs[0], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.9
    L(bg.outputs[0], out.inputs["Surface"])

    # haze: none around the train, thickening into the distance (object-space Y gradient)
    haze = bpy.data.materials.new("M_Haze")
    haze.use_nodes = True
    hn = haze.node_tree
    hn.nodes.clear()
    N, L = hn.nodes.new, hn.links.new
    ho = N("ShaderNodeOutputMaterial")
    hv = N("ShaderNodeVolumePrincipled")
    hv.inputs["Color"].default_value = (1.0, 0.58, 0.32, 1)
    hv.inputs["Anisotropy"].default_value = 0.6
    t2 = N("ShaderNodeTexCoord")
    s2 = N("ShaderNodeSeparateXYZ")
    r2 = N("ShaderNodeMapRange")
    L(t2.outputs["Object"], s2.inputs[0])
    L(s2.outputs["Y"], r2.inputs[0])
    # object y -0.5..0.5 spans world y -55..245: density ramps from world y ~35 to ~200
    r2.inputs[1].default_value, r2.inputs[2].default_value = -0.17, 0.35
    r2.inputs[3].default_value, r2.inputs[4].default_value = 0.0, 0.012
    L(r2.outputs[0], hv.inputs["Density"])
    L(hv.outputs[0], ho.inputs["Volume"])
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    me = bpy.data.meshes.new("HAZE_Volume")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(haze)
    hz = bpy.data.objects.new("HAZE_Volume", me)
    coll.objects.link(hz)
    hz.location = (0, 95, 35)
    hz.scale = (180, 300, 80)

    # level grade on the (level-local copy of the) kit material: deeper, richer canyon reds
    km = bpy.data.materials["M_DesertKit"].node_tree
    tex = next(n for n in km.nodes if n.type == 'TEX_IMAGE')
    bsdf = next(n for n in km.nodes if n.type == 'BSDF_PRINCIPLED')
    hsv = km.nodes.get("LVL_Grade") or km.nodes.new("ShaderNodeHueSaturation")
    hsv.name = "LVL_Grade"
    km.links.new(tex.outputs["Color"], hsv.inputs["Color"])
    km.links.new(hsv.outputs["Color"], bsdf.inputs["Base Color"])
    hsv.inputs["Hue"].default_value = 0.49
    hsv.inputs["Saturation"].default_value = 1.12
    hsv.inputs["Value"].default_value = 0.80


def setup_camera(scene, coll):
    cam = bpy.data.objects.new("CAM_Gameplay", bpy.data.cameras.new("CAM_Gameplay"))
    coll.objects.link(cam)
    cam.data.lens = 27
    cam.data.clip_end = 600
    # 3/4 gameplay view: a little left of the route, looking down the canyon; train large on the lower right
    pos = Vector((path_x(0) - 4.0, -6.0, 8.5))
    tgt = Vector((path_x(30) + 2.5, 30.0, 2.0))
    cam.location = pos
    cam.rotation_euler = (tgt - pos).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.engine = 'BLENDER_EEVEE'
    ee = scene.eevee
    for attr, val in (("volumetric_end", 300.0), ("volumetric_tile_size", '8'), ("use_volumetric_shadows", True),
                      ("use_shadows", True), ("use_raytracing", True), ("taa_render_samples", 64),
                      ("volumetric_samples", 64), ("use_gtao", True), ("gtao_distance", 1.5)):
        if hasattr(ee, attr):
            try:
                setattr(ee, attr, val)
            except Exception:
                pass
    scene.view_settings.view_transform = 'AgX'
    try:
        scene.view_settings.look = 'AgX - Punchy'
    except Exception:
        scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.view_settings.exposure = 0.35


def render(path, cam_name="CAM_Gameplay", res=(1920, 1080)):
    scene = bpy.context.scene
    scene.camera = bpy.data.objects[cam_name]
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path
