class_name ServiceCard extends PanelContainer

static var scene : PackedScene = load("res://service_card.tscn")
@export var uuid_label : Label
@export var index_label : Label
@export var characteristics_container : Control

static func make(handle : BLEGattHandle) -> ServiceCard:
  var card = scene.instantiate()
  card.uuid_label.text = handle.ServiceUUID
  card.index_label.text = str(handle.ServiceIndex)
  return card
