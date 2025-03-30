using System;
using System.Collections.Generic;
using Godot;
using GodotBLE;

[GlobalClass]
public partial class GattObserver : RefCounted
{
  /// <summary>
  /// Emitted when the observed GATT characteristic or descriptor receives a new value.
  /// This can be the result of a completed read or a notification.
  /// </summary>
  [Signal]
  public delegate void ValueChangedEventHandler();

  /// <summary>
  /// Emitted when the observed GATT characteristic or descriptor is no longer available.
  /// This can happen if the device changes its GATT profile such that this observer's handle
  /// is no longer valid. After this point, no further updates to the value will take place,
  /// and references to this observer can be discarded.
  /// </summary>
  [Signal]
  public delegate void GoneEventHandler();

  /// <summary>
  /// Asynchronous version of ValueChanged that is not forwarded to the main thread.
  /// </summary>
  public Action ValueChangedAsync;

  /// <summary>
  /// Asynchronous version of Gone that is not forwarded to the main thread.
  /// </summary>
  public Action GoneAsync;

}