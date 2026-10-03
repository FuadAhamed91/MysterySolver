"""
The Spooky Crime Scene - Case 3: "The Nightshade Conservatory" asset generator.

Run headless:
  blender --background --factory-startup --python SourceArt/build_greenhouse.py -- <unity_project_root>

A Victorian glasshouse at night: brick base, painted iron frame, glass walls and gable roof, potting benches full
of plants, palms, a coal boiler with heating pipes, the locked door, the roof vent with its crank and ladder,
the glowing Moonflower on its display table, and the case's clue props.
Blender axes: +Z up, long axis along X (door at -X, boiler at +X), north wall at +Y.
Unity import maps Blender (x, y, z) -> (-x, z, -y).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from meshkit import (T, reset_scene, mat, new_obj, bm_box, bm_cyl, bm_sphere, bm_ico, bm_lathe, bm_ring, bm_beam,
                     bm_rod, bm_quad, rot_x, rot_y, rot_z, scale, folded_paper, book, export_groups)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT_ROOT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(PROJECT_ROOT, "Assets", "Models", "Greenhouse")

reset_scene()
for name, rgb, metal, rough, alpha in (
    ("M_Brick", (0.3, 0.1, 0.06), 0.0, 0.85, 1.0),
    ("M_GlassPane", (0.6, 0.8, 0.72), 0.0, 0.05, 0.12),
    ("M_FrameGreen", (0.07, 0.14, 0.1), 0.5, 0.5, 1.0),
    ("M_Gravel", (0.25, 0.24, 0.22), 0.0, 0.95, 1.0),
    ("M_Soil", (0.06, 0.04, 0.025), 0.0, 1.0, 1.0),
    ("M_Terracotta", (0.42, 0.16, 0.08), 0.0, 0.8, 1.0),
    ("M_Leaf", (0.05, 0.18, 0.04), 0.0, 0.6, 1.0),
    ("M_LeafDark", (0.02, 0.08, 0.02), 0.0, 0.6, 1.0),
    ("M_PlantStem", (0.08, 0.15, 0.04), 0.0, 0.7, 1.0),
    ("M_Moonflower", (0.55, 0.2, 1.0), 0.0, 0.4, 1.0),
    ("M_Glove", (0.32, 0.22, 0.12), 0.0, 0.7, 1.0),
    ("M_BrownGlass", (0.15, 0.07, 0.02), 0.0, 0.1, 0.85),
    ("M_Mud", (0.05, 0.035, 0.02), 0.0, 1.0, 1.0),
    ("M_BookGreen", (0.04, 0.16, 0.08), 0.0, 0.55, 1.0),
    ("M_LampGlow", (1.0, 0.85, 0.5), 0.0, 1.0, 1.0),
    ("M_Iron", (0.08, 0.08, 0.085), 0.9, 0.55, 1.0),
    ("M_Brass", (0.78, 0.56, 0.24), 1.0, 0.32, 1.0),
    ("M_Wood", (0.3, 0.17, 0.08), 0.0, 0.5, 1.0),
    ("M_Timber", (0.12, 0.075, 0.045), 0.0, 0.75, 1.0),
    ("M_Paper", (0.86, 0.8, 0.66), 0.0, 0.9, 1.0),
    ("M_Ink", (0.02, 0.02, 0.03), 0.0, 0.2, 1.0),
    ("M_Leather", (0.18, 0.04, 0.03), 0.0, 0.55, 1.0),
    ("M_Cork", (0.55, 0.4, 0.25), 0.0, 0.9, 1.0),
    ("M_Wax", (0.45, 0.02, 0.02), 0.0, 0.35, 1.0),
    ("M_Rust", (0.35, 0.12, 0.04), 0.3, 0.2, 1.0),
    ("M_Proxy", (1.0, 0.0, 1.0), 0.0, 1.0, 1.0),
):
    mat(name, rgb, metal, rough, alpha)

rng = random.Random(1888)
L, W = 7.0, 4.0          # half-length (x) and half-width (y)
BASE, EAVE, RIDGE = 0.6, 2.6, 4.4
BAY = 1.0

def roof_z(y):
    return EAVE + (RIDGE - EAVE) * (1 - abs(y) / W)

# ============================================================================ structure
house = []
bm = bmesh.new()  # brick base wall, with a gap for the door at -X
for y in (-W, W):
    bm_box(bm, (2 * L + 0.25, 0.25, BASE), T((0, y, BASE / 2)))
for x in (-L, L):
    if x < 0:
        for sy in (-1, 1):
            bm_box(bm, (0.25, W - 0.55, BASE), T((x, sy * (W + 0.55) / 2, BASE / 2)))
    else:
        bm_box(bm, (0.25, 2 * W, BASE), T((x, 0, BASE / 2)))
house.append(new_obj("GH_BrickBase", bm, "M_Brick"))

bm = bmesh.new()  # floor: gravel aisle and soil beds
bm_box(bm, (2 * L, 2 * W, 0.1), T((0, 0, -0.05)))
house.append(new_obj("GH_Floor", bm, "M_Gravel"))

frame = bmesh.new()  # painted iron frame
xs = [x * BAY - L for x in range(int(2 * L / BAY) + 1)]
for x in xs:
    for y in (-W, W):
        bm_box(frame, (0.06, 0.06, EAVE - BASE), T((x, y, (BASE + EAVE) / 2)))
    for sy in (-1, 1):  # rafters
        bm_beam(frame, (x, sy * W, EAVE), (x, 0, RIDGE), 0.06, 0.08)
for y in (-W, W):
    for z in (BASE + 0.02, 1.6, EAVE):
        bm_box(frame, (2 * L, 0.06, 0.06), T((0, y, z)))
bm_box(frame, (2 * L, 0.1, 0.1), T((0, 0, RIDGE)))  # ridge beam
for sy in (-1, 1):  # purlins
    for t in (0.33, 0.66):
        y = sy * W * (1 - t)
        bm_box(frame, (2 * L, 0.05, 0.05), T((0, y, roof_z(y))))
for x in (-L, L):  # gable ends
    for y in [i * BAY - W for i in range(int(2 * W / BAY) + 1)]:
        if x < 0 and abs(y) < 0.6:
            continue  # door opening
        bm_box(frame, (0.06, 0.06, roof_z(y) - BASE), T((x, y, (BASE + roof_z(y)) / 2)))
    for z in (BASE + 0.02, 1.6, EAVE):
        for sy in (-1, 1):
            if x < 0 and z < 2.3:
                bm_box(frame, (0.06, W - 0.6, 0.06), T((x, sy * (W + 0.6) / 2, z)))
            else:
                bm_box(frame, (0.06, W, 0.06), T((x, sy * W / 2, z)))
# door frame
bm_box(frame, (0.1, 0.1, 2.3), T((-L, -0.62, 1.15)))
bm_box(frame, (0.1, 0.1, 2.3), T((-L, 0.62, 1.15)))
bm_box(frame, (0.1, 1.34, 0.1), T((-L, 0, 2.3)))
house.append(new_obj("GH_Frame", frame, "M_FrameGreen"))

glass = bmesh.new()  # one quad per wall/roof surface (the frame breaks it up visually)
for y in (-W, W):
    bm_quad(glass, [(-L, y, BASE), (L, y, BASE), (L, y, EAVE), (-L, y, EAVE)])
for sy in (-1, 1):
    bm_quad(glass, [(-L, sy * W, EAVE), (L, sy * W, EAVE), (L, 0, RIDGE), (-L, 0, RIDGE)])
for x in (-L, L):
    if x < 0:
        for sy in (-1, 1):
            bm_quad(glass, [(x, sy * 0.62, BASE), (x, sy * W, BASE), (x, sy * W, EAVE), (x, sy * 0.62, EAVE)])
        bm_quad(glass, [(x, -W, EAVE), (x, W, EAVE), (x, 0, RIDGE)][:3] + [(x, 0, RIDGE)])
        bm_quad(glass, [(x, -0.62, 2.3), (x, 0.62, 2.3), (x, 0.62, EAVE), (x, -0.62, EAVE)])
    else:
        bm_quad(glass, [(x, -W, BASE), (x, W, BASE), (x, W, EAVE), (x, -W, EAVE)])
        bm_quad(glass, [(x, -W, EAVE), (x, W, EAVE), (x, 0, RIDGE), (x, 0, RIDGE + 0.0001)])
house.append(new_obj("GH_Glass", glass, "M_GlassPane"))

# The roof vent on the north slope (x 3.6..4.8), propped a hand's width open, with a crank rod to a wheel
VENT_X0, VENT_X1 = 3.6, 4.8
vent_y0, vent_y1 = W * 0.62, W * 0.95
bm = bmesh.new()
hinge = Vector((0, vent_y0, roof_z(vent_y0)))
slope = math.degrees(math.atan2(RIDGE - EAVE, W))
vent_m = T(((VENT_X0 + VENT_X1) / 2, (vent_y0 + vent_y1) / 2, (roof_z(vent_y0) + roof_z(vent_y1)) / 2 + 0.08)) @ rot_x(slope + 6)
bm_box(bm, (VENT_X1 - VENT_X0, vent_y1 - vent_y0 + 0.05, 0.05), vent_m)
CRANK = Vector((4.2, W - 0.12, 1.45))
bm_rod(bm, CRANK, ((VENT_X0 + VENT_X1) / 2, vent_y1 - 0.1, roof_z(vent_y1 - 0.1)), 0.015, 8)
bm_ring(bm, 0.09, 0.11, 0.02, T(CRANK) @ rot_x(90), 24)
for k in range(4):
    a = k * math.pi / 2
    bm_beam(bm, CRANK, CRANK + Vector((0.1 * math.cos(a), 0, 0.1 * math.sin(a))), 0.015, 0.015)
bm_rod(bm, CRANK + Vector((0.1, 0, 0)), CRANK + Vector((0.1, -0.08, 0)), 0.012, 8)  # handle
house.append(new_obj("GH_VentAndCrank", bm, "M_Iron"))

# Ladder leaning against the north wall under the vent
LADDER_FOOT = Vector((4.25, W - 1.15, 0.0))
LADDER_TOP = Vector((4.25, W - 0.08, EAVE + 0.15))
bm = bmesh.new()
d = (LADDER_TOP - LADDER_FOOT)
for sx in (-0.22, 0.22):
    bm_beam(bm, LADDER_FOOT + Vector((sx, 0, 0)), LADDER_TOP + Vector((sx, 0, 0)), 0.05, 0.05)
for k in range(1, 10):
    p = LADDER_FOOT + d * (k / 10.0)
    bm_rod(bm, p + Vector((-0.22, 0, 0)), p + Vector((0.22, 0, 0)), 0.018, 8)
house.append(new_obj("GH_Ladder", bm, "M_Wood"))

# The door: iron-banded timber, shut and padlocked from the inside
bm = bmesh.new()
bm_box(bm, (0.06, 1.18, 2.2), T((-L + 0.02, 0, 1.12)))
house.append(new_obj("GH_Door", bm, "M_Timber"))
bm = bmesh.new()
for z in (0.4, 1.9):
    bm_box(bm, (0.07, 1.2, 0.06), T((-L + 0.02, 0, z)))
bm_box(bm, (0.04, 0.08, 0.1), T((-L + 0.07, 0.42, 1.05)))
bm_ring(bm, 0.02, 0.03, 0.012, T((-L + 0.08, 0.42, 1.13)) @ rot_y(90), 12)
house.append(new_obj("GH_DoorIron", bm, "M_Iron"))

# Boiler at the east end, heating pipes along both long walls, and the floor grate (the "boiler tunnel")
bm = bmesh.new()
BOILER = Vector((L - 0.8, -2.2, 0))
bm_cyl(bm, 0.45, 1.3, T(BOILER + Vector((0, 0, 0.65))), 20)
bm_cyl(bm, 0.5, 0.08, T(BOILER + Vector((0, 0, 1.34))), 20)
bm_rod(bm, BOILER + Vector((0, 0, 1.35)), BOILER + Vector((0, 0, 4.6)), 0.09, 12)  # flue through the roof
bm_box(bm, (0.32, 0.1, 0.28), T(BOILER + Vector((-0.45, 0, 0.5))))  # firebox door
for y in (-W + 0.3, W - 0.3):
    for z in (0.18, 0.32):
        bm_rod(bm, (-L + 0.3, y, z), (L - 0.3, y, z), 0.045, 10)
bm_box(bm, (1.2, 0.8, 0.03), T((L - 1.9, -2.2, 0.015)))  # floor grate over the stoke-hole
house.append(new_obj("GH_Boiler", bm, "M_Iron"))
bm = bmesh.new()
bm_box(bm, (0.3, 0.75, 0.05), T(BOILER + Vector((0.0, 0.85, 1.1))))  # ledge beside the boiler
house.append(new_obj("GH_BoilerLedge", bm, "M_Wood"))
BOILER_LEDGE_TOP = BOILER + Vector((0.0, 0.85, 1.125))

# ============================================================================ plants & benches
plants = []
bench = bmesh.new()
pots = bmesh.new()
soil = bmesh.new()
leaves = bmesh.new()
leaves_dark = bmesh.new()
stems = bmesh.new()
BENCH_H = 0.85
for sy in (-1, 1):
    y = sy * (W - 0.75)
    x0, x1 = -L + 1.2, L - 1.6
    if sy > 0:  # leave a gap in the north bench for the ladder
        segments = [(x0, 3.4), (5.1, x1)]
    else:
        segments = [(x0, 4.9)]
    for a, b in segments:
        bm_box(bench, (b - a, 1.0, 0.05), T(((a + b) / 2, y, BENCH_H)))
        bm_box(bench, (b - a, 1.0, 0.04), T(((a + b) / 2, y, 0.25)))
        for x in (a + 0.05, b - 0.05):
            for dy in (-0.45, 0.45):
                bm_box(bench, (0.06, 0.06, BENCH_H), T((x, y + dy, BENCH_H / 2)))
        x = a + 0.35
        while x < b - 0.3:  # pots with plants
            r = rng.uniform(0.11, 0.17)
            p = Vector((x, y + rng.uniform(-0.25, 0.25), BENCH_H + 0.025))
            bm_lathe(pots, [(0.0, 0.0), (r * 0.7, 0.0), (r, r * 1.4), (r * 1.08, r * 1.4), (r * 1.08, r * 1.6),
                            (r * 0.9, r * 1.6), (0.0, r * 1.5)], T(p), 14)
            bm_cyl(soil, r * 0.88, 0.01, T(p + Vector((0, 0, r * 1.52))), 12)
            top = p + Vector((0, 0, r * 1.5))
            kind = rng.random()
            if kind < 0.45:  # leafy clump
                for k in range(rng.randint(3, 6)):
                    bm_ico(leaves if rng.random() < 0.6 else leaves_dark, rng.uniform(0.09, 0.16),
                           T(top + Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), rng.uniform(0.05, 0.25))))
                           @ scale(1, 1, 0.7), 1)
            elif kind < 0.8:  # spiky plant
                for k in range(rng.randint(5, 9)):
                    a_ = rng.uniform(0, 360)
                    tilt = rng.uniform(15, 40)
                    hgt = rng.uniform(0.25, 0.5)
                    bm_cyl(leaves_dark if rng.random() < 0.5 else leaves, 0.03, hgt,
                           T(top) @ rot_z(a_) @ rot_x(tilt) @ T((0, 0, hgt / 2)), 4, r2=0.002)
            else:  # tall stem with a bud
                hgt = rng.uniform(0.4, 0.7)
                bm_rod(stems, top, top + Vector((0, 0, hgt)), 0.008, 6)
                bm_ico(leaves, 0.06, T(top + Vector((0, 0, hgt))), 1)
            x += rng.uniform(0.38, 0.55)
plants.append(new_obj("GH_Benches", bench, "M_Wood"))
plants.append(new_obj("GH_Pots", pots, "M_Terracotta"))
plants.append(new_obj("GH_PotSoil", soil, "M_Soil"))
plants.append(new_obj("GH_Leaves", leaves, "M_Leaf"))
plants.append(new_obj("GH_LeavesDark", leaves_dark, "M_LeafDark"))
plants.append(new_obj("GH_Stems", stems, "M_PlantStem"))

# Palms in the corners near the door and the boiler
palm_trunks = bmesh.new()
palm_fronds = bmesh.new()
for (px, py) in ((-L + 0.8, W - 0.8), (-L + 0.8, -W + 0.8), (L - 0.9, W - 0.8)):
    base = Vector((px, py, 0))
    bm_lathe(palm_trunks, [(0.0, 0.0), (0.32, 0.0), (0.36, 0.45), (0.3, 0.5), (0.0, 0.5)], T(base), 16)
    trunk_top = base + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 2.4))
    bm_rod(palm_trunks, base + Vector((0, 0, 0.45)), trunk_top, 0.09, 10)
    for k in range(9):
        a_ = k * 40 + rng.uniform(-10, 10)
        bm_cyl(palm_fronds, 0.16, 1.3, T(trunk_top) @ rot_z(a_) @ rot_x(rng.uniform(55, 80)) @ T((0, 0, 0.62)) @ scale(1, 0.25, 1), 5, r2=0.01)
plants.append(new_obj("GH_PalmTrunks", palm_trunks, "M_Timber"))
plants.append(new_obj("GH_PalmFronds", palm_fronds, "M_LeafDark"))

# Display table with the Moonflower, Lady Agatha's chair, and her oil lantern
TABLE = Vector((1.4, 0.4, 0))
bm = bmesh.new()
bm_cyl(bm, 0.55, 0.05, T(TABLE + Vector((0, 0, 0.8))), 24)
bm_rod(bm, TABLE + Vector((0, 0, 0.05)), TABLE + Vector((0, 0, 0.78)), 0.05, 10)
bm_cyl(bm, 0.3, 0.05, T(TABLE + Vector((0, 0, 0.025))), 16)
CHAIR = TABLE + Vector((-0.85, -0.55, 0))
chair_m = T(CHAIR) @ rot_z(35)
bm_box(bm, (0.45, 0.45, 0.05), chair_m @ T((0, 0, 0.46)))
bm_box(bm, (0.45, 0.05, 0.55), chair_m @ T((0, -0.22, 0.75)))
for sx in (-0.19, 0.19):
    for sy in (-0.19, 0.19):
        bm_box(bm, (0.04, 0.04, 0.46), chair_m @ T((sx, sy, 0.23)))
plants.append(new_obj("GH_DisplayTable", bm, "M_Wood"))
TABLE_TOP = TABLE + Vector((0, 0, 0.825))

bm = bmesh.new()  # Moonflower: pot, stem, leaves
mf = TABLE_TOP + Vector((0.1, 0.1, 0))
bm_lathe(bm, [(0.0, 0.0), (0.1, 0.0), (0.14, 0.2), (0.15, 0.22), (0.0, 0.21)], T(mf), 16)
plants.append(new_obj("GH_MoonflowerPot", bm, "M_Terracotta"))
bm = bmesh.new()
stem_top = mf + Vector((0.02, -0.03, 0.75))
bm_rod(bm, mf + Vector((0, 0, 0.2)), stem_top, 0.012, 8)
for k in range(5):
    a_ = k * 72
    bm_cyl(bm, 0.07, 0.32, T(mf + Vector((0, 0, 0.25 + 0.07 * k))) @ rot_z(a_) @ rot_x(60) @ T((0, 0, 0.16)) @ scale(1, 0.25, 1), 6, r2=0.005)
plants.append(new_obj("GH_MoonflowerStem", bm, "M_Leaf"))
bm = bmesh.new()  # the glowing flower: a ring of petals around a bright heart
for k in range(7):
    a_ = k * (360 / 7)
    bm_sphere(bm, 0.07, T(stem_top) @ rot_z(a_) @ rot_x(-55) @ T((0, 0.075, 0)) @ scale(0.45, 1, 0.12), 10, 6)
bm_sphere(bm, 0.03, T(stem_top + Vector((0, 0, 0.01))), 10, 6)
plants.append(new_obj("GH_MoonflowerBloom", bm, "M_Moonflower"))
MOONFLOWER = stem_top

bm = bmesh.new()  # oil lantern on the table
lan = TABLE_TOP + Vector((-0.3, -0.15, 0))
bm_cyl(bm, 0.08, 0.04, T(lan + Vector((0, 0, 0.02))), 16)
bm_cyl(bm, 0.07, 0.03, T(lan + Vector((0, 0, 0.26))), 16)
for k in range(4):
    a_ = k * math.pi / 2 + math.pi / 4
    bm_rod(bm, lan + Vector((0.065 * math.cos(a_), 0.065 * math.sin(a_), 0.04)), lan + Vector((0.065 * math.cos(a_), 0.065 * math.sin(a_), 0.25)), 0.006, 6)
bm_ring(bm, 0.03, 0.04, 0.008, T(lan + Vector((0, 0, 0.32))) @ rot_x(90), 12)
plants.append(new_obj("GH_Lantern", bm, "M_Brass"))
bm = bmesh.new()
bm_sphere(bm, 0.035, T(lan + Vector((0, 0, 0.14))) @ scale(1, 1, 1.4), 10, 8)
plants.append(new_obj("GH_LanternFlame", bm, "M_LampGlow"))
LANTERN = lan + Vector((0, 0, 0.14))

# Lectern for the visitor book by the door
bm = bmesh.new()
LECTERN = Vector((-L + 1.0, 1.4, 0))
bm_box(bm, (0.08, 0.08, 1.0), T(LECTERN + Vector((0, 0, 0.5))))
bm_box(bm, (0.4, 0.35, 0.03), T(LECTERN + Vector((0, 0, 1.06))) @ rot_y(-15))
bm_box(bm, (0.35, 0.35, 0.03), T(LECTERN + Vector((0, 0, 0.015))))
plants.append(new_obj("GH_Lectern", bm, "M_Wood"))
LECTERN_TOP = LECTERN + Vector((0, 0, 1.085))

# Collision proxy: floor, base walls (door closed), benches, boiler, table, palms, lectern
bm = bmesh.new()
bm_box(bm, (2 * L + 0.3, 2 * W + 0.3, 0.1), T((0, 0, -0.05)))
for y in (-W, W):
    bm_box(bm, (2 * L + 0.25, 0.3, 3.0), T((0, y, 1.5)))
for x in (-L, L):
    bm_box(bm, (0.3, 2 * W, 3.0), T((x, 0, 1.5)))
# Benches: only a slab at table height, so you can still see (and reach) what's on the shelf underneath.
for sy in (-1, 1):
    y = sy * (W - 0.75)
    if sy > 0:
        bm_box(bm, (3.4 - (-L + 1.2), 1.0, 0.16), T(((3.4 + (-L + 1.2)) / 2, y, 0.84)))
        bm_box(bm, ((L - 1.6) - 5.1, 1.0, 0.16), T((((L - 1.6) + 5.1) / 2, y, 0.84)))
    else:
        bm_box(bm, (4.9 - (-L + 1.2), 1.0, 0.16), T(((4.9 + (-L + 1.2)) / 2, y, 0.84)))
bm_cyl(bm, 0.5, 1.4, T(BOILER + Vector((0, 0, 0.7))), 12)
bm_cyl(bm, 0.55, 0.85, T(TABLE + Vector((0, 0, 0.42))), 12)
for (px, py) in ((-L + 0.8, W - 0.8), (-L + 0.8, -W + 0.8), (L - 0.9, W - 0.8)):
    bm_cyl(bm, 0.36, 1.0, T((px, py, 0.5)), 8)
bm_box(bm, (0.4, 0.4, 1.1), T(LECTERN + Vector((0, 0, 0.55))))
house.append(new_obj("UBX_Greenhouse_Room", bm, "M_Proxy"))

# ============================================================================ clue props (at the origin)
props = {}

# Lady Agatha's gardening gloves (a pair, lying flat; fingers toward +Y)
g = []
bm = bmesh.new()
for side, ox in ((-1, -0.07), (1, 0.07)):
    base = Vector((ox, 0, 0.012))
    bm_box(bm, (0.085, 0.1, 0.022), T(base) @ rot_z(side * 8))
    bm_box(bm, (0.09, 0.07, 0.024), T(base + Vector((0, -0.08, 0))) @ rot_z(side * 8))  # cuff
    for k, fx in enumerate((-0.03, -0.01, 0.01, 0.03)):
        ln = (0.065, 0.075, 0.072, 0.058)[k]
        bm_rod(bm, base + Vector((fx, 0.05, 0.002)), base + Vector((fx * 1.15, 0.05 + ln, 0.002)), 0.0095, 8)
    bm_rod(bm, base + Vector((side * 0.04, 0.0, 0.002)), base + Vector((side * 0.085, 0.05, 0.002)), 0.011, 8)
g.append(new_obj("Gloves_Leather", bm, "M_Glove"))
props["Prop_Gloves"] = g

# Nicotine wash: tall brown bottle with a paper label and a cork
n = []
bm = bmesh.new()
bm_lathe(bm, [(0.0, 0.0), (0.035, 0.0), (0.038, 0.004), (0.038, 0.13), (0.03, 0.15), (0.013, 0.165), (0.013, 0.19),
              (0.016, 0.193), (0.0, 0.193)], Matrix(), 24)
n.append(new_obj("PoisonBottle_Glass", bm, "M_BrownGlass"))
bm = bmesh.new()
bm_ring(bm, 0.038, 0.0388, 0.06, T((0, 0, 0.07)), 24)
n.append(new_obj("PoisonBottle_Label", bm, "M_Paper"))
bm = bmesh.new()
bm_lathe(bm, [(0.0, 0.185), (0.012, 0.185), (0.0145, 0.212), (0.0, 0.212)], Matrix(), 12)
n.append(new_obj("PoisonBottle_Cork", bm, "M_Cork"))
props["Prop_PoisonBottle"] = n

# Orchid name tag: a little stake with a label board
t = []
bm = bmesh.new()
bm_box(bm, (0.012, 0.004, 0.16), T((0, 0, 0.08)))
t.append(new_obj("OrchidTag_Stake", bm, "M_Wood"))
bm = bmesh.new()
bm_box(bm, (0.09, 0.004, 0.035), T((0, -0.003, 0.15)))
t.append(new_obj("OrchidTag_Label", bm, "M_Paper"))
props["Prop_OrchidTag"] = t

props["Prop_VisitorBook"] = book("VisitorBook", "M_BookGreen", 0.25, 0.33, 0.04)
props["Prop_GardenDiary"] = book("GardenDiary", "M_Leather", 0.15, 0.21, 0.03)
props["Prop_BettingSlip"] = folded_paper("BettingSlip", 0.08, 0.11)

# Muddy boot prints (small, a woman's size) from the chair to the ladder; examined in place
f = []
bm = bmesh.new()
start = CHAIR + Vector((0.3, 0.4, 0))
end = LADDER_FOOT + Vector((0, -0.25, 0))
steps = 9
for k in range(steps):
    tt = k / (steps - 1)
    p = start.lerp(end, tt)
    heading = math.degrees(math.atan2(end.y - start.y, end.x - start.x)) - 90
    side = -1 if k % 2 == 0 else 1
    sole = T(p + Vector((0, 0, 0.004))) @ rot_z(heading) @ T((side * 0.08, 0, 0))
    bm_cyl(bm, 0.045, 0.004, sole @ T((0, 0.04, 0)) @ scale(1, 1.6, 1), 10)
    bm_cyl(bm, 0.035, 0.004, sole @ T((0, -0.07, 0)), 10)
f.append(new_obj("Footprints_Mud", bm, "M_Mud"))
props["Prop_Footprints"] = f

# ============================================================================ export
groups = {"Greenhouse_Room": house, "Greenhouse_Plants": plants}
groups.update(props)
export_groups(groups, EXPORT_DIR)

fmt = lambda v: tuple(round(c, 3) for c in v)
print(f"PLACE table_top={fmt(TABLE_TOP)} moonflower={fmt(MOONFLOWER)} lantern={fmt(LANTERN)} chair={fmt(CHAIR)}")
print(f"PLACE crank={fmt(CRANK)} ladder_foot={fmt(LADDER_FOOT)} lectern_top={fmt(LECTERN_TOP)} boiler_ledge={fmt(BOILER_LEDGE_TOP)}")
print(f"PLACE footprints_mid={fmt(start.lerp(end, 0.5))} boiler={fmt(BOILER)}")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT_ROOT, "SourceArt", "Greenhouse.blend"))
