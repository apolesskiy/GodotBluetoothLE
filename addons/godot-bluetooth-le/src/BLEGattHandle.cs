using System;
using Godot;

namespace GodotBLE;

/// <summary>
/// This class exists for the benefit of GDScript users. It is identical in purpose
/// to BluetoothDevice.GattHandle, but the latter is a record type.
/// </summary>
[GlobalClass]
public partial class BLEGattHandle : Resource
{
  [Export]
  public string ServiceUUID { get; set; } = string.Empty;
  [Export]
  public string CharacteristicUUID { get; set; } = string.Empty;
  [Export]
  public string DescriptorUUID { get; set; } = string.Empty;
  [Export]
  public int ServiceIndex { get; set; } = 0;
  [Export]
  public int CharacteristicIndex { get; set; } = 0;
  [Export]
  public int DescriptorIndex { get; set; } = 0;
}