namespace Jmodot.Core.Input;

using Jmodot.Core.Shared.Attributes;

[GlobalClass, Tool]
public abstract partial class VectorBindingBase : Resource
{
    [Export, RequiredExport] public InputAction Action { get; set; } = null!;
    public abstract Vector2 GetVectorInput(Node3D entity);

    /// <summary>
    /// Declares what the emitted Vector2 semantically represents so consumers
    /// can interpret it correctly without brittle magnitude heuristics.
    /// </summary>
    public abstract VectorInputSemantic Semantic { get; }
}
