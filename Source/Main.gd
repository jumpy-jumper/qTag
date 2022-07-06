extends Control


func _ready():
	set_meta("instance", self)

var last_pos = Vector2.ZERO
var last_scale = Vector2.ONE

func set_content(content, button : Control = null):
	$Content.visible = true
	$Content/TextureRect.texture = content
	$TabContainer.modulate.a = 0
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
