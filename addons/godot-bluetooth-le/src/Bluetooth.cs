using Godot;
using System;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.EventArgs;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.Generic;

namespace GodotBLE;

/// <summary>
/// Singleton node that provides access to the Bluetooth Low Energy (BLE) API.
/// Add this as a global singleton in your project settings.
/// 
/// Usage:
/// - Connect to the BluetoothInitialized signal to know when the Bluetooth stack is ready.
/// - Discover devices with StartDiscovery() and StopDiscovery().
/// - Connect to the DeviceDetected signal to know when a device is found.
/// </summary>
public partial class Bluetooth : Node
{

  private static TimeSpan BLE_TIMEOUT = TimeSpan.FromSeconds(10);

  private static Bluetooth _instance;

  public static Bluetooth Instance
  {
    get
    {
      return _instance;
    }
  }


  private static IAdapter _adapter;

  // Intentionally non-public.
  internal static IAdapter Adapter
  {
    get
    {
      return _adapter;
    }
  }

  private ConcurrentDictionary<Guid, BluetoothDevice> _devices = new ConcurrentDictionary<Guid, BluetoothDevice>();

  private IBluetoothLE _ble;

  private bool _initialized = false;

  private string _state = BluetoothState.Unknown.ToString();

  public string State
  {
    get
    {
      return _state;
    }
  }

  /// <summary>
  /// Emitted when the Bluetooth stack has been initialized.
  /// </summary>
  [Signal]
  public delegate void BluetoothInitializedEventHandler();
  
  /// <summary>
  /// Emitted when the global Bluetooth state changes, such as when an adapter is turned on/off.
  /// </summary>
  [Signal]
  public delegate void BluetoothStateChangedEventHandler(String newState);

  /// <summary>
  /// Emitted when a BLE device is discovered. This is emitted for each found device every time
  /// a scan is run.
  /// </summary>
  [Signal]
  public delegate void DeviceDetectedEventHandler();

  /// <summary>
  /// Emitted when a BLE device is connected. Listeners should
  /// connect to the Device's Disconnected signal to know when it disconnects.
  /// </summary>
  [Signal]
  public delegate void DeviceConnectedEventHandler(BluetoothDevice device);

  /// <summary>
  /// Emitted when BLE device discovery is started.
  /// </summary>
  [Signal]
  public delegate void ScanStartedEventHandler();

  /// <summary>
  /// Emitted when BLE device discovery is stopped.
  /// </summary>
  [Signal]
  public delegate void ScanStoppedEventHandler();


  public override void _EnterTree()
  {
    SignalForwarder.Init();

    if (Instance != null && !Instance.IsQueuedForDeletion())
    {
      GD.PushError("Attempted multiple instantiations of Bluetooth singleton.");
      QueueFree();
      return;
    }

    // Set up BLE plugin logging to use GD.Print(). Preformat the string so Godot isn't confused by C# stuff.
    Plugin.BLE.Abstractions.Trace.TraceImplementation = (s, obj) => GD.Print(String.Format(s, obj));

    _instance = this;

    var bleImpl = new Plugin.BLE.BleImplementation();

    _ble = bleImpl;
    _ble.StateChanged += OnBleStateChanged;
    _state = _ble.State.ToString();

    new Task(() =>
    {
      bleImpl.Initialize();
    }).Start();
  }


  /// <summary>
  /// Called after BLE initialization succeeds. Initializes adapter and hooks up signals.
  /// </summary>
  private void InitializeAdapter()
  {
    _adapter = _ble.Adapter;
    _adapter.DeviceDiscovered += OnDeviceDiscovered;
    _adapter.DeviceConnected += OnDeviceConnected;
    _adapter.DeviceDisconnected += OnDeviceDisconnected;
    _adapter.DeviceConnectionError += OnDeviceConnectionError;
    _adapter.DeviceConnectionLost += OnDeviceConnectionLost;
  }


  private void OnDeviceDiscovered(object sender, DeviceEventArgs e)
  {
    BluetoothDevice device = _devices.TryGetValue(e.Device.Id, out BluetoothDevice d) ? d : null;
    if (device == null)
    {
      device = new BluetoothDevice(this, _adapter, e.Device);
      _devices.TryAdd(e.Device.Id, device);
    }

    SignalForwarder.ToMainThreadAsync(() => {
      EmitSignal(SignalName.DeviceDetected, device);
    }, "Bluetooth device detected");
  }


  private async void OnDeviceConnected(object sender, DeviceEventArgs e)
  {
    var device = _devices.TryGetValue(e.Device.Id, out BluetoothDevice d) ? d : null;
    if (device == null)
    {
      GD.PrintErr("Bluetooth: Got connection event for unknown device.");
      return;
    }
    await device.BuildGattCache();
    SignalForwarder.ToMainThreadAsync(() => {
      EmitSignal(SignalName.DeviceConnected, device);
      device.EmitSignal(BluetoothDevice.SignalName.Connected);
    }, "Bluetooth device connected");
  }


  private void OnDeviceConnectionLost(object sender, DeviceErrorEventArgs e)
  {
    var device = _devices.TryGetValue(e.Device.Id, out BluetoothDevice d) ? d : null;
    if (device == null)
    {
      GD.PrintErr("Bluetooth: Got connection lost event for unknown device.");
      return;
    }
    SignalForwarder.ToMainThreadAsync(() => {
      device.EmitSignal(BluetoothDevice.SignalName.Disconnected, BluetoothDevice.DisconnectReasonConnectionLost());
    }, "Bluetooth device connection lost");
  }


  private void OnDeviceConnectionError(object sender, DeviceErrorEventArgs e)
  {
    var device = _devices.TryGetValue(e.Device.Id, out BluetoothDevice d) ? d : null;
    if (device == null)
    {
      GD.PrintErr("Bluetooth: Got connection error event for unknown device.");
      return;
    }
    SignalForwarder.ToMainThreadAsync(() => {
      device.EmitSignal(BluetoothDevice.SignalName.Disconnected, BluetoothDevice.DisconnectReasonError());
    }, "Bluetooth device connection error");
  }


  private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
  {
    var device = _devices.TryGetValue(e.Device.Id, out BluetoothDevice d) ? d : null;
    if (device == null)
    {
      GD.PrintErr("Bluetooth: Got disconnection event for unknown device.");
      return;
    }
    SignalForwarder.ToMainThreadAsync(() => {
      device.EmitSignal(BluetoothDevice.SignalName.Disconnected, BluetoothDevice.DisconnectReasonDisconnected());
    }, "Bluetooth device disconnected");
  }


  private void OnBleStateChanged(object _, BluetoothStateChangedArgs args)
  {
    if (args.NewState == BluetoothState.On)
    {
      if (!_initialized)
      {
        _initialized = true;
        
        InitializeAdapter();

        SignalForwarder.ToMainThreadAsync(() => {
          EmitSignal(SignalName.BluetoothInitialized);
        }, "Bluetooth initialization");
      }

      _state = args.NewState.ToString();

      SignalForwarder.ToMainThreadAsync(() => 
      {
        GD.Print($"Bluetooth state changed to {_state}");
        EmitSignal(nameof(SignalName.BluetoothStateChanged), _state);
      }, "Bluetooth state change");
    }
  }


  public IReadOnlyCollection<BluetoothDevice> GetDevices()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot get devices: adapter not initialized.");
      return new List<BluetoothDevice>();
    }

    return (IReadOnlyCollection<BluetoothDevice>)_devices.Values;
  }

  public Godot.Collections.Array<BluetoothDevice> GetDevicesArray()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot get devices: adapter not initialized.");
      return new Godot.Collections.Array<BluetoothDevice>();
    }

    return new Godot.Collections.Array<BluetoothDevice>(_devices.Values);
  }


  /// <summary>
  /// Start scanning for BLE devices, asynchronously.
  /// Found devices will emit the DeviceDetected signal.
  /// Only one scan can be active at a time. Calling this when already scanning
  /// will print an error and do nothing.
  /// </summary>
  public void StartScan()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot start discovery: adapter not initialized.");
      return;
    }
    
    if (_adapter.IsScanning)
    {
      GD.Print("Bluetooth: StartScan called while already scanning.");
      return;
    }

    EmitSignal(SignalName.ScanStarted);
    new Task(async () => 
    {
      await _adapter.StartScanningForDevicesAsync();
      SignalForwarder.ToMainThreadAsync(() => {
        EmitSignal(nameof(SignalName.ScanStopped));
      }, "Bluetooth discovery stop");
    }).Start();
  }


  // TODO: Add C# StartDiscovery-with-filter.


  /// <summary>
  /// Start scanning for BLE devices, recording only devices that return true
  /// for the given filter function. The filter function will be called asynchronously
  /// in an IO-bound context! It is recommended to thoroughly insulate it from the main game loop.
  /// </summary>
  /// <param name="deviceFilter"></param>
  public void StartScan(Callable deviceFilter)
  {
    // TODO: Godot glue here to wrap a device.
    throw new NotImplementedException();
  }


  /// <summary>
  /// Stop scanning for BLE devices. This is asynchronous and idempotent (calling this when not scanning does nothing).
  /// </summary>
  public void StopScan()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot stop discovery: adapter not initialized.");
      return;
    }

    new Task(async () => {
      await _adapter.StopScanningForDevicesAsync();
    }).Start();
  }

  public bool IsScanning()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot check discovery state: adapter not initialized.");
      return false;
    }
    return _adapter.IsScanning;
  }
}
