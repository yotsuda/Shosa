# Renders a dance written by `shosa gltf` on a VRM character, frame by frame, into PNGs for a video.
# One .glb per dancer (a score of one dancer each), each imported with the Shosa bone map, and the
# character imported with its own bone map (tools\godot_bonemap.gd).
#
#   godot --path PROJECT -s C:\...\godot_video.gd -- character.glb OUT_FOLDER dance_alice.glb dance_bob.glb ... [--fps 30] [--size 1920x1080] [--seconds N]
#
# Not --headless: the headless renderer draws nothing.
extends SceneTree


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var files: Array[String] = []
	var options := {"--fps": "30", "--size": "1920x1080", "--seconds": ""}
	var i := 0
	while i < args.size():
		if options.has(args[i]) and i + 1 < args.size():
			options[args[i]] = args[i + 1]
			i += 2
		else:
			files.append(args[i])
			i += 1
	if files.size() < 3:
		print("usage: -- CHARACTER.glb OUT_FOLDER DANCER.glb... [--fps 30] [--size WxH] [--seconds N]")
		quit(1)
		return
	await process_frame  # the root is in the tree only from here
	var size_parts: PackedStringArray = options["--size"].split("x")
	var size := Vector2i(int(size_parts[0]), int(size_parts[1]))
	var fps := float(options["--fps"])

	var viewport := SubViewport.new()
	viewport.size = size
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d = Viewport.MSAA_4X
	root.add_child(viewport)

	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.93, 0.94, 0.97)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color.WHITE
	environment.ambient_light_energy = 0.6
	var world := WorldEnvironment.new()
	world.environment = environment
	viewport.add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-35, 20, 0)
	sun.light_energy = 0.9
	sun.shadow_enabled = true
	viewport.add_child(sun)
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(30, 30)
	floor_mesh.mesh = plane
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color(0.55, 0.57, 0.63)
	floor_mesh.material_override = floor_material
	viewport.add_child(floor_mesh)

	# Each dancer: its own figure (hidden, it only carries the animation) and a character playing it.
	var players: Array[AnimationPlayer] = []
	var animation := ""
	var length := 0.0
	for file in files.slice(2):
		var scene := instantiate(file)
		if scene == null:
			return
		viewport.add_child(scene)
		scene.visible = false
		var player: AnimationPlayer = scene.find_children("*", "AnimationPlayer", true, false)[0]
		animation = player.get_animation_list()[0]
		length = player.get_animation(animation).length
		var figure: Node3D = scene.find_children("*", "Skeleton3D", true, false)[0].get_parent()
		var character := instantiate(files[0])
		if character == null:
			return
		viewport.add_child(character)
		character.position = figure.position
		var character_player := AnimationPlayer.new()
		character.add_child(character_player)
		character_player.add_animation_library("", player.get_animation_library(""))
		character_player.play(animation)
		character_player.pause()
		players.append(character_player)
		print(file, ": figure node at ", figure.position, ", '", animation, "' ", "%.3f" % length, " s")

	var camera := Camera3D.new()
	camera.fov = 30
	viewport.add_child(camera)
	camera.look_at_from_position(Vector3(0, 1.15, 6.2), Vector3(0, 0.85, 0))

	var seconds := length if options["--seconds"] == "" else minf(length, float(options["--seconds"]))
	var frames := int(floor(seconds * fps + 0.5))
	for p in players:
		p.seek(length, true)
	await process_frame
	for f in frames:
		var time := f / fps
		for p in players:
			p.seek(time, true)
		await process_frame
		await RenderingServer.frame_post_draw
		var image := viewport.get_texture().get_image()
		image.save_png("%s/%05d.png" % [files[1], f])
		if f % 60 == 0:
			print("frame ", f, " / ", frames)
	print("done: ", frames, " frames at ", fps, " fps -> ", files[1])
	quit()


func instantiate(file: String) -> Node3D:
	var packed: PackedScene = load("res://" + file) if ResourceLoader.exists("res://" + file) else null
	if packed == null:
		print("res://", file, " is not imported")
		quit(1)
		return null
	return packed.instantiate()
