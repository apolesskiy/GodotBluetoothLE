using Godot;
using System;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.EventArgs;

namespace GodotBluetooth;

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

  private static Bluetooth _instance;

  private static IAdapter _adapter;

  public static Bluetooth Instance
  {
    get
    {
      return _instance;
    }
  }

  private IBluetoothLE _ble;

  private bool _initialized = false;

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
  /// Emitted when BLE device discovery is started.
  /// </summary>
  [Signal]
  public delegate void DiscoveryStartedEventHandler();

  /// <summary>
  /// Emitted when BLE device discovery is stopped.
  /// </summary>
  [Signal]
  public delegate void DiscoveryStoppedEventHandler();


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

    var bleImpl = new BleImplementation();



    // Start initializing the BLE stack asynchronously.
    new Task(() => {
      bleImpl.Initialize();
    }).Start();

    _ble = bleImpl;
    _ble.StateChanged += OnBleStateChanged;
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
    GD.Print(e.Device.Name);
  }


  private void OnDeviceConnectionLost(object sender, DeviceErrorEventArgs e)
  {
    throw new NotImplementedException();
  }


  private void OnDeviceConnectionError(object sender, DeviceErrorEventArgs e)
  {
    throw new NotImplementedException();
  }


  private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
  {
    throw new NotImplementedException();
  }


  private void OnDeviceConnected(object sender, DeviceEventArgs e)
  {
    throw new NotImplementedException();
  }


  /// <summary>
  /// Start scanning for BLE devices, asynchronously.
  /// Found devices will emit the DeviceDetected signal.
  /// Only one scan can be active at a time. Calling this when already scanning
  /// will print an error and do nothing.
  /// </summary>
  public void StartDiscovery()
  {
    if (_adapter == null)
    {
      GD.PrintErr("Bluetooth: Cannot start discovery: adapter not initialized.");
      return;
    }

    EmitSignal(SignalName.DiscoveryStarted);
    new Task(async () => 
    {
      await _adapter.StartScanningForDevicesAsync();
      SignalForwarder.ToMainThreadAsync(() => {
        EmitSignal(nameof(SignalName.DiscoveryStopped));
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
  public void StartDiscovery(Callable deviceFilter)
  {
    // TODO: Godot glue here to wrap a device.
    throw new NotImplementedException();
  }


  /// <summary>
  /// Stop scanning for BLE devices. This is asynchronous and idempotent (calling this when not scanning does nothing).
  /// </summary>
  public void StopDiscovery()
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

      SignalForwarder.ToMainThreadAsync(() => 
      {
        var stateName = args.NewState.ToString();
        GD.Print($"Bluetooth state changed to {stateName}");
        EmitSignal(nameof(SignalName.BluetoothStateChanged), stateName);
      }, "Bluetooth state change");
    }
  }
}
