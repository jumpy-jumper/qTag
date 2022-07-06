extends Control


var init_flag = false
func _ready():
	set_meta("instance", self)
	load_tabs()
	init_flag = true

func _process(delta):
	pass

func load_tabs():
	for tab_data in Global.settings["tabs"]:
		var c = Control.new()
		c.name = tab_data["name"]
		$TabContainer.add_child(c)
		FileIndex.get_window(tab_data["expr"])
	plus = Control.new()
	plus.name = "+"
	$TabContainer.add_child(plus)
	$TabContainer.current_tab = 0
	$Tab.expression = Global.settings["tabs"][0]["expr"]

var plus


func add_tab(tab_data, save=true):
	var new_tab = Control.new()
	new_tab.name = tab_data["name"]
	$TabContainer.remove_child(plus)
	$TabContainer.add_child(new_tab, true)
	$TabContainer.add_child(plus)
	if save:
		var foo = Global.settings["tabs"]
		tab_data.name = new_tab.name # in case it gets renamed when added to the container
		foo.append(tab_data)
		Global.set_setting("tabs", foo)

func delete_tab(idx, save=true):
	$TabContainer.get_child(idx).queue_free()
	if save:
		var foo = Global.settings["tabs"]
		foo.remove(idx)
		Global.set_setting("tabs", foo)
	if $TabContainer.current_tab == $TabContainer.get_child_count() -2:
		$TabContainer.current_tab = $TabContainer.get_child_count() -3

func _on_TabContainer_tab_changed(tab):
	if not init_flag:
		return
	if $TabContainer.get_child(tab) == plus:
		add_tab({"name":"NewTab", "expr":""})
		$TabContainer.current_tab = $TabContainer.get_child_count()-2
		Global.set_setting("last_tab", $TabContainer.current_tab)
	else:
		Global.set_setting("last_tab", tab)
		$TabContainer.current_tab = tab
		$Tab.expression = Global.settings["tabs"][tab]["expr"]

func _on_TabContainer_gui_input(event : InputEvent):
	if event is InputEventMouseButton and not event.pressed:
		if event.button_index == 2:
			var popup = ConfirmationDialog.new()
			var t = $TabContainer.current_tab
			popup.dialog_text = "Delete " + $TabContainer.get_child(t).name + "?"
			popup.connect("confirmed", self, "delete_tab", [t])
			add_child(popup)
			popup.popup_centered()
		else:
			$TabContainer.remove_child(plus)
			$TabContainer.add_child(plus)


var last_pos = Vector2.ZERO
var last_scale = Vector2.ONE

func set_content(content, button : Control = null):
	$Content.visible = true
	$Content/TextureRect.texture = content
	$TabContainer.modulate.a = 0
	$Tab.modulate.a = 0
	$Content/TextureRect.move_pos = Vector2.INF
	if button:
		last_pos = button.rect_global_position - $Content/TextureRect.rect_size/2
		last_scale = button.rect_size / $Content/TextureRect.rect_size
		$Content/TextureRect.rect_global_position = last_pos
		$Content/TextureRect.rect_scale = last_scale
	$Content/TextureRect.reset()

func swap_content(content):
	$Content/TextureRect.texture = content

func _on_Content_gui_input(event):
	if event is InputEventMouseButton:
		if event.button_index == 2 and not event.pressed:
			$Content.visible = false
			$TabContainer.modulate.a = 1
			$Tab.modulate.a = 1
