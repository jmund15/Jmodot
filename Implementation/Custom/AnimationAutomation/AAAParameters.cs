#region

using Godot.Collections;
using Jmodot.Implementation.Custom.AnimationAutomation;

#endregion

[GlobalClass]
[Tool]
public partial class AAAParameters : Resource
{
    [Export]
    public Dictionary<SpriteType, int> SpriteTypePixelMap { get; private set; }
        = new()
        {
            { SpriteType.Monster, 32 },
            { SpriteType.Critter, 10 }
        };

    [Export]
    public Dictionary<string, Animation.LoopModeEnum> AnimLoopMap { get; private set; }
        = new()
        {
            { "idle", Animation.LoopModeEnum.Linear },
            { "walk", Animation.LoopModeEnum.Linear },
            { "run", Animation.LoopModeEnum.Linear },
            { "land", Animation.LoopModeEnum.None },
            { "land2", Animation.LoopModeEnum.None },
            { "jump", Animation.LoopModeEnum.None },
            { "punchStartup", Animation.LoopModeEnum.None },
            { "punch1", Animation.LoopModeEnum.None },
            { "punch2", Animation.LoopModeEnum.None },
            { "wallPunch", Animation.LoopModeEnum.None },
            { "wallKick", Animation.LoopModeEnum.None },
            { "lift", Animation.LoopModeEnum.None },
            { "grab", Animation.LoopModeEnum.None },
            { "eat", Animation.LoopModeEnum.None },
            { "special", Animation.LoopModeEnum.None },
            { "swimLand", Animation.LoopModeEnum.None },
            { "swimSurface", Animation.LoopModeEnum.None },
            { "wallSpecial", Animation.LoopModeEnum.None }
        };

    [Export(PropertyHint.SaveFile, "*.tscn")]
    public string SavePath { get; private set; }

    [Export] public SpriteType SpriteType { get; private set; }

    [Export]
    public Dictionary<string, int> BodyParts { get; private set; } = new()
    {
        { "body", 2 },
        { "pants", 2 },
        { "shirt", 2 },
        { "hat", 2 }
    };

    [Export]
    public Dictionary<string, Array<string>>
        PartConfigLabels { get; private set; } =
        new();

    //[Export]
    //public DirectionType DirType { get; private set } = DirectionType.UpDown;

    [Export]
    public Array<AAADirection> AnimDirections { get; private set; } = new()
    {
        AAADirection.UP,
        AAADirection.DOWN
    };
}
