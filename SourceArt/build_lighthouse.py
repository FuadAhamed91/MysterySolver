"""
The Spooky Crime Scene - Case 2: "The Lighthouse at Gull Point" asset generator.

Run headless:
  blender --background --factory-startup --python SourceArt/build_lighthouse.py -- <unity_project_root>

The lamp room of a storm-lashed lighthouse: a round glazed room on a gallery high above the sea, a rotating
Fresnel lens with light-beam cones, the stair hatch the keeper "fell" down, and the case's clue props.
Blender axes: +Z up, room centred on the origin, stair hatch toward +X.
Unity import maps Blender (x, y, z) -> (-x, z, -y).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from meshkit import (T, MATS, reset_scene, mat, new_obj, bm_box, bm_cyl, bm_sphere, bm_ico, bm_lathe, bm_ring,
                     bm_tube_wall, bm_beam, bm_rod, rot_x, rot_y, rot_z, scale, beam_matrix, circle, bm_ring2d,
                     pocket_watch, folded_paper, book, export_groups)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT_ROOT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(PROJECT_ROOT, "Assets", "Models", "Lighthouse")

reset_scene()
for name, rgb, metal, rough, alpha in (
    ("M_WhitePaint", (0.78, 0.76, 0.7), 0.0, 0.6, 1.0),
    ("M_DeckPlate", (0.12, 0.13, 0.14), 0.6, 0.5, 1.0),
    ("M_StormGlass", (0.5, 0.62, 0.7), 0.0, 0.05, 0.12),
    ("M_Copper", (0.1, 0.3, 0.24), 0.5, 0.5, 1.0),
    ("M_LensGlass", (0.75, 0.9, 1.0), 0.0, 0.0, 0.35),
    ("M_Beam", (1.0, 0.95, 0.75), 0.0, 1.0, 0.06),
    ("M_LampGlow", (1.0, 0.85, 0.5), 0.0, 1.0, 1.0),
    ("M_Sea", (0.01, 0.03, 0.05), 0.0, 0.1, 1.0),
    ("M_Rock", (0.05, 0.05, 0.06), 0.0, 0.9, 1.0),
    ("M_RedPaint", (0.35, 0.05, 0.04), 0.0, 0.6, 1.0),
    ("M_Oilskin", (0.6, 0.45, 0.05), 0.0, 0.35, 1.0),
    ("M_ClayPipe", (0.85, 0.82, 0.74), 0.0, 0.7, 1.0),
    ("M_Iron", (0.08, 0.08, 0.085), 0.9, 0.55, 1.0),
    ("M_Brass", (0.78, 0.56, 0.24), 1.0, 0.32, 1.0),
    ("M_DarkBrass", (0.45, 0.32, 0.14), 1.0, 0.45, 1.0),
    ("M_Wood", (0.3, 0.17, 0.08), 0.0, 0.5, 1.0),
    ("M_Timber", (0.12, 0.075, 0.045), 0.0, 0.75, 1.0),
    ("M_Paper", (0.86, 0.8, 0.66), 0.0, 0.9, 1.0),
    ("M_Ink", (0.02, 0.02, 0.03), 0.0, 0.2, 1.0),
    ("M_Enamel", (0.92, 0.9, 0.84), 0.0, 0.25, 1.0),
    ("M_Leather", (0.18, 0.04, 0.03), 0.0, 0.55, 1.0),
    ("M_BookBlue", (0.04, 0.08, 0.2), 0.0, 0.55, 1.0),
    ("M_Rust", (0.35, 0.12, 0.04), 0.3, 0.2, 1.0),
    ("M_Wax", (0.45, 0.02, 0.02), 0.0, 0.35, 1.0),
    ("M_Proxy", (1.0, 0.0, 1.0), 0.0, 1.0, 1.0),
):
    mat(name, rgb, metal, rough, alpha)

R_IN = 3.4          # inner radius of the lamp room
WALL = 0.35         # parapet thickness
PARAPET = 1.0       # parapet height (glazing starts here)
GLASS_TOP = 3.5
HATCH = Vector((2.2, 0.0))  # stair hatch centre (+X)
HATCH_R = 0.55

# ============================================================================ room
room = []

# Floor deck with the stair hatch cut out
bm = bmesh.new()
outer = circle(R_IN + WALL, 64)
hole = [(HATCH.x + HATCH_R * math.cos(2 * math.pi * i / 24), HATCH.y + HATCH_R * math.sin(2 * math.pi * i / 24)) for i in range(24)]
bm_cyl(bm, R_IN + WALL, 0.15, T((0, 0, -0.075)), 64)
floor = new_obj("LH_Floor", bm, "M_DeckPlate")
cut = bmesh.new()
bm_cyl(cut, HATCH_R, 1.0, T((HATCH.x, HATCH.y, 0)), 24)
cutter = new_obj("cut_hatch", cut, "M_Proxy")
mod = floor.modifiers.new("hatch", 'BOOLEAN')
mod.operation = 'DIFFERENCE'
mod.solver = 'EXACT'
mod.object = cutter
dg = bpy.context.evaluated_depsgraph_get()
me = bpy.data.meshes.new_from_object(floor.evaluated_get(dg))
old = floor.data
floor.modifiers.clear()
floor.data = me
bpy.data.meshes.remove(old)
bpy.data.objects.remove(cutter, do_unlink=True)
room.append(floor)

# Parapet wall + sill
bm = bmesh.new()
bm_tube_wall(bm, R_IN + WALL, WALL, 0.0, PARAPET, Matrix(), 64)
bm_tube_wall(bm, R_IN + WALL + 0.04, WALL + 0.1, PARAPET - 0.06, PARAPET + 0.02, Matrix(), 64)  # sill cap
room.append(new_obj("LH_Parapet", bm, "M_WhitePaint"))

# Glazing frame: mullions and rings
bm = bmesh.new()
MULLIONS = 16
GLASS_R = R_IN + 0.12
for i in range(MULLIONS):
    a = 2 * math.pi * (i + 0.5) / MULLIONS
    p = Vector((math.cos(a), math.sin(a), 0))
    bm_box(bm, (0.07, 0.07, GLASS_TOP - PARAPET), T(p * GLASS_R + Vector((0, 0, (PARAPET + GLASS_TOP) / 2))) @ rot_z(math.degrees(a)))
for z in (PARAPET + 0.02, 2.25, GLASS_TOP):
    bm_ring(bm, GLASS_R - 0.05, GLASS_R + 0.05, 0.07, T((0, 0, z)), 64)
room.append(new_obj("LH_GlazingFrame", bm, "M_Iron"))

# Storm glass: one open cylinder
bm = bmesh.new()
res = bmesh.ops.create_cone(bm, cap_ends=False, segments=MULLIONS * 2, radius1=GLASS_R, radius2=GLASS_R,
                            depth=GLASS_TOP - PARAPET)
bmesh.ops.transform(bm, matrix=T((0, 0, (PARAPET + GLASS_TOP) / 2)), verts=res["verts"])
room.append(new_obj("LH_StormGlass", bm, "M_StormGlass"))

# Dome roof (copper outside, a cornice ring, a ventilator ball on top)
bm = bmesh.new()
dome = [(0.0, 5.35), (0.9, 5.25), (2.0, 4.85), (3.0, 4.2), (3.85, 3.62), (3.9, 3.5), (3.75, 3.5),
        (2.9, 4.08), (1.95, 4.7), (0.88, 5.08), (0.0, 5.18)]
bm_lathe(bm, dome, Matrix(), 48)
bm_sphere(bm, 0.28, T((0, 0, 5.55)), 16, 10)
bm_rod(bm, (0, 0, 5.3), (0, 0, 6.3), 0.03)
room.append(new_obj("LH_Dome", bm, "M_Copper"))
bm = bmesh.new()
bm_ring(bm, R_IN - 0.1, 3.95, 0.16, T((0, 0, GLASS_TOP + 0.04)), 64)
room.append(new_obj("LH_Cornice", bm, "M_Iron"))

# Outside gallery: walkway ring + railing
bm = bmesh.new()
bm_ring(bm, R_IN + WALL - 0.02, 4.75, 0.12, T((0, 0, -0.06)), 64)
room.append(new_obj("LH_GalleryDeck", bm, "M_DeckPlate"))
bm = bmesh.new()
for z in (0.5, 1.0):
    bm_ring(bm, 4.6, 4.66, 0.05, T((0, 0, z)), 64)
for i in range(32):
    a = 2 * math.pi * i / 32
    bm_rod(bm, (4.63 * math.cos(a), 4.63 * math.sin(a), 0.0), (4.63 * math.cos(a), 4.63 * math.sin(a), 1.02), 0.022, 8)
room.append(new_obj("LH_GalleryRail", bm, "M_Iron"))

# The tower falling away beneath the gallery: white with a red band, flaring out to the rocks far below
bm = bmesh.new()
bm_lathe(bm, [(0.0, -0.2), (4.3, -0.2), (4.6, -10.0), (5.1, -28.0), (0.0, -28.0)], Matrix(), 48)
room.append(new_obj("LH_TowerWhite", bm, "M_WhitePaint"))
bm = bmesh.new()
bm_lathe(bm, [(0.0, -10.0), (4.62, -10.0), (4.85, -18.0), (0.0, -18.0)], T((0, 0, 0)) @ scale(1.002, 1.002, 1), 48)
room.append(new_obj("LH_TowerBand", bm, "M_RedPaint"))

# The sea, the rocks ("the Teeth") and the wreck of the Merrow
bm = bmesh.new()
bm_cyl(bm, 600.0, 0.5, T((0, 0, -28.5)), 64)
room.append(new_obj("LH_Sea", bm, "M_Sea"))
bm = bmesh.new()
import random
rng = random.Random(315)
teeth_centre = Vector((-34.0, 26.0, -28.0))
for i in range(11):
    off = Vector((rng.uniform(-9, 9), rng.uniform(-6, 6), 0))
    h = rng.uniform(3, 9)
    r = rng.uniform(1.2, 3.0)
    bm_cyl(bm, r, h, T(teeth_centre + off + Vector((0, 0, h / 2))) @ rot_x(rng.uniform(-12, 12)) @ rot_y(rng.uniform(-12, 12)),
           7, r2=rng.uniform(0.05, 0.4))
for i in range(6):  # rocks at the foot of the tower
    a = rng.uniform(0, 2 * math.pi)
    h = rng.uniform(1.5, 4)
    bm_cyl(bm, rng.uniform(2, 3.5), h, T((7.5 * math.cos(a), 7.5 * math.sin(a), -28 + h / 2)), 6, r2=0.6)
room.append(new_obj("LH_Rocks", bm, "M_Rock"))
bm = bmesh.new()
wreck = teeth_centre + Vector((4.0, -3.0, 0))
hull = T(wreck + Vector((0, 0, 0.6))) @ rot_z(25) @ rot_x(18)
bm_box(bm, (3.0, 11.0, 2.0), hull)
for k, (h, y) in enumerate(((12.0, 2.5), (10.0, -2.5))):
    bm_rod(bm, hull @ Vector((0, y, 0.5)), hull @ Vector((0.0, y + 1.0, h)), 0.16, 8)
    bm_beam(bm, hull @ Vector((-2.2, y + 0.6, h * 0.65)), hull @ Vector((2.2, y + 0.6, h * 0.65)), 0.12, 0.12)
room.append(new_obj("LH_Wreck", bm, "M_Timber"))

# Stair hatch: railing around the opening and the first spiral steps disappearing into the dark
bm = bmesh.new()
for z in (0.5, 0.95):
    bm_ring(bm, HATCH_R + 0.12, HATCH_R + 0.17, 0.045, T((HATCH.x, HATCH.y, z)), 32)
for i in range(10):
    a = 2 * math.pi * i / 10
    p = Vector((HATCH.x + (HATCH_R + 0.145) * math.cos(a), HATCH.y + (HATCH_R + 0.145) * math.sin(a), 0))
    bm_rod(bm, p, p + Vector((0, 0, 0.97)), 0.02, 8)
bm_rod(bm, (HATCH.x, HATCH.y, -2.2), (HATCH.x, HATCH.y, 0.02), 0.06, 12)  # newel post
for k in range(9):
    a = math.radians(-40 - k * 32)
    z = -0.2 - k * 0.2
    mid = Vector((HATCH.x + 0.3 * math.cos(a), HATCH.y + 0.3 * math.sin(a), z))
    bm_box(bm, (0.5, 0.24, 0.04), T(mid) @ rot_z(math.degrees(a)))
room.append(new_obj("LH_StairHatch", bm, "M_Iron"))
bm = bmesh.new()  # the shaft below: a dark cylinder so the hatch looks bottomless
bm_tube_wall(bm, HATCH_R + 0.05, 0.05, -3.0, -0.15, T((HATCH.x, HATCH.y, 0)), 24)
room.append(new_obj("LH_StairShaft", bm, "M_DeckPlate"))

# Keeper's corner: a writing shelf on the parapet, a stool, a coat on a hook, oil barrels
bm = bmesh.new()
desk_a = math.radians(200)
desk_p = Vector((math.cos(desk_a), math.sin(desk_a), 0)) * (R_IN - 0.3)
desk_m = T(desk_p + Vector((0, 0, 0.92))) @ rot_z(math.degrees(desk_a) + 90)
bm_box(bm, (1.1, 0.55, 0.04), desk_m)
for sx in (-0.48, 0.48):
    bm_box(bm, (0.05, 0.05, 0.9), desk_m @ T((sx, -0.2, -0.47)))
stool_p = desk_p + (Vector((0, 0, 0)) - desk_p).normalized() * 0.7
bm_cyl(bm, 0.2, 0.04, T(stool_p + Vector((0, 0, 0.55))), 16)
for i in range(3):
    a = 2 * math.pi * i / 3
    bm_rod(bm, stool_p + Vector((0.14 * math.cos(a), 0.14 * math.sin(a), 0)), stool_p + Vector((0.08 * math.cos(a), 0.08 * math.sin(a), 0.54)), 0.018, 6)
room.append(new_obj("LH_KeeperDesk", bm, "M_Wood"))
DESK_TOP = desk_p + Vector((0, 0, 0.94))

bm = bmesh.new()  # oilskin coat hanging on a hook against a mullion
coat_a = math.radians(140)
coat_p = Vector((math.cos(coat_a), math.sin(coat_a), 0)) * (R_IN - 0.25)
coat_m = T(coat_p) @ rot_z(math.degrees(coat_a) + 90) @ scale(1, 0.55, 1)
bm_lathe(bm, [(0.0, 0.55), (0.3, 0.55), (0.26, 0.9), (0.2, 1.35), (0.22, 1.55), (0.1, 1.62), (0.0, 1.62)], coat_m, 24)
bm_lathe(bm, [(0.0, 1.62), (0.13, 1.62), (0.18, 1.66), (0.12, 1.8), (0.0, 1.82)], coat_m, 20)  # sou'wester
room.append(new_obj("LH_Oilskin", bm, "M_Oilskin"))
bm = bmesh.new()
for k, a_deg in enumerate((70, 86)):
    a = math.radians(a_deg)
    p = Vector((math.cos(a), math.sin(a), 0)) * (R_IN - 0.45)
    bm_lathe(bm, [(0.0, 0.0), (0.27, 0.0), (0.3, 0.12), (0.31, 0.4), (0.3, 0.68), (0.27, 0.8), (0.0, 0.8)], T(p), 20)
room.append(new_obj("LH_OilBarrels", bm, "M_Rust"))
OIL_BARRELS = [Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0)) * (R_IN - 0.45) for a in (70, 86)]

# Collision proxy: floor disc (hatch covered, the railing keeps players out), parapet ring, lamp pedestal
bm = bmesh.new()
bm_cyl(bm, R_IN + WALL, 0.15, T((0, 0, -0.075)), 32)
bm_tube_wall(bm, R_IN + WALL, WALL + 0.05, 0.0, 2.4, Matrix(), 32)
bm_cyl(bm, HATCH_R + 0.17, 1.0, T((HATCH.x, HATCH.y, 0.5)), 16)
bm_cyl(bm, 0.5, 2.6, T((0, 0, 1.3)), 16)
room.append(new_obj("UBX_Lighthouse_Room", bm, "M_Proxy"))

# ============================================================================ the lamp
lamp = []
bm = bmesh.new()  # cast-iron pedestal
bm_lathe(bm, [(0.0, 0.0), (0.5, 0.0), (0.5, 0.08), (0.36, 0.16), (0.3, 0.5), (0.34, 0.8), (0.44, 0.9),
              (0.44, 0.98), (0.0, 0.98)], Matrix(), 32)
lamp.append(new_obj("Lens_Pedestal", bm, "M_Iron"))
# Fuel tank and the valve handwheel (the "fuel valve" clue faces -Y, toward the player's start)
bm = bmesh.new()
tank = Vector((0.0, -0.62, 0.0))
bm_cyl(bm, 0.14, 0.42, T(tank + Vector((0, 0, 0.45))), 20)
bm_rod(bm, tank + Vector((0, 0, 0.66)), Vector((0, -0.3, 0.92)), 0.02)
valve_c = tank + Vector((0, -0.17, 0.5))
bm_ring(bm, 0.07, 0.085, 0.018, T(valve_c) @ rot_x(90), 24)
for k in range(4):
    bm_beam(bm, valve_c, valve_c + Vector((0.075 * math.cos(k * math.pi / 2), 0, 0.075 * math.sin(k * math.pi / 2))), 0.012, 0.012)
bm_rod(bm, valve_c, valve_c + Vector((0, 0.17, 0)), 0.015)
lamp.append(new_obj("Lens_FuelTank", bm, "M_Brass"))
VALVE = valve_c

# Rotating optic: glass prism rings + brass frame (all centred on the Z axis so it spins around its pivot)
LENS_Z0, LENS_Z1, LENS_R = 1.0, 2.55, 0.78
bm = bmesh.new()
bands = 8
for b in range(bands):
    z0 = LENS_Z0 + (LENS_Z1 - LENS_Z0) * b / bands
    hgt = (LENS_Z1 - LENS_Z0) / bands
    bulge = 0.09 * (1.0 - abs(b - (bands - 1) / 2) / bands)
    bm_lathe(bm, [(LENS_R - 0.05, z0 + 0.01), (LENS_R, z0 + 0.01), (LENS_R + bulge, z0 + hgt * 0.55),
                  (LENS_R, z0 + hgt - 0.01), (LENS_R - 0.05, z0 + hgt - 0.01)], Matrix(), 40)
lamp.append(new_obj("Lens_Prisms", bm, "M_LensGlass"))
bm = bmesh.new()
for z in (LENS_Z0, LENS_Z1):
    bm_ring(bm, LENS_R - 0.08, LENS_R + 0.12, 0.08, T((0, 0, z)), 40)
for k in range(8):
    a = 2 * math.pi * k / 8
    p = Vector((math.cos(a), math.sin(a), 0)) * (LENS_R + 0.06)
    bm_rod(bm, p + Vector((0, 0, LENS_Z0)), p + Vector((0, 0, LENS_Z1)), 0.025, 8)
bm_lathe(bm, [(0.0, LENS_Z1), (LENS_R + 0.1, LENS_Z1), (0.25, LENS_Z1 + 0.45), (0.12, LENS_Z1 + 0.6), (0.0, LENS_Z1 + 0.6)],
         Matrix(), 32)  # brass crown
bm_ring(bm, 0.3, 0.5, 0.06, T((0, 0, LENS_Z0 - 0.02)), 32)  # rotating carriage
lamp.append(new_obj("Lens_Frame", bm, "M_Brass"))
bm = bmesh.new()  # burner + glowing mantle inside the lens
bm_cyl(bm, 0.07, 0.4, T((0, 0, LENS_Z0 + 0.3)), 16)
lamp.append(new_obj("Lens_Burner", bm, "M_Iron"))
bm = bmesh.new()
bm_sphere(bm, 0.11, T((0, 0, (LENS_Z0 + LENS_Z1) / 2)) @ scale(1, 1, 1.5), 16, 10)
lamp.append(new_obj("Lens_Flame", bm, "M_LampGlow"))
bm = bmesh.new()  # two light-beam cones, 180 degrees apart, reaching far out over the sea
BEAM_Z = (LENS_Z0 + LENS_Z1) / 2
for d in (1, -1):
    res = bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=0.6, radius2=7.0, depth=60.0)
    bmesh.ops.transform(bm, matrix=T((0, d * 30.6, BEAM_Z)) @ rot_x(-90 * d), verts=res["verts"])
lamp.append(new_obj("Lens_Beams", bm, "M_Beam"))

# ============================================================================ clue props (modelled at the origin)
props = {}

# Spyglass: brass draw tubes with a leather grip, lying on its side (length along +Y)
s = []
bm = bmesh.new()
bm_rod(bm, (0, -0.2, 0.026), (0, 0.02, 0.026), 0.026, 20)
bm_rod(bm, (0, 0.02, 0.026), (0, 0.17, 0.026), 0.021, 20)
bm_rod(bm, (0, 0.17, 0.026), (0, 0.28, 0.026), 0.016, 20)
bm_ring(bm, 0.026, 0.031, 0.02, T((0, -0.2, 0.026)) @ rot_x(90), 20)
bm_ring(bm, 0.016, 0.02, 0.012, T((0, 0.28, 0.026)) @ rot_x(90), 20)
s.append(new_obj("Spyglass_Brass", bm, "M_Brass"))
bm = bmesh.new()
bm_ring(bm, 0.025, 0.029, 0.14, T((0, -0.09, 0.026)) @ rot_x(90), 20)
s.append(new_obj("Spyglass_Grip", bm, "M_Leather"))
props["Prop_Spyglass"] = s

# Matchbook from the Anchor & Lamp tavern + three spent matches
m = []
bm = bmesh.new()
bm_box(bm, (0.045, 0.06, 0.004), T((0, 0, 0.002)))
bm_box(bm, (0.045, 0.018, 0.006), T((0, -0.026, 0.006)))
m.append(new_obj("Matchbook_Cover", bm, "M_RedPaint"))
bm = bmesh.new()
for k, (x, y, a) in enumerate(((0.05, 0.02, 30), (0.065, -0.01, -20), (0.04, -0.035, 75))):
    bm_box(bm, (0.002, 0.042, 0.002), T((x, y, 0.001)) @ rot_z(a))
    bm_sphere(bm, 0.0035, T((x, y, 0.002)) @ rot_z(a) @ T((0, 0.021, 0)), 8, 6)
m.append(new_obj("Matchbook_SpentMatches", bm, "M_Ink"))
props["Prop_Matchbook"] = m

# Clay pipe (bowl + long stem)
c = []
bm = bmesh.new()
bm_lathe(bm, [(0.0, 0.0), (0.012, 0.0), (0.017, 0.012), (0.018, 0.045), (0.014, 0.045), (0.013, 0.012), (0.0, 0.01)],
         T((0, 0.11, 0.0)), 20)
bm_rod(bm, (0, -0.12, 0.012), (0, 0.105, 0.008), 0.0045, 10)
c.append(new_obj("ClayPipe", bm, "M_ClayPipe"))
props["Prop_ClayPipe"] = c

# Silas's watch, glass cracked in the fall, stopped at 11:49
props["Prop_PocketWatch1149"] = pocket_watch("Watch1149", 11, 49)

# Cargo manifest of the Merrow (folded) and the keeper's logbook (blue cloth)
props["Prop_Manifest"] = folded_paper("Manifest", 0.14, 0.19, seal=True)
props["Prop_KeeperLog"] = book("KeeperLog", "M_BookBlue")

# Torn uniform button (examined where it hangs on the hatch railing)
b = []
bm = bmesh.new()
bm_cyl(bm, 0.011, 0.004, T((0, 0, 0.002)), 20)
bm_ring(bm, 0.006, 0.008, 0.0015, T((0, 0, 0.0045)), 16)
b.append(new_obj("Button_Brass", bm, "M_Brass"))
props["Prop_Button"] = b

# ============================================================================ export
groups = {"Lighthouse_Room": room, "Lighthouse_Lamp": lamp}
groups.update(props)
export_groups(groups, EXPORT_DIR)

# Placement data for the Unity scene builder (Blender coordinates)
print(f"PLACE desk_top={tuple(round(v, 3) for v in DESK_TOP)} desk_angle={math.degrees(desk_a):.1f}")
print(f"PLACE valve={tuple(round(v, 3) for v in VALVE)} hatch={tuple(HATCH)}")
print(f"PLACE barrels={[tuple(round(v, 3) for v in p) for p in OIL_BARRELS]}")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT_ROOT, "SourceArt", "Lighthouse.blend"))
