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
  /// Asynchronous version of ValueChanged that is not forwarded to the main thread.
  /// </summary>
  public Action ValueChangedAsync;
}