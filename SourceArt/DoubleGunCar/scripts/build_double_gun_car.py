"""
Drillex Double-Barrel Gun Car - Blender 5.2 build. Body box identical to the Basic Gun Car
(1.10 m wide incl. pillars, body top 0.98 m, 2.04 m long incl. couplers); the turret is lower than
the Basic Gun turret and the two identical parallel cannons share one mesh.

Conventions: meters, Z up, front faces -Y, root pivot on the ground under the car center.
Game scene:
  DRL_GunCar (root empty)
  |- Body            one merged static mesh (shell, frames, lamps, vents, undercarriage)
  |- Turret          yaw pivot on the turret ring
  |   '- Cannon      pitch pivot at the mantlet
  |       '- VFX_Muzzle   empty at the barrel tip (+forward)
  |- Axle_1..4       wheelsets sharing one mesh, pivot on the axle
  '- Coupler_Front / Coupler_Rear
"""
import bpy
import importlib.util
import math
import os
from mathutils import Matrix

HERE = r"D:\Drillex\SourceArt\DoubleGunCar"


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, "scripts", name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


geo = _load("geo_lib")
geo.PALETTE["YellowPaint"] = ((0.85, 0.55, 0.03, 1), 0.0, 0.45, None)
geo.PALETTE["Camo"] = ((0.50, 0.035, 0.025, 1), 0.0, 0.50, None)
box, cyl, lathe, lamp = geo.box, geo.cyl, geo.lathe, geo.lamp

AXLE_YS = (-0.66, -0.30, 0.30, 0.66)
WHEEL_X = 0.50
WHEEL_R = 0.17
TURRET_PIVOT = (0.0, 0.08, 0.95)
BARREL_X = 0.16                     # barrels mirrored at +/- this offset
CANNON_PIVOT = (0.0, -0.25, 1.13)   # breech at the turret front
MUZZLE_Y = -1.17


def xs(sx, a, b):
    return tuple(sorted((sx * a, sx * b)))


def ys(sy, a, b):
    return tuple(sorted((sy * a, sy * b)))


def build_body(c):
    g = "Body"
    R, K, S, B, Y, H = "RedPaint", "BlackMetal", "Steel", "Brass", "YellowPaint", "Hazard"

    # ---- shell ----
    box(g, c, "Camo", -0.46, 0.46, -0.76, 0.76, 0.45, 0.87, 0.035, name="shell")
    box(g, c, K, -0.48, 0.48, -0.86, 0.86, 0.36, 0.46, 0.012, name="sill_frame")
    for sx in (-1, 1):
        box(g, c, H, *xs(sx, 0.47, 0.495), -0.74, 0.74, 0.375, 0.455, 0.006, name="sill_hazard")
    for sy in (-1, 1):
        box(g, c, H, -0.40, 0.40, *ys(sy, 0.86, 0.885), 0.375, 0.455, 0.006, name="sill_hazard_end")
    # hazard top rim + black roof deck
    box(g, c, H, -0.475, 0.475, -0.78, 0.78, 0.86, 0.925, 0.01, name="top_rim")
    box(g, c, "Camo", -0.42, 0.42, -0.72, 0.72, 0.915, 0.95, 0.01, name="roof_deck")
    for y in (-0.55, 0.55):
        box(g, c, "Grille", -0.12, 0.12, y - 0.08, y + 0.08, 0.94, 0.965, 0.004, name="roof_vent")

    # ---- corner pillars ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(g, c, K, *xs(sx, 0.40, 0.53), *ys(sy, 0.74, 0.90), 0.36, 0.95, 0.02, name="pillar")
            box(g, c, H, *xs(sx, 0.53, 0.545), *ys(sy, 0.77, 0.87), 0.47, 0.86, 0.004, name="pillar_hazard")
            box(g, c, S, *xs(sx, 0.38, 0.55), *ys(sy, 0.73, 0.91), 0.93, 0.98, 0.01, name="pillar_cap")

    # ---- sides: pipe runs, piston housings, rear grille (no lamps, no doors) ----
    for sx in (-1, 1):
        ax = 'X' if sx > 0 else '-X'
        for z in (0.56, 0.78):
            cyl(g, c, Y, (sx * 0.49, 0.0, z), 0.022, 1.36, 'Y', 10, name="side_pipe")
            for y in (-0.55, -0.15, 0.25):
                box(g, c, K, *xs(sx, 0.46, 0.515), y - 0.03, y + 0.03, z - 0.035, z + 0.035, 0.004, name="pipe_clamp")
        box(g, c, H, *xs(sx, 0.46, 0.475), -0.62, 0.30, 0.64, 0.70, 0.0, name="side_hazard")
        for y in (-0.58, 0.58):
            box(g, c, K, *xs(sx, 0.455, 0.50), y - 0.09, y + 0.09, 0.50, 0.84, 0.01, name="piston_housing")
            cyl(g, c, S, (sx * 0.505, y, 0.67), 0.05, 0.03, ax, 12, name="piston_cap")
            cyl(g, c, B, (sx * 0.52, y, 0.67), 0.025, 0.02, ax, 8, name="piston_bolt")
        box(g, c, K, *xs(sx, 0.455, 0.485), 0.30, 0.46, 0.53, 0.72, 0.006, name="side_grille_frame")
        box(g, c, "Grille", *xs(sx, 0.475, 0.49), 0.32, 0.44, 0.55, 0.70, 0.0, name="side_grille")

    # ---- ends: grille panel, piston housings, pipes ----
    for sy in (-1, 1):
        ax = 'Y' if sy > 0 else '-Y'
        box(g, c, "Camo", -0.40, 0.40, *ys(sy, 0.76, 0.90), 0.40, 0.92, 0.02, name="end_panel")
        box(g, c, Y, -0.20, 0.20, *ys(sy, 0.89, 0.91), 0.47, 0.70, 0.006, name="end_plate")
        box(g, c, K, -0.15, 0.15, *ys(sy, 0.905, 0.92), 0.50, 0.66, 0.006, name="end_grille_frame")
        box(g, c, "Grille", -0.12, 0.12, *ys(sy, 0.912, 0.928), 0.52, 0.64, 0.0, name="end_grille")
        for sx in (-1, 1):
            box(g, c, K, *xs(sx, 0.24, 0.38), *ys(sy, 0.86, 0.94), 0.50, 0.76, 0.01, name="end_housing")
            cyl(g, c, S, (sx * 0.31, sy * 0.945, 0.63), 0.045, 0.03, ax, 12, name="end_cap")
        cyl(g, c, Y, (0, sy * 0.925, 0.80), 0.02, 0.70, 'X', 10, name="end_pipe")
        cyl(g, c, S, (0, sy * 0.925, 0.43), 0.02, 0.70, 'X', 10, name="end_pipe_low")

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
    K, S, B, H, Y = "BlackMetal", "Steel", "Brass", "Hazard", "YellowPaint"
    cyl(g, c, K, (px, py, pz + 0.03), 0.30, 0.06, 'Z', 24, bevel=0.008, name="ring")
    cyl(g, c, B, (px, py, pz + 0.07), 0.27, 0.02, 'Z', 24, name="ring_brass")
    # low, wide armoured block with heavy chamfers (octagonal plan like the reference)
    box(g, c, "Camo", -0.36, 0.36, py - 0.33, py + 0.31, pz + 0.08, pz + 0.27, 0.06, name="turret_body")
    box(g, c, K, -0.37, 0.37, py - 0.34, py + 0.32, pz + 0.075, pz + 0.11, 0.01, name="turret_skirt")
    box(g, c, "Camo", -0.29, 0.29, py - 0.25, py + 0.24, pz + 0.27, pz + 0.31, 0.04, name="turret_top")
    # top hatch: black ring, steel disc, brass bolts
    cyl(g, c, K, (px - 0.04, py - 0.02, pz + 0.315), 0.15, 0.03, 'Z', 20, name="hatch_ring")
    cyl(g, c, S, (px - 0.04, py - 0.02, pz + 0.335), 0.10, 0.02, 'Z', 18, name="hatch")
    cyl(g, c, K, (px - 0.04, py - 0.02, pz + 0.35), 0.03, 0.02, 'Z', 10, name="hatch_knob")
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        cyl(g, c, B, (px - 0.04 + math.cos(a) * 0.125, py - 0.02 + math.sin(a) * 0.125, pz + 0.333),
            0.012, 0.012, 'Z', 6, name="hatch_bolt")
    # grille box on the rear-right corner of the turret
    box(g, c, Y, 0.16, 0.34, py + 0.08, py + 0.30, pz + 0.10, pz + 0.30, 0.012, name="grille_box")
    box(g, c, K, 0.18, 0.32, py + 0.10, py + 0.28, pz + 0.30, pz + 0.315, 0.0, name="grille_box_frame")
    box(g, c, "Grille", 0.195, 0.305, py + 0.115, py + 0.265, pz + 0.31, pz + 0.322, 0.0, name="grille_box_top")
    for sx in (-1, 1):
        box(g, c, H, *xs(sx, 0.355, 0.375), py - 0.24, py + 0.20, pz + 0.13, pz + 0.19, 0.004, name="side_hazard")
        box(g, c, B, *xs(sx, 0.36, 0.385), py - 0.30, py + 0.28, pz + 0.085, pz + 0.12, 0.005, name="side_bracket")
        # breech housings: one per barrel, fixed to the turret
        box(g, c, K, sx * BARREL_X - 0.10, sx * BARREL_X + 0.10, py - 0.38, py - 0.30, pz + 0.09, pz + 0.27,
            0.012, name="breech_housing")
    box(g, c, H, -0.06, 0.06, py - 0.37, py - 0.31, pz + 0.12, pz + 0.24, 0.004, name="center_plate")


def build_cannon(c):
    """One barrel on the X=0 axis; both barrel objects share this mesh."""
    g = "Cannon"
    _, py, pz = CANNON_PIVOT
    cyl(g, c, "Hazard", (0, py - 0.07, pz), 0.088, 0.14, 'Y', 18, bevel=0.008, name="breech_collar")
    cyl(g, c, "Brass", (0, py - 0.15, pz), 0.092, 0.025, 'Y', 18, name="collar_band")
    cyl(g, c, "BlackMetal", (0, (py - 0.16 + MUZZLE_Y + 0.12) / 2, pz), 0.062,
        (py - 0.16) - (MUZZLE_Y + 0.12), 'Y', 16, name="barrel")
    cyl(g, c, "Hazard", (0, py - 0.47, pz), 0.072, 0.07, 'Y', 16, name="mid_band")
    cyl(g, c, "Steel", (0, py - 0.70, pz), 0.068, 0.03, 'Y', 16, name="barrel_band")
    lathe(g, c, "RedPaint",
          [(0.05, 0.0), (0.088, 0.0), (0.092, 0.01), (0.092, 0.12), (0.082, 0.13), (0.05, 0.13), (0.05, 0.0)],
          (0, MUZZLE_Y + 0.13, pz), '-Y', 18, name="muzzle")
    cyl(g, c, "BlackMetal", (0, MUZZLE_Y + 0.10, pz), 0.051, 0.01, 'Y', 12, name="bore_back")


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

    col = geo._coll("DRL_DoubleGunCar")
    work = geo._coll("_build_tmp")
    build_body(work)
    build_turret(work)
    build_cannon(work)
    for i, y in enumerate(AXLE_YS, 1):
        build_axle(work, i, y)
    culled = geo.cull_hidden(("Body",)) + geo.cull_hidden(("Turret",)) + geo.cull_hidden(("Cannon",))
    for i in range(1, 5):
        culled += geo.cull_hidden((f"Axle_{i}",))

    root = geo._empty("DRL_DoubleGunCar", (0, 0, 0), col, size=0.3, kind='PLAIN_AXES')
    body = geo._join("Body", "Body", col, (0, 0, 0))
    geo._parent(body, root)
    turret = geo._join("Turret", "Turret", col, TURRET_PIVOT)
    geo._parent(turret, root)
    cannon = geo._join("Cannon", "Cannon_R", col, CANNON_PIVOT)   # front is -Y, so -X is the right side
    cannon.data.name = "SM_Cannon"
    geo._parent(cannon, turret)
    # identical twin barrel sharing the mesh, mirrored about the turret centre line
    twin = bpy.data.objects.new("Cannon_L", cannon.data)
    col.objects.link(twin)
    cannon.location.x = -BARREL_X
    twin.location = (BARREL_X, CANNON_PIVOT[1], CANNON_PIVOT[2])
    geo._parent(twin, turret)
    bpy.context.view_layer.update()
    geo._empty("VFX_Muzzle_R", (-BARREL_X, MUZZLE_Y, CANNON_PIVOT[2]), col, cannon, 0.1)
    geo._empty("VFX_Muzzle_L", (BARREL_X, MUZZLE_Y, CANNON_PIVOT[2]), col, twin, 0.1)
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
