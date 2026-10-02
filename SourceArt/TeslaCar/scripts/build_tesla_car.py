"""
Drillex Tesla Cannon Car - Blender 5.2 build (same red/orange/black body block as every Drillex car).
Weapon: Turret (yaw, yoke + power cables) > Cannon (pitch pivot on the trunnion): housing, coil stacks,
cyan energy chamber, emitter cage + core > VFX_Muzzle, VFX_Arc_1..4 (cage tips), VFX_Arc_Top_L/R (rods).
Derived from the Rocket Launcher Car:
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

HERE = r"D:\Drillex\SourceArt\TeslaCar"


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, "scripts", name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


geo = _load("geo_lib")
geo.PALETTE["YellowPaint"] = ((0.85, 0.55, 0.03, 1), 0.0, 0.45, None)
geo.PALETTE["Camo"] = ((0.50, 0.035, 0.025, 1), 0.0, 0.50, None)
geo.PALETTE["Electric"] = ((0.10, 0.60, 1.0, 1), 0.0, 0.15, (0.05, 0.55, 1.0))
box, cyl, lathe, lamp = geo.box, geo.cyl, geo.lathe, geo.lamp

AXLE_YS = (-0.66, -0.30, 0.30, 0.66)
WHEEL_X = 0.50
WHEEL_R = 0.17
TURRET_PIVOT = (0.0, 0.10, 0.95)
CANNON_PIVOT = (0.0, 0.10, 1.22)    # trunnion: cannon pitch pivot
EMITTER_Y = -0.92                   # front of the emitter cage


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


def _cable(g, c, pts, r, mat="Electric"):
    """Power cable along a quadratic bezier (short cylinders)."""
    p0, p1, p2 = (Vector(v) for v in pts)
    prev = p0
    n = 8
    for i in range(1, n + 1):
        t = i / n
        pt = p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t ** 2
        d = pt - prev
        o = cyl(g, c, mat, (0, 0, 0), r, d.length + r * 0.6, 'Z', 8, name="cable")
        o.data.transform(d.to_track_quat('Z', 'Y').to_matrix().to_4x4())
        o.data.transform(Matrix.Translation((pt + prev) / 2))
        prev = pt


def build_turret(c):
    """Yaw part: ring, deck, yellow yoke with trunnion bosses, capacitor bank and power cables."""
    g = "Turret"
    px, py, pz = TURRET_PIVOT
    K, S, B, H, Y = "BlackMetal", "Steel", "Brass", "Hazard", "YellowPaint"
    cz = CANNON_PIVOT[2]
    cyl(g, c, K, (px, py, pz + 0.03), 0.30, 0.06, 'Z', 24, bevel=0.008, name="ring")
    cyl(g, c, B, (px, py, pz + 0.07), 0.27, 0.02, 'Z', 24, name="ring_brass")
    for i in range(10):
        a = math.radians(i * 36 + 18)
        cyl(g, c, S, (px + math.cos(a) * 0.285, py + math.sin(a) * 0.285, pz + 0.065), 0.014, 0.012, 'Z', 6, name="ring_bolt")
    box(g, c, "Camo", -0.27, 0.27, py - 0.22, py + 0.30, pz + 0.08, pz + 0.15, 0.02, name="deck")
    box(g, c, H, -0.275, 0.275, py - 0.225, py + 0.305, pz + 0.125, pz + 0.145, 0.003, name="deck_band")
    for sx in (-1, 1):
        box(g, c, Y, *xs(sx, 0.20, 0.25), py - 0.14, py + 0.20, pz + 0.12, cz - 0.03, 0.012, name="yoke_arm")
        box(g, c, Y, *xs(sx, 0.20, 0.25), py - 0.07, py + 0.07, cz - 0.05, cz + 0.06, 0.01, name="yoke_head")
        cyl(g, c, K, (sx * 0.26, py, cz), 0.065, 0.04, 'X' if sx > 0 else '-X', 14, bevel=0.005, name="trunnion_boss")
        cyl(g, c, "Electric", (sx * 0.283, py, cz), 0.03, 0.012, 'X' if sx > 0 else '-X', 12, name="trunnion_glow")
        # capacitor canisters on the deck
        for y in (py + 0.12, py + 0.22):
            cyl(g, c, K, (sx * 0.13, y, pz + 0.19), 0.04, 0.09, 'Z', 10, name="capacitor")
            cyl(g, c, "Electric", (sx * 0.13, y, pz + 0.24), 0.028, 0.012, 'Z', 10, name="capacitor_cap")
        # glowing power cable from the deck up into the cannon housing
        _cable(g, c, ((sx * 0.13, py + 0.17, pz + 0.24), (sx * 0.18, py + 0.30, pz + 0.30), (sx * 0.12, py + 0.30, cz - 0.08)), 0.018)
        lamp(g, c, (sx * 0.20, py - 0.225, pz + 0.115), '-Y', 0.025)


def build_cannon(c):
    """Pitch part: rear housing, layered rings, coil stacks, cyan energy chamber, emitter cage + core."""
    g = "Cannon"
    _, cy, cz = CANNON_PIVOT
    K, S, B, H = "BlackMetal", "Steel", "Brass", "Hazard"
    # rear housing (body colours) with top Tesla rods
    box(g, c, "Camo", -0.19, 0.19, cy - 0.06, cy + 0.34, cz - 0.15, cz + 0.15, 0.03, name="housing")
    box(g, c, K, -0.20, 0.20, cy + 0.31, cy + 0.35, cz - 0.16, cz + 0.16, 0.008, name="housing_back")
    box(g, c, "Grille", -0.11, 0.11, cy + 0.349, cy + 0.36, cz - 0.09, cz + 0.09, 0.0, name="housing_vent")
    box(g, c, H, -0.195, 0.195, cy + 0.04, cy + 0.10, cz + 0.145, cz + 0.16, 0.003, name="housing_band")
    for sx in (-1, 1):
        x = sx * 0.10
        # short Tesla rods (kept under the 1.47 m turret height of the other cars)
        cyl(g, c, K, (x, cy + 0.12, cz + 0.16), 0.03, 0.02, 'Z', 8, name="rod_base")
        cyl(g, c, "RedPaint", (x, cy + 0.12, cz + 0.18), 0.022, 0.02, 'Z', 8, name="rod_insulator")
        cyl(g, c, S, (x, cy + 0.12, cz + 0.20), 0.008, 0.03, 'Z', 6, name="rod")
        lathe(g, c, "Electric", [(0.0, 0.0), (0.016, 0.0), (0.016, 0.008), (0.0, 0.02)], (x, cy + 0.12, cz + 0.212), 'Z', 8, name="rod_tip")
    # main barrel spine
    y0 = cy - 0.06
    cyl(g, c, K, (0, (y0 + EMITTER_Y + 0.18) / 2, cz), 0.07, y0 - (EMITTER_Y + 0.18), 'Y', 16, name="spine")
    # layered rings: steel collars and copper coil stacks
    for y, r, d, m in ((y0 - 0.02, 0.15, 0.04, S), (y0 - 0.06, 0.13, 0.03, H), (cy - 0.62, 0.14, 0.035, S),
                       (cy - 0.66, 0.12, 0.025, K), (EMITTER_Y + 0.20, 0.13, 0.03, S)):
        cyl(g, c, m, (0, y, cz), r, d, 'Y', 20, bevel=0.004, name="collar")
    for k in range(5):
        cyl(g, c, B, (0, y0 - 0.10 - k * 0.022, cz), 0.115, 0.012, 'Y', 18, name="coil_ring")
    for k in range(6):
        cyl(g, c, B, (0, cy - 0.70 - k * 0.022, cz), 0.105, 0.012, 'Y', 18, name="coil_ring")
    # energy chamber: cyan core with black louvre bars
    s0, s1 = y0 - 0.22, cy - 0.58
    cyl(g, c, "Electric", (0, (s0 + s1) / 2, cz), 0.11, s0 - s1, 'Y', 18, name="chamber")
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        rot = Matrix.Rotation(a, 3, 'Y')
        box(g, c, K, -0.022, 0.022, s1, s0, -0.013, 0.013, 0.0, rot=rot, name="chamber_bar")
        ob = geo._pieces[g][-1]
        ob.data.transform(Matrix.Translation((math.cos(a) * 0.112, 0, cz + math.sin(a) * 0.112)))
    for y in (s0 + 0.01, s1 - 0.01):
        cyl(g, c, S, (0, y, cz), 0.125, 0.03, 'Y', 18, name="chamber_ring")
    # emitter: cage of four prongs around a glowing core
    e0 = EMITTER_Y + 0.18
    cyl(g, c, K, (0, e0 - 0.01, cz), 0.10, 0.04, 'Y', 16, name="emitter_base")
    lathe(g, c, "Electric", [(0.0, 0.0), (0.055, 0.0), (0.075, 0.05), (0.06, 0.10), (0.0, 0.12)],
          (0, e0 - 0.03, cz), '-Y', 14, name="emitter_core")
    for i in range(4):
        a = math.radians(i * 90 + 45)
        rot = Matrix.Rotation(a, 3, 'Y')
        box(g, c, S, -0.018, 0.018, EMITTER_Y, e0 - 0.02, -0.012, 0.012, 0.004, rot=rot, name="cage_prong")
        ob = geo._pieces[g][-1]
        ob.data.transform(Matrix.Translation((math.cos(a) * 0.12, 0, cz + math.sin(a) * 0.12)))
        cyl(g, c, "Electric", (math.cos(a) * 0.12, EMITTER_Y - 0.01, cz + math.sin(a) * 0.12), 0.016, 0.02, 'Y', 8, name="prong_tip")
    cyl(g, c, B, (0, EMITTER_Y + 0.06, cz), 0.135, 0.015, 'Y', 18, name="cage_ring")


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

    col = geo._coll("DRL_TeslaCar")
    work = geo._coll("_build_tmp")
    build_body(work)
    build_turret(work)
    build_cannon(work)
    for i, y in enumerate(AXLE_YS, 1):
        build_axle(work, i, y)
    culled = geo.cull_hidden(("Body",)) + geo.cull_hidden(("Turret",)) + geo.cull_hidden(("Cannon",))
    for i in range(1, 5):
        culled += geo.cull_hidden((f"Axle_{i}",))

    root = geo._empty("DRL_TeslaCar", (0, 0, 0), col, size=0.3, kind='PLAIN_AXES')
    body = geo._join("Body", "Body", col, (0, 0, 0))
    geo._parent(body, root)
    turret = geo._join("Turret", "Turret", col, TURRET_PIVOT)
    geo._parent(turret, root)
    cannon = geo._join("Cannon", "Cannon", col, CANNON_PIVOT)
    geo._parent(cannon, turret)
    cz = CANNON_PIVOT[2]
    geo._empty("VFX_Muzzle", (0, EMITTER_Y - 0.03, cz), col, cannon, 0.12)
    for i in range(4):
        a = math.radians(i * 90 + 45)
        geo._empty(f"VFX_Arc_{i + 1}", (math.cos(a) * 0.12, EMITTER_Y - 0.02, cz + math.sin(a) * 0.12), col, cannon, 0.05)
    for name, x in (("VFX_Arc_Top_L", 0.10), ("VFX_Arc_Top_R", -0.10)):
        geo._empty(name, (x, CANNON_PIVOT[1] + 0.12, cz + 0.235), col, cannon, 0.05)
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
