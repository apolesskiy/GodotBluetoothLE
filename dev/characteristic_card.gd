class_name CharacteristicCard extends Control

static var scene : PackedScene = load("res://dev/characteristic_card.tscn")

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

  return card


func _ready():
  uuid_label.text = handle.CharacteristicUUID
  var char_props = device.GetCharacteristicProperties(handle)
  props_label.text = str(handle.CharacteristicIndex)

  if char_props & BluetoothConstants.CHARACTERISTIC_PROPERTY_READ:
    read_button.disabled = false
  else:
    read_button.disabled = true

  if char_props & (BluetoothConstants.CHARACTERISTIC_PROPERTY_WRITE | 
                   BluetoothConstants.CHARACTERISTIC_PROPERTY_WRITE_NO_RESPONSE):
    write_button.disabled = false
  else:
    write_button.disabled = true

  if char_props & BluetoothConstants.CHARACTERISTIC_PROPERTY_NOTIFY:
    notify_button.disabled = false
  else:
    notify_button.disabled = true

  read_button.pressed.connect(_on_read_button_pressed)
  write_button.pressed.connect(_on_write_button_pressed)
  notify_button.pressed.connect(_on_notify_toggle)

  device.Connected.connect(_on_device_connected)
  if device.State == BluetoothDevice.StateConnected():
    _on_device_connected()


func _on_device_connected():
  # Subscribe to the backing characteristic.
  var sub = device.GetSubscription(handle)
  if not sub.ValueChanged.is_connected(on_value_updated):
    sub.ValueChanged.connect(on_value_updated)


func on_value_updated():
  # Update the value box with the new value.
  var value = device.GetValue(handle)
  print("Updating value to ", value)
  if value:
    value_box.text = value.get_string_from_utf8()


func _on_read_button_pressed():
  var read = device.Read(handle)
  read.Done.connect(func (op, success):
    if success:
      var value = op.Result
      print("Read bytes: ", value)
    else:
      print("Read error!")
  )
  read.Start()


func _on_write_button_pressed():
  var value = value_box.text
  var bytes = value.to_utf8_buffer()
  var write = device.Write(handle, bytes)
  write.Done.connect(func (op, success):
    if success:
      print("Write response code: ", op.Result)
    else:
      print("Write error!")
  )
  write.Start()


func _on_notify_toggle():
  if notify_button.button_pressed:
    var op = device.EnableNotify(handle)
    op.Error.connect(func (_op, _err):
      notify_button.set_pressed_no_signal(false)
    )
    op.Start()
  else:
    var op = device.DisableNotify(handle)
    op.Error.connect(func (_op, _err):
      notify_button.set_pressed_no_signal(true)
    )
    op.Start()
