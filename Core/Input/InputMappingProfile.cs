namespace Jmodot.Core.Input;

using Godot;
using GCol = Godot.Collections;

[GlobalClass]
public partial class InputMappingProfile : Resource
{
    [Export]
    public GCol.Array<ActionBinding> ActionBindings { get; private set; } = new();

    [Export]
    public GCol.Array<VectorActionBinding> VectorBindings { get; private set; } = new();
}
