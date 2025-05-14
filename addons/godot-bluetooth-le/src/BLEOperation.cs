using System;
using System.Threading.Tasks;
using Godot;

namespace GodotBLE;

/// <summary>
/// BLEOperation is a handle to an asynchronous operation.
/// </summary>
[GlobalClass]
public partial class BLEOperation : RefCounted
{
  /// <summary>
  /// Signal emitted when the Operation completes.
  /// The first parameter is the operation itself, the second is whether the operation was successful.
  /// The result of the operation can be retrieved from the handle.
  /// </summary>
  /// <param name="operation"></param>
  /// <param name="success"></param>
  [Signal]
  public delegate void DoneEventHandler(BLEOperation operation, bool success);

  /// <summary>
  /// Signal emitted when the Operation fails.
  /// </summary>
  /// <param name="operation"></param>
  /// <param name="message"></param>
  [Signal]
  public delegate void ErrorEventHandler(BLEOperation operation, string message);

  private Variant _result = default;

  private string _err = "";

  private Func<BLEOperation, Task> _content = null;

  private enum OperationState
  {
    NOT_STARTED,
    RUNNING,
    SUCCESS,
    ERROR,
  }

  private OperationState state = OperationState.NOT_STARTED;

  private BLEOperation()
  {
    // Constructor is private to enforce the use of Create method
  }

  public static BLEOperation Create(Func<BLEOperation, Task> action)
  {
    var operation = new BLEOperation();
    operation._content = action;
    return operation;
  }

  public void Start()
  {
    if (state != OperationState.NOT_STARTED)
    {
      // Idempotent.
      return;
    }
    if (_content == null)
    {
      GD.PushError("BLEOperation: No action to invoke");
      return;
    }

    state = OperationState.RUNNING;
    new Task(async () => await _content(this)).Start();
  }


  public void Succeed(Variant result = default)
  {
    _result = result;
    state = OperationState.SUCCESS;
    SignalForwarder.ToMainThreadAsync(() =>
    {
      EmitSignal(SignalName.Done, this, true);
    }, "BLEOperation succeeded");
  }

  public void Fail(string err)
  {
    _err = err;
    GD.PushError($"BLEOperation: Error {err}");
    state = OperationState.ERROR;
    SignalForwarder.ToMainThreadAsync(() =>
    {
      EmitSignal(SignalName.Done, this, false);
      EmitSignal(SignalName.Error, this, err);
    }, "BLEOperation failed");
  }

  public Variant Result
  {
    get
    {
      return _result;
    }
  }

  public bool Success
  {
    get
    {
      return state == OperationState.SUCCESS;
    }
  }

  public bool IsDone
  {
    get
    {
      return state == OperationState.SUCCESS || state == OperationState.ERROR;
    }
  }
}