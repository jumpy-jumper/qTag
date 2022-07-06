extends PopupMenu

var button = null
export var path = ""

func _ready():
	popup()
	for tag in TagDB.get_file_exposed_tags(path):
		add_tag(tag)

func _process(delta):
	pass
	var c = $VBoxContainer
	rect_size = c.rect_size + Vector2(c.margin_left-c.margin_right, c.margin_top-c.margin_bottom)

func _on_Open_pressed():
	if path != "":
		var c = FileIndex.get_content(path)
		if c:
			get_node("/root/Main").set_content(c)
		else:
			FileIndex.execute(path)
	hide()

func _on_OpenExternal_pressed():
	if path != "":
		FileIndex.execute(path)
	hide()

func _on_Copy_pressed():
	if path != "":
		OS.clipboard = path
	hide()

func _on_Delete_pressed():
	if path != "":
		Directory.new().remove(path)
		FileIndex.delete(path)
		button.path = ""
	hide()

func _on_Properties_pressed():
	if path != "":
		FileIndex.execute_properties(path)
	hide()

func _on_FileContextMenu_popup_hide():
	if do_not_kill:
		popup()
		do_not_kill = false
	else:
		queue_free()


var do_not_kill = false
func _on_NewTag_pressed():
	var c = load("res://Source/NewTagMenu.tscn").instance()
	add_child(c)
	c.rect_global_position = $VBoxContainer/NewTag.rect_global_position + Vector2($VBoxContainer/NewTag.rect_size.x, 0)
	c.connect("tag_selected", self, "_on_NewTag_tag_selected")
	do_not_kill = true

func _on_NewTag_tag_selected(tag):
	TagDB.set_tag(path, tag, 0)
	add_tag(tag)
	do_not_kill = true

func add_tag(tag):
	var n : Control = load("res://Source/TagButton.tscn").instance()
	n.file = path
	n.tag_id = tag
	$VBoxContainer.add_child_below_node($VBoxContainer/NewTag, n)
