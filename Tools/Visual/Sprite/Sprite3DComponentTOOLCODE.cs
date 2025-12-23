// //// --- Editor Generation Logic ---
// #if TOOLS
//     [ExportGroup("Animation Generator")]
//
//     [Export(PropertyHint.NodePath)]
//     public AnimationPlayer TargetPlayer { get; set; }
//
//     [Export]
//     public SpriteDirectionTemplate DirectionTemplate { get; set; }
//
//     [Export]
//     public Vector2i FrameSize { get; set; } = new Vector2i(64, 64);
//
//     [Export]
//     public string BaseAnimationName { get; set; } = "idle";
//
//     [Export]
//     public string SeparationSuffix { get; set; } = "_";
//
//     [Export(PropertyHint.Range, "1, 60")]
//     public float FramesPerSecond { get; set; } = 10.0f;
//
//     [Export]
//     public bool LoopAnimations { get; set; } = true;
//
//     [Export]
//     public string TargetLibraryName { get; set; } = ""; // Empty string = Default/Global library
//
//     // Assuming you have the ExportToolButton addon or infrastructure.
//     // If not, standard Godot 4 requires an InspectorPlugin to render this Callable as a button,
//     // or you can use a boolean toggle: [Export] public bool Generate { set { if(value) GenerateAnimations(); } }
//     [ExportToolButton("Generate Animations")]
//     public Callable GenerateButton => Callable.From(GenerateAnimations);
//
//     private void GenerateAnimations()
//     {
//         if (!ValidateConfig(out int cols, out int rows)) return;
//
//         // 1. Configure the Sprite3D properties to match the grid
//         this.HFrames = cols;
//         this.VFrames = rows;
//         this.FrameCoords = new Vector2i(0, 0);
//         GD.Print($"[Sprite3DComponent] Updated Sprite HFrames to {cols}, VFrames to {rows}.");
//
//         // 2. Get or Create Library
//         AnimationLibrary library;
//         if (TargetPlayer.HasAnimationLibrary(TargetLibraryName))
//         {
//             library = TargetPlayer.GetAnimationLibrary(TargetLibraryName);
//         }
//         else
//         {
//             library = new AnimationLibrary();
//             TargetPlayer.AddAnimationLibrary(TargetLibraryName, library);
//             GD.Print($"[Sprite3DComponent] Created new AnimationLibrary: '{TargetLibraryName}'");
//         }
//
//         // 3. Generate Animations per column
//         float frameDuration = 1.0f / FramesPerSecond;
//
//         for (int col = 0; col < cols; col++)
//         {
//             // Determine direction name
//             string directionName = DirectionTemplate.DirectionSuffixes.Length > col
//                 ? DirectionTemplate.DirectionSuffixes[col]
//                 : $"dir{col}";
//
//             string finalAnimName = $"{BaseAnimationName}{SeparationSuffix}{directionName}";
//
//             // Create Animation
//             var anim = new Animation();
//             anim.LoopMode = LoopAnimations ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
//             anim.Length = frameDuration * rows;
//             anim.Step = frameDuration;
//
//             // Create Track for 'frame_coords'
//             // We use frame_coords because it allows us to visualize x/y grid easily
//             int trackIdx = anim.AddTrack(Animation.TrackType.Value);
//             string path = TargetPlayer.GetPathTo(this);
//             anim.TrackSetPath(trackIdx, $"{path}:frame_coords");
//             anim.TrackSetInterpolationType(trackIdx, Animation.InterpolationType.Nearest); // Pixel art style
//
//             // Add Keyframes (Rows are the frames of animation)
//             for (int row = 0; row < rows; row++)
//             {
//                 var coordValue = new Vector2i(col, row);
//                 anim.TrackInsertKey(trackIdx, row * frameDuration, coordValue);
//             }
//
//             // Add to library (overwrite if exists)
//             library.AddAnimation(finalAnimName, anim);
//             GD.Print($"[Sprite3DComponent] Generated Animation: {finalAnimName} ({rows} frames)");
//         }
//
//         // Notify Editor that things changed (so Ctrl+S works)
//         NotifyPropertyListChanged();
//         GD.Print("[Sprite3DComponent] Generation Complete.");
//     }
//
//     private bool ValidateConfig(out int cols, out int rows)
//     {
//         cols = 0;
//         rows = 0;
//
//         if (Texture == null)
//         {
//             GD.PrintErr("[Sprite3DComponent] No Texture assigned.");
//             return false;
//         }
//
//         if (TargetPlayer == null)
//         {
//             GD.PrintErr("[Sprite3DComponent] No Target AnimationPlayer assigned.");
//             return false;
//         }
//
//         if (DirectionTemplate == null)
//         {
//             GD.PrintErr("[Sprite3DComponent] No Direction Template assigned.");
//             return false;
//         }
//
//         if (FrameSize.X <= 0 || FrameSize.Y <= 0)
//         {
//             GD.PrintErr("[Sprite3DComponent] Invalid Frame Size.");
//             return false;
//         }
//
//         int texW = Texture.GetWidth();
//         int texH = Texture.GetHeight();
//
//         if (texW % FrameSize.X != 0 || texH % FrameSize.Y != 0)
//         {
//             GD.PrintErr($"[Sprite3DComponent] Texture size ({texW}x{texH}) is not divisible by Frame Size ({FrameSize.X}x{FrameSize.Y}). Check dimensions.");
//             return false;
//         }
//
//         cols = texW / FrameSize.X;
//         rows = texH / FrameSize.Y;
//
//         if (cols != DirectionTemplate.DirectionSuffixes.Length)
//         {
//             GD.PrintErr($"[Sprite3DComponent] Mismatch: Texture has {cols} columns, but Template has {DirectionTemplate.DirectionSuffixes.Length} directions defined.");
//             return false;
//         }
//
//         return true;
//     }
// #endif
