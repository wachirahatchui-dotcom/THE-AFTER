"""Fit a humanoid armature to Matha and skin her to it.

Runs headless:
    blender --background --factory-startup --python rig_matha.py

The mesh arrives as one un-rigged lump, so the joints have to be found rather
than assumed: every limb is located by tracing the mesh itself, band by band up
the body. Guessed joint positions are what make an auto-rig bend in the wrong
place, and tracing costs almost nothing.

Bone names follow the Mixamo convention minus the prefix, which is what Unity's
Humanoid auto-mapper recognises most reliably.
"""
import bpy
import json
import math
from mathutils import Vector

# Assets/.../Matha.fbx now holds a rigged mesh, so a re-run has to start from the
# untouched backup or it would rig an already-rigged file.
SRC = r"C:/Users/omgpo/THE AFTER/Tools/Rigging/SourceMeshes/Matha_BEFORE_RIG.fbx"
OUT = r"C:/Users/omgpo/THE AFTER/Tools/Rigging/Matha_Rig.blend"

log = {"steps": [], "joints": {}, "warnings": []}


def step(msg):
    log["steps"].append(msg)


# ----------------------------------------------------------------- import
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

mesh = next((o for o in bpy.data.objects if o.type == 'MESH'), None)
if mesh is None:
    raise SystemExit("no mesh in " + SRC)

# Bake any import transform into the data so world space == object space.
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

verts = [v.co.copy() for v in mesh.data.vertices]
lo = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
hi = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
H = hi.z - lo.z

step("mesh %s  verts=%d  height=%.4f" % (mesh.name, len(verts), H))

# Toes stick out further than heels, so the deeper side of the foot is the
# way she is facing. Everything left/right depends on getting this right.
foot_band = [v for v in verts if v.z < lo.z + H * 0.05]
front_sign = -1.0 if abs(min(v.y for v in foot_band)) > abs(max(v.y for v in foot_band)) else 1.0
step("facing %sY  (left hand side is %sX)" % ("-" if front_sign < 0 else "+",
                                              "+" if front_sign < 0 else "-"))
left_sign = 1.0 if front_sign < 0 else -1.0


def band(z_frac, half=0.015):
    z = lo.z + H * z_frac
    w = H * half
    return [v for v in verts if abs(v.z - z) <= w]


def centroid(vs, fallback):
    if not vs:
        return fallback
    n = len(vs)
    return Vector((sum(v.x for v in vs) / n, sum(v.y for v in vs) / n, sum(v.z for v in vs) / n))


def point_inside(p):
    """True when p is inside the mesh.

    Counts surface crossings on the way out: an odd number means the ray
    started inside. Used to check a joint actually sits in the body rather
    than floating just off the skin.
    """
    hits, origin, d = 0, p.copy(), Vector((0.0, 0.0, 1.0))
    for _ in range(64):
        ok, loc, nor, idx = mesh.ray_cast(origin, d)
        if not ok:
            break
        hits += 1
        origin = loc + d * 1e-5
    return hits % 2 == 1


def limb_point(z_frac, side, min_abs_x, max_abs_x=None):
    """Centre of one limb in a horizontal slice, on the given side."""
    out = []
    for v in band(z_frac):
        ax = abs(v.x)
        if ax < min_abs_x:
            continue
        if max_abs_x is not None and ax > max_abs_x:
            continue
        if (v.x > 0) != (side > 0):
            continue
        out.append(v)
    return centroid(out, None)


# ------------------------------------------------------- find the joints
# Torso: the middle column of the body, so limbs are excluded by an x cutoff.
def torso_point(z_frac, cutoff):
    return centroid([v for v in band(z_frac) if abs(v.x) < cutoff],
                    Vector((0, 0, lo.z + H * z_frac)))


hips = torso_point(0.53, H * 0.16)
spine = torso_point(0.62, H * 0.16)
chest = torso_point(0.74, H * 0.16)
neck = torso_point(0.865, H * 0.10)
head = torso_point(0.915, H * 0.10)
head_top = Vector((head.x, head.y, hi.z))

# Arms hang away from the body, so anything beyond the torso at these heights
# is an arm. The cutoff widens going down because the arms splay outwards.
arm_pts = {}
for name, frac, cut in [("shoulder", 0.815, H * 0.055),
                        ("elbow", 0.665, H * 0.105),
                        ("wrist", 0.545, H * 0.155)]:
    for side, tag in ((left_sign, "L"), (-left_sign, "R")):
        p = limb_point(frac, side, cut)
        if p is None:
            log["warnings"].append("could not trace %s %s" % (tag, name))
        arm_pts[tag + "_" + name] = p

# Legs are the two columns under the hips.
leg_pts = {}
for name, frac in [("hip", 0.50), ("knee", 0.27), ("ankle", 0.045)]:
    for side, tag in ((left_sign, "L"), (-left_sign, "R")):
        p = limb_point(frac, side, H * 0.012, H * 0.16)
        if p is None:
            log["warnings"].append("could not trace %s %s" % (tag, name))
        leg_pts[tag + "_" + name] = p


def fallback(p, x, z):
    return p if p is not None else Vector((x, 0.0, lo.z + H * z))


for tag, s in (("L", left_sign), ("R", -left_sign)):
    arm_pts[tag + "_shoulder"] = fallback(arm_pts[tag + "_shoulder"], s * H * 0.08, 0.815)
    arm_pts[tag + "_elbow"] = fallback(arm_pts[tag + "_elbow"], s * H * 0.16, 0.665)
    arm_pts[tag + "_wrist"] = fallback(arm_pts[tag + "_wrist"], s * H * 0.25, 0.545)
    leg_pts[tag + "_hip"] = fallback(leg_pts[tag + "_hip"], s * H * 0.06, 0.50)
    leg_pts[tag + "_knee"] = fallback(leg_pts[tag + "_knee"], s * H * 0.06, 0.27)
    leg_pts[tag + "_ankle"] = fallback(leg_pts[tag + "_ankle"], s * H * 0.06, 0.045)

for k, v in list(arm_pts.items()) + list(leg_pts.items()):
    log["joints"][k] = [round(c, 4) for c in v]
for k, v in [("hips", hips), ("spine", spine), ("chest", chest),
             ("neck", neck), ("head", head), ("head_top", head_top)]:
    log["joints"][k] = [round(c, 4) for c in v]

# ------------------------------------------------------ build the armature
bpy.ops.object.armature_add(enter_editmode=False, location=(0, 0, 0))
arm = bpy.context.object
arm.name = "Armature"
arm.data.name = "MathaRig"

bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for b in list(eb):
    eb.remove(b)


def bone(name, head_v, tail_v, parent=None, connected=False):
    b = eb.new(name)
    b.head = head_v
    b.tail = tail_v
    if parent is not None:
        b.parent = parent
        b.use_connect = connected
    return b


b_hips = bone("Hips", hips, spine)
b_spine = bone("Spine", spine, chest, b_hips, True)
b_chest = bone("Chest", chest, neck, b_spine, True)
b_neck = bone("Neck", neck, head, b_chest, True)
bone("Head", head, head_top, b_neck, True)

for tag, label in (("L", "Left"), ("R", "Right")):
    sh = arm_pts[tag + "_shoulder"]
    el = arm_pts[tag + "_elbow"]
    wr = arm_pts[tag + "_wrist"]

    # A short collar bone from the base of the neck out to the shoulder joint
    # gives the shoulder somewhere to rotate from.
    collar = Vector((chest.x + (sh.x - chest.x) * 0.25, chest.y, neck.z * 0.98))
    b_sh = bone(label + "Shoulder", collar, sh, b_chest, False)
    b_up = bone(label + "Arm", sh, el, b_sh, True)
    b_lo = bone(label + "ForeArm", el, wr, b_up, True)

    hand_dir = (wr - el).normalized() if (wr - el).length > 1e-5 else Vector((0, 0, -1))
    bone(label + "Hand", wr, wr + hand_dir * (H * 0.07), b_lo, True)

for tag, label in (("L", "Left"), ("R", "Right")):
    hp = leg_pts[tag + "_hip"]
    kn = leg_pts[tag + "_knee"]
    an = leg_pts[tag + "_ankle"]

    b_ul = bone(label + "UpLeg", hp, kn, b_hips, False)
    b_ll = bone(label + "Leg", kn, an, b_ul, True)
    toe = Vector((an.x, an.y + front_sign * H * 0.06, lo.z + H * 0.012))
    b_ft = bone(label + "Foot", an, toe, b_ll, True)
    bone(label + "ToeBase", toe, toe + Vector((0, front_sign * H * 0.03, 0)), b_ft, True)


# ------------------------------------------------- tidy the traced skeleton
# Everything above came from centroids of horizontal mesh slices, and slices lie:
# the chest and the face drag the torso points off the centre line, and the two
# sides get traced independently so they never quite match. Both were visible
# once the rig was opened in the GUI - the spine zig-zagged 3.6 cm front to back
# and the right shoulder joint sat outside the skin.

# 1. Put the torso on the axis. A stylised character does not need a curved
#    spine, and a straight column makes head and spine rotation predictable.
_chain = ["Hips", "Spine", "Chest", "Neck", "Head"]
for _name in _chain:
    eb[_name].head.x = 0.0
    eb[_name].head.y = 0.0
eb["Head"].tail.x = 0.0
eb["Head"].tail.y = 0.0
for _a, _b in zip(_chain, _chain[1:]):
    eb[_a].tail = eb[_b].head.copy()

# 2. Mirror the left side onto the right so the two match exactly.
for _l, _r in [("Shoulder", "Shoulder"), ("Arm", "Arm"), ("ForeArm", "ForeArm"),
               ("Hand", "Hand"), ("UpLeg", "UpLeg"), ("Leg", "Leg"),
               ("Foot", "Foot"), ("ToeBase", "ToeBase")]:
    _bl, _br = eb["Left" + _l], eb["Right" + _r]
    _br.head = Vector((-_bl.head.x, _bl.head.y, _bl.head.z))
    _br.tail = Vector((-_bl.tail.x, _bl.tail.y, _bl.tail.z))

# 3. The collar roots were left level with the neck, where the body is only as
#    wide as the throat. Search down and inward for the first spot inside the
#    skin - that is where a real collarbone starts.
_collar_z = neck.z * 0.98
for _z in [_collar_z, _collar_z - H * 0.01, _collar_z - H * 0.02, _collar_z - H * 0.03]:
    _hit = None
    for _x in [H * 0.034, H * 0.029, H * 0.025, H * 0.020, H * 0.016]:
        if point_inside(Vector((_x, 0.0, _z))) and point_inside(Vector((-_x, 0.0, _z))):
            _hit = _x
            break
    if _hit is not None:
        eb["LeftShoulder"].head = Vector((_hit, 0.0, _z))
        eb["RightShoulder"].head = Vector((-_hit, 0.0, _z))
        log["joints"]["collar_root"] = [round(_hit, 4), 0.0, round(_z, 4)]
        break

bpy.ops.object.mode_set(mode='OBJECT')
step("armature built: %d bones" % len(arm.data.bones))

# ------------------------------------------------------------- skinning
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm

skinned = False
try:
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    skinned = True
    step("skinned with automatic (bone heat) weights")
except Exception as exc:
    log["warnings"].append("automatic weights failed: %s" % exc)

if not skinned:
    bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')
    step("skinned with envelope weights (fallback)")

groups = [g.name for g in mesh.vertex_groups]
log["vertex_groups"] = groups
step("vertex groups on mesh: %d" % len(groups))

unweighted = 0
for v in mesh.data.vertices:
    if sum(g.weight for g in v.groups) <= 0.0001:
        unweighted += 1
log["unweighted_verts"] = unweighted
if unweighted:
    log["warnings"].append("%d vertices ended up with no weight" % unweighted)

# ------------------------------------------------------------ save .blend
# Same rig as rig_matha.py, kept as a native scene so the armature can be
# inspected and nudged in the GUI instead of only through the exporter.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm

# Bones shown through the mesh - otherwise the skin hides every joint.
arm.data.display_type = 'OCTAHEDRAL'
arm.show_in_front = True

bpy.ops.wm.save_as_mainfile(filepath=OUT)
step("saved " + OUT)

print("---MATHA-RIG-START---")
print(json.dumps(log, indent=1))
print("---MATHA-RIG-END---")
