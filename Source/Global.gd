extends Node

func _ready():
	OS.window_per_pixel_transparency_enabled = true
	get_tree().get_root().transparent_bg = true
	last_size = OS.window_size
	last_pos = OS.window_position
	OS.window_borderless = true
	OS.window_size = OS.get_screen_size() + Vector2(0, 1)
	OS.center_window()
	load_from_disk()

var last_size = Vector2.ZERO
var last_pos = Vector2.ZERO

func _input(event):
	if Input.is_action_just_pressed("fullscreen"):
		if OS.window_borderless:
			OS.window_borderless = false
			OS.window_size = last_size
			OS.window_position = last_pos
		else:
			last_size = OS.window_size
			last_pos = OS.window_position
			OS.window_borderless = true
			OS.window_size = OS.get_screen_size() + Vector2(0, 1)
			OS.center_window()
	if Input.is_action_just_pressed("restart"):
		get_tree().reload_current_scene()
	if Input.is_action_pressed("zoom_modifier"):
		if event.is_action_pressed("scroll_up"):
			set_setting("zoom_modifier", settings["zoom_modifier"]*1.05)
		if event.is_action_pressed("scroll_down"):
			set_setting("zoom_modifier", settings["zoom_modifier"]/1.05)

# SETTINGS

var settings := {
	"double_click" : 0.5,
	"zoom_modifier" : 1,
}

func set_setting(setting, value):
	settings[setting] = value
	save_to_disk()

func load_from_disk():
	var file := File.new()
	file.open("user://settings.ini", File.READ)
	var dict = str2var(file.get_as_text())
	if not dict:
		return
	for key in dict:
		settings[key] = dict[key]

func save_to_disk():
	var file := File.new()
	file.open("user://settings.ini", File.WRITE)
	file.store_string(var2str(settings));
