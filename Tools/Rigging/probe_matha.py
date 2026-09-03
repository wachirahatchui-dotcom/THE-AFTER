"""Measure Matha's mesh so an armature can be fitted to it.

Runs headless:  blender --background --python probe_matha.py

Prints a JSON block between MARKERs so the caller can parse it out of
Blender's very chatty stdout.
"""
import bpy
import json
import sys
from mathutils import Vector

FBX = r"C:\Users\omgpo\THE AFTER\Assets\Models\Characters\Matha\Matha.fbx"

# Empty the default scene (cube, camera, light).
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
report = {"meshes": [], "objects": [o.name for o in bpy.data.objects]}

for obj in meshes:
    mw = obj.matrix_world
    verts = [mw @ v.co for v in obj.data.vertices]
    if not verts:
        continue

    xs = [v.x for v in verts]
    ys = [v.y for v in verts]
    zs = [v.z for v in verts]

    lo = Vector((min(xs), min(ys), min(zs)))
    hi = Vector((max(xs), max(ys), max(zs)))
    size = hi - lo

    # Which axis is "up" decides everything downstream. FBX out of Blender is
    # usually Z-up; re-imported FBX often comes back Y-up.
    up_axis = "z" if size.z >= size.x and size.z >= size.y else ("y" if size.y >= size.x else "x")
    height = max(size.x, size.y, size.z)

    def slab(frac, band=0.02):
        """Vertices in a horizontal slice at `frac` of the height."""
        if up_axis == "z":
            lo_v, hi_v = lo.z, hi.z
            key = lambda v: v.z
        elif up_axis == "y":
            lo_v, hi_v = lo.y, hi.y
            key = lambda v: v.y
        else:
            lo_v, hi_v = lo.x, hi.x
            key = lambda v: v.x

        target = lo_v + (hi_v - lo_v) * frac
        w = (hi_v - lo_v) * band
        return [v for v in verts if abs(key(v) - target) <= w]

    # Width of the body at a few heights tells us where the shoulders,
    # the waist and the feet are, and how far out the hands reach.
    profile = {}
    for name, frac in [("feet", 0.02), ("knee", 0.27), ("hip", 0.52),
                       ("waist", 0.60), ("chest", 0.74), ("shoulder", 0.82),
                       ("neck", 0.87), ("head", 0.93)]:
        band = slab(frac)
        if not band:
            profile[name] = None
            continue

        if up_axis == "z":
            a = [v.x for v in band]; b = [v.y for v in band]
        elif up_axis == "y":
            a = [v.x for v in band]; b = [v.z for v in band]
        else:
            a = [v.y for v in band]; b = [v.z for v in band]

        profile[name] = {
            "count": len(band),
            "spread_a": [round(min(a), 4), round(max(a), 4)],
            "spread_b": [round(min(b), 4), round(max(b), 4)],
        }

    report["meshes"].append({
        "name": obj.name,
        "verts": len(obj.data.vertices),
        "polys": len(obj.data.polygons),
        "loc": [round(c, 4) for c in obj.location],
        "rot_euler_deg": [round(c * 57.2957795, 2) for c in obj.rotation_euler],
        "scale": [round(c, 4) for c in obj.scale],
        "bounds_lo": [round(c, 4) for c in lo],
        "bounds_hi": [round(c, 4) for c in hi],
        "size": [round(c, 4) for c in size],
        "up_axis": up_axis,
        "height": round(height, 4),
        "vertex_groups": [g.name for g in obj.vertex_groups],
        "profile": profile,
    })

print("---MATHA-PROBE-START---")
print(json.dumps(report, indent=1))
print("---MATHA-PROBE-END---")
