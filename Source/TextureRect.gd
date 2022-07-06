extends TextureRect

var move_pos = Vector2.INF
var starting_pos = Vector2.INF

const snap = 90

var target_pos = Vector2.ZERO
var target_scale = Vector2.ONE

func reset():
	move_pos = Vector2.INF
	rect_pivot_offset = rect_size/2
	target_pos = Vector2.ZERO
	target_scale = Vector2.ONE

func snap_pos():
	rect_position = target_pos

func snap_scale():
	rect_scale = target_scale

func _process(delta):
	if move_pos != Vector2.INF:
		var p = starting_pos + (get_global_mouse_position() - move_pos)
		if p != starting_pos:
			double_click_t = 0	# Don't trigger double click function if moving image
		target_pos = p
		var fitting_x = target_scale == get_stretch() \
			or get_stretch().x < 1 and target_scale == Vector2.ONE
		var fitting_y = target_scale == Vector2.ONE/get_stretch() \
			or get_stretch().x > 1 and target_scale == Vector2.ONE
		if fitting_x and abs(rect_position.x) < snap * get_stretch().x:
			target_pos.x = 0
		elif fitting_y and abs(rect_position.y) < snap * get_stretch().x:
			target_pos.y = 0
		if rect_scale != Vector2.ONE:	# snap other axis when fitting to screen
			if fitting_x:
				var foo = rect_size.y * (get_stretch().x-1)
				foo /= 2
				if target_pos.y < -foo and target_pos.y > -foo-(snap/get_stretch().x):
					target_pos.y = -foo
				elif target_pos.y > foo and target_pos.y < foo+(snap/get_stretch().x):
					target_pos.y = foo
			elif fitting_y:
				var foo = rect_size.x * (1/get_stretch().x-1)
				foo /= 2
				if target_pos.x < -foo and target_pos.x > -foo-(snap*get_stretch().x):
					target_pos.x = -foo
				elif target_pos.x > foo and target_pos.x < foo+(snap*get_stretch().x):
					target_pos.x = foo
		snap_pos()
	double_click_t -= delta

	rect_position = lerp(rect_position, target_pos, 0.25)
	rect_scale = lerp(rect_scale, target_scale, 0.25)

func get_stretch():
	var ratio = rect_size.y/rect_size.x
	var ratio_texture = texture.get_size().y/texture.get_size().x
	var stretch = ratio_texture/ratio
	return Vector2(stretch, stretch)

var double_click_t = 0
func _on_TextureRect_gui_input(event : InputEvent):
	var zoom = Vector2.ONE
	if event is InputEventMouseButton:
		var m = get_global_mouse_position()
		var sz = OS.window_size
		var inside = m.x > sz.x * 0 and m.x < sz.x * 1
		if (event.button_index == 1):
			move_pos = m if event.pressed else Vector2.INF
			starting_pos = target_pos
			if event.pressed:
				if double_click_t > 0:
					move_pos = Vector2.INF
					double_click_t = 0
					if target_scale == Vector2.ONE:
						target_scale = get_stretch() if get_stretch().x > 1 else (Vector2.ONE/get_stretch())
						target_pos.x = 0
					else:
						reset()
				else:
					double_click_t = Global.settings["double_click"]
	rect_pivot_offset = rect_size / 2
	if event.is_action_pressed("scroll_up"):
		zoom = Vector2(1.25, 1.25)
	elif event.is_action_pressed("scroll_down"):
		zoom = Vector2(1/1.25, 1/1.25)
	target_scale *= zoom
	target_pos += (target_pos + ((rect_pivot_offset - get_global_mouse_position()) \
		if zoom.x > 1 else Vector2.ZERO)) * (zoom-Vector2.ONE)
	if (event.is_action_pressed("reset_content")):
		reset()
