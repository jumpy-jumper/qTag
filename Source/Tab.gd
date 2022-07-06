extends Control

var thread : Thread
var stop_thread = false

func _ready():
	$Body/LineEdit.text = expression
	thread = Thread.new()
	thread.start(self, "window_update_proc")

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
export(String) var expression = "-s:created"

export var button_height = 192
enum ViewStyle {LIST=0,GRID=1,MEDIA=2}
export(ViewStyle) var view_style = ViewStyle.LIST

func _process(delta):
	$Body/ScrollContainer.scroll_vertical_enabled = not Input.is_action_pressed("zoom_modifier")
	if not visible:
		return

func window_update_proc():
	#if not is_visible_in_tree():
		#return
	window = FileIndex.get_window(name, expression)
	while(not stop_thread):
		if visible:
			var s = OS.get_ticks_msec()
			window = FileIndex.get_window(name, expression)
			for i in range (max(len(window), len(buttons))):
				if stop_thread:
					break
				elif OS.get_ticks_msec() - s > 1000/120:
					yield(get_tree(), "idle_frame")
				var cont
				if container.get_child_count() <= i/BLOCK_SIZE:
					break
					cont = block_template.duplicate()
					container.add_child(cont)
					buttons.append_array(cont.get_children())
					cont.visible = false
					yield(get_tree(), "idle_frame")
				else:
					cont = container.get_child(i/BLOCK_SIZE)
				var c = buttons[i]
				cont.visible = true
				c.view_style = view_style
				if i < len(window):
					c.set_path(window[i])
				else:
					c.set_path("")
			#print("Window Size: ", len(window), " | Window Retrieval Time: ", OS.get_ticks_msec(), "ms")
			#print("t: ", OS.get_ticks_msec()-s, "ms")
			yield(get_tree(), "idle_frame")
		else:
			yield(get_tree().create_timer(0.2), "timeout")

func _on_LineEdit_text_changed(new_text):
	expression = new_text
