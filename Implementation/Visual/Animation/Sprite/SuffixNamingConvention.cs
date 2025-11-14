namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using System.Collections.Generic;
using System.Linq;
using Core.Visual.Animation.Sprite;
using Shared;

/// <summary>
/// A concrete naming convention that appends variants as suffixes, separated by a character.
/// Example: "run" + ["N", "sword"] with separator "_" -> "run_N_sword"
/// </summary>
[GlobalClass]
public partial class SuffixNamingConvention : AnimationNamingConvention
{
    [Export] public string Separator { get; set; } = "_";

    public override StringName GetFullAnimationName(StringName baseName, IEnumerable<string> variants)
    {
        if (string.IsNullOrEmpty(baseName)) return "";

        var validVariants = variants.Where(v => !string.IsNullOrEmpty(v));
        //JmoLogger.Info(this, $"validVariants: {validVariants.Count()}");
        if (!validVariants.Any()) return baseName;

        // Using Concat for performance with many variants.
        return new StringName(baseName + string.Concat(validVariants.Select(v => Separator + v)));
    }
}
