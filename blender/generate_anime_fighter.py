"""Generate a new stylized anime humanoid, rig and action clips for Unity.

The character is generated entirely from Blender primitives/mesh code. No third-party
character mesh or animation asset is embedded. Blender 4.5+.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "generated"
OUT.mkdir(parents=True, exist_ok=True)
FPS = 30
SPEC = {}


def load_spec():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--spec", type=Path, default=ROOT / "blender/specs/vanguard.json")
    args, _ = parser.parse_known_args(argv)
    path = args.spec.resolve()
    data = json.loads(path.read_text(encoding="utf-8"))
    data["_path"] = str(path)
    return data


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def srgb(hex_color: str):
    def cv(v: int) -> float:
        x = v / 255.0
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return tuple(cv(int(hex_color[i:i+2], 16)) for i in (0, 2, 4)) + (1.0,)


def material(name: str, color: str, metallic: float = 0.0, roughness: float = 0.65):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = srgb(color)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = mat.diffuse_color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


MATS = {}


def finish(obj, name: str, mat_name: str):
    obj.name = name
    if hasattr(obj.data, "materials"):
        obj.data.materials.append(MATS[mat_name])
    return obj


def sphere(name, loc, scale, mat, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=loc)
    obj = finish(bpy.context.object, name, mat)
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def capsule(name, a, b, radius, mat, vertices=16):
    a, b = Vector(a), Vector(b)
    vec = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=vec.length, location=(a+b)/2)
    obj = finish(bpy.context.object, name, mat)
    obj.rotation_euler = vec.to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def cone(name, loc, radius1, radius2, depth, mat, rot=(0,0,0), vertices=12):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.object, name, mat)


def cube(name, loc, scale, mat, bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = finish(bpy.context.object, name, mat)
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("SoftEdge", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    return obj


BONES = [
    ("Root", (0,0,0), (0,0,0.14), None),
    ("Hips", (0,0,0.91), (0,0,1.06), "Root"),
    ("Spine", (0,0,1.06), (0,0,1.27), "Hips"),
    ("Chest", (0,0,1.27), (0,0,1.36), "Spine"),
    ("UpperChest", (0,0,1.36), (0,0,1.44), "Chest"),
    ("Neck", (0,0,1.44), (0,0,1.55), "UpperChest"),
    ("Head", (0,0,1.55), (0,0,1.88), "Neck"),
    ("LeftUpperArm", (-0.23,0,1.39), (-0.43,0,1.20), "UpperChest"),
    ("LeftLowerArm", (-0.43,0,1.20), (-0.58,0,1.02), "LeftUpperArm"),
    ("LeftHand", (-0.58,0,1.02), (-0.62,-0.02,0.94), "LeftLowerArm"),
    ("RightUpperArm", (0.23,0,1.39), (0.43,0,1.20), "UpperChest"),
    ("RightLowerArm", (0.43,0,1.20), (0.58,0,1.02), "RightUpperArm"),
    ("RightHand", (0.58,0,1.02), (0.62,-0.02,0.94), "RightLowerArm"),
    ("LeftUpperLeg", (-0.105,0,0.91), (-0.11,0,0.55), "Hips"),
    ("LeftLowerLeg", (-0.11,0,0.55), (-0.11,0,0.18), "LeftUpperLeg"),
    ("LeftFoot", (-0.11,0,0.18), (-0.11,-0.17,0.08), "LeftLowerLeg"),
    ("RightUpperLeg", (0.105,0,0.91), (0.11,0,0.55), "Hips"),
    ("RightLowerLeg", (0.11,0,0.55), (0.11,0,0.18), "RightUpperLeg"),
    ("RightFoot", (0.11,0,0.18), (0.11,-0.17,0.08), "RightLowerLeg"),
]


def create_rig():
    arm_data = bpy.data.armatures.new("AnimeFighterSkeleton")
    rig = bpy.data.objects.new("AnimeFighterRig", arm_data)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in BONES:
        b = arm_data.edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def bone_parent(obj, rig, bone_name):
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name


def create_character(rig):
    style = SPEC.get("style", {})
    eye_scale = float(style.get("eye_scale", 1.0))
    bang_length = float(style.get("bang_length", 1.0))
    hair_spike_scale = float(style.get("hair_spike_scale", 1.0))
    shoulder_scale = float(style.get("shoulder_scale", 1.0))
    coat_tail_length = float(style.get("coat_tail_length", 1.0))

    parts = [
        (sphere("HeadMesh", (0,-0.005,1.73), (0.205,0.185,0.235), "skin"), "Head"),
        (capsule("NeckMesh", (0,0,1.45), (0,0,1.57), 0.072, "skin"), "Neck"),
        (sphere("ChestMesh", (0,0,1.31), (0.235,0.135,0.23), "coat"), "UpperChest"),
        (sphere("WaistMesh", (0,0,1.07), (0.17,0.115,0.19), "shirt"), "Spine"),
        (sphere("HipMesh", (0,0,0.91), (0.19,0.13,0.16), "pants"), "Hips"),
    ]
    for side, prefix in [(-1,"Left"), (1,"Right")]:
        parts += [
            (capsule(prefix+"UpperArmMesh", (side*0.235,0,1.38), (side*0.43,0,1.20), 0.072, "coat"), prefix+"UpperArm"),
            (capsule(prefix+"LowerArmMesh", (side*0.43,0,1.20), (side*0.58,0,1.02), 0.061, "skin"), prefix+"LowerArm"),
            (sphere(prefix+"HandMesh", (side*0.60,-0.01,0.98), (0.055,0.045,0.075), "skin"), prefix+"Hand"),
            (capsule(prefix+"ThighMesh", (side*0.105,0,0.88), (side*0.11,0,0.55), 0.103, "pants"), prefix+"UpperLeg"),
            (capsule(prefix+"ShinMesh", (side*0.11,0,0.53), (side*0.11,0,0.20), 0.082, "pants_dark"), prefix+"LowerLeg"),
            (cube(prefix+"BootMesh", (side*0.11,-0.075,0.105), (0.16,0.28,0.14), "boots", 0.04), prefix+"Foot"),
        ]
    for side in (-1,1):
        side_name = "L" if side < 0 else "R"
        parts += [
            (sphere(side_name+"Ear", (side*0.202,-0.002,1.735), (0.030,0.022,0.050), "skin", 12, 8), "Head"),
            (sphere(side_name+"Eye", (side*0.075,-0.174,1.765), (0.052*eye_scale,0.018,0.064*eye_scale), "eye_white", 16, 10), "Head"),
            (sphere(side_name+"Iris", (side*0.075,-0.190,1.762), (0.029*eye_scale,0.010,0.041*eye_scale), "iris", 16, 10), "Head"),
            (sphere(side_name+"Pupil", (side*0.075,-0.198,1.758), (0.013*eye_scale,0.006,0.023*eye_scale), "hair", 12, 8), "Head"),
            (sphere(side_name+"EyeHighlight", (side*0.065,-0.204,1.778), (0.008,0.004,0.010), "eye_white", 10, 6), "Head"),
            (capsule(side_name+"Brow", (side*0.118,-0.191,1.835), (side*0.035,-0.195,1.842), 0.010, "hair", 8), "Head"),
        ]
    parts.append((sphere("Nose", (0,-0.193,1.705), (0.018,0.012,0.026), "skin", 10, 6), "Head"))
    parts.append((capsule("Mouth", (-0.045,-0.187,1.65), (0.045,-0.187,1.65), 0.009, "mouth", 8), "Head"))
    parts.append((sphere("HairCap", (0,0.025,1.805), (0.218,0.19,0.215), "hair", 18, 10), "Head"))

    # Layered forehead bangs make the face readable at gameplay distance.
    bang_specs = [
        (-0.130, -0.155, 1.815, -0.18),
        (-0.065, -0.172, 1.805, -0.08),
        (0.000, -0.180, 1.795, 0.00),
        (0.065, -0.172, 1.805, 0.08),
        (0.130, -0.155, 1.815, 0.18),
    ]
    for i, (x, y, z, roll) in enumerate(bang_specs):
        parts.append((cone(f"HairBang{i}", (x,y,z), 0.016, 0.055, 0.22*bang_length, "hair", (0.05,0.0,roll), 10), "Head"))
    spikes = [
        ((0,-0.11,1.96), (0.0,0.35,0.0)),
        ((-0.10,-0.08,1.94), (0.15,0.25,-0.25)),
        ((0.10,-0.08,1.94), (-0.15,0.25,0.25)),
        ((-0.16,-0.01,1.88), (0.35,0.05,-0.45)),
        ((0.16,-0.01,1.88), (-0.35,0.05,0.45)),
        ((0.0,0.13,1.90), (math.pi,0,0)),
    ]
    for i,(loc,rot) in enumerate(spikes):
        parts.append((cone(f"HairSpike{i}", loc, 0.085, 0.018, 0.26*hair_spike_scale, "hair", rot, 10), "Head"))
    parts.append((cube("Scarf", (0,-0.145,1.39), (0.22,0.06,0.08), "accent", 0.025), "UpperChest"))
    parts.append((capsule("ScarfTail", (0.10,0.04,1.37), (0.25,0.30,1.20), 0.040, "accent", 10), "UpperChest"))
    parts.append((cube("Belt", (0,-0.002,0.995), (0.38,0.145,0.035), "pants_dark", 0.012), "Hips"))
    parts.append((cube("BeltBuckle", (0,-0.148,0.995), (0.052,0.022,0.048), "metal", 0.008), "Hips"))
    for side in (-1,1):
        prefix = "Left" if side < 0 else "Right"
        parts.append((sphere(prefix+"ShoulderGuard", (side*0.245,-0.005,1.395), (0.115*shoulder_scale,0.105*shoulder_scale,0.075*shoulder_scale), "coat", 14, 8), prefix+"UpperArm"))
        parts.append((cube(prefix+"Cuff", (side*0.52,-0.005,1.08), (0.085,0.075,0.055), "pants_dark", 0.015), prefix+"LowerArm"))
        parts.append((cube("CoatTailL" if side<0 else "CoatTailR", (side*0.09,0.045,0.91), (0.14,0.07,0.30*coat_tail_length), "coat", 0.03), "Hips"))
    for obj in (
        capsule("SwordGrip", (0.61,-0.03,0.97), (0.61,-0.03,0.78), 0.026, "grip", 10),
        cube("SwordGuard", (0.61,-0.03,0.79), (0.18,0.045,0.035), "metal", 0.01),
        cube("SwordBlade", (0.61,-0.03,0.51), (0.07,0.026,0.52), "blade", 0.008),
        cone("SwordTip", (0.61,-0.03,0.235), 0.07, 0.0, 0.16, "blade", (0,0,0), 4),
    ):
        parts.append((obj, "RightHand"))
    for obj,bone in parts:
        bone_parent(obj, rig, bone)


def clear_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0,0,0)
        pb.location = (0,0,0)
        pb.scale = (1,1,1)


def key(rig, frame, rotations=None, locations=None):
    for name, rot in (rotations or {}).items():
        pb = rig.pose.bones[name]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = rot
        pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    for name, loc in (locations or {}).items():
        pb = rig.pose.bones[name]
        pb.location = loc
        pb.keyframe_insert(data_path="location", frame=frame, group=name)


def make_action(rig, name, length, keys, loop=False):
    action = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = action
    clear_pose(rig)
    for frame, rots, locs in keys:
        key(rig, frame, rots, locs)
    action["loop"] = loop
    action["lengthFrames"] = length
    return action


def create_actions(rig):
    actions = [
        make_action(rig, "Idle", 60, [
            (0, {"Chest":(0,0,0)}, {"Hips":(0,0,0)}),
            (30, {"Chest":(0.025,0,0), "Head":(-0.015,0,0)}, {"Hips":(0,0,0.012)}),
            (60, {"Chest":(0,0,0)}, {"Hips":(0,0,0)}),
        ], True),
        make_action(rig, "Run", 20, [
            (0, {"LeftUpperLeg":(0.60,0,0), "RightUpperLeg":(-0.55,0,0), "LeftLowerLeg":(-0.35,0,0), "RightLowerLeg":(0.60,0,0), "LeftUpperArm":(-0.65,0,0), "RightUpperArm":(0.65,0,0), "Chest":(0.12,0,0)}, {"Hips":(0,0,0.03)}),
            (10, {"LeftUpperLeg":(-0.55,0,0), "RightUpperLeg":(0.60,0,0), "LeftLowerLeg":(0.60,0,0), "RightLowerLeg":(-0.35,0,0), "LeftUpperArm":(0.65,0,0), "RightUpperArm":(-0.65,0,0), "Chest":(0.12,0,0)}, {"Hips":(0,0,-0.01)}),
            (20, {"LeftUpperLeg":(0.60,0,0), "RightUpperLeg":(-0.55,0,0), "LeftLowerLeg":(-0.35,0,0), "RightLowerLeg":(0.60,0,0), "LeftUpperArm":(-0.65,0,0), "RightUpperArm":(0.65,0,0), "Chest":(0.12,0,0)}, {"Hips":(0,0,0.03)}),
        ], True),
        make_action(rig, "Attack", 28, [
            (0, {}, {}),
            (7, {"Chest":(0,-0.18,-0.32), "RightUpperArm":(-0.30,-0.15,-1.15), "RightLowerArm":(-0.70,0.10,-0.25), "Hips":(0,0,-0.18)}, {}),
            (12, {"Chest":(0,0.18,0.55), "RightUpperArm":(0.20,0.20,1.20), "RightLowerArm":(-0.20,-0.10,0.65), "LeftUpperArm":(-0.20,0,-0.25), "Hips":(0,0,0.24)}, {}),
            (18, {"Chest":(0,0.04,0.30), "RightUpperArm":(0.10,0.15,0.85), "RightLowerArm":(-0.10,0,0.45), "Hips":(0,0,0.10)}, {}),
            (28, {"Chest":(0,0,0), "RightUpperArm":(0,0,0), "RightLowerArm":(0,0,0), "LeftUpperArm":(0,0,0), "Hips":(0,0,0)}, {}),
        ]),
        make_action(rig, "Dodge", 24, [
            (0, {}, {"Hips":(0,0,0)}),
            (6, {"Chest":(0.55,0,0), "LeftUpperArm":(0.25,0,0.35), "RightUpperArm":(0.25,0,-0.35)}, {"Hips":(0,-0.05,-0.08)}),
            (13, {"Chest":(0.85,0,0), "Head":(-0.35,0,0), "LeftUpperLeg":(0.55,0,0), "RightUpperLeg":(-0.35,0,0)}, {"Hips":(0,-0.38,-0.22)}),
            (20, {"Chest":(0.18,0,0), "LeftUpperLeg":(0,0,0), "RightUpperLeg":(0,0,0)}, {"Hips":(0,-0.10,-0.03)}),
            (24, {"Chest":(0,0,0), "Head":(0,0,0)}, {"Hips":(0,0,0)}),
        ]),
    ]
    rig.animation_data.action = None
    for action in actions:
        track = rig.animation_data.nla_tracks.new()
        track.name = action.name
        strip = track.strips.new(action.name, 0, action)
        strip.action_frame_start = 0
        strip.action_frame_end = action["lengthFrames"]


def setup_preview():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(OUT / "anime_fighter.png")
    scene.world.color = (0.055,0.07,0.09)
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0,0,0))
    finish(bpy.context.object, "PreviewFloor", "floor")
    for name, loc, energy, size in [
        ("Key", (-3,-4,5), 850, 4),
        ("Fill", (3,-2,3), 400, 3),
    ]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = loc
        light.rotation_euler = (Vector((0,0,1.0))-light.location).to_track_quat("-Z","Y").to_euler()
    data = bpy.data.cameras.new("Camera")
    cam = bpy.data.objects.new("Camera", data)
    bpy.context.collection.objects.link(cam)
    cam.location = (3.2,-5.5,2.5)
    cam.rotation_euler = (Vector((0,0,1.0))-cam.location).to_track_quat("-Z","Y").to_euler()
    data.lens = 55
    scene.camera = cam
    scene.frame_set(0)


def export(rig):
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "anime_fighter.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for obj in bpy.context.scene.objects:
        if obj.parent == rig:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / "anime_fighter.fbx"),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=True,
        bake_anim_simplify_factor=0.0,
        object_types={"ARMATURE","MESH"},
        mesh_smooth_type="FACE",
    )


if __name__ == "__main__":
    SPEC = load_spec()
    palette = SPEC.get("palette", {})
    reset_scene()
    MATS.update({
        "skin": material("Skin", palette.get("skin", "F2C6A8")),
        "hair": material("Hair", palette.get("hair", "1C2230"), 0.0, 0.45),
        "coat": material("Coat", palette.get("coat", "233A66")),
        "shirt": material("Shirt", palette.get("shirt", "E8EEF5")),
        "pants": material("Pants", palette.get("pants", "202739")),
        "pants_dark": material("PantsDark", palette.get("pants_dark", "141A28")),
        "boots": material("Boots", palette.get("boots", "19151A")),
        "accent": material("Accent", palette.get("accent", "C33E4A")),
        "eye_white": material("EyeWhite", palette.get("eye_white", "FAFAF8")),
        "iris": material("Iris", palette.get("iris", "2E7CA5")),
        "mouth": material("Mouth", palette.get("mouth", "7D3543")),
        "metal": material("Metal", palette.get("metal", "808A9A"), 0.75, 0.35),
        "blade": material("Blade", palette.get("blade", "DDE8F2"), 0.75, 0.22),
        "grip": material("Grip", palette.get("grip", "33252A")),
        "floor": material("Floor", palette.get("floor", "28303A")),
    })
    rig = create_rig()
    create_character(rig)
    create_actions(rig)
    export(rig)
    setup_preview()
    bpy.ops.render.render(write_still=True)
    print("Generated:", OUT / "anime_fighter.fbx", "spec=", SPEC.get("_path"), "name=", SPEC.get("name"))
