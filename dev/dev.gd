extends Node2D

@export var device_list : Control
@export var scan_button : Button

var scan_op : BLEOperation = null

var _bluetooth_ready : bool = false

func _ready() -> void:
  Bluetooth.BluetoothStateChanged.connect(func(state): print("Bluetooth state updated: ", state))
  Bluetooth.DeviceDetected.connect(on_device_detected)
  Bluetooth.BluetoothInitialized.connect(func(): _bluetooth_ready = true)
  Bluetooth.ScanStarted.connect(_on_scan_started)

  scan_button.pressed.connect(_on_scan_button_pressed)


func _on_scan_button_pressed() -> void:
  if Bluetooth.IsScanning():
    _stop_scan()
  else:
    _start_scan()


func _start_scan() -> void:
  if scan_op == null || scan_op.IsDone:
    scan_op = Bluetooth.Scan()
    if not scan_op.Done.is_connected(_on_scan_done):
      scan_op.Done.connect(_on_scan_done)
    scan_button.text = "Starting..."
    scan_op.Start()
    if scan_op.IsDone:
      _on_scan_done(scan_op, scan_op.Success)



func _stop_scan() -> void:
  scan_button.disabled = true
  scan_button.text = "Stopping..."
  Bluetooth.StopScan()


func _on_scan_started() -> void:
  scan_button.disabled = false
  scan_button.text = "Stop Scan"
  print("Scan started")


func _on_scan_done(_op, success) -> void:
  if success:
    print("Scan stopped")
  else:
    print("Scan error!")
  scan_button.disabled = false
  scan_button.text = "Scan"
  

func on_device_detected(device) -> void:
  print("Device detected: (" + device.Address + ") " + device.Name)
  var card = DeviceCard.make_from(device)
  device_list.add_child(card)
  
