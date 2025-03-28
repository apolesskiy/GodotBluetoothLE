extends Node2D

@onready var device_list : Control = $DeviceList

func _ready() -> void:
  Bluetooth.BluetoothStateChanged.connect(func(state): print("Bluetooth state updated: ", state))
  Bluetooth.DeviceDetected.connect(on_device_detected)
  Bluetooth.BluetoothInitialized.connect(func(): Bluetooth.StartScan())


func on_device_detected(device) -> void:
  print("Device detected: (" + device.Address + ") " + device.Name)
  var card = DeviceCard.make_from(device)
  device_list.add_child(card)
  
    
    
func start_device(device):
  device.Connected.connect(func(): print("Connected!"))
  print("Connecting to " + device.Address)
  device.StartConnect()
