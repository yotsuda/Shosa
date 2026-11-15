# Opens the Godot 4 editor's own screens on a file of a project and writes down what they show:
# the Advanced Import Settings of a .glb, its skeleton, and the bone map the editor guesses when
# the humanoid profile is chosen there (pictures and a log). It is an editor plugin, since only a
# plugin runs inside the editor. It does nothing unless SHOSA_PROBE_OUT is set.
#
#   PROJECT\addons\probe\probe.gd    this file
#   PROJECT\addons\probe\plugin.cfg  [plugin] name="probe" description="" author="" version="1" script="probe.gd"  (one per line)
#   PROJECT\project.godot            [editor_plugins] enabled=PackedStringArray("res://addons/probe/plugin.cfg")
#
#   $env:SHOSA_PROBE_OUT = 'C:/full/path/out'    # 0-editor.png … 4-bonemap-humanoid.png, probe.log
#   $env:SHOSA_PROBE_FILE = 'walk.glb'           # as it lies in the project
#   cmd /d /s /c "godot_console.exe --editor --single-window --windowed --resolution 1920x1080 --path PROJECT < NUL > run.log 2>&1"
#
# The first start of a new project only imports: run it twice. The editor closes itself (about 10 s).
# Nothing is saved: the map is set in the dialog and Reimport is not pressed.
@tool
extends EditorPlugin

var out_dir := ""
var log_file: FileAccess


func _enter_tree() -> void:
	out_dir = OS.get_environment("SHOSA_PROBE_OUT")
	if out_dir == "":
		return
	log_file = FileAccess.open(out_dir + "/probe.log", FileAccess.WRITE)
	OS.low_processor_usage_mode = false
	run.call_deferred()


func say(text: String) -> void:
	log_file.store_line(text)
	log_file.flush()


func frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func shot(name: String) -> void:
	# The editor draws only when something changes, so `frame_post_draw` may never come: ask for a frame.
	EditorInterface.get_base_control().queue_redraw()
	await frames(4)
	var image := get_tree().root.get_texture().get_image()
	say("shot " + name + ": " + error_string(image.save_png(out_dir + "/" + name + ".png")))


func dump(node: Node, depth: int, max_depth: int) -> void:
	var line := "  ".repeat(depth) + node.get_class() + " '" + node.name + "'"
	if node is Control:
		var c := node as Control
		if not c.is_visible_in_tree():
			return
		line += " " + str(Rect2i(c.get_global_rect()))
	for p in ["text", "title", "tooltip_text"]:
		if p in node and str(node.get(p)) != "":
			line += " " + p + "=" + str(node.get(p)).left(60).replace("\n", "\\n")
	if node is EditorProperty:
		line += " property=" + str((node as EditorProperty).get_edited_property())
	say(line)
	if node is Tree:
		var item := (node as Tree).get_root()
		while item != null:
			say("  ".repeat(depth + 1) + "item '" + item.get_text(0) + "' meta=" + str(item.get_metadata(0)).left(60) + (" SELECTED" if item.is_selected(0) else ""))
			item = item.get_next_in_tree()
	if depth < max_depth:
		for child in node.get_children(true):
			dump(child, depth + 1, max_depth)


func find_item(tree: Tree, part: String) -> TreeItem:
	var item := tree.get_root()
	while item != null:
		if item.get_text(0).contains(part) or str(item.get_metadata(0)).contains(part):
			return item
		item = item.get_next_in_tree()
	return null


func open_import_settings(path: String) -> Node:
	var dock := EditorInterface.get_file_system_dock()
	dock.navigate_to_path(path)
	await frames(10)
	for tree: Tree in dock.find_children("*", "Tree", true, false):
		var item := find_item(tree, path)
		if item != null and tree.is_visible_in_tree():
			item.select(0)
			await frames(2)
			tree.item_activated.emit()
			await frames(30)
			break
	var dialogs := get_tree().root.find_children("*", "SceneImportSettingsDialog", true, false)
	say("dialogs: " + str(dialogs.size()))
	return dialogs[0] if not dialogs.is_empty() else null


func run() -> void:
	var fs := EditorInterface.get_resource_filesystem()
	await frames(30)
	while fs.is_scanning():
		await frames(5)
	await frames(120)
	say("godot " + Engine.get_version_info()["string"] + ", window " + str(get_tree().root.size))
	await shot("0-editor")

	var file := OS.get_environment("SHOSA_PROBE_FILE")
	var dialog := await open_import_settings("res://" + file)
	if dialog == null:
		dump(EditorInterface.get_file_system_dock(), 0, 12)
		get_tree().quit()
		return
	await shot("1-dialog")
	dump(dialog, 0, 14)

	# Select the skeleton in the dialog's tree of nodes.
	for tree: Tree in dialog.find_children("*", "Tree", true, false):
		var item := find_item(tree, "Skeleton3D")
		if item != null:
			item.select(0)
			tree.item_selected.emit()
			say("selected '" + item.get_text(0) + "'")
			break
	await frames(20)
	await shot("2-skeleton")
	say("---- after selecting the skeleton")
	for inspector in dialog.find_children("*", "EditorInspector", true, false):
		dump(inspector, 0, 14)

	# Give the skeleton an empty BoneMap the way the inspector does, open it, then choose the humanoid
	# profile: the editor guesses the bones from their names at that moment.
	var map := BoneMap.new()
	var property := bone_map_property(dialog)
	if property == null:
		say("no retarget/bone_map property")
		get_tree().quit()
		return
	property.emit_changed("retarget/bone_map", map)
	await frames(30)
	property = bone_map_property(dialog)
	var buttons := property.find_children("*", "Button", true, false)
	for b: Button in buttons:
		say("button '" + b.text + "' " + b.get_class())
		if b.text.contains("BoneMap"):
			b.pressed.emit()
			break
	await frames(30)
	say("bone map editors: " + str(dialog.find_children("*", "BoneMapEditor", true, false).size()))
	await shot("3-bonemap-empty")
	# The guess runs when the profile is chosen in the map's own "profile" property, not when it is set from a script.
	var chosen := false
	for p: EditorProperty in dialog.find_children("*", "EditorProperty", true, false):
		if p.get_edited_property() == &"profile":
			p.emit_changed("profile", SkeletonProfileHumanoid.new())
			chosen = true
			break
	say("profile property found: " + str(chosen))
	await frames(60)
	await shot("4-bonemap-humanoid")
	say("---- the guessed map (profile bone <- skeleton bone)")
	var profile := map.profile
	var missing := 0
	for i in profile.bone_size:
		var bone := profile.get_bone_name(i)
		var found := map.get_skeleton_bone_name(bone)
		if found == &"":
			missing += 1
		say("  " + bone + " <- " + str(found))
	say("mapped " + str(profile.bone_size - missing) + " of " + str(profile.bone_size))
	say("---- the inspector with the map open")
	for inspector in dialog.find_children("*", "EditorInspector", true, false):
		dump(inspector, 0, 8)
	get_tree().quit()


func bone_map_property(dialog: Node) -> EditorProperty:
	for p: EditorProperty in dialog.find_children("*", "EditorProperty", true, false):
		if p.get_edited_property() == &"retarget/bone_map" and p.is_visible_in_tree():
			return p
	return null
