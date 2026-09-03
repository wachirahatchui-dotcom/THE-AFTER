"""Make Asher Kid's skeleton acceptable to Unity's Humanoid rig.

Runs headless:
    blender --background --factory-startup --python fix_asherkid_hierarchy.py

The model arrives already rigged, but as an IK rig: the feet and the knee pole
targets hang off the root rather than off the legs, because that is how the
animator drove them in Blender. Unity's Humanoid needs the foot to be a
descendant of the lower leg, so it rejects the whole avatar with
"Required human bone 'LeftFoot' not found".

Reparenting is safe here. Bone names and rest positions are untouched, so the
existing vertex weights still apply, and the IK constraints that made the old
hierarchy meaningful do not survive an FBX export anyway.
"""
import bpy
import json

SRC = r"C:/Users/omgpo/THE AFTER/Tools/Rigging/SourceMeshes/AsherKid_BEFORE_FIX.fbx"
OUT = r"C:\Users\omgpo\THE AFTER\Assets\Models\Characters\Asher Kid.fbx"

log = {"steps": [], "warnings": []}


def step(msg):
    log["steps"].append(msg)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
if arm is None:
    raise SystemExit("no armature in " + SRC)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
step("armature %s with %d bones, %d meshes" % (arm.name, len(arm.data.bones), len(meshes)))
log["bones_before"] = [b.name for b in arm.data.bones]

bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones

# Hang each foot off its own lower leg. use_connect stays off so the foot keeps
# the exact rest position it was authored at - moving it would shift the weights
# painted against it.
for side in ("L", "R"):
    foot = eb.get("Foot." + side)
    shin = eb.get("LowerLeg." + side)
    if foot is None or shin is None:
        log["warnings"].append("could not find Foot.%s / LowerLeg.%s" % (side, side))
        continue
    foot.parent = shin
    foot.use_connect = False
    step("Foot.%s reparented under LowerLeg.%s" % (side, side))

# The pole targets only meant something to the IK constraints, and Unity will
# try to map them as real bones.
for name in list(eb.keys()):
    if name.startswith("PoleTarget"):
        eb.remove(eb[name])
        step("removed " + name)

# The shin tip bones now sit between the knee and the foot and confuse the
# mapper, which reads them as a second lower leg.
for side in ("L", "R"):
    tip = eb.get("LowerLeg.%s_end" % side)
    if tip is not None:
        eb.remove(tip)
        step("removed LowerLeg.%s_end" % side)

bpy.ops.object.mode_set(mode='OBJECT')
log["bones_after"] = [b.name for b in arm.data.bones]

# Every material in the source file carries alpha 0 and a hashed blend mode, so
# the character imports completely see-through. There are no textures at all -
# these are flat colours (Skin, Shirt, Pants, Hair...) - so making them opaque
# costs nothing and is the only way the model is visible.
fixed = []
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type != 'BSDF_PRINCIPLED':
            continue
        if node.inputs['Alpha'].default_value < 1.0:
            node.inputs['Alpha'].default_value = 1.0
            fixed.append(mat.name)
    if hasattr(mat, "blend_method"):
        mat.blend_method = 'OPAQUE'
log["materials_made_opaque"] = fixed
step("made %d materials opaque" % len(fixed))

# Read the hierarchy back so the caller can see the feet landed in the right place.
log["parents"] = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    apply_scale_options='FBX_SCALE_NONE',
    object_types={'ARMATURE', 'MESH'},
    use_armature_deform_only=False,
    add_leaf_bones=False,
    bake_anim=False,
    # The source FBX carries its textures inside it. Stripping them on the way
    # out left the character rendering as a transparent ghost in the scene.
    path_mode='COPY',
    embed_textures=True,
)
step("exported " + OUT)

print("---ASHER-FIX-START---")
print(json.dumps(log, indent=1))
print("---ASHER-FIX-END---")
