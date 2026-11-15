# Writes the BoneMap that tells Godot which bone of a glTF is which bone of its humanoid
# (SkeletonProfileHumanoid), so that an animation from `shosa gltf` can play on another character.
# With the map on both files, Godot names both skeletons `GeneralSkeleton` and both sets of bones
# by its own names, and one file's animations play on the other.
#
#   godot --headless --path PROJECT -s C:\…\tools\godot_bonemap.gd -- walk.glb shosa_bonemap.tres
#   godot --headless --path PROJECT -s C:\…\tools\godot_bonemap.gd -- character.glb character_bonemap.tres
#
# The file is read as its JSON: a VRM (1.0 or 0.x, renamed to .glb) says its humanoid bones itself;
# any other file is taken to name its bones as VRM does (`hips`, `leftUpperArm`: what Shosa writes).
# Then, in FILE.glb.import, under `_subresources` > `nodes` (the script prints the skeleton's path
# when the file is already imported):
#
#   "PATH:solo/Skeleton3D": { "retarget/bone_map": Resource("res://shosa_bonemap.tres") }
extends SceneTree


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		print("usage: -- FILE.glb OUT.tres")
		quit(1)
		return
	var res_path := "res://" + args[0]
	var state := GLTFState.new()
	var error := GLTFDocument.new().append_from_file(ProjectSettings.globalize_path(res_path), state)
	if error != OK:
		print("cannot read ", res_path, ": ", error_string(error))
		quit(1)
		return
	var json: Dictionary = state.json
	var nodes: Array = json.get("nodes", [])
	var extensions: Dictionary = json.get("extensions", {})

	# VRM's name of a bone -> the node's name in this file.
	var bones := {}
	if extensions.has("VRMC_vrm"):
		var human: Dictionary = extensions["VRMC_vrm"]["humanoid"]["humanBones"]
		for name in human:
			bones[name] = nodes[int(human[name]["node"])].get("name", "")
	elif extensions.has("VRM"):
		for b: Dictionary in extensions["VRM"]["humanoid"]["humanBones"]:
			bones[b["bone"]] = nodes[int(b["node"])].get("name", "")
	else:
		for skin: Dictionary in json.get("skins", []):
			for j in skin.get("joints", []):
				var name: String = nodes[int(j)].get("name", "")
				bones[name] = name

	var profile := SkeletonProfileHumanoid.new()
	var map := BoneMap.new()
	map.profile = profile
	var mapped := 0
	var left_out: Array[String] = []
	for name: String in bones:
		if name.is_empty():
			continue
		var godot_name := name[0].to_upper() + name.substr(1)  # hips -> Hips, leftUpperArm -> LeftUpperArm
		if profile.find_bone(godot_name) >= 0:
			map.set_skeleton_bone_name(godot_name, bones[name])
			mapped += 1
		else:
			left_out.append(name)
	error = ResourceSaver.save(map, "res://" + args[1])
	print("res://", args[1], ": ", error_string(error), ", ", mapped, " of the profile's ", profile.bone_size, " bones",
		"" if left_out.is_empty() else " (not in Godot's humanoid: %s)" % ", ".join(left_out))

	var packed: PackedScene = load(res_path) if ResourceLoader.exists(res_path) else null
	if packed != null:
		var scene := packed.instantiate()
		for s in scene.find_children("*", "Skeleton3D", true, false):
			print("in ", args[0], ".import, under _subresources > nodes:  \"PATH:", scene.get_path_to(s), "\": { \"retarget/bone_map\": Resource(\"res://", args[1], "\") }")
		scene.free()
	quit()
