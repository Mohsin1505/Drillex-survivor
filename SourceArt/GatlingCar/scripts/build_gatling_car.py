"""
Drillex Gatling Gun Car - Blender 5.2 build. Same body block, wheels and couplers as the other
cars; a low turret base carries a six-barrel rotary gun, an ammo can and a flexible feed belt.

Conventions: meters, Z up, front faces -Y, root pivot on the ground under the car center.
Game scene:
  DRL_GunCar (root empty)
  |- Body            one merged static mesh (shell, frames, lamps, vents, undercarriage)
  |- Turret          yaw pivot on the turret ring
  |   '- Cannon      barrel cluster, pivot on the rotary axis at the receiver (can spin)
  |       '- VFX_Muzzle   empty at the centre of the barrel cluster tip (+forward)
  |- Axle_1..4       wheelsets sharing one mesh, pivot on the axle
  '- Coupler_Front / Coupler_Rear
"""
import bpy
import importlib.util
import math
import os
from mathutils import Matrix, Vector

HERE = r"D:\Drillex\SourceArt\GatlingCar"


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, "scripts", name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


geo = _load("geo_lib")
geo.PALETTE["YellowPaint"] = ((0.85, 0.55, 0.03, 1), 0.0, 0.45, None)
box, cyl, lathe, lamp = geo.box, geo.cyl, geo.lathe, geo.lamp

AXLE_YS = (-0.66, -0.30, 0.30, 0.66)
WHEEL_X = 0.50
WHEEL_R = 0.17
TURRET_PIVOT = (0.0, 0.08, 0.95)
CANNON_PIVOT = (0.0, -0.26, 1.29)   # rotary axis, at the receiver front
MUZZLE_Y = -1.16
BARRELS = 6
BARREL_RING = 0.076                  # barrel centres sit on this radius


def xs(sx, a, b):
    return tuple(sorted((sx * a, sx * b)))


def ys(sy, a, b):
    return tuple(sorted((sy * a, sy * b)))


def build_body(c):
    g = "Body"
    R, K, S, B, Y, H = "RedPaint", "BlackMetal", "Steel", "Brass", "YellowPaint", "Hazard"

    # ---- shell ----
    box(g, c, R, -0.46, 0.46, -0.90, 0.90, 0.45, 0.92, 0.012, name="body_block")  # walls + ends + top
    box(g, c, K, -0.48, 0.48, -0.86, 0.86, 0.36, 0.46, 0.012, name="sill_frame")
    for sx in (-1, 1):
        box(g, c, H, *xs(sx, 0.47, 0.495), -0.74, 0.74, 0.375, 0.455, 0.006, name="sill_hazard")
    for sy in (-1, 1):
        box(g, c, H, -0.40, 0.40, *ys(sy, 0.86, 0.885), 0.375, 0.455, 0.006, name="sill_hazard_end")
    # hazard top rim + black roof deck
    # hazard trim band around the top of the block (thin, proud of the faces, flush with the top)
    for sx in (-1, 1):
        box(g, c, H, *xs(sx, 0.455, 0.472), -0.74, 0.74, 0.855, 0.92, 0.004, name="top_trim_side")
    for sy in (-1, 1):
        box(g, c, H, -0.40, 0.40, *ys(sy, 0.895, 0.912), 0.855, 0.92, 0.004, name="top_trim_end")
    box(g, c, R, -0.42, 0.42, -0.72, 0.72, 0.92, 0.95, 0.008, name="roof_deck")  # sits on the block top
    for y in (-0.55, 0.55):
        box(g, c, "Grille", -0.12, 0.12, y - 0.08, y + 0.08, 0.94, 0.965, 0.004, name="roof_vent")

    # ---- corner pillars ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(g, c, K, *xs(sx, 0.40, 0.53), *ys(sy, 0.74, 0.92), 0.36, 0.95, 0.02, name="pillar")  # wraps the block corner
            box(g, c, H, *xs(sx, 0.53, 0.545), *ys(sy, 0.77, 0.87), 0.47, 0.86, 0.004, name="pillar_hazard")
            box(g, c, S, *xs(sx, 0.38, 0.55), *ys(sy, 0.73, 0.91), 0.93, 0.98, 0.01, name="pillar_cap")

    # ---- sides ----
    for sx in (-1, 1):
        # main vent with lamp strip
        box(g, c, K, *xs(sx, 0.455, 0.485), -0.30, 0.18, 0.49, 0.80, 0.01, name="vent_frame")
        box(g, c, "Grille", *xs(sx, 0.475, 0.49), -0.26, 0.14, 0.52, 0.70, 0.0, name="vent")
        box(g, c, "WindowGlow", *xs(sx, 0.478, 0.492), -0.22, 0.10, 0.72, 0.77, 0.0, name="lamp_strip")
        for y in (-0.14, -0.06, 0.02):
            box(g, c, K, *xs(sx, 0.48, 0.496), y - 0.007, y + 0.007, 0.715, 0.775, 0.0, name="strip_divider")
        # small rear vent
        box(g, c, K, *xs(sx, 0.455, 0.485), 0.30, 0.62, 0.52, 0.78, 0.008, name="vent2_frame")
        box(g, c, "Grille", *xs(sx, 0.475, 0.49), 0.33, 0.59, 0.55, 0.75, 0.0, name="vent2")
        # yellow pipes beside the main vent
        for y in (-0.42, -0.36, 0.23):
            cyl(g, c, Y, (sx * 0.49, y, 0.65), 0.016, 0.34, 'Z', 8, name="side_pipe")
        for y, z in ((-0.39, 0.50), (-0.39, 0.80), (0.23, 0.50), (0.23, 0.80)):
            box(g, c, B, *xs(sx, 0.46, 0.505), y - 0.05, y + 0.05, z - 0.012, z + 0.012, 0.004, name="pipe_clamp")
        # gauge box near the front pillar
        box(g, c, S, *xs(sx, 0.46, 0.51), -0.66, -0.56, 0.47, 0.57, 0.008, name="gauge_box")
        cyl(g, c, K, (sx * 0.515, -0.61, 0.52), 0.035, 0.012, 'X' if sx > 0 else '-X', 12, name="gauge")

    # ---- ends: lamps, grille, pipes ----
    for sy in (-1, 1):
        ax = 'Y' if sy > 0 else '-Y'
        box(g, c, K, -0.24, 0.24, *ys(sy, 0.89, 0.915), 0.47, 0.76, 0.008, name="end_grille_frame")
        box(g, c, "Grille", -0.20, 0.20, *ys(sy, 0.905, 0.925), 0.50, 0.66, 0.0, name="end_grille")
        box(g, c, "WindowGlow", -0.16, 0.16, *ys(sy, 0.905, 0.922), 0.68, 0.73, 0.0, name="end_strip")
        for sx in (-1, 1):
            box(g, c, R, *xs(sx, 0.28, 0.42), *ys(sy, 0.86, 0.95), 0.72, 0.88, 0.012, name="lamp_block")
            lamp(g, c, (sx * 0.35, sy * 0.95, 0.80), ax, 0.05)
            for x in (0.30, 0.36):
                cyl(g, c, Y, (sx * x, sy * 0.925, 0.55), 0.015, 0.26, 'Z', 8, name="end_pipe")
        # corner marker lamps on the pillars
        for sx in (-1, 1):
            lamp(g, c, (sx * 0.47, sy * 0.905, 0.91), ax, 0.03)

    # ---- undercarriage (same kit as the storage car) ----
    box(g, c, K, -0.40, 0.40, -0.86, 0.86, 0.27, 0.37, 0.012, name="underframe")
    for yc in (-0.48, 0.48):
        for sx in (-1, 1):
            box(g, c, K, *xs(sx, 0.43, 0.46), yc - 0.30, yc + 0.30, 0.11, 0.27, 0.01, name="bogie_side")
            for y in (yc - 0.18, yc + 0.18):
                box(g, c, B, *xs(sx, 0.445, 0.475), y - 0.05, y + 0.05, 0.12, 0.22, 0.006, name="axle_box")
                box(g, c, S, *xs(sx, 0.445, 0.47), y - 0.012, y + 0.012, 0.22, 0.30, 0.0, name="spring")
        box(g, c, K, -0.43, 0.43, yc - 0.05, yc + 0.05, 0.20, 0.27, 0.008, name="bogie_bolster")
    cyl(g, c, S, (0, 0.0, 0.21), 0.14, 0.34, 'Y', 16, bevel=0.012, name="tank")
    for y in (-0.10, 0.10):
        cyl(g, c, K, (0, y, 0.21), 0.145, 0.025, 'Y', 16, name="tank_band")
    for sy in (-1, 1):
        ax = 'Y' if sy > 0 else '-Y'
        box(g, c, K, -0.09, 0.09, *ys(sy, 0.86, 1.00), 0.20, 0.34, 0.012, name="coupler")
        cyl(g, c, S, (0, sy * 1.00, 0.27), 0.05, 0.04, ax, 12, name="coupler_face")
        for sx in (-1, 1):
            cyl(g, c, K, (sx * 0.30, sy * 0.93, 0.28), 0.035, 0.08, ax, 10, name="buffer_rod")
            cyl(g, c, S, (sx * 0.30, sy * 0.98, 0.28), 0.06, 0.03, ax, 14, bevel=0.005, name="buffer_head")


def build_turret(c):
    g = "Turret"
    px, py, pz = TURRET_PIVOT
    R, K, S, B, H, Y = "RedPaint", "BlackMetal", "Steel", "Brass", "Hazard", "YellowPaint"
    ax, ay, az = CANNON_PIVOT
    # ring + low armoured base
    cyl(g, c, K, (px, py, pz + 0.035), 0.31, 0.07, 'Z', 24, bevel=0.008, name="ring")
    cyl(g, c, B, (px, py, pz + 0.08), 0.27, 0.025, 'Z', 24, name="ring_brass")
    for i in range(10):
        a = math.radians(i * 36 + 18)
        cyl(g, c, S, (px + math.cos(a) * 0.29, py + math.sin(a) * 0.29, pz + 0.072), 0.015, 0.012, 'Z', 6, name="ring_bolt")
    box(g, c, R, -0.30, 0.30, py - 0.24, py + 0.28, pz + 0.09, pz + 0.24, 0.03, name="base")
    box(g, c, K, -0.315, 0.315, py - 0.25, py + 0.29, pz + 0.085, pz + 0.12, 0.008, name="base_skirt")
    box(g, c, H, -0.305, 0.305, py - 0.245, py + 0.285, pz + 0.20, pz + 0.225, 0.004, name="base_band")
    # receiver / motor housing the barrels come out of
    box(g, c, R, -0.15, 0.15, ay - 0.02, py + 0.20, pz + 0.22, pz + 0.46, 0.025, name="receiver")
    box(g, c, K, -0.16, 0.16, ay - 0.03, ay + 0.03, az - 0.15, az + 0.15, 0.01, name="receiver_face")
    cyl(g, c, K, (px, py + 0.23, az), 0.12, 0.10, 'Y', 18, bevel=0.01, name="motor_drum")
    cyl(g, c, S, (px, py + 0.285, az), 0.07, 0.02, 'Y', 14, name="motor_cap")
    for sx in (-1, 1):
        box(g, c, "Grille", *xs(sx, 0.148, 0.158), ay + 0.06, py + 0.14, pz + 0.27, pz + 0.41, 0.0, name="receiver_vent")
        box(g, c, B, *xs(sx, 0.15, 0.17), ay + 0.02, py + 0.18, pz + 0.24, pz + 0.26, 0.004, name="receiver_rail")
        lamp(g, c, (sx * 0.24, py - 0.245, pz + 0.17), '-Y', 0.03)
    box(g, c, H, -0.10, 0.10, ay + 0.05, py + 0.15, pz + 0.46, pz + 0.475, 0.003, name="receiver_top_hazard")
    # ammo can on the right side of the turret (vehicle right = -X, front faces -Y)
    box(g, c, Y, -0.46, -0.30, py - 0.17, py + 0.17, pz + 0.10, pz + 0.33, 0.012, name="ammo_can")
    box(g, c, K, -0.47, -0.29, py - 0.18, py + 0.18, pz + 0.33, pz + 0.35, 0.006, name="ammo_lid")
    box(g, c, H, -0.465, -0.455, py - 0.15, py + 0.15, pz + 0.14, pz + 0.29, 0.0, name="ammo_hazard")
    # flexible feed belt: cartridge links along an arc from the ammo can to the receiver side
    p0 = Vector((-0.38, py - 0.02, pz + 0.35))
    p1 = Vector((-0.32, py - 0.06, pz + 0.55))
    p2 = Vector((-0.15, ay + 0.10, pz + 0.40))
    n = 11
    for i in range(n):
        t = (i + 0.5) / n
        pt = p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t ** 2
        tan = (p1 - p0) * 2 * (1 - t) + (p2 - p1) * 2 * t
        rot = tan.to_track_quat('Z', 'Y').to_matrix()
        box(g, c, B, -0.035, 0.035, -0.011, 0.011, -0.012, 0.012, 0.003, rot=rot, name="belt_round")
        ob = geo._pieces[g][-1]
        ob.data.transform(Matrix.Translation(pt))
        box(g, c, K, -0.04, 0.04, -0.006, 0.006, -0.016, 0.016, 0.0, rot=rot, name="belt_link")
        ob = geo._pieces[g][-1]
        ob.data.transform(Matrix.Translation(pt + rot @ Vector((0, -0.012, 0))))
    box(g, c, K, -0.20, -0.14, ay + 0.04, ay + 0.16, pz + 0.34, pz + 0.44, 0.006, name="feed_port")


def build_cannon(c):
    """Six-barrel rotary cluster; pivot on the rotary axis so the cluster can also spin."""
    g = "Cannon"
    _, ay, az = CANNON_PIVOT
    length = ay - MUZZLE_Y
    for i in range(BARRELS):
        a = math.radians(i * 360 / BARRELS)
        x, z = math.cos(a) * BARREL_RING, az + math.sin(a) * BARREL_RING
        cyl(g, c, "BlackMetal", (x, (ay + MUZZLE_Y + 0.03) / 2, z), 0.027, length - 0.03, 'Y', 8, name="barrel")
        cyl(g, c, "Steel", (x, MUZZLE_Y + 0.015, z), 0.031, 0.03, 'Y', 8, name="barrel_tip")
    cyl(g, c, "Steel", (0, (ay + MUZZLE_Y) / 2, az), 0.03, length - 0.02, 'Y', 8, name="spindle")
    # clamps holding the barrels together
    for y, r, d, m in ((ay - 0.04, 0.13, 0.08, "BlackMetal"), (ay - 0.30, 0.12, 0.03, "Hazard"),
                       (ay - 0.55, 0.117, 0.025, "Brass"), (MUZZLE_Y + 0.08, 0.12, 0.035, "RedPaint")):
        cyl(g, c, m, (0, y, az), r, d, 'Y', 18, bevel=0.004, name="clamp")
    for i in range(6):
        a = math.radians(i * 60 + 30)
        cyl(g, c, "Steel", (math.cos(a) * 0.115, ay - 0.085, az + math.sin(a) * 0.115), 0.01, 0.012, '-Y', 6, name="clamp_bolt")


def build_axle(c, idx, y):
    g = f"Axle_{idx}"
    cyl(g, c, "Steel", (0, y, WHEEL_R), 0.028, 2 * WHEEL_X + 0.02, 'X', 8, name="axle_bar")
    w = 0.07
    for sx in (-1, 1):
        prof = [(0.125, -w / 2), (0.17, -w / 2), (0.17, w / 2 - 0.02), (0.19, w / 2 - 0.01),
                (0.19, w / 2), (0.125, w / 2), (0.125, -w / 2)]
        if sx < 0:
            prof = [(r, -a) for r, a in prof]
        lathe(g, c, "BlackMetal", prof, (sx * WHEEL_X, y, WHEEL_R), 'X', 18, name="wheel_rim")
        cyl(g, c, "Brass", (sx * WHEEL_X, y, WHEEL_R), 0.048, w + 0.03, 'X', 12, bevel=0.005, name="wheel_hub")
        for i in range(6):
            geo.box(g, c, "Steel", -0.015, 0.015, -0.016, 0.016, 0.04, 0.13, 0.0, name="wheel_spoke")
            ob = geo._pieces[g][-1]
            ob.data.transform(Matrix.Rotation(math.radians(i * 60), 4, 'X'))
            ob.data.transform(Matrix.Translation((sx * WHEEL_X, y, WHEEL_R)))


def build():
    geo._purge()
    for img in list(bpy.data.images):
        if img.users == 0:
            bpy.data.images.remove(img)
    geo._pieces.clear()
    geo.MATS.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0

    col = geo._coll("DRL_GatlingCar")
    work = geo._coll("_build_tmp")
    build_body(work)
    build_turret(work)
    build_cannon(work)
    for i, y in enumerate(AXLE_YS, 1):
        build_axle(work, i, y)
    culled = geo.cull_hidden(("Body",)) + geo.cull_hidden(("Turret",)) + geo.cull_hidden(("Cannon",))
    for i in range(1, 5):
        culled += geo.cull_hidden((f"Axle_{i}",))

    root = geo._empty("DRL_GatlingCar", (0, 0, 0), col, size=0.3, kind='PLAIN_AXES')
    body = geo._join("Body", "Body", col, (0, 0, 0))
    geo._parent(body, root)
    turret = geo._join("Turret", "Turret", col, TURRET_PIVOT)
    geo._parent(turret, root)
    cannon = geo._join("Cannon", "Cannon", col, CANNON_PIVOT)
    geo._parent(cannon, turret)
    geo._empty("VFX_Muzzle", (0, MUZZLE_Y - 0.01, CANNON_PIVOT[2]), col, cannon, 0.1)
    axles = []
    for i, y in enumerate(AXLE_YS, 1):
        a = geo._join(f"Axle_{i}", f"Axle_{i}", col, (0, y, WHEEL_R))
        geo._parent(a, root)
        axles.append(a)
    shared = axles[0].data
    shared.name = "SM_Wheelset"
    for a in axles[1:]:
        old = a.data
        a.data = shared
        bpy.data.meshes.remove(old)
    geo._empty("Coupler_Front", (0, -1.02, 0.27), col, root, 0.1)
    geo._empty("Coupler_Rear", (0, 1.02, 0.27), col, root, 0.1)

    bpy.data.collections.remove(work)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH'}
    return {"culled": culled, "tris": tris}
