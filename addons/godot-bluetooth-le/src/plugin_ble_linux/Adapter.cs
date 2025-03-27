using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Linux.Bluetooth;

namespace Plugin.BLE
{
  public class Adapter : AdapterBase
  {
    Linux.Bluetooth.Adapter _adapter;

    public Adapter(Linux.Bluetooth.Adapter adapter)
    {
      _adapter = adapter;
      _adapter.DeviceFound += OnDeviceFoundAsync;
    }

    public void Dispose()
    {
      _adapter.DeviceFound -= OnDeviceFoundAsync;
    }

    static Guid AddressToGuid(string addr)
    {
      // Convert MAC address to Guid. The Guid does not need to be an actual UUID, since our mac addresses
      // are the unique identifiers.
      var bytes = addr.Split(':').Select(b => Convert.ToByte(b, 16)).ToArray();
      var guidBytes = new byte[16];
      Array.Copy(bytes, 0, guidBytes, 10, 6); // Place MAC address in the last 6 bytes of the GUID
      return new Guid(guidBytes);
    }

    private async Task OnDeviceFoundAsync(Linux.Bluetooth.Adapter sender, Linux.Bluetooth.DeviceFoundEventArgs e)
    {
      Device device = null;
      var addr = await e.Device.GetAddressAsync();
      Trace.Message($"Bluetooth: Found device: ({addr})");
      Guid id = AddressToGuid(addr);
      // If the device is already known, get it from the dict.
      if (e.IsStateChange)
      {
        device = DiscoveredDevicesRegistry[id] as Device;
      }
      else
      {
        device = new Device(this, id, e.Device);
      }
      
      await device.UpdateInfo();
      if (!e.IsStateChange)
    {
      HandleDiscoveredDevice(device);
    }
    }

    public override Task BondAsync(IDevice device)
    {
      return new Task(async () =>
      {
        var nativeDevice = device as Device;
        if (nativeDevice is null)
        {
          Trace.Message($"BondAsync failed (null) for device: {device.Name}: {device.Id} ");
          // return;
        }
        await nativeDevice.NativeDevice.PairAsync();
      });
    }

    public override async Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default)
    {
      if (!DiscoveredDevicesRegistry.ContainsKey(deviceGuid))
      {
        throw new ArgumentException($"Device with id {deviceGuid} not found in known devices.");
      }
      var dev = DiscoveredDevicesRegistry[deviceGuid] as Device;
      await dev.ConnectAsync();
      if (dev.State != DeviceState.Connected)
      {
        HandleConnectionFail(dev, "connect failure - unknown reason.");
      }
      return dev; 
    }

    public override IReadOnlyList<IDevice> GetKnownDevicesByIds(Guid[] ids)
    {
      return DiscoveredDevicesRegistry.Where((dev) => ids.Contains(dev.Key)).Select((dev) => dev.Value).ToList();
    }

    public override IReadOnlyList<IDevice> GetSystemConnectedOrPairedDevices(Guid[] services = null)
    {
      return DiscoveredDevices.Where((dev) => dev.State == DeviceState.Connected).ToList();
    }

    protected override Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken)
    {
      return (device.NativeDevice as Device).ConnectAsync();
    }

    protected override void DisconnectDeviceNative(IDevice device)
    {
      (device.NativeDevice as Device).DisconnectAsync().Wait();
    }

    protected override IReadOnlyList<IDevice> GetBondedDevices()
    {
      return DiscoveredDevices.Where((dev) => dev.BondState == DeviceBondState.Bonded).ToList();
    }

    protected override Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken)
    {
      return Task.Run(async () =>
      {
        if (scanFilterOptions == null)
        {
          scanFilterOptions = new ScanFilterOptions();
        }

        var nativeFilterOptions = new Dictionary<string, object>();

        // Build Bluez filter options. See https://web.git.kernel.org/pub/scm/bluetooth/bluez.git/tree/doc/org.bluez.Adapter.rst
        if (scanFilterOptions.ServiceUuids != null && scanFilterOptions.ServiceUuids.Count() > 0)
        {
          var uuids = new String[scanFilterOptions.ServiceUuids.Count()];
          int i = 0;
          Array.ForEach(scanFilterOptions.ServiceUuids, (val) =>
          {
            uuids[i] = val.ToString();
            i++;
          });
          nativeFilterOptions.Add("UUIDs", uuids);
        }

        // Bluez supports prefix filter on either address or name with a single string.
        // Check if either field is set, fail if more than one is provided.
        if ((scanFilterOptions.DeviceNames == null ? 0 : scanFilterOptions.DeviceNames.Count())
          + (scanFilterOptions.DeviceAddresses == null ? 0 : scanFilterOptions.DeviceAddresses.Count()) > 1)
        {
          throw new ArgumentException("Bluetooth: Only prefix or exact match discovery is supported on Linux. Please provide only one DeviceName or DeviceAddress.");
        }

        if (scanFilterOptions.DeviceNames != null && scanFilterOptions.DeviceNames.Count() > 0)
        {
          nativeFilterOptions.Add("Pattern", scanFilterOptions.DeviceNames[0]);
        }

        if (scanFilterOptions.DeviceAddresses != null && scanFilterOptions.DeviceAddresses.Count() > 0)
        {
          nativeFilterOptions.Add("Pattern", scanFilterOptions.DeviceAddresses[0]);
        }

        if (allowDuplicatesKey)
        {
          nativeFilterOptions.Add("DuplicateData", true);
        }

        await _adapter.SetDiscoveryFilterAsync(nativeFilterOptions);

        Trace.Message("Bluetooth: Starting scan. Filters: ");
        foreach (var key in nativeFilterOptions.Keys)
        {
          Trace.Message($"Bluetooth:   > {key}: {nativeFilterOptions[key]}");
        }

        await _adapter.StartDiscoveryAsync();
      });
    }


    protected override void StopScanNative()
    {
      Trace.Message("Bluetooth: Stopping scan... ");
      _adapter.StopDiscoveryAsync().Wait();
      Trace.Message("Bluetooth: Stopped scan.");
    }
  }
}