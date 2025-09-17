namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;

/// <summary>
/// Abstract base class for all sprite animation variant sources.
/// These are data-driven Resources that provide a string component (e.g., "_north", "_sword")
/// to be used by an AnimationNamingConvention to construct a final animation name.
/// </summary>
[GlobalClass]
public abstract partial class AnimVariantSource : Resource
{
    /// <summary>
    /// The order in which this variant should be applied by the naming convention.
    /// Lower numbers are applied first.
    /// </summary>
    [Export]
    public int Order { get; private set; } = 0;

    /// <summary>
    /// Gets the current string variant provided by this source.
    /// This value is calculated and cached internally when state is updated.
    /// </summary>
    /// <returns>The animation variant string (e.g., "_N"), or an empty string if not applicable.</returns>
    public abstract string GetVariant();
}
