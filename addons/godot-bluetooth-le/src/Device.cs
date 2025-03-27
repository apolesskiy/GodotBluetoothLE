using Godot;
using Plugin.BLE.Abstractions.Contracts;

namespace GodotBluetooth;

/// <summary>
/// Representation of a Bluetooth Low Energy device.
/// </summary>
public partial class Device : RefCounted
{
  private IDevice _device;

  public Device(IDevice device)
  {
    _device = device;
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
  public delegate void DisconnectedEventHandler();

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
      return _device.Id.ToString();
    }
  }

  public string State
  {
    get
    {
      return _device.State.ToString();
    }
  }


}