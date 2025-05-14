using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Godot;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace GodotBLE;

/// <summary>
/// Representation of a Bluetooth Low Energy device.
/// </summary>
[GlobalClass]
public partial class BluetoothDevice : RefCounted
{
  private static StringName snDisconnectReasonDisconnected = new StringName("disconnected");
  private static StringName snDisconnectReasonConnectionLost = new StringName("connection_lost");
  private static StringName snDisconnectReasonError = new StringName("connect_error");

  private static StringName snStateConnected = DeviceState.Connected.ToString();
  private static StringName snStateConnecting = DeviceState.Connecting.ToString();
  private static StringName snStateDisconnected = DeviceState.Disconnected.ToString();
  private static StringName snStateLimited = DeviceState.Limited.ToString();

  // Wouldn't it be nice if C# enums worked in godot?
  public static StringName DisconnectReasonDisconnected()
  {
    return snDisconnectReasonDisconnected;
  }

  public static StringName DisconnectReasonConnectionLost()
  {
    return snDisconnectReasonConnectionLost;
  }


  public static StringName DisconnectReasonError()
  {
    return snDisconnectReasonError;
  }


  public static StringName StateConnected()
  {
    return snStateConnected;
  }


  public static StringName StateConnecting()
  {
    return snStateConnecting;
  }


  public static StringName StateDisconnected()
  {
    return snStateDisconnected;
  }


  public static StringName StateLimited()
  {
    return snStateLimited;
  }


  static Guid MacToGuid(string mac)
  {
    var bytes = mac.Split(':').Select(b => Convert.ToByte(b, 16)).ToArray();
    var guidBytes = new byte[16];
    Array.Copy(bytes, 0, guidBytes, 10, 6);
    return new Guid(guidBytes);
  }

  static string GuidToMac(Guid guid)
  {
    var macBytes = new ArraySegment<byte>(guid.ToByteArray(), 10, 6);
    return string.Join(":", macBytes.Select(b => b.ToString("X2")));
  }


  public record class GattServiceHandle
  {
    public string UUID;
    public int Index;

    public GattServiceHandle(string uuid, int index)
    {
      UUID = uuid;
      Index = index;
    }

    public BLEGattHandle ToGodotHandle()
    {
      var handle = new BLEGattHandle();
      handle.ServiceUUID = UUID;
      handle.ServiceIndex = Index;
      return handle;
    }
  }

  public record class GattCharacteristicHandle
  {
    public GattServiceHandle ServiceHandle;
    public string UUID;
    public int Index;

    public GattCharacteristicHandle(GattServiceHandle serviceHandle, string uuid, int index)
    {
      ServiceHandle = serviceHandle;
      UUID = uuid;
      Index = index;
    }

    public BLEGattHandle ToGodotHandle()
    {
      var handle = new BLEGattHandle();
      handle.ServiceUUID = ServiceHandle.UUID;
      handle.ServiceIndex = ServiceHandle.Index;
      handle.CharacteristicUUID = UUID;
      handle.CharacteristicIndex = Index;
      return handle;
    }
  }

  public record class GattDescriptorHandle
  {
    public GattCharacteristicHandle CharacteristicHandle;
    public string UUID;
    public int Index;

    public GattDescriptorHandle(GattCharacteristicHandle characteristicHandle, string uuid, int index)
    {
      CharacteristicHandle = characteristicHandle;
      UUID = uuid;
      Index = index;
    }

    public BLEGattHandle ToGodotHandle()
    {
      var handle = new BLEGattHandle();
      handle.ServiceUUID = CharacteristicHandle.ServiceHandle.UUID;
      handle.ServiceIndex = CharacteristicHandle.ServiceHandle.Index;
      handle.CharacteristicUUID = CharacteristicHandle.UUID;
      handle.CharacteristicIndex = CharacteristicHandle.Index;
      handle.DescriptorUUID = UUID;
      handle.DescriptorIndex = Index;
      return handle;
    }
  }

  private static List<DeviceState> ConnectableStates = new List<DeviceState>
  {
    DeviceState.Disconnected,
    // This is something android-specific in Plugin.BLE, we can ignore it.
    DeviceState.Limited,
  };

  private IDevice _device;

  private Bluetooth _bt;

  private IAdapter _adp;

  private string _address;

  private BLEOperation _connectOp = null;

  private ConcurrentDictionary<GattServiceHandle, IService> _services = new ConcurrentDictionary<GattServiceHandle, IService>();
  private ConcurrentDictionary<GattCharacteristicHandle, ICharacteristic> _characteristics = new ConcurrentDictionary<GattCharacteristicHandle, ICharacteristic>();
  private ConcurrentDictionary<GattDescriptorHandle, IDescriptor> _descriptors = new ConcurrentDictionary<GattDescriptorHandle, IDescriptor>();

  // Observers use descriptor handles, but can observe descriptors or characteristics.

  private ConcurrentDictionary<GattDescriptorHandle, GattObserver> _observers = new ConcurrentDictionary<GattDescriptorHandle, GattObserver>();

  public BluetoothDevice(Bluetooth bt, IAdapter adp, IDevice device)
  {
    _bt = bt;
    _adp = adp;
    _device = device;
    _address = GuidToMac(device.Id);
  }

  /// <summary>
  /// Emitted when device connection is established.
  /// 
  /// Notifications must be configured every time this signal is emitted.
  /// 
  /// Note that depending on device, this may change the list of available services.
  /// In these cases, it is recommended that any BLEGattHandles be reacquired and
  /// subscriptions reinitialized.
  /// </summary>
  [Signal]
  public delegate void ConnectedEventHandler();

  /// <summary>
  /// Emitted when device is disconnected or connection is lost.
  /// </summary>
  [Signal]
  public delegate void DisconnectedEventHandler(string reason);


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
      return _address;
    }
  }

  public string State
  {
    get
    {
      return _device.State.ToString();
    }
  }

  public bool CanConnect()
  {
    return ConnectableStates.Contains(_device.State);
  }

  /// <summary>
  /// Start connecting to this device.
  /// Listen to Connected and Disconnected signals to know when the connection is established.
  /// </summary>
  /// <returns>
  /// Connect operation. Since only one connection attempt is possible, the same operation will
  /// be returned if connect is called multiple times.
  /// The operation will be completed when the connection is established or fails.
  /// It is generally recommended to use the DeviceConnected/DeviceDisconnected signals, unless
  /// the outcome of the specific connection attempt is important.
  /// </returns>
  public BLEOperation Connect()
  {
    // We save the op instead of returning it because device connections may come from outside of this request.
    // We need to finalize these connections in the adapter's OnDeviceConnected callback, and only complete the
    // connect operation when that is done.
    if (_connectOp == null || _connectOp.IsDone)
    {
      _connectOp = BLEOperation.Create(async (op) =>
      {
        try
        {
          if (!CanConnect())
          {
            throw new InvalidOperationException($"Bluetooth: Cannot connect to device {_address}, device not in a connectable state.");
          }
          await _adp.ConnectToDeviceAsync(_device);
        }
        catch (Exception e)
        {
          op.Fail($"Bluetooth: Error connecting to device {_address}: {e.ToString()}");
          return;
        }
      });
    }
    return _connectOp;
  }

  public async Task DisconnectAsync()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException($"Bluetooth: Cannot disconnect from device {_address}, device not connected.");
    }
    await _adp.DisconnectDeviceAsync(_device);
  }

  public BLEOperation Disconnect()
  {
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        await DisconnectAsync();
        op.Succeed();
      }
      catch (Exception e)
      {
        op.Fail($"Bluetooth: Error disconnecting from device {_address}: {e.ToString()}");
      }
    });
  }

  /// <summary>
  /// Get the list of services on this device.
  /// </summary>
  /// <returns></returns>
  public List<GattServiceHandle> GetServices()
  {
    return _services.Keys.ToList();
  }

  /// <summary>
  /// Get the list of services on this device as a Godot array. GDScript users should call this method.
  /// </summary>
  public Godot.Collections.Array<BLEGattHandle> GetServicesArray()
  {
    return new Godot.Collections.Array<BLEGattHandle>(_services.Keys.Select(s => s.ToGodotHandle()));
  }


  /// <summary>
  /// Get the list of services on this device with the given UUID as a Godot array. GDScript users should call this method.
  /// </summary>
  public Godot.Collections.Array<BLEGattHandle> GetServicesArrayByUUID(string uuid)
  {
    return new Godot.Collections.Array<BLEGattHandle>(_services.Keys.Where(s => s.UUID == uuid).Select(s => s.ToGodotHandle()).ToList());
  }

  /// <summary>
  /// Get the characteristics of a service.
  /// </summary>
  public List<GattCharacteristicHandle> GetCharacteristics(GattServiceHandle handle)
  {
    return _characteristics.Keys.Where(c => c.ServiceHandle == handle).ToList();
  }

  /// <summary>
  /// Get the characteristics of a service as a Godot array.
  /// </summary>
  public Godot.Collections.Array<BLEGattHandle> GetCharacteristicsArray(BLEGattHandle handle)
  {
    return new Godot.Collections.Array<BLEGattHandle>(GetCharacteristics(handle.GetServiceHandle()).Select(c => c.ToGodotHandle()).ToList());
  }

  /// <summary>
  /// Get the descriptors of a characteristic.
  /// </summary>
  /// <param name="handle"></param>
  /// <returns></returns>
  public List<GattDescriptorHandle> GetDescriptors(GattCharacteristicHandle handle)
  {
    return _descriptors.Keys.Where(d => d.CharacteristicHandle == handle).ToList();
  }

  /// <summary>
  /// Get the descriptors of a characteristic.
  /// </summary>
  public Godot.Collections.Array<BLEGattHandle> GetDescriptorsArray(BLEGattHandle handle)
  {
    return new Godot.Collections.Array<BLEGattHandle>(GetDescriptors(handle.GetCharacteristicHandle()).Select(d => d.ToGodotHandle()).ToList());
  }

  /// <summary>
  /// Get the properties of a characteristic.
  /// This is a set of flags.
  /// </summary>
  /// <param name="handle"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  public int GetCharacteristicProperties(GattCharacteristicHandle handle)
  {
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    var characteristic = _characteristics[handle];
    return (int)characteristic.Properties;
  }

  public int GetCharacteristicProperties(BLEGattHandle handle)
  {
    return GetCharacteristicProperties(handle.GetCharacteristicHandle());
  }

  /// <summary>
  /// Write to a characteristic if it supports writing. The write itself will be done asynchronously.
  /// Writes do not update the cached value, because characteristiscs may have WRITE, but not READ.
  /// If the characteristic is WRITE_WITH_RESPONSE, the result will contain the device's response code.
  /// Otherwise, the result will be 0.
  /// </summary>
  /// <param name="handle"></param>
  /// <param name="data"></param>
  public async Task<int> WriteAsync(GattCharacteristicHandle handle, byte[] data)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException($"Bluetooth: Cannot write to device {_address}, device not connected.");
    }

    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }

    var characteristic = _characteristics[handle];
    if (!characteristic.CanWrite)
    {
      throw new InvalidOperationException("Characteristic does not support writing.");
    }

    return await characteristic.WriteAsync(data);
  }


  /// <summary>
  /// Write to a characteristic if it supports writing. The write itself will be done asynchronously.
  /// Writes do not update the cached value, because characteristiscs may have WRITE, but not READ.
  /// </summary>
  /// <param name="handle"></param>
  /// <param name="data"></param>
  /// <returns>
  /// BLEOperation of the write. If the characteristic is WRITE_WITH_RESPONSE, the result
  /// contain the device's response code.
  /// </returns>
  public BLEOperation Write(GattCharacteristicHandle handle, byte[] data)
  {
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        var result = await WriteAsync(handle, data);
        op.Succeed(result);
      }
      catch (Exception e)
      {
        op.Fail($"Exception while writing characteristic {handle}: {e.ToString()}");
      }
    });
  }


  /// <summary>
  /// Write to a characteristic if it supports writing.
  /// Writes do not update the cached value, because characteristiscs may have WRITE, but not READ.
  /// If the characteristic is WRITE_WITH_RESPONSE, the result will contain the device's response code.
  /// Otherwise, the result will be 0.
  public async Task<int> WriteAsync(BLEGattHandle handle, byte[] data)
  {
    if (!handle.IsCharacteristic())
    {
      throw new ArgumentException("Handle is not a characteristic.");
    }
    return await WriteAsync(handle.GetCharacteristicHandle(), data);
  }


  /// <summary>
  /// Write to a characteristic if it supports writing. The write will be done asynchronously.
  /// Writes do not update the cached value, because characteristiscs may have WRITE, but not READ.
  /// </summary>
  /// <param name="handle"></param>
  /// <param name="data"></param>
  public BLEOperation Write(BLEGattHandle handle, byte[] data)
  {
    if (!handle.IsCharacteristic())
    {
      throw new ArgumentException("Handle is not a characteristic.");
    }
    return Write(handle.GetCharacteristicHandle(), data);
  }


  /// <summary>
  /// Start a read operation on a characteristic.
  /// This will be done asynchronously in a separate thread.
  /// The result of the read will be returned as a byte array.
  /// The ValueChanged signal will be emitted for observers of this
  /// characteristic when the read is complete.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public BLEOperation Read(GattCharacteristicHandle handle)
  {
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        var data = await ReadCharacteristicAsync(handle);
        op.Succeed(data);
      }
      catch (Exception e)
      {
        op.Fail($"Exception while reading characteristic {handle}: {e.Message}");
      }
    });
  }


  /// <summary>
  /// Start a read operation on a descriptor.
  /// This will be done asynchronously in a separate thread.
  /// To retrieve the value, subscribe to the ValueChanged signal for this handle,
  /// and call GetValue() when the read is complete.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public BLEOperation Read(GattDescriptorHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException($"Bluetooth: Cannot write to device {_address}, device not connected.");
    }
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        var data = await ReadDescriptorAsync(handle);
        op.Succeed(data);
      }
      catch (Exception e)
      {
        op.Fail($"Exception while reading descriptor {handle}: {e.Message}");
      }
    });
  }

  /// <summary>
  /// Start a read operation on a characteristic or descriptor.
  /// The method returns when the read is complete.
  /// Upon completion, this method will also update the cached value and notify observers.
  /// </summary>
  /// <param name="handle"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  public async Task<byte[]> ReadAsync(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return await ReadCharacteristicAsync(handle.GetCharacteristicHandle());
    }
    if (handle.IsDescriptor())
    {
      return await ReadDescriptorAsync(handle.GetDescriptorHandle());
    }
    throw new ArgumentException("Handle is neither a characteristic nor a descriptor.");
  }


  /// <summary>
  /// Start a read operation on a characteristic or descriptor.
  /// This will be done asynchronously.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public BLEOperation Read(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return Read(handle.GetCharacteristicHandle());
    }
    if (handle.IsDescriptor())
    {
      return Read(handle.GetDescriptorHandle());
    }
    throw new ArgumentException("Handle is neither a characteristic nor a descriptor.");
  }


  /// <summary>
  /// Get the cached value of a characteristic or descriptor.
  /// This is the value that was last read or received via notification.
  /// This method does not perform a read operation.
  /// </summary>
  /// <param name="handle"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  /// <exception cref="ArgumentException"></exception>
  public byte[] GetValue(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return GetValue(handle.GetCharacteristicHandle());
    }
    if (handle.IsDescriptor())
    {
      return GetValue(handle.GetDescriptorHandle());
    }
    throw new ArgumentException("Handle is neither a characteristic nor a descriptor.");
  }


  public byte[] GetValue(GattCharacteristicHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      GD.PushWarning($"Bluetooth: Device {_address} not connected. Returned value may be invalid.");
    }
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    return _characteristics[handle].Value;
  }


  public byte[] GetValue(GattDescriptorHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      GD.PushWarning($"Bluetooth: Device {_address} not connected. Returned value may be invalid.");
    }
    if (!_descriptors.ContainsKey(handle))
    {
      throw new ArgumentException("Descriptor not found.");
    }
    return _descriptors[handle].Value;
  }


  /// <summary>
  /// Read a characteristic asynchronously.
  /// The method returns when the read is complete.
  /// Upon completion, this method will also update the cached value and notify observers.
  /// </summary>
  public async Task<byte[]> ReadCharacteristicAsync(GattCharacteristicHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException("Device not connected.");
    }
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    var characteristic = _characteristics[handle];
    if (!characteristic.CanRead)
    {
      throw new InvalidOperationException("Characteristic does not support reading.");
    }
    var result = await characteristic.ReadAsync();
    NotifyValueChanged(new GattDescriptorHandle(handle, string.Empty, 0));
    return result.Item1;
  }


  /// <summary>
  /// Read a descriptor asynchronously.
  /// The method returns when the read is complete.
  /// Upon completion, this method will also update the cached value and notify observers.
  /// </summary>
  public async Task<byte[]> ReadDescriptorAsync(GattDescriptorHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException("Device not connected.");
    }
    if (!_descriptors.ContainsKey(handle))
    {
      throw new ArgumentException("Descriptor not found.");
    }
    var descriptor = _descriptors[handle];
    var result = await descriptor.ReadAsync();
    NotifyValueChanged(handle);
    return result;
  }


  /// <summary>
  /// Notify that a value has changed, only if there is an observer for it.
  /// </summary>
  /// <param name="handle"></param>
  private void NotifyValueChanged(GattDescriptorHandle handle)
  {
    if (_observers.ContainsKey(handle))
    {
      var observer = _observers[handle];

      // Send async and main thread signals.
      observer.ValueChangedAsync?.Invoke();
      SignalForwarder.ToMainThreadAsync(() =>
      {
        observer.EmitSignal(GattObserver.SignalName.ValueChanged);
      }, "Bluetooth device value changed");
    }
  }


  /// <summary>
  /// Returns a GattObserver object. Its ValueChanged signal will fire any time the target characteristic or descriptor value is updated.
  /// This object is valid for the lifetime of the BluetoothDevice, including between connections.
  /// 
  /// Example: device.Observe(myCharacteristic).ValueChanged.connect(value_changed)
  /// </summary>
  public GattObserver GetSubscription(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return GetSubscription(handle.GetCharacteristicHandle());
    }
    else if (handle.IsDescriptor())
    {
      return GetSubscription(handle.GetDescriptorHandle());
    }
    throw new ArgumentException("Handle is neither a characteristic nor a descriptor.");
  }

  /// <summary>
  /// Returns a GattObserver object. Its ValueChanged signal will fire any time the target characteristic or descriptor value is updated.
  /// This object is valid for the lifetime of the BluetoothDevice, including between connections.
  /// </summary>
  public GattObserver GetSubscription(GattCharacteristicHandle handle)
  {
    return GetSubscription(new GattDescriptorHandle(handle, string.Empty, 0));
  }


  /// <summary>
  /// Returns a GattObserver object. Its ValueChanged signal will fire any time the target characteristic or descriptor value is updated.
  /// This object is valid for the lifetime of the BluetoothDevice, including between connections.
  /// </summary>
  /// <param name="handle"></param>
  public GattObserver GetSubscription(GattDescriptorHandle handle)
  {
    if (_observers.ContainsKey(handle))
    {
      return _observers[handle];
    }
    var observer = new GattObserver();
    _observers[handle] = observer;
    return observer;
  }


  /// <summary>
  /// Enable notifications on a given characteristic if it supports them.
  /// </summary>
  public async Task EnableNotifyAsync(GattCharacteristicHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException("Device not connected.");
    }
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    var characteristic = _characteristics[handle];
    if (!characteristic.CanUpdate)
    {
      throw new InvalidOperationException("Characteristic does not support notifications.");
    }
    await characteristic.StartUpdatesAsync();
  }


  /// <summary>
  /// Enable notifications on a given characteristic if it supports them. This will be done asynchronously.
  /// </summary>
  /// <param name="handle"></param>
  public BLEOperation EnableNotify(GattCharacteristicHandle handle)
  {
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        await EnableNotifyAsync(handle);
        op.Succeed();
      }
      catch (Exception e)
      {
        op.Fail($"Exception while starting notifications on characteristic {handle}: {e.ToString()}");
      }
    });
  }


  /// <summary>
  /// Start notifications on a given characteristic if it supports them. This will be done asynchronously.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public BLEOperation EnableNotify(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return EnableNotify(handle.GetCharacteristicHandle());
    }
    throw new ArgumentException("Handle is not a characteristic.");
  }


  public async Task DisableNotifyAsync(GattCharacteristicHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      throw new InvalidOperationException("Device not connected.");
    }
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    var characteristic = _characteristics[handle];
    if (!characteristic.CanUpdate)
    {
      throw new InvalidOperationException("Characteristic does not support notifications.");
    }
    await characteristic.StopUpdatesAsync();
  }


  /// <summary>
  /// Stop notifications on a given characteristic. This will be done asynchronously.
  /// </summary>
  /// <param name="handle"></param>
  public BLEOperation DisableNotify(GattCharacteristicHandle handle)
  {
    return BLEOperation.Create(async (op) =>
    {
      try
      {
        await DisableNotifyAsync(handle);
        op.Succeed();
      }
      catch (Exception e)
      {
        op.Fail($"Exception while stopping notifications on characteristic {handle}: {e.ToString()}");
      }
    });
  }


  /// <summary>
  /// Stop notifications on a given characteristic. This will be done asynchronously.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public BLEOperation DisableNotify(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      return DisableNotify(handle.GetCharacteristicHandle());
    }

    throw new ArgumentException("Handle is not a characteristic.");
  }


  private async Task BuildGattCache()
  {
    GD.Print("Building GATT cache for device " + _address);
    _services.Clear();
    _characteristics.Clear();
    _descriptors.Clear();

    var services = await _device.GetServicesAsync();
    Dictionary<Guid, int> svcCount = new Dictionary<Guid, int>();
    Dictionary<Guid, int> charCount = new Dictionary<Guid, int>();
    Dictionary<Guid, int> descCount = new Dictionary<Guid, int>();
    foreach (var service in services)
    {
      if (svcCount.ContainsKey(service.Id))
      {
        svcCount[service.Id]++;
      }
      else
      {
        svcCount[service.Id] = 0;
      }
      var serviceHandle = new GattServiceHandle(service.Id.ToString(), svcCount[service.Id]);

      _services[serviceHandle] = service;

      var characteristics = await service.GetCharacteristicsAsync();
      charCount.Clear();
      foreach (var characteristic in characteristics)
      {
        if (charCount.ContainsKey(characteristic.Id))
        {
          charCount[characteristic.Id]++;
        }
        else
        {
          charCount[characteristic.Id] = 0;
        }
        var charHandle = new GattCharacteristicHandle(serviceHandle, characteristic.Id.ToString(), charCount[characteristic.Id]);
        _characteristics[charHandle] = characteristic;
        var obsHandle = new GattDescriptorHandle(charHandle, string.Empty, 0);
        // Characteristics only, register for updates. This only applies to notifies and is handled by the backing implementation.
        characteristic.ValueUpdated += (sender, args) =>
        {
          NotifyValueChanged(obsHandle);
        };
        var descriptors = await characteristic.GetDescriptorsAsync();
        descCount.Clear();
        foreach (var descriptor in descriptors)
        {
          if (descCount.ContainsKey(descriptor.Id))
          {
            descCount[descriptor.Id]++;
          }
          else
          {
            descCount[descriptor.Id] = 0;
          }
          var descHandle = new GattDescriptorHandle(charHandle, descriptor.Id.ToString(), descCount[descriptor.Id]);
          _descriptors[descHandle] = descriptor;
        }
      }
    }
  }

  /// <summary>
  /// Build a cache of the device's services and characteristics.
  /// This is done every connection.
  /// Handles *should* stay valid between connections, but it depends on device.
  /// If a device changes its services between connections, handles may no longer be valid.
  /// This is handled gracefully within this class.
  /// </summary>
  public async Task CompleteConnection()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      _connectOp?.Fail($"Bluetooth: Device {_address} not connected.");
      return;
    }

    try
    {
      await BuildGattCache();

      _connectOp?.Succeed();
      SignalForwarder.ToMainThreadAsync(() =>
      {
        Bluetooth.Instance.EmitSignal(Bluetooth.SignalName.DeviceConnected, this);
        EmitSignal(BluetoothDevice.SignalName.Connected);
      }, "Bluetooth device connected");
    }
    catch (Exception e)
    {
      _connectOp?.Fail($"Bluetooth: Error while building GATT cache for device {_address}: {e.Message}");

      // If we are here, we are in a bad state: the device is connected to the adapter, but we can't access it.
      // Disconnect.
      Disconnect();
    }
  }
}
