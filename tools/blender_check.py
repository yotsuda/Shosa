# Reads a BVH written by `shosa bvh`, or a glTF written by `shosa gltf`, into Blender, compares
# every bone's position with the solver's own joints (`shosa clip`), and renders a few frames
# with a box on each bone.
#
#   blender -b --python tools\blender_check.py -- out\walk.bvh out\walk.clip.json out\blender\walk [figure index] [frames...]
#   blender -b --python tools\blender_check.py -- out\walk.glb out\walk.clip.json out\blender\walk.glb [figure index] [frames...]
#
# Left is blue and right is red, as on the contact sheet. The boxes are flat, so a limb
# twisted about its own length shows.
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
# Absolute: Blender resolves a relative render path against the drive's root, not the working directory.
bvh_path, clip_path, out_prefix = (os.path.abspath(a) for a in args[:3])
figure_index = int(args[3]) if len(args) > 3 else 0
wanted = [int(a) for a in args[4:]]

with open(clip_path, encoding="utf-8") as f:
    clip = json.load(f)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
is_gltf = bvh_path.lower().endswith((".glb", ".gltf"))
if is_gltf:
    # The glTF import places keys at seconds times the scene's frame rate, and frame 0 is time 0.
    scene.render.fps = clip["fps"]
    bpy.ops.import_scene.gltf(filepath=bvh_path)
    armatures = sorted((o for o in scene.objects if o.type == "ARMATURE"), key=lambda o: o.name)
    print("== armatures:", [o.name for o in armatures])
    wanted_id = clip["figures"][figure_index]["id"]
    arm = next((o for o in armatures if o.name == wanted_id), armatures[figure_index])
    first_frame = 0
else:
    bpy.ops.import_anim.bvh(filepath=bvh_path, update_scene_fps=True, update_scene_duration=True)
    arm = bpy.context.object
    first_frame = scene.frame_start

print("== bones (rest pose, armature space)")
for bone in arm.data.bones:
    h, t = bone.head_local, bone.tail_local
    print(f"  {bone.name:14} head ({h.x:6.3f} {h.y:6.3f} {h.z:6.3f})  tail ({t.x:6.3f} {t.y:6.3f} {t.z:6.3f})  length {bone.length:.3f}")
print(f"== fps {scene.render.fps}, frames {scene.frame_start}..{scene.frame_end}")

# ---- Positions: Blender's bones against the solver's joints.
joints = clip["joints"]
frames = clip["figures"][figure_index]["frames"]
bone_joint = {
    "hips": "pelvis", "neck": "chest", "head": "head",
    "leftUpperArm": "leftShoulder", "leftLowerArm": "leftElbow", "leftHand": "leftHand",
    "rightUpperArm": "rightShoulder", "rightLowerArm": "rightElbow", "rightHand": "rightHand",
    "leftUpperLeg": "leftHip", "leftLowerLeg": "leftKnee", "leftFoot": "leftAnkle",
    "rightUpperLeg": "rightHip", "rightLowerLeg": "rightKnee", "rightFoot": "rightAnkle",
}


def solver_joint(frame, name):
    i = joints.index(name) * 3
    x, y, z = frames[frame][i:i + 3]
    return Vector((x, -z, y))  # Shosa is y up, facing +z; Blender is z up, and the import turns +z to -y


worst = (0.0, "", 0)
for f in range(len(frames)):
    scene.frame_set(first_frame + f)
    for bone, joint in bone_joint.items():
        error = (arm.matrix_world @ arm.pose.bones[bone].head - solver_joint(f, joint)).length
        if error > worst[0]:
            worst = (error, bone, f)
print(f"== worst position error over {len(frames)} frames: {worst[0] * 1000:.2f} mm ({worst[1]}, frame {worst[2]})")

scene.frame_set(first_frame)
if not is_gltf:  # glTF has no end sites: where the head bone's tail goes is the importer's guess
    nose = arm.matrix_world @ arm.pose.bones["head"].tail - arm.matrix_world @ arm.pose.bones["head"].head
    print(f"== at frame 0 the nose points ({nose.x:.2f} {nose.y:.2f} {nose.z:.2f}) in Blender (front is -y)")


# ---- Pictures: a flat box on every bone.
def material(name, colour):
    m = bpy.data.materials.new(name)
    m.diffuse_color = colour
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        for node in m.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                node.inputs["Base Color"].default_value = colour
    return m


colours = {
    "l": material("left", (0.1, 0.25, 0.9, 1)),
    "r": material("right", (0.9, 0.12, 0.1, 1)),
    "c": material("centre", (0.55, 0.55, 0.55, 1)),
}
widths = {"neck": (0.08, 0.08), "Hand": (0.07, 0.02), "Foot": (0.08, 0.03), "head": (0.14, 0.16), "Arm": (0.07, 0.04), "Leg": (0.10, 0.06)}

# The glTF import keeps the file's axes on every bone (all point up at rest), so a box cannot
# simply lie along the bone: it is laid from the bone to its child in the rest pose instead.
# Where a leaf ends is not in the file (Shosa's rest pose, in Blender's axes).
leaf_ends = {"head": Vector((0, 0, 0.24)), "Hand": None, "Foot": Vector((0, -0.17, -0.06))}
if is_gltf:
    arm.data.pose_position = "REST"
    bpy.context.view_layer.update()

for bone in arm.data.bones:
    if bone.name == "hips":  # no length of its own: the spine starts where it does
        continue
    wide, thin = next((w for key, w in widths.items() if key in bone.name), (0.24, 0.12))
    bpy.ops.mesh.primitive_cube_add(size=1)
    box = bpy.context.object
    box.parent = arm
    box.parent_type = "BONE"
    box.parent_bone = bone.name
    if is_gltf:
        if bone.children:
            along = bone.children[0].head_local - bone.head_local
        else:
            along = next(v for key, v in leaf_ends.items() if key in bone.name)
            if along is None:  # a hand goes on the way the forearm does
                along = (bone.head_local - bone.parent.head_local).normalized() * 0.08
        length = along.length
        along = along.normalized()
        # Flat the way the limb bends: arms are thin up and down, everything else front to back.
        across = Vector((0, 1, 0)) if abs(along.x) > 0.7 else Vector((1, 0, 0))
        normal = across.cross(along).normalized()
        across = along.cross(normal)
        rest = Matrix((across * wide, along * length, normal * thin)).transposed().to_4x4()
        rest.translation = bone.head_local + along * (length / 2)
        box.matrix_world = arm.matrix_world @ rest
    else:
        box.scale = (wide, bone.length, thin)
        box.location = (0, -bone.length / 2, 0)  # a bone's child sits at its tail, y along the bone
    side = "l" if bone.name.startswith("left") else "r" if bone.name.startswith("right") else "c"
    box.data.materials.append(colours[side])

if is_gltf:
    arm.data.pose_position = "POSE"

bpy.ops.mesh.primitive_plane_add(size=40)
bpy.context.object.data.materials.append(material("floor", (0.85, 0.85, 0.82, 1)))

bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(-30)))
bpy.context.object.data.energy = 3
world = bpy.data.worlds.new("world")
scene.world = world
try:
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.9, 0.9, 0.9, 1)
except Exception:
    world.color = (0.9, 0.9, 0.9)

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.lens = 50
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 12
scene.render.resolution_x = 560
scene.render.resolution_y = 640

if not wanted:
    count = 6
    wanted = [round(i * (len(frames) - 1) / (count - 1)) for i in range(count)]
for f in wanted:
    scene.frame_set(first_frame + f)
    hips =arm.matrix_world @ arm.pose.bones["hips"].head
    target = Vector((hips.x, hips.y, 0.9))
    camera.location = target + Vector((2.2, -4.2, 0.6))  # in front of a figure facing -y, a little to its left
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{out_prefix}.{f:04}.png"
    bpy.ops.render.render(write_still=True)
    print("== wrote", scene.render.filepath)
