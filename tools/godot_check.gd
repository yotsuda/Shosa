# Reads a glTF written by `shosa gltf` in Godot 4 and says what Godot made of it: the nodes,
# the skeleton, the animations, where the animation's `extras` went, and how far every bone
# is from the solver's own joints (`shosa clip`).
#
#   godot --headless --path out\godot\proj --import          (once, after the .glb is put in the project)
#   godot --headless --path out\godot\proj -s C:\…\tools\godot_check.gd -- walk.glb [C:\…\out\godot\walk.clip.json [figure index]]
#
# The .glb is named as it lies in the project (res://); the script and the clip by their FULL paths
# on disk (Godot's working directory is the project's).
extends SceneTree

const BONE_JOINT := {
	"hips": "pelvis", "chest": "waist", "neck": "chest", "head": "head",
	"leftUpperArm": "leftShoulder", "leftLowerArm": "leftElbow", "leftHand": "leftHand",
	"rightUpperArm": "rightShoulder", "rightLowerArm": "rightElbow", "rightHand": "rightHand",
	"leftUpperLeg": "leftHip", "leftLowerLeg": "leftKnee", "leftFoot": "leftAnkle",
	"rightUpperLeg": "rightHip", "rightLowerLeg": "rightKnee", "rightFoot": "rightAnkle",
}


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		print("usage: -- FILE.glb [CLIP.json [figure index]]")
		quit(1)
		return
	var res_path := "res://" + args[0]

	# ---- The file as its JSON: this is where the extras are, whatever the importer does with them.
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	var error := document.append_from_file(ProjectSettings.globalize_path(res_path), state)
	print("== GLTFDocument.append_from_file: ", error_string(error))
	var json: Dictionary = state.json
	for a in json.get("animations", []):
		print("  json animation '", a.get("name"), "' extras: ", JSON.stringify(a.get("extras")))
	for a in state.get_animations():
		print("  GLTFAnimation '", a.original_name, "' loop ", a.loop, "  additional data 'extras': ", a.get_additional_data("extras"))

	# ---- The scene the editor's import made.
	var packed: PackedScene = load(res_path)
	if packed == null:
		print("== not imported: run `godot --headless --path PROJECT --import` first")
		quit(1)
		return
	var scene := packed.instantiate()
	root.add_child(scene)
	print("== nodes")
	dump(scene, 1)

	var skeletons: Array[Node] = scene.find_children("*", "Skeleton3D", true, false)
	var profile := SkeletonProfileHumanoid.new()
	for s: Skeleton3D in skeletons:
		print("== skeleton ", scene.get_path_to(s), ": ", s.get_bone_count(), " bones")
		for i in s.get_bone_count():
			var name := s.get_bone_name(i)
			var rest := s.get_bone_global_rest(i).origin
			var godot_name := name[0].to_upper() + name.substr(1)
			var in_profile := profile.find_bone(godot_name) >= 0
			print("  %-14s parent %-14s rest (%6.3f %6.3f %6.3f)  SkeletonProfileHumanoid: %s" % [
				name, s.get_bone_name(s.get_bone_parent(i)) if s.get_bone_parent(i) >= 0 else "-",
				rest.x, rest.y, rest.z, godot_name if in_profile else "NOT THERE"])

	var players: Array[Node] = scene.find_children("*", "AnimationPlayer", true, false)
	for p: AnimationPlayer in players:
		print("== player ", scene.get_path_to(p), " (root node ", p.root_node, ")")
		for name in p.get_animation_list():
			var a := p.get_animation(name)
			var paths := {}
			var types := {}
			for t in a.get_track_count():
				paths[str(a.track_get_path(t)).get_slice(":", 1)] = true
				types[a.track_get_type(t)] = types.get(a.track_get_type(t), 0) + 1
			print("  '%s' length %.3f s  loop_mode %d  tracks %d (by type %s) on %s  meta %s" % [
				name, a.length, a.loop_mode, a.get_track_count(), types, paths.keys(), a.get_meta_list()])
			if a.has_meta("shosa"):  # put there by tools\godot_shosa_import.gd
				print("    shosa: ", JSON.stringify(a.get_meta("shosa")))

	# ---- The marks as signals (tools\godot_shosa_import.gd): play every animation once through
	# and compare what `ShosaMarks.mark_reached` said with the marks of the extras.
	for p: AnimationPlayer in players:
		var marks_node := p.get_node_or_null(p.root_node).get_node_or_null("ShosaMarks") if p.get_node_or_null(p.root_node) else null
		if marks_node == null:
			continue
		print("== marks as signals (", scene.get_path_to(marks_node), ", script ", marks_node.get_script(), ", has the method: ", marks_node.has_method("_shosa_mark"), ")")
		await process_frame  # before the first frame the player calls no method
		var heard: Array = []
		marks_node.connect("mark_reached", func(mark, figure, section, play): heard.append("%s/%s/%s/%d" % [mark, figure, section, play]))
		p.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
		p.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		for name in p.get_animation_list():
			var a := p.get_animation(name)
			var want: Array = []
			if a.has_meta("shosa"):
				for m in a.get_meta("shosa").get("marks", []):
					want.append("%s/%s/%s/%d" % [m["name"], m["figure"], m["section"], int(m["play"])])
			heard.clear()
			p.play(name)
			p.advance(0)
			var t := 0.0
			while t < a.length - 0.0001:  # once through: a looping animation would say the marks again
				var dt: float = min(1 / 60.0, a.length - t)
				p.advance(dt)
				t += dt
			p.stop()
			var got := heard.duplicate()
			want.sort()
			got.sort()
			print("  '%s': %d marks, %d signals  %s" % [name, want.size(), got.size(), "ok" if want == got else "DIFFERENT: want %s, got %s" % [want, got]])
			# A looping animation three times round, in steps that do not land on the seam: every
			# mark three times, the ones at the very start and the very end too?
			if a.loop_mode != Animation.LOOP_NONE and not want.is_empty():
				heard.clear()
				p.play(name)
				p.advance(0)
				t = 0.0
				while t < a.length * 3 - 0.02:
					p.advance(0.0137)
					t += 0.0137
				p.stop()
				print("    three times round (to just before the end of the third): %d signals, %d marks a round" % [heard.size(), want.size()])
		p.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_IDLE  # as it was, for the positions below

		# The same through an AnimationTree: one animation alone, then two blended half and half.
		var marked: Array = []
		for name in p.get_animation_list():
			if p.get_animation(name).has_meta("shosa") and not p.get_animation(name).get_meta("shosa").get("marks", []).is_empty():
				marked.append(name)
		if not marked.is_empty():
			var tree := AnimationTree.new()
			p.get_parent().add_child(tree)
			tree.anim_player = tree.get_path_to(p)
			tree.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
			tree.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
			var blend := AnimationNodeBlendTree.new()
			var one := AnimationNodeAnimation.new()
			one.animation = marked[0]
			var two := AnimationNodeAnimation.new()
			two.animation = marked[-1]
			var mix := AnimationNodeBlend2.new()
			blend.add_node("one", one)
			blend.add_node("two", two)
			blend.add_node("mix", mix)
			blend.connect_node("mix", 0, "one")
			blend.connect_node("mix", 1, "two")
			blend.connect_node("output", 0, "mix")
			tree.tree_root = blend
			tree.active = true
			for amount: float in [0.0, 0.5, 1.0] if marked.size() > 1 else [0.0]:
				tree.set("parameters/mix/blend_amount", amount)
				heard.clear()
				tree.active = false  # off and on: both animations start again
				tree.active = true
				tree.advance(0)
				var shortest: float = min(p.get_animation(marked[0]).length, p.get_animation(marked[-1]).length)
				var t := 0.0
				while t < shortest - 0.0001:
					var dt: float = min(1 / 60.0, shortest - t)
					tree.advance(dt)
					t += dt
				var count := func(name: String) -> int:
					var n := 0
					for m in p.get_animation(name).get_meta("shosa").get("marks", []):
						if m["time"] <= shortest + 0.0001:
							n += 1
					return n
				print("  AnimationTree, '%s' and '%s' blended %.1f, %.3f s: %d signals %s (marks in that time: %d and %d)" % [
					marked[0], marked[-1], amount, shortest, heard.size(), heard, count.call(marked[0]), count.call(marked[-1])])
			tree.active = false
			tree.free()

	# ---- Positions: Godot's bones against the solver's joints.
	if args.size() > 1 and not players.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
		if not parsed is Dictionary:  # an error here would leave Godot running with nothing to do
			print("== no clip at ", args[1], " (give the path in full: Godot's working directory is the project's)")
			quit(1)
			return
		var clip: Dictionary = parsed
		var figure_index := int(args[2]) if args.size() > 2 else 0
		var figure: Dictionary = clip["figures"][figure_index]
		var skeleton: Skeleton3D = skeletons[0]
		for s: Skeleton3D in skeletons:
			if str(scene.get_path_to(s)).contains(figure["id"]):
				skeleton = s
		var player: AnimationPlayer = players[0]
		var animation := player.get_animation_list()[0]
		var joints: Array = clip["joints"]
		var frames: Array = figure["frames"]
		var worst := 0.0
		var worst_at := ""
		player.play(animation)
		player.pause()  # or the time runs on by itself between the frames
		player.seek(player.get_animation(animation).length, true)  # a paused player does nothing for a seek to where it is: frame 0
		await process_frame
		for f in frames.size():
			player.seek(f / float(clip["fps"]), true)
			# The player puts the pose on the skeleton when a frame is processed, and the signal
			# comes before the nodes are processed: the second one is after this seek.
			await process_frame
			await process_frame
			for bone in BONE_JOINT:
				var i: int = joints.find(BONE_JOINT[bone]) * 3
				var want := Vector3(frames[f][i], frames[f][i + 1], frames[f][i + 2])
				var got: Vector3 = (to_scene(skeleton, scene) * skeleton.get_bone_global_pose(find_bone(skeleton, bone))).origin
				if got.distance_to(want) > worst:
					worst = got.distance_to(want)
					worst_at = "%s, frame %d: Godot (%.3f %.3f %.3f), solver (%.3f %.3f %.3f)" % [bone, f, got.x, got.y, got.z, want.x, want.y, want.z]
		print("== '%s' on %s: worst position error over %d frames: %.2f mm (%s)" % [
			animation, scene.get_path_to(skeleton), frames.size(), worst * 1000, worst_at])
	quit()


func dump(node: Node, depth: int) -> void:
	var meta := ""
	for key in node.get_meta_list():
		meta += "  meta %s = %s" % [key, node.get_meta(key)]
	var where := ""
	if node is Node3D and node.transform != Transform3D.IDENTITY:
		where = "  at %s" % node.position
	print("  ".repeat(depth), node.name, " (", node.get_class(), ")", where, meta)
	for child in node.get_children():
		dump(child, depth + 1)


# By Shosa's name, or by Godot's when the import retargeted the skeleton (`hips` → `Hips`).
func find_bone(skeleton: Skeleton3D, name: String) -> int:
	var i := skeleton.find_bone(name)
	return i if i >= 0 else skeleton.find_bone(name[0].to_upper() + name.substr(1))


# The node's transform in the scene, without asking the tree for it.
func to_scene(node: Node3D, scene: Node) -> Transform3D:
	var t := node.transform
	var parent := node.get_parent()
	while parent != null and parent != scene.get_parent():
		if parent is Node3D:
			t = parent.transform * t
		parent = parent.get_parent()
	return t
