using System;
using System.Threading.Tasks;
using Godot;

namespace GodotBLE;

/// <summary>
/// BLEOperation is a wrapper around an asynchronous operation.
/// It allows GDScript to access operation state and handle asynchronous
/// errors that come from C#.
/// </summary>
[GlobalClass]
public partial class BLEOperation : RefCounted
{
  /// <summary>
  /// Signal emitted when the Operation completes.
  /// The first parameter is the operation itself, the second is whether the operation was successful.
  /// The result of the operation can be retrieved from the handle.
  /// This signal is emitted on Start() if the operation completes synchronously.
  /// </summary>
  /// <param name="operation"></param>
  /// <param name="success"></param>
  [Signal]
  public delegate void DoneEventHandler(BLEOperation operation, bool success);

  /// <summary>
  /// Signal emitted when the Operation fails.
  /// This signal is emitted on Start() if the operation fails synchronously.
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

  /// <summary>
  /// Create a new operation wit the given asynchronous task.
  /// Used internally.
  /// </summary>
  /// <param name="action"></param>
  /// <returns></returns>
  public static BLEOperation Create(Func<BLEOperation, Task> action)
  {
    var operation = new BLEOperation();
    operation._content = action;
    return operation;
  }

  /// <summary>
  /// Start the operation.
  /// </summary>
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

  /// <summary>
  /// Report that the operation has completed successfully.
  /// Used internally.
  /// </summary>
  /// <param name="result"></param>
  public void Succeed(Variant result = default)
  {
    _result = result;
    state = OperationState.SUCCESS;
    SignalForwarder.ToMainThreadAsync(() =>
    {
      EmitSignal(SignalName.Done, this, true);
    }, "BLEOperation succeeded");
  }

  /// <summary>
  /// Report that the operation has failed.
  /// Used internally.
  /// </summary>
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

  /// <summary>
  /// The result of the operation.
  /// This is only valid if the operation has completed successfully.
  /// </summary>
  public Variant Result
  {
    get
    {
      return _result;
    }
  }

  /// <summary>
  /// True if the operation succeeded.
  /// </summary>
  public bool Success
  {
    get
    {
      return state == OperationState.SUCCESS;
    }
  }

  /// <summary>
  /// True if the operation is no longer running.
  /// Check the Success property to see if it succeeded or failed.
  /// </summary>
  public bool IsDone
  {
    get
    {
      return state == OperationState.SUCCESS || state == OperationState.ERROR;
    }
  }
}