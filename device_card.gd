class_name DeviceCard extends Control

static var scene : PackedScene = load("res://device_card.tscn")

var device : BluetoothDevice

@export var address_label : Label
@export var name_label : Label
@export var connect_button : Button 

static func make_from(d : BluetoothDevice) -> DeviceCard:
  var card = scene.instantiate()
  card.device = d
  return card

func _ready() -> void:
  address_label.text = device.Address
  name_label.text = device.Name
  device.Connected.connect(on_connected)
  device.Disconnected.connect(on_disconnected)
  connect_button.pressed.connect(connect_button_pressed)


func connect_button_pressed():
  if device.State == BluetoothDevice.StateConnected():
    try_disconnect()
  elif device.State == BluetoothDevice.StateDisconnected():
    try_connect()


func try_connect():
  connect_button.disabled = true
  connect_button.text = "Connecting..."
  device.StartConnect()

func try_disconnect():
  connect_button.disabled = true
  connect_button.text = "Disconnecting..."
  device.StartDisconnect()

func on_connected():
  connect_button.disabled = false
  connect_button.text = "Disconnect"

func on_disconnected(_reason):
  connect_button.disabled = false
  connect_button.text = "Connect"
