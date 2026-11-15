# Godot 4 drops the `extras` of a glTF animation. This import script reads them from the file
# again and puts them where a game can use them:
#
#   animation.loop_mode          from `loop`
#   animation.get_meta("shosa")  the whole extras: { loop, velocity, marks }
#   a node `ShosaMarks`          beside the AnimationPlayer, with the signal
#                                mark_reached(mark: String, figure: String, section: String, play: int)
#                                emitted when a playing animation passes a mark (`!hit` in the .shosa file)
#
#   $Hero/ShosaMarks.mark_reached.connect(func(mark, figure, section, play): if mark == "hit": ...)
#
# Copy it into the Godot project and name it in the .glb's Import dock under Import Script > Path
# (or `import_script/path="res://godot_shosa_import.gd"` in the .glb.import file), then reimport.
@tool
extends EditorScenePostImport

# The script of the ShosaMarks node. It is kept inside the imported scene, so this file is the
# only one to copy. The animations call `_shosa_mark` from a method track; the game connects to the signal.
const MARKS_SCRIPT := """extends Node

## A playing animation passed a mark: its name (`!hit` is "hit"), the figure that wrote it,
## the section it is in and which play of that section this is (from 1).
signal mark_reached(mark: String, figure: String, section: String, play: int)


func _shosa_mark(mark: String, figure: String, section: String, play: int) -> void:
	mark_reached.emit(mark, figure, section, play)
"""


func _post_import(scene: Node) -> Object:
	var state := GLTFState.new()
	if GLTFDocument.new().append_from_file(ProjectSettings.globalize_path(get_source_file()), state) != OK:
		return scene
	for player: AnimationPlayer in scene.find_children("*", "AnimationPlayer", true, false):
		var marks_node: Node = null
		for a: Dictionary in state.json.get("animations", []):
			var name: String = a.get("name", "")
			if not player.has_animation(name):
				continue
			var extras: Dictionary = a.get("extras", {})
			var animation := player.get_animation(name)
			animation.loop_mode = Animation.LOOP_LINEAR if extras.get("loop", false) else Animation.LOOP_NONE
			animation.set_meta("shosa", extras)
			var marks: Array = extras.get("marks", [])
			if marks.is_empty():
				continue
			if marks_node == null:
				marks_node = _marks_node(scene, player)
			# Track paths are from the player's root node, and that is where the node was put.
			var path := NodePath(marks_node.name)
			var tracks: Array[int] = []
			for m: Dictionary in marks:
				var time := clampf(m.get("time", 0.0), 0.0, animation.length)
				# A track holds one key at a time: two marks of the same moment (two dancers clap) need two tracks.
				var track := -1
				for t in tracks:
					if animation.track_find_key(t, time, Animation.FIND_MODE_APPROX) < 0:
						track = t
						break
				if track < 0:
					track = animation.add_track(Animation.TYPE_METHOD)
					animation.track_set_path(track, path)
					tracks.append(track)
				animation.track_insert_key(track, time, {
					"method": &"_shosa_mark",
					"args": [m.get("name", ""), m.get("figure", ""), m.get("section", ""), int(m.get("play", 1))],
				})
	return scene


func _marks_node(scene: Node, player: AnimationPlayer) -> Node:
	var script := GDScript.new()
	script.source_code = MARKS_SCRIPT
	script.reload()
	var node := Node.new()
	node.name = "ShosaMarks"
	node.set_script(script)
	var parent := player.get_node_or_null(player.root_node)
	if parent == null:
		parent = scene
	parent.add_child(node)
	node.owner = scene
	return node
