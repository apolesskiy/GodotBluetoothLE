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

  public bool IsService()
  {
    return ServiceUUID != string.Empty && CharacteristicUUID == string.Empty && DescriptorUUID == string.Empty;
  }

  public bool IsCharacteristic()
  {
    return ServiceUUID != string.Empty && CharacteristicUUID != string.Empty && DescriptorUUID == string.Empty;
  }

  public bool IsDescriptor()
  {
    return ServiceUUID != string.Empty && CharacteristicUUID != string.Empty && DescriptorUUID != string.Empty;
  }

  public BluetoothDevice.GattServiceHandle GetServiceHandle()
  {
    return new BluetoothDevice.GattServiceHandle(ServiceUUID, ServiceIndex);
  }

  public BluetoothDevice.GattCharacteristicHandle GetCharacteristicHandle()
  {
    if (IsService())
    {
      throw new InvalidOperationException("Cannot get characteristic handle from a service handle.");
    }

    return new BluetoothDevice.GattCharacteristicHandle(GetServiceHandle(), CharacteristicUUID, CharacteristicIndex);
  }

  public BluetoothDevice.GattDescriptorHandle GetDescriptorHandle()
  {
    if (IsService() || IsCharacteristic())
    {
      throw new InvalidOperationException("Cannot get descriptor handle from a service or characteristic handle.");
    }

    return new BluetoothDevice.GattDescriptorHandle(GetCharacteristicHandle(), DescriptorUUID, DescriptorIndex);
  }
}