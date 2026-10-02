"""
Drillex Rocket Launcher Car - Blender 5.2 build (same body block as every Drillex car).
Launcher: turret (yaw) > Cannon = 2x2 tube pod on a trunnion (pitch pivot) > Rocket_1..4 (shared mesh).
Previously derived from the Double-Barrel Gun Car: Body box identical to the Basic Gun Car
(1.10 m wide incl. pillars, body top 0.98 m, 2.04 m long incl. couplers); the turret is lower than
the Basic Gun turret and the two identical parallel cannons share one mesh. Side and end panels match the Basic Gun car.

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
from mathutils import Matrix, Vector

HERE = r"D:\Drillex\SourceArt\RocketCar"


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
TURRET_PIVOT = (0.0, 0.10, 0.95)
CANNON_PIVOT = (0.0, 0.10, 1.25)    # trunnion: launcher pod pitch pivot
TUBE_X = 0.095                      # 2 x 2 tube grid offsets
TUBE_Z = (1.165, 1.355)
TUBE_R = 0.085
TUBE_FRONT_Y = -0.58
TUBE_BACK_Y = 0.12
ROCKET_TIP_Y = -0.80
MUZZLE_Y = -0.84


def xs(sx, a, b):
    return tuple(sorted((sx * a, sx * b)))


def ys(sy, a, b):
    return tuple(sorted((sy * a, sy * b)))


def build_body(c):
    g = "Body"
    R, K, S, B, Y, H = "RedPaint", "BlackMetal", "Steel", "Brass", "YellowPaint", "Hazard"

    # ---- shell ----
    box(g, c, "Camo", -0.46, 0.46, -0.90, 0.90, 0.45, 0.92, 0.012, name="body_block")  # walls + ends + top
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
    box(g, c, "Camo", -0.42, 0.42, -0.72, 0.72, 0.92, 0.95, 0.008, name="roof_deck")  # sits on the block top
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
        box(g, c, K, *xs(sx, 0.455, 0.48), -0.70, -0.50, 0.50, 0.80, 0.006, name="emblem_plate")
        rot = Matrix.Rotation(math.radians(45), 3, 'X')
        box(g, c, "LampAmber", *xs(sx, 0.47, 0.486), -0.645, -0.555, 0.605, 0.695, 0.0, rot=rot, name="emblem_glow")

    # ---- ends: lamps, grille, pipes ----
    for sy in (-1, 1):
        ax = 'Y' if sy > 0 else '-Y'
        box(g, c, K, -0.24, 0.24, *ys(sy, 0.89, 0.915), 0.47, 0.76, 0.008, name="end_grille_frame")
        box(g, c, "Grille", -0.20, 0.20, *ys(sy, 0.905, 0.925), 0.50, 0.66, 0.0, name="end_grille")
        box(g, c, "WindowGlow", -0.16, 0.16, *ys(sy, 0.905, 0.922), 0.68, 0.73, 0.0, name="end_strip")
        for sx in (-1, 1):
            box(g, c, "Camo", *xs(sx, 0.28, 0.42), *ys(sy, 0.86, 0.95), 0.72, 0.88, 0.012, name="lamp_block")
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
    """Yaw part: ring, deck and the yellow yoke that carries the launcher trunnion."""
    g = "Turret"
    px, py, pz = TURRET_PIVOT
    K, S, B, H, Y = "BlackMetal", "Steel", "Brass", "Hazard", "YellowPaint"
    cz = CANNON_PIVOT[2]
    cyl(g, c, K, (px, py, pz + 0.03), 0.30, 0.06, 'Z', 24, bevel=0.008, name="ring")
    cyl(g, c, B, (px, py, pz + 0.07), 0.27, 0.02, 'Z', 24, name="ring_brass")
    for i in range(10):
        a = math.radians(i * 36 + 18)
        cyl(g, c, S, (px + math.cos(a) * 0.285, py + math.sin(a) * 0.285, pz + 0.065), 0.014, 0.012, 'Z', 6, name="ring_bolt")
    box(g, c, "Camo", -0.26, 0.26, py - 0.22, py + 0.24, pz + 0.08, pz + 0.16, 0.02, name="deck")
    box(g, c, H, -0.265, 0.265, py - 0.225, py + 0.245, pz + 0.13, pz + 0.15, 0.003, name="deck_band")
    for sx in (-1, 1):
        # yoke arm: angled yellow plate from the deck up to the trunnion boss
        box(g, c, Y, *xs(sx, 0.20, 0.25), py - 0.16, py + 0.18, pz + 0.12, cz - 0.02, 0.012, name="yoke_arm")
        box(g, c, Y, *xs(sx, 0.20, 0.25), py - 0.06, py + 0.08, cz - 0.04, cz + 0.06, 0.01, name="yoke_head")
        cyl(g, c, K, (sx * 0.26, py, cz), 0.06, 0.04, 'X' if sx > 0 else '-X', 14, bevel=0.005, name="trunnion_boss")
        cyl(g, c, S, (sx * 0.285, py, cz), 0.03, 0.02, 'X' if sx > 0 else '-X', 10, name="trunnion_cap")
        # hydraulic elevation ram
        cyl(g, c, S, (sx * 0.17, py + 0.12, pz + 0.22), 0.025, 0.16, 'Z', 8, name="ram")
        cyl(g, c, K, (sx * 0.17, py + 0.12, pz + 0.13), 0.035, 0.06, 'Z', 8, name="ram_base")
        lamp(g, c, (sx * 0.20, py - 0.225, pz + 0.12), '-Y', 0.028)


def build_cannon(c):
    """Pitch part: rear launcher box + four separate tubes (2 x 2) with bands and front rings."""
    g = "Cannon"
    _, cy, cz = CANNON_PIVOT
    tube_len = TUBE_BACK_Y - TUBE_FRONT_Y
    box(g, c, "Camo", -0.205, 0.205, TUBE_BACK_Y - 0.02, cy + 0.36, cz - 0.17, cz + 0.17, 0.025, name="pod_box")
    box(g, c, "BlackMetal", -0.215, 0.215, cy + 0.33, cy + 0.37, cz - 0.18, cz + 0.18, 0.008, name="pod_back")
    box(g, c, "Grille", -0.12, 0.12, cy + 0.369, cy + 0.38, cz - 0.10, cz + 0.10, 0.0, name="pod_vent")
    box(g, c, "Hazard", -0.21, 0.21, cy + 0.05, cy + 0.11, cz + 0.165, cz + 0.18, 0.003, name="pod_top_band")
    cyl(g, c, "BlackMetal", (0, cy + 0.20, cz + 0.18), 0.05, 0.02, 'Z', 12, name="pod_hatch")
    for sx in (-1, 1):
        box(g, c, "Brass", *xs(sx, 0.205, 0.22), cy + 0.02, cy + 0.30, cz - 0.10, cz + 0.10, 0.004, name="pod_rail")
    for x in (-TUBE_X, TUBE_X):
        for z in TUBE_Z:
            yc = (TUBE_FRONT_Y + TUBE_BACK_Y) / 2
            cyl(g, c, "BlackMetal", (x, yc, z), TUBE_R, tube_len, 'Y', 16, name="tube")
            for y, m in ((TUBE_FRONT_Y + 0.05, "RedPaint"), (-0.30, "Hazard"), (-0.05, "Hazard")):
                cyl(g, c, m, (x, y, z), TUBE_R + 0.008, 0.06, 'Y', 16, name="tube_band")
    # clamp strap around all four tubes
    box(g, c, "YellowPaint", -0.20, 0.20, -0.20, -0.14, cz - 0.20, cz + 0.20, 0.008, name="tube_strap")


def build_rocket(c):
    """One rocket on the X=0 axis (origin at its tube axis); four instances share this mesh."""
    g = "Rocket"
    z = 0.0
    cyl(g, c, "BlackMetal", (0, TUBE_FRONT_Y + 0.02, z), TUBE_R - 0.012, 0.04, 'Y', 14, name="rocket_body")
    cyl(g, c, "Hazard", (0, TUBE_FRONT_Y - 0.035, z), TUBE_R - 0.012, 0.03, 'Y', 14, name="rocket_band")
    lathe(g, c, "RedPaint", [(0.0, 0.0), (TUBE_R - 0.012, 0.0), (TUBE_R - 0.016, 0.05), (0.03, 0.17), (0.0, 0.22)],
          (0, TUBE_FRONT_Y - 0.05, z), '-Y', 14, name="rocket_nose")


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

    col = geo._coll("DRL_RocketCar")
    work = geo._coll("_build_tmp")
    build_body(work)
    build_turret(work)
    build_cannon(work)
    build_rocket(work)
    for i, y in enumerate(AXLE_YS, 1):
        build_axle(work, i, y)
    culled = geo.cull_hidden(("Body",)) + geo.cull_hidden(("Turret",)) + geo.cull_hidden(("Cannon",)) + geo.cull_hidden(("Rocket",))
    for i in range(1, 5):
        culled += geo.cull_hidden((f"Axle_{i}",))

    root = geo._empty("DRL_RocketCar", (0, 0, 0), col, size=0.3, kind='PLAIN_AXES')
    body = geo._join("Body", "Body", col, (0, 0, 0))
    geo._parent(body, root)
    turret = geo._join("Turret", "Turret", col, TURRET_PIVOT)
    geo._parent(turret, root)
    cannon = geo._join("Cannon", "Cannon", col, CANNON_PIVOT)
    geo._parent(cannon, turret)
    # four rockets, one per tube, sharing one mesh; origins on their tube axes
    rocket_src = geo._join("Rocket", "Rocket_1", col, (0, 0, 0))
    rocket_src.data.name = "SM_Rocket"
    i = 0
    for z in TUBE_Z[::-1]:
        for x in (TUBE_X, -TUBE_X):          # vehicle left (+X) first, top row first
            i += 1
            r = rocket_src if i == 1 else bpy.data.objects.new(f"Rocket_{i}", rocket_src.data)
            if i > 1:
                col.objects.link(r)
            r.location = (x, 0.0, z)
            geo._parent(r, cannon)
            geo._empty(f"VFX_Tube_{i}", (x, TUBE_FRONT_Y - 0.01, z), col, cannon, 0.06)
    bpy.context.view_layer.update()
    geo._empty("VFX_Muzzle", (0, MUZZLE_Y, CANNON_PIVOT[2]), col, cannon, 0.12)
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
