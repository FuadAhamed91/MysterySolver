"""
The 3:15 Escapement - procedural asset generator.

Run headless:
  blender --background --factory-startup --python SourceArt/build_clocktower.py -- <unity_project_root>

Builds the clocktower room, gear/pendulum mechanism and four clue props, applies all
transforms and exports FBX files (forward -Z, up Y) into Assets/Models/Clocktower/.
Blender axes: +Z up, +Y = north (clock face wall), +X = east.
"""
import bpy
import bmesh
import math
import os
import sys
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT_ROOT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(PROJECT_ROOT, "Assets", "Models", "Clocktower")
os.makedirs(EXPORT_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Scene reset
# ---------------------------------------------------------------------------
for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
    for block in list(coll):
        coll.remove(block)

scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

# ---------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------
MATS = {}


def mat(name, rgb, metallic=0.0, rough=0.6, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, alpha)
    m.metallic = metallic
    m.roughness = rough
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Alpha"].default_value = alpha
    MATS[name] = m
    return m


mat("M_Stone", (0.32, 0.30, 0.28), 0.0, 0.85)
mat("M_StoneTrim", (0.22, 0.21, 0.20), 0.0, 0.8)
mat("M_FloorBoards", (0.16, 0.10, 0.06), 0.0, 0.7)
mat("M_Timber", (0.12, 0.075, 0.045), 0.0, 0.75)
mat("M_Iron", (0.08, 0.08, 0.085), 0.9, 0.55)
mat("M_Brass", (0.78, 0.56, 0.24), 1.0, 0.32)
mat("M_DarkBrass", (0.45, 0.32, 0.14), 1.0, 0.45)
mat("M_Wood", (0.30, 0.17, 0.08), 0.0, 0.5)
mat("M_Paper", (0.86, 0.80, 0.66), 0.0, 0.9)
mat("M_Ink", (0.02, 0.02, 0.03), 0.0, 0.2)
mat("M_Enamel", (0.92, 0.90, 0.84), 0.0, 0.25)
mat("M_Glass", (0.35, 0.55, 0.45), 0.0, 0.05, alpha=0.35)
mat("M_Cork", (0.55, 0.40, 0.25), 0.0, 0.9)
mat("M_Proxy", (1.0, 0.0, 1.0), 0.0, 1.0)
mat("M_Leather", (0.18, 0.04, 0.03), 0.0, 0.55)
mat("M_Wax", (0.45, 0.02, 0.02), 0.0, 0.35)
mat("M_Rust", (0.35, 0.12, 0.04), 0.3, 0.2)
mat("M_Tea", (0.12, 0.05, 0.02), 0.0, 0.05)
mat("M_Coat", (0.16, 0.13, 0.09), 0.0, 0.7)
mat("M_Felt", (0.03, 0.03, 0.035), 0.0, 0.8)
mat("M_Skin", (0.5, 0.3, 0.2), 0.0, 0.5)
mat("M_Scarf", (0.22, 0.02, 0.03), 0.0, 0.85)
mat("M_Shirt", (0.7, 0.66, 0.58), 0.0, 0.8)
mat("M_Vest", (0.2, 0.12, 0.06), 0.0, 0.6)
mat("M_CapGreen", (0.06, 0.12, 0.07), 0.0, 0.85)
mat("M_Candle", (0.85, 0.8, 0.62), 0.0, 0.5)

# ---------------------------------------------------------------------------
# Geometry helpers (all geometry is baked in world space; objects keep identity transforms)
# ---------------------------------------------------------------------------


def new_obj(name, bm, material):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MATS[material])
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    return ob


def bm_box(bm, size, matrix):
    res = bmesh.ops.create_cube(bm, size=1.0, calc_uvs=True)
    bmesh.ops.transform(bm, matrix=matrix @ Matrix.Diagonal((*size, 1.0)), verts=res["verts"])


def bm_cyl(bm, r1, depth, matrix, segs=24, r2=None):
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                                radius1=r1, radius2=r1 if r2 is None else r2, depth=depth, calc_uvs=True)
    bmesh.ops.transform(bm, matrix=matrix, verts=res["verts"])


def bm_lathe(bm, profile, matrix, segs=32):
    """Revolve a closed (r, z) profile loop around local Z."""
    rings = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c, s = math.cos(a), math.sin(a)
        rings.append([bm.verts.new(matrix @ Vector((r * c, r * s, z))) for r, z in profile])
    n = len(profile)
    for i in range(segs):
        ra, rb = rings[i], rings[(i + 1) % segs]
        for j in range(n):
            k = (j + 1) % n
            quad = [ra[j], rb[j], rb[k], ra[k]]
            # collapse degenerate axis verts (r == 0) into triangles
            uniq = []
            for v in quad:
                if all((v.co - u.co).length > 1e-7 for u in uniq):
                    uniq.append(v)
            if len(uniq) >= 3:
                try:
                    bm.faces.new(uniq)
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)


def bm_ring2d(bm, outer, inner, z0, z1, matrix):
    """Solid band between two equal-length 2D loops, extruded z0..z1 along local Z."""
    n = len(outer)
    of = [bm.verts.new(matrix @ Vector((x, y, z1))) for x, y in outer]
    ob_ = [bm.verts.new(matrix @ Vector((x, y, z0))) for x, y in outer]
    inf = [bm.verts.new(matrix @ Vector((x, y, z1))) for x, y in inner]
    inb = [bm.verts.new(matrix @ Vector((x, y, z0))) for x, y in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([of[i], of[j], inf[j], inf[i]])
        bm.faces.new([ob_[j], ob_[i], inb[i], inb[j]])
        bm.faces.new([ob_[i], ob_[j], of[j], of[i]])
        bm.faces.new([inb[j], inb[i], inf[i], inf[j]])


def circle(r, n, phase=0.0):
    return [(r * math.cos(phase + 2 * math.pi * i / n), r * math.sin(phase + 2 * math.pi * i / n)) for i in range(n)]


def bm_ring(bm, r_in, r_out, depth, matrix, segs=48):
    bm_ring2d(bm, circle(r_out, segs), circle(r_in, segs), -depth / 2, depth / 2, matrix)


def beam_matrix(p0, p1):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    return Matrix.Translation((p0 + p1) / 2) @ rot, d.length


def bm_beam(bm, p0, p1, w, h):
    m, length = beam_matrix(p0, p1)
    bm_box(bm, (w, h, length), m)


def bm_rod(bm, p0, p1, r, segs=12):
    m, length = beam_matrix(p0, p1)
    bm_cyl(bm, r, length, m, segs)


def boolean_diff(target, cutters):
    for i, c in enumerate(cutters):
        mod = target.modifiers.new(f"cut{i}", 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.solver = 'EXACT'
        mod.object = c
    dg = bpy.context.evaluated_depsgraph_get()
    new_me = bpy.data.meshes.new_from_object(target.evaluated_get(dg))
    old = target.data
    target.modifiers.clear()
    target.data = new_me
    bpy.data.meshes.remove(old)
    for c in cutters:
        bpy.data.objects.remove(c, do_unlink=True)


def rot_x(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'X')


def rot_y(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Y')


def rot_z(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Z')


T = Matrix.Translation

# ---------------------------------------------------------------------------
# 1. Architecture
# ---------------------------------------------------------------------------
R = 6.0
H = 4.5
WALL_T = 0.35
APOTHEM = R * math.cos(math.radians(22.5))
SIDE = 2 * R * math.sin(math.radians(22.5))
WALL_DIRS = {i: 90.0 - 45.0 * i for i in range(8)}  # 0=N, 2=E, 4=S, 6=W
room_objs = []


def wall_matrix(i, extra_out=0.0):
    ang = math.radians(WALL_DIRS[i])
    rc = APOTHEM + WALL_T / 2 + extra_out
    return T((rc * math.cos(ang), rc * math.sin(ang), 0.0)) @ rot_z(WALL_DIRS[i] - 90.0)


# Walls (local: X along wall, Y = outward normal, Z up).
# Each wall is its own closed box so the EXACT booleans never see overlapping (self-intersecting) input.
wall_objs = []
for i in range(8):
    bm = bmesh.new()
    bm_box(bm, (SIDE + 0.3, WALL_T, H), wall_matrix(i) @ T((0, 0, H / 2)))
    wall_objs.append(new_obj(f"Wall_{i}", bm, "M_Stone"))
bm = bmesh.new()
for i in range(8):
    bm_box(bm, (SIDE + 0.55, 0.3, 0.8), wall_matrix(i, 0.12) @ T((0, 0, H + 0.3)))  # eave band, seals roof line
wall_objs.append(new_obj("Wall_Eaves", bm, "M_Stone"))

# North clock opening: 4.0 m diameter circle
CLOCK_Z = 2.35
cbm = bmesh.new()
bm_cyl(cbm, 2.0, 2.0, wall_matrix(0) @ T((0, 0, CLOCK_Z)) @ rot_x(90), segs=64)
boolean_diff(wall_objs[0], [new_obj("cut_clock", cbm, "M_Proxy")])


# East / West Gothic lancets (equilateral pointed arch)
def lancet_profile(w=1.1, sill=0.9, spring=2.9, segs=10):
    pts = [(-w / 2, sill), (w / 2, sill), (w / 2, spring)]
    for k in range(1, segs + 1):  # right arc, centre at (-w/2, spring)
        a = math.radians(60.0 * k / segs)
        pts.append((-w / 2 + w * math.cos(a), spring + w * math.sin(a)))
    for k in range(1, segs):  # left arc, centre at (w/2, spring)
        a = math.radians(120.0 + 60.0 * k / segs)
        pts.append((w / 2 + w * math.cos(a), spring + w * math.sin(a)))
    pts.append((-w / 2, spring))
    return pts


def prism(bm, pts, m, half=1.0):
    front = [bm.verts.new(m @ Vector((u, -half, v))) for u, v in pts]
    back = [bm.verts.new(m @ Vector((u, half, v))) for u, v in pts]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([front[i], back[i], back[j], front[j]])


for i in (2, 6):
    lbm = bmesh.new()
    prism(lbm, lancet_profile(), wall_matrix(i))
    boolean_diff(wall_objs[i], [new_obj(f"cut_lancet_{i}", lbm, "M_Proxy")])

for o in bpy.data.objects:
    o.select_set(o in wall_objs)
bpy.context.view_layer.objects.active = wall_objs[0]
bpy.ops.object.join()
walls = wall_objs[0]
walls.name = walls.data.name = "Clocktower_Walls"
room_objs.append(walls)

# Plinth + cornice trim
bm = bmesh.new()
for i in range(8):
    bm_box(bm, (SIDE + 0.25, 0.12, 0.28), wall_matrix(i, -WALL_T / 2 - 0.06 + 0.001) @ T((0, 0, 0.14)))
    bm_box(bm, (SIDE + 0.45, 0.22, 0.18), wall_matrix(i, -WALL_T / 2 - 0.05) @ T((0, 0, H - 0.09)))
# Lancet sills
for i in (2, 6):
    bm_box(bm, (1.3, 0.5, 0.08), wall_matrix(i) @ T((0, -0.05, 0.86)))
room_objs.append(new_obj("Clocktower_Trim", bm, "M_StoneTrim"))

# Floor
bm = bmesh.new()
bm_cyl(bm, R + 0.45, 0.2, T((0, 0, -0.1)) @ rot_z(22.5), segs=8)
room_objs.append(new_obj("Clocktower_Floor", bm, "M_FloorBoards"))

# Open timber truss
APEX = 8.2
PLATE_Z = H + 0.12
bm = bmesh.new()
corners = [Vector((R * math.cos(math.radians(22.5 + 45 * k)), R * math.sin(math.radians(22.5 + 45 * k)), PLATE_Z))
           for k in range(8)]
apex = Vector((0, 0, APEX))
for k in range(8):
    a, b = corners[k], corners[(k + 1) % 8]
    bm_beam(bm, a, b, 0.28, 0.24)                       # wall plate
    bm_beam(bm, a, apex, 0.18, 0.22)                    # hip rafters
    t = 0.45                                            # collar ring
    bm_beam(bm, a.lerp(apex, t), b.lerp(apex, t), 0.12, 0.16)
# tie beams N-S / E-W and diagonals
for ang in (0, 90, 45, 135):
    d = Vector((math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0)) * (APOTHEM + 0.1)
    z = PLATE_Z + (0.0 if ang in (0, 90) else 0.25)
    bm_beam(bm, (-d.x, -d.y, z), (d.x, d.y, z), 0.22, 0.24)
# king post + struts
bm_beam(bm, (0, 0, PLATE_Z), (0, 0, APEX), 0.26, 0.26)
for k in range(0, 8, 2):
    bm_beam(bm, (0, 0, PLATE_Z + 0.9), corners[k].lerp(apex, 0.35), 0.12, 0.12)
room_objs.append(new_obj("Clocktower_Truss", bm, "M_Timber"))

# Roof shell (thick, so it renders and casts shadows from both sides)
bm = bmesh.new()
shell_prof = [(0.0, APEX + 0.25), (0.0, APEX + 0.45), (R + 0.75, H + 0.45), (R + 0.75, H + 0.25)]
bm_lathe(bm, shell_prof, rot_z(22.5), segs=8)
room_objs.append(new_obj("Clocktower_RoofShell", bm, "M_Timber"))

# Skeleton clock dial filling the north opening (seen from inside, hands at 3:15)
dial_m = wall_matrix(0) @ T((0, 0.05, CLOCK_Z)) @ rot_x(90)  # local Y = up, local Z = inward (south)
bm = bmesh.new()
bm_ring(bm, 1.86, 2.02, 0.18, dial_m, 96)
bm_ring(bm, 1.30, 1.38, 0.08, dial_m, 64)
bm_cyl(bm, 0.22, 0.2, dial_m, 32)
for h in range(12):
    a = math.radians(30 * h)
    bm_box(bm, (0.035, 0.035, 1.66), dial_m @ rot_z(math.degrees(a)) @ T((0, 1.04, 0)) @ rot_x(90))
room_objs.append(new_obj("Clocktower_ClockDial", bm, "M_Iron"))
bm = bmesh.new()
for h in range(12):
    bm_box(bm, (0.12, 0.34 if h % 3 == 0 else 0.22, 0.05),
           dial_m @ rot_z(30 * h) @ T((0, 1.66, 0.08)))


def hand_matrix(angle_cw_from_12_outside, length, z):
    # Local +Z faces the inside viewer, so +rot_z is CCW inside == clockwise for townsfolk outside.
    return dial_m @ rot_z(angle_cw_from_12_outside) @ T((0, length / 2 - 0.15, z))


bm_box(bm, (0.1, 1.2, 0.04), hand_matrix(97.5, 1.2, 0.14))   # hour hand  (3:15)
bm_box(bm, (0.07, 1.7, 0.04), hand_matrix(90.0, 1.7, 0.19))  # minute hand
room_objs.append(new_obj("Clocktower_ClockHands", bm, "M_DarkBrass"))

# Collision proxy: floor slab + 8 solid wall boxes (all convex pieces)
bm = bmesh.new()
bm_cyl(bm, R + 0.45, 0.2, T((0, 0, -0.1)) @ rot_z(22.5), segs=8)
for i in range(8):
    bm_box(bm, (SIDE + 0.3, WALL_T, H), wall_matrix(i) @ T((0, 0, H / 2)))
room_objs.append(new_obj("UBX_Clocktower_Room", bm, "M_Proxy"))

# ---------------------------------------------------------------------------
# 2. Mechanism: three meshing spur gears + collapsed pendulum
# ---------------------------------------------------------------------------
mech_objs = []
GEAR_PLANE_Y = 2.5
GEAR_T = 0.12
MODULE = 0.1  # 2r/n is identical for all three gears, so they mesh
gears = [  # name, pitch radius, teeth, centre (x, z), spokes
    ("Gear_Escapement_24T", 1.2, 24, (0.0, 2.7), 6),
    ("Gear_Intermediate_16T", 0.8, 16, (-1.6, 1.5), 5),
    ("Gear_Pinion_8T", 0.4, 8, (-2.32, 2.46), 0),
]


def gear_outline(r, n, phase):
    ro, rr = r + MODULE, r - 1.25 * MODULE
    P = 2 * math.pi / n
    pts = []
    for k in range(n):
        c = phase + k * P
        for rad, off in ((rr, -0.30), (ro, -0.13), (ro, 0.13), (rr, 0.30)):
            pts.append((rad * math.cos(c + off * P), rad * math.sin(c + off * P)))
    return pts


def ang2(a, b):
    return math.atan2(b[1] - a[1], b[0] - a[0])


# Tooth phases so neighbouring gears interleave (tooth faces gap along the centre line)
phases = {}
g0, g1, g2 = gears
phases[0] = ang2(g0[3], g1[3])
phases[1] = ang2(g1[3], g0[3]) + math.pi / g1[2]
P1 = 2 * math.pi / g1[2]
f = ((ang2(g1[3], g2[3]) - phases[1]) / P1) % 1.0
phases[2] = ang2(g2[3], g1[3]) - ((0.5 - f) % 1.0) * (2 * math.pi / g2[2])

for gi, (name, r, n, (cx, cz), spokes) in enumerate(gears):
    m = T((cx, GEAR_PLANE_Y, cz)) @ rot_x(90)  # local X/Y -> world X/Z, so phases match ang2()
    bm = bmesh.new()
    outline = gear_outline(r, n, phases[gi])
    rim_in = r - 1.25 * MODULE - (0.12 if spokes else r - 1.25 * MODULE - 0.07)
    inner = []
    for x, y in outline:
        a = math.atan2(y, x)
        inner.append((rim_in * math.cos(a), rim_in * math.sin(a)))
    bm_ring2d(bm, outline, inner, -GEAR_T / 2, GEAR_T / 2, m)
    bm_ring(bm, 0.05, 0.16 if spokes else 0.09, GEAR_T + 0.06, m, 24)
    for s in range(spokes):
        a = 360.0 * s / spokes + math.degrees(phases[gi])
        bm_box(bm, (rim_in - 0.1, 0.07, GEAR_T * 0.7), m @ rot_z(a) @ T(((rim_in + 0.12) / 2, 0, 0)))
    mech_objs.append(new_obj(name, bm, "M_Brass"))

# Cast-iron frame, axles, bearings and the escapement anchor
bm = bmesh.new()
FRAME_Y = GEAR_PLANE_Y + 0.42
for x in (1.55, -3.05):
    bm_box(bm, (0.16, 0.16, 4.35), T((x, FRAME_Y, 4.35 / 2)))
    bm_box(bm, (0.5, 0.6, 0.08), T((x, FRAME_Y, 0.04)))
for z in (2.7, 1.5, 3.95):
    bm_box(bm, (4.75, 0.14, 0.14), T((-0.75, FRAME_Y, z)))
bm_box(bm, (0.14, 0.14, 0.95), T((-2.32, FRAME_Y, 2.46 - 0.4)))  # pinion bracket
for _, r, n, (cx, cz), _s in gears:
    bm_rod(bm, (cx, GEAR_PLANE_Y - 0.12, cz), (cx, FRAME_Y + 0.08, cz), 0.05, 16)
    bm_cyl(bm, 0.1, 0.12, T((cx, GEAR_PLANE_Y - 0.12, cz)) @ rot_x(90), 16)
# Anchor / pallet arbour above the escapement wheel
anchor_c = Vector((0.0, GEAR_PLANE_Y - 0.02, 4.12))
bm_rod(bm, (0.0, GEAR_PLANE_Y - 0.12, 4.12), (0.0, FRAME_Y, 4.12), 0.04, 12)
for side in (-1, 1):
    tip = anchor_c + Vector((side * 0.75, 0, -0.42))
    bm_beam(bm, anchor_c, tip, 0.08, 0.06)
    bm_box(bm, (0.08, 0.08, 0.16), T(tip) @ T((0, 0, -0.06)))
# Snapped crutch where the pendulum used to hang
bm_beam(bm, (0.0, GEAR_PLANE_Y - 0.16, 4.12), (0.06, GEAR_PLANE_Y - 0.16, 3.7), 0.05, 0.03)
mech_objs.append(new_obj("Mechanism_Frame", bm, "M_Iron"))

# Collapsed pendulum: 3.0 m arm at 30 degrees, propped across the escapement wheel, bob on the floor
ARM_LEN = 3.0
BOB_R = 0.35
bob_c = Vector((1.3, 0.05, BOB_R))
arm_dir = Vector((-math.sin(math.radians(30)) * math.cos(math.radians(30)),
                  math.cos(math.radians(30)) * math.cos(math.radians(30)),
                  math.sin(math.radians(30)))).normalized()
arm_top = bob_c + arm_dir * ARM_LEN
bm = bmesh.new()
bm_rod(bm, bob_c, arm_top, 0.028, 16)
bm_box(bm, (0.12, 0.02, 0.22), beam_matrix(arm_top - arm_dir * 0.1, arm_top)[0])  # suspension spring block
bm_rod(bm, arm_top - arm_dir * 0.35, arm_top - arm_dir * 0.2, 0.045, 12)          # rating nut
mech_objs.append(new_obj("Pendulum_Arm", bm, "M_DarkBrass"))
bob_axis = arm_dir.cross(Vector((0, 0, 1))).normalized()
bob_rot = Vector((0, 0, 1)).rotation_difference(bob_axis).to_matrix().to_4x4()
bm = bmesh.new()
bob_prof = [(0.0, -0.09), (BOB_R - 0.03, -0.09), (BOB_R, -0.05), (BOB_R, 0.05), (BOB_R - 0.03, 0.09), (0.0, 0.09)]
bm_lathe(bm, bob_prof, T(bob_c) @ bob_rot, 48)
mech_objs.append(new_obj("Pendulum_Bob", bm, "M_Brass"))

# ---------------------------------------------------------------------------
# 3. Clue props (modelled at the origin, base on Z = 0, front facing -Y)
# ---------------------------------------------------------------------------
props = {}

# -- Pocket watch, frozen at 3:03 ------------------------------------------------
w = []
bm = bmesh.new()
case_prof = [(0.0, 0.0), (0.019, 0.0), (0.0235, 0.002), (0.0255, 0.0055), (0.0245, 0.009), (0.0225, 0.0108), (0.0, 0.0108)]
bm_lathe(bm, case_prof, Matrix(), 40)
bm_ring(bm, 0.0205, 0.0238, 0.0035, T((0, 0, 0.0118)), 40)                          # bezel
bm_cyl(bm, 0.0042, 0.006, T((0, 0.0285, 0.0055)) @ rot_x(90), 16)                   # crown
bm_ring(bm, 0.0045, 0.0065, 0.0022, T((0, 0.0355, 0.0055)) @ rot_y(90), 20)          # bow
w.append(new_obj("PocketWatch_Case", bm, "M_Brass"))
bm = bmesh.new()
bm_cyl(bm, 0.021, 0.0008, T((0, 0, 0.0112)), 40)
w.append(new_obj("PocketWatch_Dial", bm, "M_Enamel"))
bm = bmesh.new()
for h in range(12):
    bm_box(bm, (0.0012, 0.0035 if h % 3 == 0 else 0.002, 0.0003), rot_z(-30 * h) @ T((0, 0.0175, 0.0118)))


def watch_hand(deg_cw, length, width, z):
    return rot_z(-deg_cw) @ T((0, length / 2 - 0.002, z)), (width, length, 0.0004)


for deg, length, width, z in ((91.5, 0.012, 0.0016, 0.0121), (18.0, 0.0175, 0.0011, 0.0125)):
    m_, s_ = watch_hand(deg, length, width, z)
    bm_box(bm, s_, m_)
bm_cyl(bm, 0.0012, 0.0012, T((0, 0, 0.0126)), 12)
w.append(new_obj("PocketWatch_Hands", bm, "M_Ink"))
props["Prop_PocketWatch"] = w

# -- Gooseneck desk lamp ---------------------------------------------------------
l = []
bm = bmesh.new()
base_prof = [(0.0, 0.0), (0.075, 0.0), (0.075, 0.012), (0.062, 0.022), (0.03, 0.03), (0.022, 0.045), (0.0, 0.045)]
bm_lathe(bm, base_prof, Matrix(), 40)
bm_cyl(bm, 0.012, 0.08, T((0, 0, 0.085)), 16)  # stem
# gooseneck: sampled bezier swept as short rod segments with collars
from mathutils.geometry import interpolate_bezier
k0, h0, h1, k1 = Vector((0, 0, 0.12)), Vector((0, 0, 0.36)), Vector((0, -0.05, 0.46)), Vector((0, -0.2, 0.40))
pts = interpolate_bezier(k0, h0, h1, k1, 18)
for i in range(len(pts) - 1):
    bm_rod(bm, pts[i], pts[i + 1], 0.0085, 12)
    if i % 3 == 0:
        bm_cyl(bm, 0.0115, 0.008, beam_matrix(pts[i], pts[i + 1])[0], 14)
l.append(new_obj("DeskLamp_Body", bm, "M_Brass"))
# adjustable cowl on a knuckle, aimed down-forward
neck_dir = (pts[-1] - pts[-2]).normalized()
cowl_m = T(pts[-1]) @ Vector((0, 0, -1)).rotation_difference(Vector((0, -0.55, -1)).normalized()).to_matrix().to_4x4()
bm = bmesh.new()
bm_cyl(bm, 0.016, 0.03, T(pts[-1]) @ rot_y(90), 16)  # knuckle
cowl_prof = [(0.022, 0.0), (0.075, -0.1), (0.078, -0.1), (0.026, 0.004), (0.0, 0.004), (0.0, 0.0)]
bm_lathe(bm, cowl_prof, cowl_m @ T((0, 0, -0.01)), 40)
l.append(new_obj("DeskLamp_Cowl", bm, "M_DarkBrass"))
bm = bmesh.new()
bm_ring(bm, 0.012, 0.018, 0.022, cowl_m @ T((0, 0, -0.045)), 24)  # wick collar
bm_cyl(bm, 0.004, 0.018, cowl_m @ T((0, 0, -0.062)), 10)          # wick
l.append(new_obj("DeskLamp_WickCollar", bm, "M_Iron"))
props["Prop_DeskLamp"] = l

# -- Drafting desk -----------------------------------------------------------------
d = []
DW, DD = 1.3, 0.9
FRONT_Y, BOARD_END_Y, BACK_Y = -0.45, 0.13, 0.45
FRONT_Z, SHELF_Z = 0.80, 0.97
SLOPE = math.radians(15)
board_len = (BOARD_END_Y - FRONT_Y) / math.cos(SLOPE)
bm = bmesh.new()
board_mid = Vector((0, (FRONT_Y + BOARD_END_Y) / 2, FRONT_Z + (BOARD_END_Y - FRONT_Y) / 2 * math.tan(SLOPE) - 0.015))
bm_box(bm, (DW, board_len, 0.03), T(board_mid) @ rot_x(15))
bm_box(bm, (DW, 0.025, 0.035), T((0, FRONT_Y - 0.005, FRONT_Z + 0.005)))  # pencil ledge
bm_box(bm, (DW, BACK_Y - BOARD_END_Y, 0.03), T((0, (BOARD_END_Y + BACK_Y) / 2, SHELF_Z)))  # rear shelf
for x in (-DW / 2 + 0.04, DW / 2 - 0.04):
    bm_box(bm, (0.05, 0.05, FRONT_Z - 0.03), T((x, FRONT_Y + 0.05, (FRONT_Z - 0.03) / 2)))
    bm_box(bm, (0.05, 0.05, SHELF_Z - 0.015), T((x, BACK_Y - 0.04, (SHELF_Z - 0.015) / 2)))
    bm_box(bm, (0.04, DD - 0.1, 0.05), T((x, 0.0, 0.18)))   # side stretchers
    bm_box(bm, (0.03, DD - 0.06, 0.1), T((x, 0.0, FRONT_Z - 0.09)))  # side aprons
bm_box(bm, (DW - 0.08, 0.04, 0.05), T((0, BACK_Y - 0.04, 0.18)))
# Pigeonhole cabinet (left half of the rear shelf)
CAB_X0, CAB_X1, CAB_Y0, CAB_Y1, CAB_H = -0.63, 0.03, 0.2, BACK_Y, 0.36
cz0 = SHELF_Z + 0.015
cx = (CAB_X0 + CAB_X1) / 2
cy = (CAB_Y0 + CAB_Y1) / 2
bm_box(bm, (CAB_X1 - CAB_X0, 0.015, CAB_H), T((cx, CAB_Y1 - 0.0075, cz0 + CAB_H / 2)))     # back
bm_box(bm, (CAB_X1 - CAB_X0, CAB_Y1 - CAB_Y0, 0.015), T((cx, cy, cz0 + CAB_H)))             # top
bm_box(bm, (CAB_X1 - CAB_X0, CAB_Y1 - CAB_Y0, 0.012), T((cx, cy, cz0 + CAB_H / 2)))         # mid shelf
for k in range(4):
    x = CAB_X0 + (CAB_X1 - CAB_X0) * k / 3
    bm_box(bm, (0.015, CAB_Y1 - CAB_Y0, CAB_H), T((x, cy, cz0 + CAB_H / 2)))
d.append(new_obj("DraftingDesk_Wood", bm, "M_Wood"))
# Papers: a drafting sheet on the board and rolled letters in the pigeonholes
bm = bmesh.new()
bm_box(bm, (0.56, 0.42, 0.002), T(board_mid + Vector((0.18, 0.0, 0.017))) @ rot_x(15) @ rot_z(-4))
for k, (px, pz) in enumerate(((-0.52, 0.06), (-0.31, 0.06), (-0.12, 0.24), (-0.52, 0.24))):
    bm_cyl(bm, 0.018, 0.2, T((px, cy - 0.01, cz0 + pz + 0.02)) @ rot_x(90), 12)
d.append(new_obj("DraftingDesk_Papers", bm, "M_Paper"))
# Inkwell + quill
bm = bmesh.new()
ink_prof = [(0.0, 0.0), (0.032, 0.0), (0.034, 0.03), (0.014, 0.045), (0.011, 0.055), (0.0, 0.055)]
bm_lathe(bm, ink_prof, T((-0.28, 0.16, SHELF_Z + 0.015)), 24)
bm_rod(bm, (-0.28, 0.16, SHELF_Z + 0.05), (-0.25, 0.10, SHELF_Z + 0.25), 0.003, 8)
d.append(new_obj("DraftingDesk_Inkwell", bm, "M_Ink"))
props["Prop_DraftingDesk"] = d

# -- Apothecary vial ---------------------------------------------------------------
v = []
bm = bmesh.new()
vial_prof = [(0.0, 0.0), (0.016, 0.0), (0.0185, 0.003), (0.0185, 0.058), (0.015, 0.068), (0.0075, 0.073),
             (0.0075, 0.086), (0.0092, 0.089), (0.0, 0.089)]
bm_lathe(bm, vial_prof, Matrix(), 32)
v.append(new_obj("ApothecaryVial_Glass", bm, "M_Glass"))
bm = bmesh.new()
bm_lathe(bm, [(0.0, 0.08), (0.0068, 0.08), (0.0082, 0.104), (0.0, 0.104)], Matrix(), 20)
v.append(new_obj("ApothecaryVial_Cork", bm, "M_Cork"))
bm = bmesh.new()
bm_ring(bm, 0.0185, 0.0192, 0.03, T((0, 0, 0.032)), 32)
v.append(new_obj("ApothecaryVial_Label", bm, "M_Paper"))
props["Prop_ApothecaryVial"] = v

# -- Teacup + saucer (cold tea with a bitter film) ----------------------------------
t = []
bm = bmesh.new()
saucer_prof = [(0.0, 0.0), (0.045, 0.0), (0.072, 0.008), (0.074, 0.012), (0.05, 0.007), (0.0, 0.007)]
bm_lathe(bm, saucer_prof, Matrix(), 40)
cup_prof = [(0.0, 0.007), (0.026, 0.007), (0.03, 0.012), (0.042, 0.05), (0.044, 0.066), (0.041, 0.066),
            (0.039, 0.05), (0.026, 0.017), (0.0, 0.017)]
bm_lathe(bm, cup_prof, Matrix(), 40)
bm_ring(bm, 0.011, 0.016, 0.006, T((0.052, 0, 0.043)) @ rot_x(90), 20)  # handle
t.append(new_obj("Teacup_Porcelain", bm, "M_Enamel"))
bm = bmesh.new()
bm_cyl(bm, 0.037, 0.002, T((0, 0, 0.04)), 32)
t.append(new_obj("Teacup_Tea", bm, "M_Tea"))
props["Prop_Teacup"] = t

# -- Night watchman's logbook ---------------------------------------------------------
g = []
bm = bmesh.new()
bm_box(bm, (0.23, 0.31, 0.006), T((0, 0, 0.003)))
bm_box(bm, (0.23, 0.31, 0.006), T((0, 0, 0.041)))
bm_box(bm, (0.012, 0.31, 0.044), T((-0.112, 0, 0.022)))  # spine
g.append(new_obj("Logbook_Cover", bm, "M_Leather"))
bm = bmesh.new()
bm_box(bm, (0.215, 0.296, 0.032), T((0.004, 0, 0.022)))
g.append(new_obj("Logbook_Pages", bm, "M_Paper"))
props["Prop_Logbook"] = g


# -- Folded letter with wax seal / prescription slip ---------------------------------
def folded_paper(prefix, w, h, seal):
    objs = []
    bm = bmesh.new()
    for side in (-1, 1):  # two halves tented slightly along the fold
        bm_box(bm, (w / 2, h, 0.0012), T((side * w / 4 * math.cos(math.radians(6)), 0, 0.0006 + w / 4 * math.sin(math.radians(6))))
               @ rot_y(-side * 6))
    objs.append(new_obj(prefix + "_Paper", bm, "M_Paper"))
    if seal:
        bm = bmesh.new()
        bm_cyl(bm, 0.012, 0.004, T((w / 4, 0, w / 4 * math.sin(math.radians(6)) + 0.003)) @ rot_y(-6), 20)
        objs.append(new_obj(prefix + "_Seal", bm, "M_Wax"))
    return objs


props["Prop_Letter"] = folded_paper("Letter", 0.15, 0.2, True)
props["Prop_Prescription"] = folded_paper("Prescription", 0.09, 0.12, False)

# -- Rusted chisel engraved T. CRANE -------------------------------------------------
c = []
bm = bmesh.new()
bm_cyl(bm, 0.014, 0.11, T((0, -0.055, 0.014)) @ rot_x(90), 16)
c.append(new_obj("Chisel_Handle", bm, "M_Wood"))
bm = bmesh.new()
bm_ring(bm, 0.0, 0.0155, 0.014, T((0, 0.003, 0.014)) @ rot_x(90), 16)
c.append(new_obj("Chisel_Ferrule", bm, "M_Brass"))
bm = bmesh.new()
bm_box(bm, (0.014, 0.11, 0.005), T((0, 0.065, 0.006)))
bm_box(bm, (0.016, 0.012, 0.003), T((0, 0.124, 0.004)) @ rot_x(-12))
c.append(new_obj("Chisel_Blade", bm, "M_Rust"))
props["Prop_Chisel"] = c

# -- "Wren", the informant: trench coat, fedora, scarf. Head is a separate object so Unity can turn it. --
n = []
coat_squash = Matrix.Diagonal((1.0, 0.68, 1.0, 1.0))
bm = bmesh.new()
coat_prof = [(0.0, 0.42), (0.3, 0.42), (0.28, 0.62), (0.235, 0.95), (0.205, 1.12), (0.225, 1.32),
             (0.2, 1.4), (0.1, 1.46), (0.0, 1.46)]
bm_lathe(bm, coat_prof, coat_squash, 32)
for side in (-1, 1):  # sleeves, hanging slightly forward
    bm_rod(bm, (side * 0.235, 0.0, 1.36), (side * 0.27, -0.06, 0.88), 0.058, 14)
    bm_cyl(bm, 0.064, 0.05, beam_matrix(Vector((side * 0.265, -0.055, 0.92)), Vector((side * 0.27, -0.06, 0.88)))[0], 14)
# popped collar
bm_lathe(bm, [(0.1, 1.38), (0.155, 1.38), (0.17, 1.55), (0.15, 1.55), (0.12, 1.42), (0.1, 1.42)], coat_squash, 24)
n.append(new_obj("Wren_Coat", bm, "M_Coat"))

bm = bmesh.new()
for side in (-1, 1):
    bm_rod(bm, (side * 0.1, 0.0, 0.05), (side * 0.1, 0.0, 0.5), 0.065, 12)
    bm_box(bm, (0.1, 0.24, 0.07), T((side * 0.1, -0.05, 0.035)))  # shoes
n.append(new_obj("Wren_Legs", bm, "M_Felt"))

bm = bmesh.new()
for side in (-1, 1):  # gloved hands
    res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.05)
    bmesh.ops.transform(bm, matrix=T((side * 0.275, -0.07, 0.83)) @ Matrix.Diagonal((0.8, 1.0, 1.25, 1.0)), verts=res["verts"])
n.append(new_obj("Wren_Gloves", bm, "M_Felt"))

# Head group (pivot: neck at z = 1.46): head, scarf, fedora
bm = bmesh.new()
res = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=0.105)
bmesh.ops.transform(bm, matrix=T((0, 0, 1.6)) @ Matrix.Diagonal((0.92, 1.0, 1.12, 1.0)), verts=res["verts"])
bm_cyl(bm, 0.05, 0.12, T((0, 0, 1.49)), 12)  # neck
n.append(new_obj("Wren_Head", bm, "M_Skin"))
bm = bmesh.new()
bm_lathe(bm, [(0.0, 1.47), (0.12, 1.47), (0.112, 1.6), (0.0, 1.6)], T((0, -0.012, 0)), 24)  # scarf over the lower face
n.append(new_obj("Wren_Scarf", bm, "M_Scarf"))
bm = bmesh.new()
hat = T((0, 0.0, 0.0)) @ T((0, 0, 1.66)) @ rot_x(-6)  # brim tipped down over the eyes
bm_lathe(bm, [(0.0, 0.0), (0.21, 0.0), (0.215, 0.012), (0.0, 0.012)], hat, 32)                  # brim
bm_lathe(bm, [(0.0, 0.0), (0.112, 0.0), (0.118, 0.07), (0.1, 0.115), (0.04, 0.125), (0.0, 0.11)],
         hat @ T((0, 0, 0.01)), 32)                                                          # crown with pinch
n.append(new_obj("Wren_Fedora", bm, "M_Felt"))
bm = bmesh.new()
bm_ring(bm, 0.113, 0.121, 0.025, hat @ T((0, 0, 0.03)), 32)
n.append(new_obj("Wren_HatBand", bm, "M_Scarf"))
props["NPC_Informant"] = n

# -- "Finch", the messenger kid: shirt sleeves, waistcoat, flat cap, holding a candle stub. ----------------
f = []
k = 0.86  # shorter than Wren
squash = Matrix.Diagonal((1.0, 0.7, 1.0, 1.0))
bm = bmesh.new()
for side in (-1, 1):
    bm_rod(bm, (side * 0.085, 0.0, 0.05), (side * 0.085, 0.0, 0.52 * k / 0.86 * 0.86), 0.06, 12)  # trousers
    bm_box(bm, (0.09, 0.22, 0.06), T((side * 0.085, -0.04, 0.03)))
f.append(new_obj("Finch_Legs", bm, "M_Felt"))
bm = bmesh.new()
torso = [(0.0, 0.5), (0.17, 0.5), (0.18, 0.75), (0.19, 1.0), (0.17, 1.17), (0.08, 1.24), (0.0, 1.24)]
bm_lathe(bm, torso, squash, 28)
f.append(new_obj("Finch_Shirt", bm, "M_Shirt"))
bm = bmesh.new()
vest = [(0.0, 0.62), (0.188, 0.62), (0.196, 0.85), (0.2, 1.02), (0.12, 1.1), (0.0, 1.1)]
bm_lathe(bm, vest, squash @ T((0, -0.004, 0)), 28)
f.append(new_obj("Finch_Vest", bm, "M_Vest"))
bm = bmesh.new()
bm_rod(bm, (-0.19, 0.0, 1.14), (-0.22, -0.02, 0.74), 0.048, 12)            # left arm hanging
bm_rod(bm, (0.19, 0.0, 1.14), (0.24, -0.12, 0.92), 0.048, 12)              # right arm bent forward
bm_rod(bm, (0.24, -0.12, 0.92), (0.2, -0.26, 0.98), 0.044, 12)             # forearm holding the candle
f.append(new_obj("Finch_Sleeves", bm, "M_Shirt"))
bm = bmesh.new()
for c in ((-0.225, -0.025, 0.71), (0.19, -0.29, 0.98)):
    res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.043)
    bmesh.ops.transform(bm, matrix=T(c), verts=res["verts"])
f.append(new_obj("Finch_Hands", bm, "M_Skin"))
bm = bmesh.new()  # candle stub in a brass dish
bm_lathe(bm, [(0.0, 0.0), (0.045, 0.0), (0.05, 0.012), (0.0, 0.012)], T((0.19, -0.3, 1.005)), 20)
f.append(new_obj("Finch_CandleDish", bm, "M_Brass"))
bm = bmesh.new()
bm_cyl(bm, 0.017, 0.07, T((0.19, -0.3, 1.05)), 12)
f.append(new_obj("Finch_Candle", bm, "M_Candle"))
# head group (neck pivot z = 1.24)
bm = bmesh.new()
res = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=0.1)
bmesh.ops.transform(bm, matrix=T((0, 0, 1.36)) @ Matrix.Diagonal((0.95, 1.0, 1.08, 1.0)), verts=res["verts"])
bm_cyl(bm, 0.045, 0.1, T((0, 0, 1.27)), 12)
f.append(new_obj("Finch_Head", bm, "M_Skin"))
bm = bmesh.new()  # flat cap: soft crown pushed forward + short peak
bm_lathe(bm, [(0.0, 0.0), (0.118, 0.0), (0.125, 0.03), (0.1, 0.06), (0.0, 0.07)],
         T((0, -0.02, 1.43)) @ rot_x(-8) @ Matrix.Diagonal((1.0, 1.12, 1.0, 1.0)), 28)
bm_box(bm, (0.17, 0.08, 0.012), T((0, -0.14, 1.435)) @ rot_x(-14))
f.append(new_obj("Finch_Cap", bm, "M_CapGreen"))
props["NPC_Finch"] = f

# -- Corkboard case board on an easel (front faces -Y) --------------------------------------------------
cb = []
BW, BH, BZ = 1.0, 0.72, 0.92  # board width, height, bottom edge height
lean = rot_x(-10)
board_m = T((0, 0.06, BZ)) @ lean
bm = bmesh.new()
for side in (-1, 1):  # front legs
    bm_beam(bm, (side * 0.6, -0.1, 0.0), (side * 0.56, 0.24, 1.72), 0.04, 0.03)  # outside the frame
bm_beam(bm, (0, 0.75, 0.0), (0, 0.32, 1.5), 0.035, 0.03)              # back leg, behind the cork
bm_box(bm, (1.16, 0.06, 0.03), T((0, -0.02, BZ - 0.03)))                # ledge, reaching the legs
bm = bm
for (w, h, cx, cz) in ((BW + 0.06, 0.04, 0, BH + 0.02), (BW + 0.06, 0.04, 0, -0.02),
                       (0.04, BH + 0.08, -BW / 2 - 0.01, BH / 2), (0.04, BH + 0.08, BW / 2 + 0.01, BH / 2)):
    bm_box(bm, (w, 0.035, h), board_m @ T((cx, 0, cz)))                # frame
cb.append(new_obj("CaseBoard_Wood", bm, "M_Wood"))
bm = bmesh.new()
bm_box(bm, (BW, 0.02, BH), board_m @ T((0, 0.006, BH / 2)))
cb.append(new_obj("CaseBoard_Cork", bm, "M_Cork"))
cards = [(-0.33, 0.52), (0.0, 0.56), (0.33, 0.5), (-0.28, 0.2), (0.06, 0.24), (0.34, 0.16)]
bm = bmesh.new()
for i, (x, z) in enumerate(cards):
    bm_box(bm, (0.17, 0.004, 0.13), board_m @ T((x, -0.006, z)) @ rot_y((i * 37 % 11) - 5))
cb.append(new_obj("CaseBoard_Cards", bm, "M_Paper"))
bm = bmesh.new()
pins = [board_m @ Vector((x, -0.012, z + 0.05)) for x, z in cards]
for pnt in pins:
    res = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=0.011)
    bmesh.ops.transform(bm, matrix=T(pnt), verts=res["verts"])
for a_i, b_i in ((0, 1), (1, 2), (0, 4), (1, 4), (3, 4), (4, 5), (2, 5)):  # red string
    bm_beam(bm, pins[a_i] + Vector((0, -0.004, 0)), pins[b_i] + Vector((0, -0.004, 0)), 0.004, 0.002)
cb.append(new_obj("CaseBoard_PinsString", bm, "M_Wax"))
props["Prop_CaseBoard"] = cb

# ---------------------------------------------------------------------------
# 4. Apply transforms + export
# ---------------------------------------------------------------------------
view_layer = bpy.context.view_layer


def select_only(objs):
    for o in bpy.data.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    view_layer.objects.active = objs[0]


select_only(list(bpy.data.objects))
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

exports = {"Clocktower_Room": room_objs, "Gears_Pendulum": mech_objs}
exports.update(props)
report = {}
for fname, objs in exports.items():
    select_only(objs)
    path = os.path.join(EXPORT_DIR, fname + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    report[fname] = (len(objs), tris, os.path.getsize(path))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT_ROOT, "SourceArt", "Clocktower.blend"))
for k, (n, tris, size) in report.items():
    print(f"EXPORTED {k}.fbx objects={n} tris={tris} bytes={size}")
