using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Godot;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace GodotBLE;

/// <summary>
/// Representation of a Bluetooth Low Energy device.
/// </summary>
[GlobalClass]
public partial class BluetoothDevice : RefCounted
{
  private static StringName snDisconnectReasonDisconnected = new StringName("disconnected");
  private static StringName snDisconnectReasonConnectionLost = new StringName("connection_lost");
  private static StringName snDisconnectReasonError = new StringName("connect_error");

  private static StringName snStateConnected = DeviceState.Connected.ToString();
  private static StringName snStateConnecting = DeviceState.Connecting.ToString();
  private static StringName snStateDisconnected = DeviceState.Disconnected.ToString();
  private static StringName snStateLimited = DeviceState.Limited.ToString();

  // Wouldn't it be nice if C# enums worked in godot?
  public static StringName DisconnectReasonDisconnected()
  {
    return snDisconnectReasonDisconnected;
  }

  public static StringName DisconnectReasonConnectionLost()
  {
    return snDisconnectReasonConnectionLost;
  }


  public static StringName DisconnectReasonError()
  {
    return snDisconnectReasonError;
  }


  public static StringName StateConnected()
  {
    return snStateConnected;
  }


  public static StringName StateConnecting()
  {
    return snStateConnecting;
  }


  public static StringName StateDisconnected()
  {
    return snStateDisconnected;
  }


  public static StringName StateLimited()
  {
    return snStateLimited;
  }


  static Guid MacToGuid(string mac)
  {
    var bytes = mac.Split(':').Select(b => Convert.ToByte(b, 16)).ToArray();
    var guidBytes = new byte[16];
    Array.Copy(bytes, 0, guidBytes, 10, 6);
    return new Guid(guidBytes);
  }

  static string GuidToMac(Guid guid)
  {
    var macBytes = new ArraySegment<byte>(guid.ToByteArray(), 10, 6);
    return string.Join(":", macBytes.Select(b => b.ToString("X2")));
  }

  private IDevice _device;

  private Bluetooth _bt;

  private IAdapter _adp;

  private string _address;

  public BluetoothDevice(Bluetooth bt, IAdapter adp, IDevice device)
  {
    _bt = bt;
    _adp = adp;
    _device = device;
    _address = GuidToMac(device.Id);
  }

  /// <summary>
  /// Emitted when device connection is established.
  /// </summary>
  [Signal]
  public delegate void ConnectedEventHandler();

  /// <summary>
  /// Emitted when device is disconnected or connection is lost.
  /// </summary>
  [Signal]
  public delegate void DisconnectedEventHandler(string reason);

  /// <summary>
  /// Emitted when a connected device's services have been discovered.
  /// </summary>
  [Signal]
  public delegate void ServicesReadyEventHandler();

  public string Name
  {
    get
    {
      return _device.Name;
    }
  }

  public string Address
  {
    get
    {
      return _address;
    }
  }

  public string State
  {
    get
    {
      return _device.State.ToString();
    }
  }

  /// <summary>
  /// Start connecting to this device.
  /// Listen to Connected and Disconnected signals to know when the connection is established.
  /// </summary>
  public void StartConnect()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Disconnected)
    {
      return;
    }
    new Task(async () =>
    {
      await _adp.ConnectToDeviceAsync(_device);
    }).Start();
  }

  public void StartDisconnect()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      return;
    }
    new Task(async () =>
    {
      await _adp.DisconnectDeviceAsync(_device);
    }).Start();
  }


}