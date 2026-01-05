namespace Jmodot.Implementation.Visual.Sprite;

using Core.Visual.Sprite;
using Godot;
using Tools.Visual.Sprite;

/// <summary>
/// A concrete implementation of ISpriteComponent using Godot's built-in Sprite3D node.
/// </summary>
[GlobalClass, Tool]
public partial class Sprite3DComponent : Sprite3D, ISpriteComponent
{
    public float GetSpriteHeight() => Texture?.GetHeight() * PixelSize * Scale.Y ?? 0f;
    public float GetSpriteWidth() => Texture?.GetWidth() * PixelSize * Scale.X ?? 0f;
    public Node GetUnderlyingNode() => this;

    // --- Editor Generation Logic ---
#if TOOLS
    [ExportGroup("Animation Generator")]

    [Export(PropertyHint.NodePathToEditedNode)]
    public AnimationPlayer TargetPlayer { get; set; }

    [Export]
    public AnimationDirectionSuffixes DirectionSuffixes { get; set; }

    [Export] public Vector2I FrameSize { get; set; } = new(600, 600);
    private readonly Vector2I AllColumns = new(-1, -1);
    [Export] public Vector2I ColumnsToUse { get; set; } = new(-1, -1);
    [Export] public string BaseAnimationName { get; set; } = "idle";

    [Export]
    public string SeparationSuffix { get; set; } = "_";

    [Export(PropertyHint.Range, "1, 60, 1")]
    public int FramesPerSecond { get; set; } = 10;

    [Export]
    public bool LoopAnimations { get; set; } = true;

    [Export]
    public string TargetLibraryName { get; set; } = ""; // Empty string = Default/Global library

    // Assuming you have the ExportToolButton addon or infrastructure.
    // If not, standard Godot 4 requires an InspectorPlugin to render this Callable as a button,
    // or you can use a boolean toggle: [Export] public bool Generate { set { if(value) GenerateAnimations(); } }
    [ExportToolButton("Generate Animations")]
    public Callable GenerateButton => Callable.From(GenerateAnimations);

    private void GenerateAnimations()
    {
        if (!ValidateConfig(out int cols, out int rows)) return;

        // 1. Configure the Sprite3D properties to match the grid
        this.Hframes = cols;
        this.Vframes = rows;
        this.FrameCoords = new Vector2I(0, 0);
        GD.Print($"[Sprite3DComponent] Updated Sprite HFrames to {cols}, VFrames to {rows}.");

        // 2. Get or Create Library
        AnimationLibrary library;
        if (TargetPlayer.HasAnimationLibrary(TargetLibraryName))
        {
            library = TargetPlayer.GetAnimationLibrary(TargetLibraryName);
        }
        else
        {
            library = new AnimationLibrary();
            TargetPlayer.AddAnimationLibrary(TargetLibraryName, library);
            GD.Print($"[Sprite3DComponent] Created new AnimationLibrary: '{TargetLibraryName}'");
        }

        // 3. Generate Animations per row
        float frameDuration = 1.0f / FramesPerSecond;

        int colStart = 0;
        int colEnd = cols;
        if (ColumnsToUse != AllColumns)
        {
            colStart = ColumnsToUse.X;
            colEnd = ColumnsToUse.Y + 1; // inclusive
        }

        for (int row = 0; row < rows; row++)
        {
            // Determine direction name
            string directionName = DirectionSuffixes.DirectionSuffixes.Count > row
                ? DirectionSuffixes.DirectionSuffixes[row]
                : $"dir{row}";

            string finalAnimName = $"{BaseAnimationName}{SeparationSuffix}{directionName}";

            // Create Animation
            var anim = new Animation();
            anim.LoopMode = LoopAnimations ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            anim.Length = frameDuration * (colEnd - colStart);
            anim.Step = frameDuration;

            // Create Track for 'frame_coords'
            // We use frame_coords because it allows us to visualize x/y grid easily
            int trackIdx = anim.AddTrack(Animation.TrackType.Value);
            // We must find the node that the AnimationPlayer treats as "Root"
            // and calculate the path relative to THAT node.
            Node animationRoot = TargetPlayer.GetNode(TargetPlayer.RootNode);
            if (animationRoot == null)
            {
                GD.PrintErr("[Sprite3DComponent] Could not find the AnimationPlayer's Root Node.");
                return;
            }
            string path = animationRoot.GetPathTo(this);
            anim.TrackSetPath(trackIdx, $"{path}:frame_coords");
            anim.TrackSetInterpolationType(trackIdx, Animation.InterpolationType.Nearest); // Pixel art style

            // Add Keyframes (Rows are the frames of animation)
            for (int col = colStart; col < colEnd; col++)
            {
                var coordValue = new Vector2I(col, row);
                anim.TrackInsertKey(trackIdx, col * frameDuration, coordValue);
            }

            // Add to library (overwrite if exists)
            library.AddAnimation(finalAnimName, anim);
            GD.Print($"[Sprite3DComponent] Generated Animation: {finalAnimName} ({rows} frames)");
        }

        // Notify Editor that things changed (so Ctrl+S works)
        NotifyPropertyListChanged();
        GD.Print("[Sprite3DComponent] Generation Complete.");
    }

    private bool ValidateConfig(out int cols, out int rows)
    {
        cols = 0;
        rows = 0;

        if (Texture == null)
        {
            GD.PrintErr("[Sprite3DComponent] No Texture assigned.");
            return false;
        }

        if (TargetPlayer == null)
        {
            GD.PrintErr("[Sprite3DComponent] No Target AnimationPlayer assigned.");
            return false;
        }

        if (DirectionSuffixes == null)
        {
            GD.PrintErr("[Sprite3DComponent] No Direction Template assigned.");
            return false;
        }

        if (FrameSize.X <= 0 || FrameSize.Y <= 0)
        {
            GD.PrintErr("[Sprite3DComponent] Invalid Frame Size.");
            return false;
        }

        int texW = Texture.GetWidth();
        int texH = Texture.GetHeight();

        if (texW % FrameSize.X != 0 || texH % FrameSize.Y != 0)
        {
            GD.PrintErr($"[Sprite3DComponent] Texture size ({texW}x{texH}) is not divisible by Frame Size ({FrameSize.X}x{FrameSize.Y}). Check dimensions.");
            return false;
        }

        cols = texW / FrameSize.X;
        rows = texH / FrameSize.Y;

        if (rows != DirectionSuffixes.DirectionSuffixes.Count)
        {
            GD.PrintErr($"[Sprite3DComponent] Mismatch: Texture has {rows} rows, but Template has {DirectionSuffixes.DirectionSuffixes.Count} directions defined.");
            return false;
        }

        return true;
    }
#endif
}
