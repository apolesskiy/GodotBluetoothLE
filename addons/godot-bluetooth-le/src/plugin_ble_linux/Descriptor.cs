using System;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE
{
  public class Descriptor : DescriptorBase<Linux.Bluetooth.IGattDescriptor1>
  {
    private bool _inited = false;
    private Guid _guid;
    private byte[] _readValue;
    public Descriptor(ICharacteristic characteristic, IGattDescriptor1 nativeDescriptor) : base(characteristic, nativeDescriptor) { }

    public override Guid Id => _guid;
    public override byte[] Value => _readValue;

    public async Task Init()
    {
      var properties = await NativeDescriptor.GetAllAsync();
      _guid = Guid.Parse(properties.UUID);
      _readValue = properties.Value;
      _inited = true;
    }

    protected override Task<byte[]> ReadNativeAsync()
    {
      return _ReadNativeAsync();
    }

    private async Task<byte[]> _ReadNativeAsync()
    {
      if (!_inited)
      {
        throw new InvalidOperationException("Descriptor not initialized. Call Init() before reading.");
      }

      var data = await NativeDescriptor.GetValueAsync();
      _readValue = data;
      return _readValue;
    }


    protected override Task WriteNativeAsync(byte[] data)
    {
      return _WriteNativeAsync(data);
    }

    private async Task _WriteNativeAsync(byte[] data)
    {
      if (!_inited)
      {
        throw new InvalidOperationException("Descriptor not initialized. Call Init() before writing.");
      }
      await NativeDescriptor.WriteValueAsync(data, null);
    }
  }
}