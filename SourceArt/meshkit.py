"""
Shared bmesh helpers for The Spooky Crime Scene's procedural Blender generators.

Every helper bakes geometry in world space (objects keep identity transforms), so the FBX
exports need no transform fix-ups. Material names match the Unity material library
(ClocktowerSceneBuilder.Materials), which remaps them on import.
Blender axes: +Z up. Unity import maps Blender (x, y, z) -> (-x, z, -y).
"""
import bpy
import bmesh
import math
import os
from mathutils import Matrix, Vector

T = Matrix.Translation
MATS = {}


def reset_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for block in list(coll):
            coll.remove(block)
    MATS.clear()
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0


def mat(name, rgb, metallic=0.0, rough=0.6, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, alpha)
    m.metallic = metallic
    m.roughness = rough
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Alpha"].default_value = alpha
    MATS[name] = m
    return m


def palette(names):
    """Creates placeholder materials by name; Unity replaces them with the real URP materials."""
    for n in names:
        if n not in MATS:
            mat(n, (0.5, 0.5, 0.5))


def new_obj(name, bm, material):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MATS[material])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


# ---------------------------------------------------------------- primitives

def bm_box(bm, size, matrix):
    res = bmesh.ops.create_cube(bm, size=1.0, calc_uvs=True)
    bmesh.ops.transform(bm, matrix=matrix @ Matrix.Diagonal((*size, 1.0)), verts=res["verts"])


def bm_cyl(bm, r1, depth, matrix, segs=24, r2=None):
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                                radius1=r1, radius2=r1 if r2 is None else r2, depth=depth, calc_uvs=True)
    bmesh.ops.transform(bm, matrix=matrix, verts=res["verts"])


def bm_sphere(bm, radius, matrix, u=16, v=10):
    res = bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=radius)
    bmesh.ops.transform(bm, matrix=matrix, verts=res["verts"])


def bm_ico(bm, radius, matrix, subdiv=1):
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
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
            uniq = []
            for vert in quad:
                if all((vert.co - u.co).length > 1e-7 for u in uniq):
                    uniq.append(vert)
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


def bm_tube_wall(bm, radius, thickness, z0, z1, matrix=Matrix(), segs=48):
    """Hollow cylinder wall (outer radius = radius)."""
    bm_ring2d(bm, circle(radius, segs), circle(radius - thickness, segs), z0, z1, matrix)


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


def bm_quad(bm, corners):
    verts = [bm.verts.new(Vector(c)) for c in corners]
    bm.faces.new(verts)


def rot_x(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'X')


def rot_y(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Y')


def rot_z(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Z')


def scale(x, y, z):
    return Matrix.Diagonal((x, y, z, 1.0))


# ---------------------------------------------------------------- reusable props

def pocket_watch(prefix, hour, minute):
    """Brass pocket watch lying face up (stem toward +Y) with its hands frozen at hour:minute."""
    objs = []
    bm = bmesh.new()
    case_prof = [(0.0, 0.0), (0.019, 0.0), (0.0235, 0.002), (0.0255, 0.0055), (0.0245, 0.009), (0.0225, 0.0108), (0.0, 0.0108)]
    bm_lathe(bm, case_prof, Matrix(), 40)
    bm_ring(bm, 0.0205, 0.0238, 0.0035, T((0, 0, 0.0118)), 40)
    bm_cyl(bm, 0.0042, 0.006, T((0, 0.0285, 0.0055)) @ rot_x(90), 16)
    bm_ring(bm, 0.0045, 0.0065, 0.0022, T((0, 0.0355, 0.0055)) @ rot_y(90), 20)
    objs.append(new_obj(prefix + "_Case", bm, "M_Brass"))
    bm = bmesh.new()
    bm_cyl(bm, 0.021, 0.0008, T((0, 0, 0.0112)), 40)
    objs.append(new_obj(prefix + "_Dial", bm, "M_Enamel"))
    bm = bmesh.new()
    for h in range(12):
        bm_box(bm, (0.0012, 0.0035 if h % 3 == 0 else 0.002, 0.0003), rot_z(-30 * h) @ T((0, 0.0175, 0.0118)))
    hour_deg = (hour % 12) * 30 + minute * 0.5
    minute_deg = minute * 6
    for deg, length, width, z in ((hour_deg, 0.012, 0.0016, 0.0121), (minute_deg, 0.0175, 0.0011, 0.0125)):
        bm_box(bm, (width, length, 0.0004), rot_z(-deg) @ T((0, length / 2 - 0.002, z)))
    bm_cyl(bm, 0.0012, 0.0012, T((0, 0, 0.0126)), 12)
    objs.append(new_obj(prefix + "_Hands", bm, "M_Ink"))
    return objs


def folded_paper(prefix, w, h, seal=False):
    objs = []
    bm = bmesh.new()
    for side in (-1, 1):
        bm_box(bm, (w / 2, h, 0.0012),
               T((side * w / 4 * math.cos(math.radians(6)), 0, 0.0006 + w / 4 * math.sin(math.radians(6)))) @ rot_y(-side * 6))
    objs.append(new_obj(prefix + "_Paper", bm, "M_Paper"))
    if seal:
        bm = bmesh.new()
        bm_cyl(bm, 0.012, 0.004, T((w / 4, 0, w / 4 * math.sin(math.radians(6)) + 0.003)) @ rot_y(-6), 20)
        objs.append(new_obj(prefix + "_Seal", bm, "M_Wax"))
    return objs


def book(prefix, cover_mat, w=0.23, d=0.31, thick=0.044):
    objs = []
    bm = bmesh.new()
    bm_box(bm, (w, d, 0.006), T((0, 0, 0.003)))
    bm_box(bm, (w, d, 0.006), T((0, 0, thick - 0.003)))
    bm_box(bm, (0.012, d, thick), T((-w / 2 + 0.003, 0, thick / 2)))
    objs.append(new_obj(prefix + "_Cover", bm, cover_mat))
    bm = bmesh.new()
    bm_box(bm, (w - 0.015, d - 0.014, thick - 0.012), T((0.004, 0, thick / 2)))
    objs.append(new_obj(prefix + "_Pages", bm, "M_Paper"))
    return objs


# ---------------------------------------------------------------- export

def apply_all_transforms():
    objs = list(bpy.data.objects)
    for o in bpy.data.objects:
        o.select_set(o in objs)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def export_groups(groups, export_dir):
    """groups: {fbx_name: [objects]} -> one FBX per group (forward -Z, up Y), transforms applied."""
    os.makedirs(export_dir, exist_ok=True)
    apply_all_transforms()
    report = {}
    for fname, objs in groups.items():
        for o in bpy.data.objects:
            o.select_set(o in objs)
        bpy.context.view_layer.objects.active = objs[0]
        path = os.path.join(export_dir, fname + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
        report[fname] = (len(objs), tris, os.path.getsize(path))
    for k, (n, tris, size) in report.items():
        print(f"EXPORTED {k}.fbx objects={n} tris={tris} bytes={size}")
    return report
