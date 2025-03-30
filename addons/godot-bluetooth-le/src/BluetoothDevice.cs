using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Godot;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;

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

  private IDevice _device;

  private Bluetooth _bt;

  private IAdapter _adp;

  private string _address;

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

  /// <summary>
  /// Start connecting to this device.
  /// Listen to Connected and Disconnected signals to know when the connection is established.
  /// </summary>
  public void StartConnect()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Disconnected)
    {
      return;
    }
    new Task(async () =>
    {
      await _adp.ConnectToDeviceAsync(_device);
    }).Start();
  }

  public void StartDisconnect()
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      return;
    }
    new Task(async () =>
    {
      await _adp.DisconnectDeviceAsync(_device);
    }).Start();
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
  /// </summary>
  /// <param name="handle"></param>
  /// <param name="data"></param>
  public void StartWrite(GattCharacteristicHandle handle, byte[] data)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      GD.PushWarning($"Bluetooth: Cannot write to device {_address}, device not connected.");
      return;
    }
    if (!_characteristics.ContainsKey(handle))
    {
      throw new ArgumentException("Characteristic not found.");
    }
    var characteristic = _characteristics[handle];
    if(!characteristic.CanWrite)
    {
      throw new InvalidOperationException("Characteristic does not support writing.");
    }
    new Task(() =>
    {
      characteristic.WriteAsync(data).Wait();

    }).Start();
  }


  /// <summary>
  /// Write to a characteristic if it supports writing. The write itself will be done asynchronously.
  /// Writes do not update the cached value, because characteristiscs may have WRITE, but not READ.
  /// </summary>
  /// <param name="handle"></param>
  /// <param name="data"></param>
  public void StartWrite(BLEGattHandle handle, byte[] data)
  {
    if (!handle.IsCharacteristic())
    {
      throw new ArgumentException("Handle is not a characteristic.");
    }
    StartWrite(handle.GetCharacteristicHandle(), data);
  }


  /// <summary>
  /// Start a read operation on a characteristic or descriptor.
  /// This will be done asynchronously in a separate thread.
  /// To retrieve the value, subscribe to the ValueChanged signal for this handle,
  /// and call GetValue() when the read is complete.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public void StartRead(BLEGattHandle handle)
  {
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      GD.PushWarning($"Bluetooth: Cannot write to device {_address}, device not connected.");
      return;
    }
    if (handle.IsCharacteristic())
    {
      new Task(() =>
      {
        ReadCharacteristicAsync(handle.GetCharacteristicHandle()).Wait();
      }).Start();
      return;
    }

    if (handle.IsDescriptor())
    {
      new Task(() =>
      {
        ReadDescriptorAsync(handle.GetDescriptorHandle()).Wait();
      }).Start();
      return;
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
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      GD.PushWarning($"Bluetooth: Cannot write to device {_address}, device not connected. Returned value may be invalid.");
    }
    if (!_characteristics.ContainsKey(handle.GetCharacteristicHandle()) &&
        !_descriptors.ContainsKey(handle.GetDescriptorHandle()))
    {
      throw new ArgumentException("Handle not found.");
    }
    if (handle.IsCharacteristic())
    {
      return _characteristics[handle.GetCharacteristicHandle()].Value;
    }
    return _descriptors[handle.GetDescriptorHandle()].Value;
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
    GD.Print($"Read characteristic {handle.UUID}");
    var result = await characteristic.ReadAsync().ConfigureAwait(false);
    GD.Print($"Post Read characteristic {handle.UUID}");
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
    var result = await descriptor.ReadAsync().ConfigureAwait(false);
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
  /// Enable notifications on a given characteristic if it supports them. This will be done asynchronously in a separate thread.
  /// </summary>
  /// <param name="handle"></param>
  public void StartNotify(GattCharacteristicHandle handle)
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
    _ = characteristic.StartUpdatesAsync();
  }


  /// <summary>
  /// Start notifications on a given characteristic if it supports them. This will be done asynchronously in a separate thread.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public void StartNotify(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      StartNotify(handle.GetCharacteristicHandle());
    }
    else
    {
      throw new ArgumentException("Handle is not a characteristic.");
    }
  }


  /// <summary>
  /// Stop notifications on a given characteristic. This will be done asynchronously in a separate thread.
  /// </summary>
  /// <param name="handle"></param>
  public void StopNotify(GattCharacteristicHandle handle)
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
    _ = characteristic.StopUpdatesAsync();
  }


  /// <summary>
  /// Stop notifications on a given characteristic. This will be done asynchronously in a separate thread.
  /// </summary>
  /// <param name="handle"></param>
  /// <exception cref="ArgumentException"></exception>
  public void StopNotify(BLEGattHandle handle)
  {
    if (handle.IsCharacteristic())
    {
      StopNotify(handle.GetCharacteristicHandle());
    }
    else
    {
      throw new ArgumentException("Handle is not a characteristic.");
    }
  }


  /// <summary>
  /// Build a cache of the device's services and characteristics.
  /// This is done every connection.
  /// Handles *should* stay valid between connections, but it depends on device.
  /// If a device changes its services between connections, handles may no longer be valid.
  /// This is handled gracefully within this class.
  /// </summary>
  public async Task BuildGattCache()
  {
    GD.Print("Building GATT cache for device " + _address);
    if (_device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
    {
      return;
    }
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
}
