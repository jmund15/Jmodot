namespace Jmodot.Implementation.Registry;

using Jmodot.Core.Shared.Attributes;

/// <summary>
///     A global singleton (Autoload) that provides convenient, static access to the
///     project's central GameRegistry resource, which acts as the database for the framework.
/// </summary>
public partial class GlobalRegistryLIB : Node
{
    [Export(PropertyHint.File, "*.tres"), RequiredExport] private string _registryResourcePath = null!;

    /// <summary>A static instance of the singleton for easy access.</summary>
    public static GlobalRegistryLIB Instance { get; private set; }

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
            return; // Fail-fast: don't proceed with null DB
        }

        //DEBUG, should happen automatically in editor right?
        DB.RebuildRegistry();
    }
}
