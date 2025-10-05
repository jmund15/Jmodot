namespace Jmodot.Implementation.Registry;

/// <summary>
///     A global singleton (Autoload) that provides convenient, static access to the
///     project's central GameRegistry resource, which acts as the database for the framework.
/// </summary>
public partial class GlobalRegistry : Node
{
    [Export(PropertyHint.File, "*.tres")] private string _registryResourcePath;

    /// <summary>A static instance of the singleton for easy access.</summary>
    public static GlobalRegistry Instance { get; private set; }

    /// <summary>The loaded GameRegistry resource, providing the database API.</summary>
    public static GameRegistry DB { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            this.QueueFree();
            return;
        }

        Instance = this;

        if (string.IsNullOrEmpty(this._registryResourcePath))
        {
            GD.PrintErr("GlobalRegistry requires a path to a GameRegistry resource.");
            return;
        }

        DB = GD.Load<GameRegistry>(this._registryResourcePath);
        if (DB == null)
        {
            GD.PrintErr($"Failed to load GameRegistry from path: {this._registryResourcePath}");
        }

        //DEBUG, should happen automatically in editor right?
        DB.RebuildRegistry();
    }
}
