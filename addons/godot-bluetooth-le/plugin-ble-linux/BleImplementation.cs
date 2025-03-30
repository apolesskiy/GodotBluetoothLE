using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;

// Implementation of Plugin.BLE based on Linux.Bluetooth.
namespace Plugin.BLE
{
  public class BleImplementation : BleImplementationBase
  {
    private Linux.Bluetooth.Adapter _adapter;
    bool isInitialized = false;

    public BleImplementation()
    {
      Trace.Message("Creating BLE implementation.");
    }


    protected override IAdapter CreateNativeAdapter()
    {
      // Assumption: We only use the default adapter
      // Create our adapter wrapper that implements IAdapter
      return new Plugin.BLE.Adapter(_adapter);
    }

    protected override BluetoothState GetInitialStateNative()
    {
      if (_adapter == null)
      {
        return BluetoothState.Unavailable;
      }

      if (_adapter.GetPoweredAsync().Result)
      {
        return BluetoothState.On;
      }

      return BluetoothState.Off;
    }

    protected override void InitializeNative()
    {
      // Fetch adapter list from bluez
      IReadOnlyCollection<Linux.Bluetooth.Adapter> nativeAdapters = null;
      try
      {
        nativeAdapters = BlueZManager.GetAdaptersAsync().Result;
      }
      catch (Tmds.DBus.ConnectException conEx)
      {
        Trace.Message($"Failed to get bluetooth adapter. DBus connection unavailable.\n{conEx}");
        return;
      }
      catch (Tmds.DBus.DBusException dbusEx)
      {
        Trace.Message($"Failed to get bluetooth adapter. DBus error.\n{dbusEx}");
        return;
      }
      catch (Exception ex)
      {
        Trace.Message($"Failed to get bluetooth adapter. Unexpected exception. \n{ex}");
        return;
      }

      if (nativeAdapters.Count == 0)
      {
        Trace.Message("Failed to get bluetooth adapter. No Bluetooth LE adapters Found.");
        return;
      }

      // Auto-select adapter
      _adapter = nativeAdapters.First();

      if (_adapter == null)
      {
        Trace.Message("Failed to get bluetooth adapter. Found adapter was null.");
        return;
      }

      Trace.Message($"Bluetooth found adapter {_adapter.GetNameAsync().Result}.");
      
#pragma warning disable 1998
      _adapter.PoweredOn += async (sender, e) => State = BluetoothState.On;
      _adapter.PoweredOff += async (sender, e) => State = BluetoothState.Off;
#pragma warning restore 1998


      State = _adapter.GetPoweredAsync().Result ? BluetoothState.On : BluetoothState.Off;

      isInitialized = true;
    }
  }
}