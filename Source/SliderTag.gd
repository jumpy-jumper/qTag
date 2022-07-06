extends Button

var file = ""
var tag_id = ""

func _ready():
	update_tag()

func _process(delta):
	update_tag()

var last_dict = 0
func update_tag():
	var tag = TagDB.get_file_tag(file, tag_id)
	if tag.hash() != last_dict:
		last_dict = tag.hash()
		$Label.text	= tag.name
		if len(tag.quantities) > 0:
			$HSlider.max_value = len(tag.quantities)-1
			$HSlider.value = tag.value
			$Label.text	= tag.quantities[tag.value]
		else:
			$HSlider.max_value = 200
		$HSlider.tick_count = len(tag.quantities)

func _on_Delete_pressed():
	TagDB.remove_tag(file, tag_id)
	queue_free()


func _on_HSlider_value_changed(value):
	TagDB.set_tag(file, tag_id, value)
