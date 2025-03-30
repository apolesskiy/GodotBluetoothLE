using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    private static TimeSpan _connectTimeout = TimeSpan.FromSeconds(10);
    private static TimeSpan _resolveServicesTimeout = TimeSpan.FromSeconds(10);

    private Adapter _adp;

    DeviceState _state;
    bool _paired;
    bool _disconnecting;


    protected override DeviceState GetState()
    {
      return _state;
    }


    public Device(Adapter adp, Guid id, Linux.Bluetooth.Device nativeDevice) : base(adp, nativeDevice)
    {
      _adp = adp;
      Id = id;
      IsConnectable = true;
      NativeDevice = nativeDevice;
    }

    public void InitEventsAsync()
    {
      NativeDevice.Connected += OnConnected;
      NativeDevice.ServicesResolved += OnServicesResolved;
      NativeDevice.Disconnected += OnDisconnected;
    }

    public override void Dispose()
    {
      base.Dispose();
      NativeDevice.Connected -= OnConnected;
      NativeDevice.ServicesResolved -= OnServicesResolved;
      NativeDevice.Disconnected -= OnDisconnected;
    }

#pragma warning disable 1998
    public async Task OnConnected(object sender, EventArgs e)
    {
      _state = DeviceState.Connected;
      Trace.Message("Bluetooth: Device " + Id + " connected, resolving services.");
    }


    public async Task OnServicesResolved(object sender, EventArgs e)
    {
      _state = DeviceState.Connected;
      _adp.OnDeviceConnected(this);
    }

    public async Task OnDisconnected(object sender, EventArgs e)
    {
      _state = DeviceState.Disconnected;
      _adp.OnDeviceDisconnected(_disconnecting, this);
      _disconnecting = false;
    }
#pragma warning restore 1998


    // Update properties from native device in response to a state change.
    public async Task UpdateInfo()
    {
      var props = await NativeDevice.GetAllAsync();
      Name = props.Name;
      Rssi = props.RSSI;
      var connected = props.Connected;
      _paired = props.Paired;

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
      Trace.Message("Bluetooth: Attempting connection to device " + Id);
      _disconnecting = false;
      _state = DeviceState.Connecting;
      await NativeDevice.ConnectAsync();
      try {
        await NativeDevice.WaitForPropertyValueAsync("Connected", true, _connectTimeout);
        await NativeDevice.WaitForPropertyValueAsync("ServicesResolved", true, _resolveServicesTimeout);
      }
      catch (TimeoutException)
      {
        Trace.Message("Bluetooth: Connection timeout for device " + Id);
        _state = DeviceState.Disconnected;
        _adp.OnDeviceDisconnected(false, this);
      }
    }

    public async Task DisconnectAsync()
    {
      Trace.Message("Bluetooth: Attempting disconnect from device " + Id);
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
      // From the upstream repo: "Currently not being used anywhere." 
      // https://github.com/dotnet-bluetooth-le/dotnet-bluetooth-le/blob/c9261fc147a65553092df2c5b52c4542035c5dca/Source/Plugin.BLE/Shared/DeviceBase.cs#L171
      throw new NotImplementedException();
    }

    protected override Task<IReadOnlyList<IService>> GetServicesNativeAsync()
    {
      return _GetServicesNativeAsync();
    }

    private async Task<IReadOnlyList<IService>> _GetServicesNativeAsync()
    {
      var nativeServices = await NativeDevice.GetServicesAsync();
      var services = new List<IService>(nativeServices.Count);
      foreach (var nativeService in nativeServices)
      {
        var service = new Service(this, nativeService);
        await service.init();
        services.Add(service);
      }
      return services;
    }

    protected override Task<int> RequestMtuNativeAsync(int requestValue)
    {
      return Task.FromResult(0);
    }

    protected override bool UpdateConnectionIntervalNative(ConnectionInterval interval)
    {
      return false;
    }

    public override bool UpdateConnectionParameters(ConnectParameters connectParameters = default)
    {
      return false;
    }
  }
}