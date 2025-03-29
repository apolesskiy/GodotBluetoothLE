using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Plugin.BLE
{
  public class Characteristic : CharacteristicBase<IGattCharacteristic1>
  {

    private Guid _guid;

    public override Guid Id => _guid;

    public override string Uuid => Id.ToString();

    private byte[] _readValue;

    public override byte[] Value => _readValue;

    private CharacteristicPropertyType _properties;

    public override CharacteristicPropertyType Properties => _properties;

    public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

    public Characteristic(IService service, IGattCharacteristic1 nativeCharacteristic) : base(service, nativeCharacteristic)
    {}

    public async Task Init()
    {
      var properties = await NativeCharacteristic.GetAllAsync();
      _guid = Guid.Parse(properties.UUID);
      SetCharacteristicProperties(properties.Flags);
      // Register a handler on this property and pass it back.
      await NativeCharacteristic.WatchPropertiesAsync((propChange) =>
      {
        foreach (var (pname, pobj) in propChange.Changed)
        {
          if ("Value".Equals(pname))
          {
            byte[] pbytes = (byte[])pobj;
            OnValueChanged(pbytes);
          }
        }
      });
    }


    // https://github.com/bluez/bluez/blob/master/doc/org.bluez.GattCharacteristic.rst
    // https://github.com/dotnet-bluetooth-le/dotnet-bluetooth-le/blob/master/doc/characteristics.md
    private static Dictionary<string, CharacteristicPropertyType> CharacteristicPropertyMap = new Dictionary<string, CharacteristicPropertyType>
    {
      { "broadcast", CharacteristicPropertyType.Broadcast },
      { "read", CharacteristicPropertyType.Read },
      { "write-without-response", CharacteristicPropertyType.WriteWithoutResponse },
      { "write", CharacteristicPropertyType.Write },
      { "notify", CharacteristicPropertyType.Notify },
      { "indicate", CharacteristicPropertyType.Indicate },
      { "authenticated-signed-writes", CharacteristicPropertyType.AuthenticatedSignedWrites },
      { "extended-properties", CharacteristicPropertyType.ExtendedProperties },
      // There are other properties that are platform-specific or not supported by Plugin.BLE. See links above.
    };


    private void SetCharacteristicProperties(string[] properties)
    {
      foreach (var property in properties)
      {
        if (CharacteristicPropertyMap.TryGetValue(property, out CharacteristicPropertyType value))
        {
          _properties |= (CharacteristicPropertyType)value;
        }
      }
    }  


    protected override Task<IReadOnlyList<IDescriptor>> GetDescriptorsNativeAsync()
    {
      return _GetDescriptorsNativeAsync();
    }


#pragma warning disable CS1998
    private async Task<IReadOnlyList<IDescriptor>> _GetDescriptorsNativeAsync()
    {
      // Descriptors are supported in BlueZ, but there is no way to access them in Linux.Bluetooth.
      return new List<IDescriptor>();
    }
#pragma warning restore CS1998


    private void UpdateValue(byte[] value)
    {
      if (value == null)
      {
        return;
      }
      _readValue = value;
      ValueUpdated?.Invoke(this, new CharacteristicUpdatedEventArgs(this));
    }


    private void OnValueChanged(byte[] data)
    {
      UpdateValue(data);
    }


    protected override Task<(byte[] data, int resultCode)> ReadNativeAsync()
    {
      return _ReadNativeAsync();
    }


    private async Task<(byte[] data, int resultCode)> _ReadNativeAsync()
    {
      var data = await NativeCharacteristic.ReadValueAsync(null);
      _readValue = data;
      UpdateValue(data);
      return (data, 0);
    }


    protected override Task StartUpdatesNativeAsync(CancellationToken cancellationToken = default)
    {
      return NativeCharacteristic.StartNotifyAsync();
    }


    protected override Task StopUpdatesNativeAsync(CancellationToken cancellationToken = default)
    {
      return NativeCharacteristic.StopNotifyAsync();
    }


    protected override Task<int> WriteNativeAsync(byte[] data, CharacteristicWriteType writeType)
    {
      return _WriteNativeAsync(data, writeType);
    }


    private async Task<int> _WriteNativeAsync(byte[] data, CharacteristicWriteType writeType)
    {
      await NativeCharacteristic.WriteValueAsync(data, null);
      return 0;
    }
  }
}