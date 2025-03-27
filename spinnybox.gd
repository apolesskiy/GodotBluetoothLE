extends MeshInstance2D

@export var speed_radians: float = 1

func _process(delta: float) -> void:
  rotate(speed_radians * delta)
