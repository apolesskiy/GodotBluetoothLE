using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE
{
  public class Device : DeviceBase<Linux.Bluetooth.Device>
  {

    private Adapter _adp;

    DeviceState _state;
    bool _paired;
    bool _blocked;
    bool _disconnecting;


    protected override DeviceState GetState()
    {
      return _state;
    }


    public Device(Adapter adp, Guid id, Linux.Bluetooth.Device nativeDevice) : base(adp, nativeDevice)
    {
      adp = _adp;
      Id = id;
      IsConnectable = true;
      NativeDevice.Connected += OnConnected;
      NativeDevice.Disconnected += OnDisconnected;
    }

    public override void Dispose()
    {
      base.Dispose();
      NativeDevice.Connected -= OnConnected;
      NativeDevice.Disconnected -= OnDisconnected;
    }

#pragma warning disable 1998
    public async Task OnConnected(object sender, EventArgs e)
    {
      _state = DeviceState.Connected;
      _adp.HandleConnectedDevice(this);
    }

    public async Task OnDisconnected(object sender, EventArgs e)
    {
      _state = DeviceState.Disconnected;
      _adp.HandleDisconnectedDevice(_disconnecting, this);
      _disconnecting = false;
    }
#pragma warning restore 1998


    // Update properties from native device in response to e.g. a state change.
    public async Task UpdateInfo()
    {
      Name = await NativeDevice.GetNameAsync();
      Rssi = await NativeDevice.GetRSSIAsync();
      var connected = await NativeDevice.GetConnectedAsync();
      _paired = await NativeDevice.GetPairedAsync();
      var blocked = await NativeDevice.GetBlockedAsync();

      if (!connected)
      {
        _state = DeviceState.Disconnected;
      }
      else
      {
        _state = DeviceState.Connected;
      }
    }

    public async Task ConnectAsync()
    {
      _disconnecting = false;
      _state = DeviceState.Connecting;
      await NativeDevice.ConnectAsync();
    }

    public async Task DisconnectAsync()
    {
      _disconnecting = true;
      await NativeDevice.DisconnectAsync();
    }

    public override bool SupportsIsConnectable => false;

    public override bool IsConnectable { get; protected set; }

    public override Task<bool> UpdateRssiAsync()
    {
      return new Task<bool>(bool() => { NativeDevice.GetRSSIAsync(); return true; });
    }

    protected override DeviceBondState GetBondState()
    {
      return _paired ? DeviceBondState.Bonded : DeviceBondState.NotBonded;
    }

    protected override Task<IService> GetServiceNativeAsync(Guid id)
    {
        Trace.Message("Warning: Calling GetServiceNativeAsync - only the first instance of a service will be returned!");
        throw new NotImplementedException();
    }

    protected override Task<IReadOnlyList<IService>> GetServicesNativeAsync()
    {
        throw new NotImplementedException();
    }


    protected override Task<int> RequestMtuNativeAsync(int requestValue)
    {
      return Task.FromResult(0);
    }

    protected override bool UpdateConnectionIntervalNative(ConnectionInterval interval)
    {
      return false;
    }
  }
}