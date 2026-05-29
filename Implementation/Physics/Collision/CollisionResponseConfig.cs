namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Identification;
using Attr = Jmodot.Core.Stats.Attribute;
using GCol = Godot.Collections;

/// <summary>
/// Authored per-archetype collision-response configuration. Pure data: a designer assigns
/// Category-keyed responses, physics strategies, and post-bounce stat modifiers on a
/// <c>.tres</c>; <c>CollisionResponderComponent3D</c> reads it via <c>[Export]</c> and builds a
/// <see cref="CollisionResponderCore"/> from these fields plus a runtime-resolved stat provider.
///
/// Replaces the authored surface of the legacy <c>UnifiedCollisionFactory</c> — all twelve
/// exports carry over verbatim.
/// </summary>
[GlobalClass, Tool]
public partial class CollisionResponseConfig : Resource
{
    [ExportGroup("Collision Responses")]
    [Export] public GCol.Array<CategoryResponseMapping> CategoryResponses { get; set; } = new();
    [Export] public BaseCollisionResponse? DefaultResponse { get; set; }

    [ExportGroup("Normal Fallback")]
    [Export] public bool UseNormalFallback { get; set; } = true;
    [Export] public Category? GroundCategory { get; set; }
    [Export] public Category? WallCategory { get; set; }

    [ExportGroup("Physics Strategies")]
    [Export] public CollisionPhysicsStrategy? BounceStrategy { get; set; }
    [Export] public CollisionPhysicsStrategy? PierceStrategy { get; set; }
    [Export] public CollisionPhysicsStrategy? SlideStrategy { get; set; }

    [ExportGroup("Post-Bounce Modifiers")]
    [Export] public Attr? GravityScaleAttribute { get; set; }
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float PostBounceGravityMultiplier { get; set; } = 1f;
    [Export] public Attr? BounceSpeedAttribute { get; set; }

    [ExportGroup("Layer Filtering")]
    [Export(PropertyHint.Layers3DPhysics)]
    public uint ExemptLayers { get; set; } = 0;
}
