extends CheckButton

@export var expando_target : Control

func _ready():
  pressed.connect(func(): expando_target.visible = button_pressed)
  
