extends Node2D

@export var device_list : Control
@export var scan_button : Button

var _bluetooth_ready : bool = false

func _ready() -> void:
  Bluetooth.BluetoothStateChanged.connect(func(state): print("Bluetooth state updated: ", state))
  Bluetooth.DeviceDetected.connect(on_device_detected)
  Bluetooth.BluetoothInitialized.connect(func(): _bluetooth_ready = true)
  Bluetooth.ScanStarted.connect(_on_scan_started)
  Bluetooth.ScanStopped.connect(_on_scan_stopped)

  scan_button.pressed.connect(_on_scan_button_pressed)


func _on_scan_button_pressed() -> void:
  if Bluetooth.IsScanning():
    _stop_scan()
  else:
    _start_scan()


func _start_scan() -> void:
  if not Bluetooth.IsScanning():
    scan_button.text = "Starting..."
    var op = Bluetooth.Scan()
    op.Error.connect(_on_can_scan)
    op.Start()


func _stop_scan() -> void:
  scan_button.disabled = true
  scan_button.text = "Stopping..."
  var op = Bluetooth.StopScan()
  op.Error.connect(_on_can_stop_scan)
  op.Start()


func _on_can_scan() -> void:
  scan_button.disabled = false
  scan_button.text = "Scan"


func _on_can_stop_scan() -> void:
  scan_button.disabled = false
  scan_button.text = "Stop Scan"


func _on_scan_started() -> void:
  print("Scan started")
  _on_can_stop_scan()


func _on_scan_stopped() -> void:
  print("Scan stopped")
  _on_can_scan()
  

func on_device_detected(device) -> void:
  print("Device detected: (" + device.Address + ") " + device.Name)
  var card = DeviceCard.make_from(device)
  device_list.add_child(card)
  
