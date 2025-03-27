extends Node2D

func _ready() -> void:
  Bluetooth.BluetoothStateChanged.connect(func(state): print("From GDScript: new state is ", state))
  Bluetooth.BluetoothInitialized.connect(func(): Bluetooth.StartDiscovery())
