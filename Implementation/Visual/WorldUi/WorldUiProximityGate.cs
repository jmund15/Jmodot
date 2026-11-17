namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// A trigger for a <see cref="WorldUiPrompt"/>: the prompt shows while any body is inside this area's shape. Add it
/// under the host, give it a <c>CollisionShape3D</c> and the <c>collision_mask</c> of the bodies that should open it,
/// and point <see cref="Prompt"/> at the prompt. It owns one gate, <see cref="GateName"/>, which it closes at ready.
/// </summary>
[GlobalClass, Tool]
public partial class WorldUiProximityGate : Area3D
{
    private readonly HashSet<Node3D> _inside = new();

    /// <summary>The prompt this gate opens and closes. Required: with none, the gate throws at ready.</summary>
    [Export, RequiredExport]
    public WorldUiPrompt Prompt { get; set; } = null!;

    /// <summary>The gate name this trigger owns on the prompt. Give two triggers on one prompt different names.</summary>
    [Export]
    public StringName GateName { get; set; } = "proximity";

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }

        this.ValidateRequiredExports();
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        Prompt.SetGate(GateName, false);
    }

    public override string[] _GetConfigurationWarnings()
        => this.GetChildren().OfType<CollisionShape3D>().Any()
            ? System.Array.Empty<string>()
            : new[] { "This gate has no CollisionShape3D child, so nothing can enter it and its prompt never shows." };

    private void OnBodyEntered(Node3D body)
    {
        _inside.Add(body);
        Prompt.SetGate(GateName, true);
    }

    private void OnBodyExited(Node3D body)
    {
        _inside.Remove(body);
        Prompt.SetGate(GateName, _inside.Count > 0);
    }
}
