#if TOOLS
using Godot;
using System;

[Tool]
public partial class EditorPlugin : Godot.EditorPlugin
{
  public override void _EnterTree()
  {
    GD.Print("hooray ⋂^.⩀.^⋂");
    // Initialization of the plugin goes here.
    // Check if the GodotBluetoothLE assembly is loaded. If it's not, remind the user to edit their .csproj.
    
  }

  public override void _ExitTree()
  {
    // Clean-up of the plugin goes here.
  }
}
#endif
