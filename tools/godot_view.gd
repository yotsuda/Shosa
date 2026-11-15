# Draws what Godot 4 plays from a glTF written by `shosa gltf`: frames of one animation, side by
# side in one PNG. The file has bones and no mesh, so the bones are drawn as sticks (left blue,
# right red, as in `shosa sheet`). With a character (a model with a mesh, imported with a bone
# map: tools\godot_bonemap.gd) the same animation is played on it too, to the figure's left.
#
#   godot --path PROJECT -s C:\…\tools\godot_view.gd -- fighter.glb C:\…\out\godot\combo.png [options]
#     --animation NAME     which animation (the first one when not given)
#     --on CHARACTER.glb   a character of the project to play it on as well
#     --frames N           how many frames (8), evenly over the animation
#     --view front|side    where the camera stands (front)
#
# Not --headless: the headless renderer draws nothing. A window opens while it runs.
# The .glb files are named as they lie in the project (res://); the PNG by its full path.
extends SceneTree

const TILE := Vector2i(480, 440)
const COLUMNS := 4


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var files: Array[String] = []
	var options := {"--animation": "", "--on": "", "--frames": "8", "--view": "front"}
	var i := 0
	while i < args.size():
		if options.has(args[i]) and i + 1 < args.size():
			options[args[i]] = args[i + 1]
			i += 2
		else:
			files.append(args[i])
			i += 1
	if files.size() < 2:
		print("usage: -- FILE.glb OUT.png [--animation NAME] [--on CHARACTER.glb] [--frames N] [--view front|side]")
		quit(1)
		return

	var viewport := SubViewport.new()
	viewport.size = TILE
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d = Viewport.MSAA_4X
	root.add_child(viewport)

	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.96, 0.96, 0.94)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color.WHITE
	environment.ambient_light_energy = 0.7
	var world := WorldEnvironment.new()
	world.environment = environment
	viewport.add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, 30, 0)
	viewport.add_child(sun)
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(40, 40)
	floor_mesh.mesh = plane
	floor_mesh.material_override = flat(Color(0.86, 0.86, 0.83))
	viewport.add_child(floor_mesh)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 2.3
	viewport.add_child(camera)

	var scene := instantiate(files[0])
	if scene == null:
		return
	viewport.add_child(scene)
	var players := scene.find_children("*", "AnimationPlayer", true, false)
	var skeletons := scene.find_children("*", "Skeleton3D", true, false)
	if players.is_empty() or skeletons.is_empty():
		print(files[0], " has no animation or no skeleton")
		quit(1)
		return
	var player: AnimationPlayer = players[0]
	var animation_name: String = options["--animation"] if options["--animation"] != "" else player.get_animation_list()[0]
	if not player.has_animation(animation_name):
		print("no animation '", animation_name, "' in ", files[0], ": ", player.get_animation_list())
		quit(1)
		return
	var length := player.get_animation(animation_name).length

	# Sticks for every bone that has a parent, in every skeleton of the file.
	var sticks: Array = []  # [skeleton, bone, parent bone, MeshInstance3D]
	for s: Skeleton3D in skeletons:
		for b in s.get_bone_count():
			if s.get_bone_parent(b) < 0:
				continue
			var name := s.get_bone_name(b).to_lower()
			var stick := MeshInstance3D.new()
			var cylinder := CylinderMesh.new()
			cylinder.top_radius = 0.018
			cylinder.bottom_radius = 0.018
			cylinder.height = 1.0
			cylinder.radial_segments = 8
			stick.mesh = cylinder
			stick.material_override = flat(Color(0.15, 0.35, 0.9) if name.begins_with("left")
				else Color(0.9, 0.2, 0.15) if name.begins_with("right") else Color(0.2, 0.2, 0.2))
			viewport.add_child(stick)
			sticks.append([s, b, s.get_bone_parent(b), stick])

	# The character, 1.1 m to the figure's left (behind it in the side view: to the right in the
	# picture either way), with a player of its own on the same animations.
	var character: Node3D = null
	var character_player: AnimationPlayer = null
	var character_skeleton: Skeleton3D = null
	if options["--on"] != "":
		character = instantiate(options["--on"])
		if character == null:
			return
		viewport.add_child(character)
		character.position = Vector3(0, 0, -1.1) if options["--view"] == "side" else Vector3(1.1, 0, 0)
		character_skeleton = character.find_children("*", "Skeleton3D", true, false)[0]
		character_player = AnimationPlayer.new()
		character.add_child(character_player)
		# The animations call a node `ShosaMarks` beside the player (tools\godot_shosa_import.gd):
		# a copy of the file's own gives the character the same signal.
		var marks := scene.get_node_or_null("ShosaMarks")
		if marks != null:
			character.add_child(marks.duplicate())
		character_player.add_animation_library("", player.get_animation_library(""))
		print("character ", options["--on"], ": skeleton '", character_skeleton.name, "' ", character_skeleton.get_bone_count(),
			" bones, motion scale ", "%.3f" % character_skeleton.motion_scale, " (the figure's: ", "%.3f" % (skeletons[0] as Skeleton3D).motion_scale, ")")

	var frames := maxi(1, int(options["--frames"]))
	var rows := ceili(frames / float(COLUMNS))
	var sheet := Image.create(TILE.x * mini(frames, COLUMNS), TILE.y * rows, false, Image.FORMAT_RGBA8)
	for p: AnimationPlayer in [player, character_player]:
		if p == null:
			continue
		p.play(animation_name)
		p.pause()
		p.seek(length, true)  # a paused player does nothing for a seek to where it is
	await process_frame

	var side: bool = options["--view"] == "side"
	var lowest := {"figure": INF, "character": INF}
	for f in frames:
		var time := length * f / float(maxi(1, frames - 1)) if frames > 1 else 0.0
		player.seek(time, true)
		if character_player != null:
			character_player.seek(time, true)
		await process_frame
		await process_frame
		var hips := Vector3.ZERO
		for stick in sticks:
			var s: Skeleton3D = stick[0]
			var a: Vector3 = (s.global_transform * s.get_bone_global_pose(stick[1])).origin
			var b: Vector3 = (s.global_transform * s.get_bone_global_pose(stick[2])).origin
			place(stick[3], a, b)
			lowest["figure"] = minf(lowest["figure"], minf(a.y, b.y))
		# All the figures in the picture: the middle of their hips, and a camera wide enough.
		var low := Vector3(INF, 0, INF)
		var high := Vector3(-INF, 0, -INF)
		for s: Skeleton3D in skeletons:
			var at: Vector3 = (s.global_transform * s.get_bone_global_pose(0)).origin
			low = Vector3(minf(low.x, at.x), 0, minf(low.z, at.z))
			high = Vector3(maxf(high.x, at.x), 0, maxf(high.z, at.z))
		hips = (low + high) / 2
		var spread := (high.z - low.z) if side else (high.x - low.x)
		camera.size = maxf(2.3, (spread + 1.8) * TILE.y / TILE.x)
		if character_skeleton != null:
			for b in character_skeleton.get_bone_count():
				if character_skeleton.get_bone_name(b).ends_with("Foot"):
					lowest["character"] = minf(lowest["character"], (character_skeleton.global_transform * character_skeleton.get_bone_global_pose(b)).origin.y)
		# The camera follows the figures over the floor, so a walk stays in the picture.
		var centre := Vector3(hips.x, camera.size / 2 - 0.15, hips.z)
		if character != null:
			centre += character.position / 2
		camera.position = centre + (Vector3(5, 0, 0) if side else Vector3(0, 0, 5))
		camera.look_at(centre)
		await RenderingServer.frame_post_draw
		var image := viewport.get_texture().get_image()
		image.convert(Image.FORMAT_RGBA8)
		sheet.blit_rect(image, Rect2i(Vector2i.ZERO, TILE), Vector2i(TILE.x * (f % COLUMNS), TILE.y * (f / COLUMNS)))
	var error := sheet.save_png(files[1])
	print("'", animation_name, "' ", "%.3f" % length, " s, ", frames, " frames -> ", files[1], ": ", error_string(error))
	print("lowest joint of the figure: %.3f m" % lowest["figure"],
		"" if character == null else ";  lowest foot bone of the character: %.3f m" % lowest["character"])

	# The marks on the character: once through, counting what its own ShosaMarks says.
	if character != null and character.has_node("ShosaMarks"):
		var heard: Array = []
		character.get_node("ShosaMarks").connect("mark_reached", func(mark, figure, section, play): heard.append(mark))
		character_player.callback_mode_method = AnimationMixer.ANIMATION_CALLBACK_MODE_METHOD_IMMEDIATE
		character_player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		character_player.stop()
		character_player.play(animation_name)
		character_player.advance(0)
		var t := 0.0
		while t < length - 0.0001:
			var dt: float = min(1 / 60.0, length - t)
			character_player.advance(dt)
			t += dt
		var meta: Dictionary = player.get_animation(animation_name).get_meta("shosa", {})
		print("marks on the character: ", heard.size(), " signals ", heard, " (the animation has ", meta.get("marks", []).size(), " marks)")
	quit()


func instantiate(file: String) -> Node3D:
	var packed: PackedScene = load("res://" + file) if ResourceLoader.exists("res://" + file) else null
	if packed == null:
		print("res://", file, " is not imported: run `godot --headless --path PROJECT --import` first")
		quit(1)
		return null
	return packed.instantiate()


func flat(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return material


# A cylinder of height 1 along Y, laid from one joint to the other.
func place(stick: MeshInstance3D, a: Vector3, b: Vector3) -> void:
	var along := b - a
	var length := along.length()
	if length < 0.0001:
		stick.visible = false
		return
	stick.visible = true
	var y := along / length
	var x := y.cross(Vector3.FORWARD if absf(y.dot(Vector3.FORWARD)) < 0.9 else Vector3.RIGHT).normalized()
	var z := x.cross(y)
	stick.global_transform = Transform3D(Basis(x, y * length, z), (a + b) / 2)
