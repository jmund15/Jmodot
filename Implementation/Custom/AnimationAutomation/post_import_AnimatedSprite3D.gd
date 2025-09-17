extends PostImportBase

static func modify_context(
# Path to the source file from which the import is performed
	res_source_file_path: String,
	# Path to save imported resource file
	res_save_file_path: String,
	# EditorImportPlugin instance to call append_import_external_resource
	# or other methods
	editor_import_plugin: EditorImportPlugin,
	# Import options
	options: Dictionary,
	# Your custom data from middle-import script
	middle_import_data: Variant,
	# Context-object to modify
	context: Context,
) -> Error:
	# ------------------------------------------------
	# You can modify or replace objects in context fields.
	# (Be careful not to shoot yourself in the foot!)
	# ------------------------------------------------
	#
	# resource: Resource
	#     A save-ready resource that you can modify or replace as you wish
	#
	# resource_saver_flags: ResourceSaver.SaverFlags
	#     Resource save flags for use in ResourceSaver.save method
	#
	# gen_files_to_add: PackedStringArray
	#     Gen-files paths to add to gen_files array of import-function
	#
	# save_extension: String
	#     Save resource file extension

	#var animated_sprite_2d: AnimatedSprite2D = (context.resource as PackedScene).instantiate() as AnimatedSprite2D
	#animated_sprite_2d.modulate = Color.RED
	#var packed_scene = PackedScene.new()
	#packed_scene.pack(animated_sprite_2d)
	#context.resource = packed_scene
	var sprite: AnimatedSprite3D = (context.resource as PackedScene).instantiate() as AnimatedSprite3D
	#sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	sprite.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	var packed_scene = PackedScene.new()
	var result       = packed_scene.pack(sprite)
	print(result)
	if result == OK:
		var scene_name = "res://" + sprite.name + ".tscn"
		var error      = ResourceSaver.save(packed_scene, scene_name)#"res://Monsters/grizzler.tscn")  # Or "user://..."
		if error != OK:
			push_error("An error occurred while saving the scene to disk.")

	context.resource = packed_scene
	#print(sprite.texture_filter)
	return OK
