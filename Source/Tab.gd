extends Control

var thread : Thread
var stop_thread = false

func _ready():
	directives_rx.compile("(?:^|\\s)(#view):([^\\s]+)(?:$|\\s)")
	thread = Thread.new()
	#thread.start(self, "window_update_proc")

func _exit_tree():
	stop_thread = true
	thread.wait_to_finish()

var window = []

onready var container = $Body/ScrollContainer/Containers
onready var block_template = $Body/ScrollContainer/Containers/Buttons.duplicate()
onready var button_template := $Body/ScrollContainer/Containers/Buttons.get_child(0).duplicate()
onready var buttons = $Body/ScrollContainer/Containers/Buttons.get_children()
onready var BLOCK_SIZE = block_template.get_child_count()

export(NodePath) var content = ""
export(String) var expression = "" setget set_expression

func set_expression(new_text):
	expression = new_text
	var old = view_style
	view_style = ViewStyle.LIST
	for m in directives_rx.search_all(expression):
		var directive = m.strings[1].to_lower()
		var value = m.strings[2].to_lower()
		match(directive):
			"#view":
				match(value):
					"grid":
						view_style = ViewStyle.GRID
					"media":
						view_style = ViewStyle.MEDIA
	if old != view_style:
		for b in buttons:
			b.view_style = view_style
	if $Body/LineEdit.text != expression:
		$Body/LineEdit.text = expression
	Global.settings["tabs"][$"../TabContainer".current_tab]["expr"] = expression
	Global.set_setting("tabs", Global.settings["tabs"])

	NOTIF_WINDOW_UPDATE = not FileIndex.has_window(expression)
	FileIndex.cancel_requests()

export var button_height = 192
enum ViewStyle {LIST=0,GRID=1,MEDIA=2}
export(ViewStyle) var view_style = ViewStyle.LIST

var directives_rx = RegEx.new()
func _process(delta):
	$Body/ScrollContainer.scroll_vertical_enabled = not Input.is_action_pressed("zoom_modifier")
	if scrolling:
		FileIndex.cancel_requests()
	if not visible:
		return
	window_update_proc()

var NOTIF_WINDOW_UPDATE = false
var last_i = 0
func window_update_proc():
	#if not is_visible_in_tree():
		#return
	#while(not stop_thread):
		var s = OS.get_ticks_msec()
		window = FileIndex.get_window(expression)
		#print("Window Size: ", len(window), " | Window Retrieval Time: ", OS.get_ticks_msec() - s, "ms")
		for i in range (last_i, max(len(window), len(buttons))):
			last_i = i
			#if i % 100 == 0:
				#print("Updating ", i)
			if stop_thread or NOTIF_WINDOW_UPDATE:
				if NOTIF_WINDOW_UPDATE:
					#print("Window Updated")
					NOTIF_WINDOW_UPDATE = false
					for c in buttons:
						c.set_path("")
					last_i = 0
				break
			var cont
			if container.get_child_count() <= i/BLOCK_SIZE:
				break
				cont = block_template.duplicate()
				container.add_child(cont)
				buttons.append_array(cont.get_children())
				cont.visible = false
			else:
				cont = container.get_child(i/BLOCK_SIZE)
			var c = buttons[i]
			if OS.get_ticks_msec() - s > 1000/120:
				return
			cont.visible = true
			#c.view_style = view_style
			if i < len(window):
				c.set_path(window[i])
			else:
				c.set_path("")
		last_i = 0
		#print("t: ", OS.get_ticks_msec()-s, "ms")

func _on_LineEdit_text_changed(new_text):
	set_expression(new_text)

var scrolling = false

func _on_ScrollContainer_scroll_started():
	scrolling = true

func _on_ScrollContainer_scroll_ended():
	scrolling = false
