using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE
{
  public class Service : ServiceBase<Linux.Bluetooth.IGattService1>
  {
    private bool _inited = false;

    private Guid _guid;

    private bool _primary;

    public Service(IDevice device, IGattService1 nativeService) : base(device, nativeService) {}

    public override Guid Id => _guid;

    public override bool IsPrimary => _primary;

    public async Task init()
    {
      var properties = await NativeService.GetAllAsync();
      _guid = Guid.Parse(properties.UUID);
      _primary = properties.Primary;
      _inited = true;
    }

    protected override async Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync()
    {
      var nativeChara = await NativeService.GetCharacteristicsAsync();
      var characteristics = new List<ICharacteristic>();
      foreach (var nativeCharacteristic in nativeChara)
      {
        var characteristic = new Characteristic(this, nativeCharacteristic);
        await characteristic.Init();
        characteristics.Add(characteristic);
      }
      return characteristics;
    }
  }
}