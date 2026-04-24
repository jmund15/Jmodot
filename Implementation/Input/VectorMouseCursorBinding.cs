namespace Jmodot.Implementation.Input;

using Core.Input;
using Shared;

[GlobalClass, Tool]
public partial class VectorMouseCursorBinding : VectorBindingBase
{
    public override Vector2 GetVectorInput(Node3D entity)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            JmoLogger.Error(null, "VectorMouseCursorBinding: entity is null — caller must provide a valid Node3D.");
            return Vector2.Zero;
        }

        var mousePos = ViewportUtils.GetMouseWorldPosition3D();
        if (mousePos == Vector3.Zero)
        {
            return Vector2.Zero;
        }

        // Return the un-normalized local-XZ offset so the vector's length carries
        // cursor distance (in world units). Direction-only consumers must call
        // .Normalized() themselves; lob-aim-style consumers query IntentData's
        // VectorSemantic and interpret magnitude as a positional offset.
        var localMousePos = entity.ToLocal(mousePos);
        return new Vector2(localMousePos.X, localMousePos.Z);
    }

    public override VectorInputSemantic Semantic => VectorInputSemantic.PositionalOffset;
}
