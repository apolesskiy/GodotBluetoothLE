# Godot's Blue Tooth (LE)
Bluetooth GATT interface for Godot.


## Setup

1. Install `addons/godot-bluetooth-le` to your `addons` folder. ([GodotEnv](https://github.com/chickensoft-games/GodotEnv) is recommended if pulling directly from GitHub).

1. Add the following to your `.csproj`:
```xml
<PropertyGroup>
    <!-- Set target frameworks so Plugin.BLE pulls in the right platform-specific implementation. -->
    <TargetFramework Condition="'$(GodotTargetPlatform)' == 'linuxbsd'">net8.0</TargetFramework>
    <TargetFramework Condition="'$(GodotTargetPlatform)' == 'windows'">net8.0-windows10.0.22000</TargetFramework>
    <TargetFramework Condition="'$(GodotTargetPlatform)' == 'ios'">net8.0-ios</TargetFramework>
    <TargetFramework Condition="'$(GodotTargetPlatform)' == 'mac'">net8.0-maccatalyst</TargetFramework>
    <TargetFramework Condition="'$(GodotTargetPlatform)' == 'android'">net8.0-android34.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="addons\godot-bluetooth-le\godot-bluetooth-le.csproj" />
    <!-- Stop Godot from pulling in linux-specific files, they have their own .csproj -->
    <Compile Remove="addons\godot-bluetooth-le\plugin-ble-linux\*" />
  </ItemGroup>
```

1. Build the project.

1. Add `Bluetooth` as an autoload in project settings.

## Usage

See `dev-project` for a complete usage example.

### Scan for Devices
```gdscript
func _ready() -> void:
  Bluetooth.DeviceDetected.connect(on_device_detected)

func scan() -> void:
  Bluetooth.StartScan()

func on_device_detected(device) -> void:
  print("Device detected: (" + device.Address + ") " + device.Name)
```


### Connect to a Device and Discover Services
```gdscript
var my_device : BluetoothDevice

func connect() -> void:
  my_device.Connected.connect(on_connected)
  my_device.StartConnect()

func on_connected() -> void:
  service_handles = my_device.GetServicesArray()
  for handle in service_handles:
    print("Found service " + service.ServiceUUID)
```

### Write to Characteristic
```gdscript
var service_uuid = "5eb53c36-d329-44e5-afb6-252e212aa9ec"
var characteristic_uuid = "aed5ac5e-43f8-4dd4-83ad-6f4b9b91dfe8"

var chara_handle = BLEGattHandle.new()
chara_handle.ServiceUUID = service_uuid
chara_handle.CharacteristicUUID = characteristic_uuid

var data = "hello"
var bytes = value.to_utf8_buffer()
device.StartWrite(handle, bytes)
```

### Read from Characteristic
```gdscript
my_chara_handle : BLEGattHandle

# ... discover or set your service and characteristic IDs

my_device.GetSubscription(my_chara_handle).OnValueChanged.connect(my_chara_new_value)

...

func read_my_chara():
  my_device.StartRead(my_chara_handle)

func my_chara_new_value():
  var bytes = my_device.GetValue(my_chara_handle)
  print("Got", bytes.get_string_from_utf8())

```

### Notify on Changes
```gdscript
my_chara_handle : BLEGattHandle

# ... discover or set your service and characteristic IDs

my_device.GetSubscription(my_chara_handle).OnValueChanged.connect(my_chara_new_value)

my_device.StartNotify(my_chara_handle)
...

func my_chara_new_value():
  var bytes = my_device.GetValue(my_chara_handle)
  print("Got", bytes.get_string_from_utf8())

```

### Multiple Services/Characteristics with the Same UUID
```gdscript
var service_uuid = "5eb53c36-d329-44e5-afb6-252e212aa9ec"

var services = my_device.GetServicesArrayByUUID(service_uuid)

for svc in services:
  print("Found service with " + service_uuid + " and index " + str(svc.ServiceIndex))
```

## Supported Features

|Key||
|--|--|
|✅| Supported |
|🤷| Untested but might work  |
|❌| Unsupported by the addon |

|                    | Windows | Linux | Android | iOS | Mac | Web 🛑 |
|--------------------|---------|-------|---------|-----|-----|-----|
| Scan               |✅|✅|🤷|🤷|🤷||
| Connect/Disconnect |✅|✅|🤷|🤷|🤷||
| Read / Write       |✅|✅|🤷|🤷|🤷||
| Notify             |✅|✅|🤷|🤷|🤷||
| Descriptors        |✅|❌|🤷|🤷|🤷||
| Pairing            |❌|❌|❌|❌|❌||
| BLE Security       |❌|❌|❌|❌|❌||

🛑 While the [web bluetooth api]() exists, it is not yet universally adopted, nor are there readily available libraries in any Godot-adjacent languages.

## How It Works

### Bluetooth and Your Game

The Bluetooth peripheral itself is managed by the OS. The addon is only
a view on that peripheral. Therefore, almost everything is asynchronous, and 
signals may arrive without your code having triggered a corresponding action.

For example, if you tab out of your game, go to your platform's bluetooth 
manager, and connect to a device, your game will receive a DeviceConnected signal
for that device. Similarly, if you connect to a device through your game and quit
without disconnecting, that device will remain connected to your machine.

For this reason, DeviceDetected and DeviceConnected signals will be sent at the
start of the game for any devices that are already known and connected, respectively.

This also means that while the pairing process itself is not supported by the addon,
 you can still pair your device through the OS. It will show up as connected in-game.

### Concurrency

It is important to keep in mind that *everything* about Bluetooth is asynchronous. 
APIs that are not asynchronous in the addon are really just cached for convenience.

Signals emitted by the addon are safe to use directly in your game's code. This is
done via the `SignalForwarder`. This class creates a synchronization context on the main
thread at startup, and forwards signal emissions through that context. This comes at the cost
of having to parse incoming data on the main thread. 

If this is not desired, the addon provides a C#-only API that uses async and delegates
for reads, writes, and notifies. It is up to the user to ensure that their usage of
this API plays nice with the main thread.

### Why all the .csproj stuff?

The addon wraps [Plugin.BLE](https://github.com/dotnet-bluetooth-le/dotnet-bluetooth-le) to 
provide cross-platform Bluetooth LE support. Since Plugin.BLE doesn't support Linux, the
addon provides its own implementation of Plugin.BLE's contracts using 
[Linux.Bluetooth](https://github.com/SuessLabs/Linux.Bluetooth)
for the `linuxbsd` platform only.

In order to provide native functionality, Plugin.BLE targets specific platforms. In plain
.NET, "what platform to build for" is handled by the `TargetFramework` setting, which is
global to the build. Godot C#'s default target of `net8.0` (for Godot 4.5, as of this writing)
is interpreted by the build as "multiplatform". In Plugin.BLE's case, that means
"no platform" and is unsupported. So, because `TargetFramework` is global, we
need to explicitly set it, based on Godot's target platform, to something
Plugin.BLE supports.