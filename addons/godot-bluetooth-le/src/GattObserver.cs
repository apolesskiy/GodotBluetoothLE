using Godot;

[GlobalClass]
public partial class GattObserver : RefCounted
{
  [Signal]
  public delegate void ValueChangedEventHandler(byte[] value);
}