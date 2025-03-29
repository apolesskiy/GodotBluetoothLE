class_name CharacteristicCard extends Control

static var scene : PackedScene = load("res://characteristic_card.tscn")

@export var uuid_label : Label
@export var props_label : Label
@export var value_box : LineEdit
@export var read_button : Button
@export var write_button : Button
@export var notify_button : Button

var device : BluetoothDevice
var handle : BLEGattHandle

static func make(dev : BluetoothDevice, h : BLEGattHandle):
  var card = scene.instantiate()
  card.device = dev
  card.handle = h
  card.uuid_label.text = h.CharacteristicUUID
  var char_props = dev.GetCharacteristicProperties(h)
  card.props_label.text = str(h.CharacteristicIndex)

  if char_props & BluetoothConstants.CHARACTERISTIC_PROPERTY_READ:
    card.read_button.disabled = false
  else:
    card.read_button.disabled = true

  if char_props & (BluetoothConstants.CHARACTERISTIC_PROPERTY_WRITE | 
                   BluetoothConstants.CHARACTERISTIC_PROPERTY_WRITE_NO_RESPONSE):
    card.write_button.disabled = false
  else:
    card.write_button.disabled = true

  if char_props & BluetoothConstants.CHARACTERISTIC_PROPERTY_NOTIFY:
    card.notify_button.disabled = false
  else:
    card.notify_button.disabled = true

  return card
