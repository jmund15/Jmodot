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
    public float GetSpriteHeight()
    {
        // 1. Determine the base pixel height (use RegionRect if enabled, else full Texture)
        float baseHeight = RegionEnabled ? RegionRect.Size.Y : (Texture?.GetHeight() ?? 0f);

        // 2. Divide by Vframes to get the height of a single frame
        // 3. Multiply by PixelSize (meters per pixel) and Scale.Y (node transform)
        return (baseHeight / Vframes) * PixelSize * Scale.Y;
    }
    public float GetWorldHeight() => GetSpriteHeight() * Scale.Y;
    public float GetSpriteHalfHeight() => GetSpriteHeight() / 2f;
    public float GetSpriteWidth()
    {
        // 1. Determine the base pixel width (use RegionRect if enabled, else full Texture)
        float baseWidth = RegionEnabled ? RegionRect.Size.X : (Texture?.GetWidth() ?? 0f);

        // 2. Divide by Hframes to get the width of a single frame
        // 3. Multiply by PixelSize (meters per pixel) and Scale.X (node transform)
        return (baseWidth / Hframes) * PixelSize * Scale.X;
    }
    public float GetWorldWidth() => GetSpriteWidth() * Scale.X;

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
    public bool AppendToExisting { get; set; } = false;

    [Export]
    public string TargetLibraryName { get; set; } = ""; // Empty string = Default/Global library

    // Assuming you have the ExportToolButton addon or infrastructure.
    // If not, standard Godot 4 requires an InspectorPlugin to render this Callable as a button,
    // or you can use a boolean toggle: [Export] public bool Generate { set { if(value) GenerateAnimations(); } }
    [ExportToolButton("Generate Animations")]
    public Callable GenerateButton => Callable.From(GenerateAnimations);

    private void GenerateAnimations()
    {
        if (!ValidateConfig(out int cols, out int rows)) { return; }

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

        // 3. Pre-compute shared values
        float frameDuration = 1.0f / FramesPerSecond;

        int colStart = 0;
        int colEnd = cols;
        if (ColumnsToUse != AllColumns)
        {
            colStart = ColumnsToUse.X;
            colEnd = ColumnsToUse.Y + 1; // inclusive
        }

        Node animationRoot = TargetPlayer.GetNode(TargetPlayer.RootNode);
        if (animationRoot == null)
        {
            GD.PrintErr("[Sprite3DComponent] Could not find the AnimationPlayer's Root Node.");
            return;
        }
        string spritePath = animationRoot.GetPathTo(this);
        string fullTrackPath = $"{spritePath}:frame_coords";

        // 4. Generate Animations per row
        for (int row = 0; row < rows; row++)
        {
            string directionName = DirectionSuffixes.DirectionSuffixes.Count > row
                ? DirectionSuffixes.DirectionSuffixes[row]
                : $"dir{row}";

            string finalAnimName = $"{BaseAnimationName}{SeparationSuffix}{directionName}";
            bool animExists = library.HasAnimation(finalAnimName);

            if (animExists && AppendToExisting)
            {
                // --- APPEND MODE: Add track to existing animation ---
                Animation existingAnim = library.GetAnimation(finalAnimName);

                // Safety: prevent duplicate track insertion
                bool duplicateFound = false;
                for (int t = 0; t < existingAnim.GetTrackCount(); t++)
                {
                    if (existingAnim.TrackGetPath(t) == fullTrackPath)
                    {
                        GD.PrintErr($"[Sprite3DComponent] SKIP '{finalAnimName}': Track '{fullTrackPath}' already exists. Remove it first or use Override mode.");
                        duplicateFound = true;
                        break;
                    }
                }
                if (duplicateFound) { continue; }

                AddFrameCoordsTrack(existingAnim, spritePath, row, colStart, colEnd, frameDuration);

                // Grow animation length if the new track is longer
                float newLength = frameDuration * (colEnd - colStart);
                if (newLength > existingAnim.Length)
                {
                    existingAnim.Length = newLength;
                }

                GD.Print($"[Sprite3DComponent] APPENDED track to '{finalAnimName}' for '{spritePath}'");
            }
            else
            {
                // --- CREATE / OVERRIDE MODE ---
                if (animExists)
                {
                    library.RemoveAnimation(finalAnimName);
                }

                var anim = new Animation();
                anim.LoopMode = LoopAnimations ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                anim.Length = frameDuration * (colEnd - colStart);
                anim.Step = frameDuration;

                AddFrameCoordsTrack(anim, spritePath, row, colStart, colEnd, frameDuration);

                library.AddAnimation(finalAnimName, anim);
                GD.Print($"[Sprite3DComponent] Generated Animation: {finalAnimName} ({colEnd - colStart} frames)");
            }
        }

        // Notify Editor that things changed (so Ctrl+S works)
        NotifyPropertyListChanged();
        GD.Print("[Sprite3DComponent] Generation Complete.");
    }

    private void AddFrameCoordsTrack(Animation anim, string spritePath, int row, int colStart, int colEnd, float frameDuration)
    {
        int trackIdx = anim.AddTrack(Animation.TrackType.Value);
        anim.TrackSetPath(trackIdx, $"{spritePath}:frame_coords");
        anim.TrackSetInterpolationType(trackIdx, Animation.InterpolationType.Nearest);

        int frameCount = 0;
        for (int col = colStart; col < colEnd; col++)
        {
            var coordValue = new Vector2I(col, row);
            anim.TrackInsertKey(trackIdx, frameCount * frameDuration, coordValue);
            frameCount++;
        }
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
