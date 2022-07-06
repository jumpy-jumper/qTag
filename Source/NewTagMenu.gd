extends PopupMenu

func _ready():
	var tags = TagDB.get_user_tag_list()
	for tag in tags:
		add_item(tag)
	popup()

signal tag_selected(tag)

var do_not_hide = false

func _on_NewTagMenu_id_pressed(id):
	emit_signal("tag_selected", get_item_text(id))
	do_not_hide = true

func _on_NewTagMenu_popup_hide():
	if do_not_hide:
		popup()
		do_not_hide = false
	else:
		queue_free()
