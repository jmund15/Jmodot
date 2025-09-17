@tool
extends EditorScript

func _run():
	var script_node = load("res://Base/AAA.cs").new()
	script_node._Run()
