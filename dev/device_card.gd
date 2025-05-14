class_name DeviceCard extends Control

static var scene : PackedScene = load("res://dev/device_card.tscn")

var device : BluetoothDevice

@export var address_label : Label
@export var name_label : Label
@export var connect_button : Button 

@export var expando_button : Button
@export var expando : Control
@export var services_container : Control

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
  expando_button.pressed.connect(_on_expando_button_pressed)


func connect_button_pressed():
  print("Connect button pressed, device state: ", device.State)
  if device.State == BluetoothDevice.StateConnected():
    try_disconnect()
  elif device.CanConnect():
    try_connect()


func try_connect():
  connect_button.disabled = true
  connect_button.text = "Connecting..."
  device.Connect().Start()

func try_disconnect():
  connect_button.disabled = true
  connect_button.text = "Disconnecting..."
  device.Disconnect().Start()

func on_connected():
  connect_button.disabled = false
  connect_button.text = "Disconnect"
  build_characteristics_ui()
  expando_button.disabled = false


func on_disconnected(_reason):
  connect_button.disabled = false
  connect_button.text = "Connect"
  expando_button.button_pressed = false
  expando_button.disabled = true
  expando_button.expando_target.visible = false

func _on_expando_button_pressed():
  expando.visible = expando_button.button_pressed


func build_characteristics_ui():
  for child in services_container.get_children():
    child.queue_free()

  for sh in device.GetServicesArray():
    var service_card = ServiceCard.make(sh)
    for ch in device.GetCharacteristicsArray(sh):
      var char_card = CharacteristicCard.make(device, ch)
      service_card.characteristics_container.add_child(char_card)
    services_container.add_child(service_card)
