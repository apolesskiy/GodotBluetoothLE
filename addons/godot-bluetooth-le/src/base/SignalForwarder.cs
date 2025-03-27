using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GodotBluetooth;

/// <summary>
/// The SignalForwarder forwards Actions (primarily Godot signals) to the main thread.
/// Most IO happens asynchronously in separate threads, which can cause insidious and catastrophic
/// concurrency problems in the game if asynchronously invoked signals are allowed to 'leak'.
/// Therefore, all Actions destined for the game (exposed through the toplevel API) go through
/// this class, which forwards them to the main thread.
/// </summary>
public static class SignalForwarder
{
  /// <summary>
  /// This task scheduler is created statically, guaranteeing that it will be on the main Godot thread.
  /// </summary>
  private static TaskScheduler _forwardingScheduler { get; } = TaskScheduler.FromCurrentSynchronizationContext();

  /// <summary>
  /// This method forwards an Action (such as a Godot signal) to the main thread.
  /// </summary>
  public static void ToMainThreadAsync(Action f, string debugNote) {
    Trace.WriteLine($"Forward to main thread: {debugNote}");
    _ = Task.Factory.StartNew(f, CancellationToken.None, TaskCreationOptions.None, _forwardingScheduler);
  }

  /// <summary>
  /// This exists so that the static class can be force-initialized. Since we statically initialize a TaskScheduler,
  /// funky things happen if we do not do this. 
  /// </summary>
  public static void Init() 
  {
    Trace.WriteLine($"Signal forwarder forwarding to {_forwardingScheduler.Id}");
  }
}