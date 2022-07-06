extends Button

var path = ""
var path_dirty = false

func set_path(value):
	path = value

export var height = 256
export var view_style = 0

var texture = null

func _process(delta):
	if not is_on_screen():
		return
	update_button()

func update_button():
	var n = TagDB.get_file_tag(path, "name")
	if not n.empty():
		n = n.value
		get_node("Label").text = n
	var d = TagDB.get_file_tag(path, "duration")
	get_node("Time").visible = false
	if not d.empty():
		d = d.value
		get_node("Time").visible = view_style != 0
		get_node("Time/Label").text = str(floor(d/60)).pad_zeros(2) \
			+ ":" + str(floor(fmod(d, 60))).pad_zeros(2)
	path_dirty = true
	FileIndex.request_thumbnail(path)
	hint_tooltip = path
#	c.get_node("Time").visible = false
	match(view_style):
		0:
			rect_min_size = Vector2(get_parent().rect_size.x, get_node("Label").rect_min_size.y)
		1:
			rect_min_size = Vector2(height, height)
		2:
			rect_min_size = Vector2((($Image.texture.get_size().x/$Image.texture.get_size().y) if $Image.texture else 1)*height, height)
		#if texture:
		#	rect_min_size = Vector2(texture.get_size().x/texture.get_size().y*height, height)
	if (path != ""):
		texture = FileIndex.get_thumbnail(path)
		$Image.texture = texture
	$Image.visible = view_style != 0
	$Label.visible = not texture or not view_style != 0
	$Label.align = ALIGN_CENTER if view_style != 0 else ALIGN_LEFT
	$Label.margin_top = 8 if view_style != 0 else 0
	$Label.valign = VALIGN_CENTER

onready var visibility_notifier := $VisibilityNotifier2D
func is_on_screen():
	return rect_global_position.y >= -200 and rect_global_position.y <= OS.window_size.y +200

func _on_FileButton_pressed():
	if Input.is_action_just_released("context_menu"):
		var c = load("res://Source/FileContextMenu.tscn").instance()
		c.rect_global_position = get_global_mouse_position() - Vector2(8,8)
		c.path = path
		c.button = self
		get_node("/root/Main").add_child(c)
	else:
		if TagDB.get_file_tag(path, "format")["value"] == 0:
			get_node("/root/Main").set_content(FileIndex.get_content(path), self)
		else:
			FileIndex.execute(path)

var t

func swap_content_proc():
	var c = FileIndex.get_content(path)
	get_node("/root/Main").swap_content(c)
	t = null
