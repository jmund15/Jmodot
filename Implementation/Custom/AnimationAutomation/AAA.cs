//#if TOOLS

#region

using System.Collections.Generic;
using Jmodot.Implementation.Custom.AnimationAutomation;

#endregion

[Tool]
public partial class AAA : EditorScript
{
    private static readonly string _aaaParamsPath =
        "res://Base/monster_aaa_params.tres";

    //"res://Base/critter_aaa_params.tres";
    private AAAParameters _aaaParams;
    private AtlasTexture _atlasTexture;

    private int _currHeight;
    private int _frameHeight; // = 32;
    private int _initOffset; // = 32;//10
    private PortableCompressedTexture2D _portTexture;
    private Node _sprite;
    private Texture2D _texture;
    private Node _topLevel;

    public static string GetFaceDirectionString(AAADirection direction)
    {
        switch (direction)
        {
            case AAADirection.DOWN:
                return "Down";
            case AAADirection.UP:
                return "Up";
            case AAADirection.LEFT:
                return "Left";
            case AAADirection.RIGHT:
                return "Right";
            case AAADirection.DOWNLEFT:
                return "DownLeft";
            case AAADirection.DOWNRIGHT:
                return "DownRight";
            case AAADirection.UPLEFT:
                return "UpLeft";
            case AAADirection.UPRIGHT:
                return "UpRight";
            default:
                GD.PrintErr("not any face direction?? facedir = " + direction);
                return "Null";
        }
    }

    // Called when the script is executed (using File -> Run in Script Editor).
    public override void _Run()
    {
        //GD.Print("resource type: ", ResourceLoader.Load(_aaaParamsPath).GetType().FullName);
        _aaaParams = ResourceLoader.Load<AAAParameters>(_aaaParamsPath); // as AAAParameters;
        SaveThisSceneAs(_aaaParams.SavePath);
        _topLevel = GetScene();
        _sprite = _topLevel.GetFirstChildOfType<Node>();

        //GD.Print("loaded resource! param list of dirs: ", _aaaParams.AnimDirections);


        _initOffset = _aaaParams.SpriteTypePixelMap[_aaaParams.SpriteType];
        _frameHeight = _aaaParams.SpriteTypePixelMap[_aaaParams.SpriteType];


        if (_sprite is Sprite2D spritesheet)
        {
            _texture = spritesheet.Texture;
            _portTexture = spritesheet.Texture as PortableCompressedTexture2D;
            _atlasTexture = spritesheet.Texture as AtlasTexture;
        }
        else if (_sprite is Sprite3D spritesheet3D)
        {
            _texture = spritesheet3D.Texture;
            _portTexture = spritesheet3D.Texture as PortableCompressedTexture2D;
            _atlasTexture = spritesheet3D.Texture as AtlasTexture;
        }
        else
        {
            GD.PrintErr("AAA ERROR || Current scene is not a Sprite2D or Sprite3D!");
            return;
        }

        //FileDialogOptions options = new();
        //string? appendLibPath = await this.Extensibility.Shell().ShowOpenFileDialogAsync(options, cancellationToken);

        int textureHeight;
        if (_portTexture != null)
            textureHeight = _portTexture.GetHeight();
        else if (_atlasTexture != null) textureHeight = _atlasTexture.GetHeight();


        AutoAnimateSprite3D();

        //GetTree().CreateTimer(5.0f).Timeout += () => SaveThisSceneAs(SavePath);

        //var scenePath = SaveDir + "//" + SavePath;
        //CallDeferred(MethodName.SaveThisSceneAs, SavePath);


        //if (_aaaParams.BodyParts.Count > 1)
        //{
        OpenAndSaveScene(_aaaParams.SavePath);
        //}
        //else
        //{
        //    SaveNodeAsScene(_sprite, _aaaParams.SavePath);
        //}
        //EditorInterface.Singleton.SaveScene();
        //CallDeferred(MethodName.OpenAndSaveScene, _aaaParams.SavePath);
        //GD.Print($"Finished Animation Automation!");
    }

    private void AutoAnimateSprite2D()
    {
    }

    private void AutoAnimateSprite3D()
    {
        var animPlayer = _sprite.GetFirstChildOfType<AnimationPlayer>();
        var globalAnimLibrary = animPlayer.GetAnimationLibrary("");
        ResourceSaver.Save(globalAnimLibrary, $"res://Temp/{_sprite.Name}AnimLib.tres");
        var textPath = $"res://Temp/{_sprite.Name}SpriteSheet.tres";
        ResourceSaver.Save(_atlasTexture, textPath);
        animPlayer.RemoveAnimationLibrary(""); // remove globalAnimLibrary

        var topLevelName = _sprite.Name;
        _sprite.Name = "QueueDelete";
        _topLevel.Name = topLevelName;

        //var appendLibrary = ResourceLoader.Load<AnimationLibrary>();

        // TODO: MAKE NEW SPRITE FOR BODY PARTS HERE
        var partNum = 1;
        int partOffset;
        foreach (var bodyPartPair in _aaaParams.BodyParts)
        {
            var part = bodyPartPair.Key;
            var typesOfPart = bodyPartPair.Value;
            partOffset = _initOffset + (partNum - 1) * _frameHeight * typesOfPart * _aaaParams.AnimDirections.Count;
            Sprite3DComponent partSprite;
            AnimationPlayerComponent partPlayer;
            var partLibrary = new AnimationLibrary(); //globalAnimLibrary.Duplicate(true) as AnimationLibrary;
            var configLabels = new List<string>();

            //if (partNum == 1)
            //{
            //    partSprite = _sprite as Sprite3D;
            //    partPlayer = animPlayer;
            //    partPlayer.AddAnimationLibrary("", partLibrary);
            //    if (_aaaParams.BodyParts.Count > 1)
            //    {
            //        _sprite.Name = part;
            //    }
            //}
            //else
            //{
            //    //continue;

            //    //_sprite.AddChild(partSprite);
            //    //partSprite.AddChild(partPlayer);
            //}
            //if (partNum == 1 && _aaaParams.BodyParts.Count > 1)
            //{
            //    _sprite.Name = part;
            //}
            partSprite =
                new Sprite3DComponent(); //_sprite.Duplicate((int)Node.DuplicateFlags.UseInstantiation) as Sprite3D;
            _topLevel.AddChild(partSprite);
            partSprite.Owner = _topLevel;
            //partSprite.Texture = ResourceLoader.Load<AtlasTexture>(textPath);
            //partSprite.Texture.ResourceLocalToScene = true;
            partSprite.Texture = _texture.Duplicate() as Texture2D;
            partSprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            //ResourceSaver.Save(partSprite.Texture, $"res://Temp/{_topLevel.Name}_{partSprite.Name}.tres");
            partPlayer =
                new AnimationPlayerComponent(); //partSprite.GetFirstChildOfType<AnimationPlayer>();//new AnimationPlayer();
            partPlayer.AddAnimationLibrary("", partLibrary);
            partSprite.AddChild(partPlayer);
            partPlayer.Owner = _topLevel;
            //partPlayer.Reparent(partSprite);

            partSprite.Name = part;
            partPlayer.Name = part + "AnimationPlayer";
            GD.Print($"children count of {partSprite}'s node: {partSprite.GetChildCount()}");

            //Set properties of sprite
            partSprite.Centered = false;
            partSprite.Offset = new Vector2(_frameHeight / 2, 0);
            partSprite.Scale = new Vector3(8, 8, 8);

            //GD.Print($"part anim library anims: {partLibrary.GetAnimationList()}");
            //GD.Print($"blobal anim library anims: {globalAnimLibrary.GetAnimationList()}");
            int typeOffset;
            for (var typeNum = 0; typeNum < typesOfPart; typeNum++)
            {
                typeOffset = typeNum * _frameHeight * _aaaParams.AnimDirections.Count;
                var typeLabel = "";
                if (typesOfPart > 1)
                {
                    if (_aaaParams.PartConfigLabels.ContainsKey(part))
                    {
                        typeLabel = _aaaParams.PartConfigLabels[part][typeNum];
                    }
                    else
                    {
                        var typeChar = (char)(typeNum + 65);
                        typeLabel = typeChar.ToString();
                    }
                }

                configLabels.Add(typeLabel);
                foreach (var animName in globalAnimLibrary.GetAnimationList())
                {
                    _currHeight = partOffset + typeOffset;
                    //GD.Print($"Starting automating '{animName}'...");
                    var anim = globalAnimLibrary.GetAnimation(animName);
                    //foreach
                    //if (animName.ToString().ToLower().Contains(_))
                    if (_aaaParams.AnimLoopMap.ContainsKey(animName))
                        anim.LoopMode = _aaaParams.AnimLoopMap[animName];
                    //GD.Print($"Set loop mode to '{anim.LoopMode}'.");
                    else
                        anim.LoopMode = Animation.LoopModeEnum.None; // Default don't loop
                    var trackNum = 1;


                    foreach (var dir in _aaaParams.AnimDirections)
                    {
                        var dirAnim = anim.Duplicate(true) as Animation;

                        var numFrames = dirAnim.TrackGetKeyCount(trackNum);
                        for (var i = 0; i < numFrames; i++)
                        {
                            var currRect = (Rect2)dirAnim.TrackGetKeyValue(trackNum, i);
                            var dirRect = new Rect2(currRect.Position.X, _currHeight,
                                currRect.Size.X, _frameHeight);

                            dirAnim.TrackSetKeyValue(trackNum, i, Variant.From(dirRect));
                        }

                        var newAnimName = animName + GetFaceDirectionString(dir) + typeLabel;
                        GD.Print($"For {partPlayer.Name}'s animation '{newAnimName}', height is: {_currHeight}");
                        partLibrary.AddAnimation(newAnimName, dirAnim);
                        _currHeight += _frameHeight;
                        //partLibrary.RemoveAnimation(animName);
                    }
                }
            }

            partPlayer.SetConfigOptions(configLabels);
            partNum++;
            partOffset += _currHeight;
        }

        animPlayer.Free();
        _sprite.Free();
        //globalAnimLibrary.Dispose();
    }

    private void SaveThisSceneAs(string scenePath)
    {
        GD.Print("saving Scene: ", EditorInterface.Singleton.GetEditedSceneRoot().Name);
        EditorInterface.Singleton.SaveSceneAs(scenePath);
        //EditorInterface.Singleton.OpenSceneFromPath(scenePath);S
        //var topNode = EditorInterface.Singleton.GetEditedSceneRoot();
        //topNode.Name = "TESTED WOOO";
        //EditorInterface.Singleton.SaveScene();

        //var packedScene = new PackedScene();
        //packedScene.Pack(GetTree().CurrentScene);
        //GD.Print("current scene num children: ", GetTree().CurrentScene.GetChildCount());
        //ResourceSaver.Save(packedScene, scenePath);

        //GetTree().Quit();
    }

    private void SaveNodeAsScene(Node node, string scenePath)
    {
        var packedScene = new PackedScene();
        foreach (var child in node.GetChildren()) child.Owner = node;
        packedScene.Pack(node);
        ResourceSaver.Save(packedScene, scenePath);
    }

    private void OpenAndSaveScene(string scenePath)
    {
        EditorInterface.Singleton.OpenSceneFromPath(scenePath);
        EditorInterface.Singleton.SaveScene();

        GD.Print("Finished Animation Automation!");
        //var packedScene = new PackedScene();
        //packedScene.Pack(GetTree().CurrentScene);
        //GD.Print("current scene num children: ", GetTree().CurrentScene.GetChildCount());
        //ResourceSaver.Save(packedScene, scenePath);

        //GetTree().Quit();
    }
}
//#endif