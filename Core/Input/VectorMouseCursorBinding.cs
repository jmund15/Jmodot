namespace Jmodot.Core.Input;

using Implementation.Shared;

[GlobalClass, Tool]
public partial class VectorMouseCursorBinding : VectorBindingBase
{
    public override Vector2 GetVectorInput(Node3D entity)
    {
        var mousePos = ViewportUtils.GetMouseWorldPosition3D();
        var localMousePos = entity.ToLocal(mousePos);
        var flattenedNormalized = new Vector2(localMousePos.X, localMousePos.Z).Normalized();
        return flattenedNormalized;
    }
}
