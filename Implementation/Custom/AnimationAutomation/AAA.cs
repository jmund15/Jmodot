//#if TOOLS

namespace Jmodot.Implementation.Custom.AnimationAutomation;

using System.Collections.Generic;


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
        this._aaaParams = ResourceLoader.Load<AAAParameters>(_aaaParamsPath); // as AAAParameters;
        this.SaveThisSceneAs(this._aaaParams.SavePath);
        this._topLevel = this.GetScene();
        this._sprite = this._topLevel.GetFirstChildOfType<Node>();

        //GD.Print("loaded resource! param list of dirs: ", _aaaParams.AnimDirections);


        this._initOffset = this._aaaParams.SpriteTypePixelMap[this._aaaParams.SpriteType];
        this._frameHeight = this._aaaParams.SpriteTypePixelMap[this._aaaParams.SpriteType];


        if (this._sprite is Sprite2D spritesheet)
        {
            this._texture = spritesheet.Texture;
            this._portTexture = spritesheet.Texture as PortableCompressedTexture2D;
            this._atlasTexture = spritesheet.Texture as AtlasTexture;
        }
        else if (this._sprite is Sprite3D spritesheet3D)
        {
            this._texture = spritesheet3D.Texture;
            this._portTexture = spritesheet3D.Texture as PortableCompressedTexture2D;
            this._atlasTexture = spritesheet3D.Texture as AtlasTexture;
        }
        else
        {
            GD.PrintErr("AAA ERROR || Current scene is not a Sprite2D or Sprite3D!");
            return;
        }

        //FileDialogOptions options = new();
        //string? appendLibPath = await this.Extensibility.Shell().ShowOpenFileDialogAsync(options, cancellationToken);

        int textureHeight;
        if (this._portTexture != null)
        {
            textureHeight = this._portTexture.GetHeight();
        }
        else if (this._atlasTexture != null)
        {
            textureHeight = this._atlasTexture.GetHeight();
        }

        this.AutoAnimateSprite3D();

        //GetTree().CreateTimer(5.0f).Timeout += () => SaveThisSceneAs(SavePath);

        //var scenePath = SaveDir + "//" + SavePath;
        //CallDeferred(MethodName.SaveThisSceneAs, SavePath);


        //if (_aaaParams.BodyParts.Count > 1)
        //{
        this.OpenAndSaveScene(this._aaaParams.SavePath);
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
        var animPlayer = this._sprite.GetFirstChildOfType<AnimationPlayer>();
        var globalAnimLibrary = animPlayer.GetAnimationLibrary("");
        ResourceSaver.Save(globalAnimLibrary, $"res://Temp/{this._sprite.Name}AnimLib.tres");
        var textPath = $"res://Temp/{this._sprite.Name}SpriteSheet.tres";
        ResourceSaver.Save(this._atlasTexture, textPath);
        animPlayer.RemoveAnimationLibrary(""); // remove globalAnimLibrary

        var topLevelName = this._sprite.Name;
        this._sprite.Name = "QueueDelete";
        this._topLevel.Name = topLevelName;

        //var appendLibrary = ResourceLoader.Load<AnimationLibrary>();

        // TODO: MAKE NEW SPRITE FOR BODY PARTS HERE
        var partNum = 1;
        int partOffset;
        foreach (var bodyPartPair in this._aaaParams.BodyParts)
        {
            var part = bodyPartPair.Key;
            var typesOfPart = bodyPartPair.Value;
            partOffset = this._initOffset +
                         (partNum - 1) * this._frameHeight * typesOfPart * this._aaaParams.AnimDirections.Count;
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
            this._topLevel.AddChild(partSprite);
            partSprite.Owner = this._topLevel;
            //partSprite.Texture = ResourceLoader.Load<AtlasTexture>(textPath);
            //partSprite.Texture.ResourceLocalToScene = true;
            partSprite.Texture = this._texture.Duplicate() as Texture2D;
            partSprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            //ResourceSaver.Save(partSprite.Texture, $"res://Temp/{_topLevel.Name}_{partSprite.Name}.tres");
            partPlayer =
                new AnimationPlayerComponent(); //partSprite.GetFirstChildOfType<AnimationPlayer>();//new AnimationPlayer();
            partPlayer.AddAnimationLibrary("", partLibrary);
            partSprite.AddChild(partPlayer);
            partPlayer.Owner = this._topLevel;
            //partPlayer.Reparent(partSprite);

            partSprite.Name = part;
            partPlayer.Name = part + "AnimationPlayer";
            GD.Print($"children count of {partSprite}'s node: {partSprite.GetChildCount()}");

            //Set properties of sprite
            partSprite.Centered = false;
            partSprite.Offset = new Vector2(this._frameHeight / 2, 0);
            partSprite.Scale = new Vector3(8, 8, 8);

            //GD.Print($"part anim library anims: {partLibrary.GetAnimationList()}");
            //GD.Print($"blobal anim library anims: {globalAnimLibrary.GetAnimationList()}");
            int typeOffset;
            for (var typeNum = 0; typeNum < typesOfPart; typeNum++)
            {
                typeOffset = typeNum * this._frameHeight * this._aaaParams.AnimDirections.Count;
                var typeLabel = "";
                if (typesOfPart > 1)
                {
                    if (this._aaaParams.PartConfigLabels.ContainsKey(part))
                    {
                        typeLabel = this._aaaParams.PartConfigLabels[part][typeNum];
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
                    this._currHeight = partOffset + typeOffset;
                    //GD.Print($"Starting automating '{animName}'...");
                    var anim = globalAnimLibrary.GetAnimation(animName);
                    //foreach
                    //if (animName.ToString().ToLower().Contains(_))
                    if (this._aaaParams.AnimLoopMap.ContainsKey(animName))
                    {
                        anim.LoopMode = this._aaaParams.AnimLoopMap[animName];
                    }
                    //GD.Print($"Set loop mode to '{anim.LoopMode}'.");
                    else
                    {
                        anim.LoopMode = Animation.LoopModeEnum.None; // Default don't loop
                    }

                    var trackNum = 1;


                    foreach (var dir in this._aaaParams.AnimDirections)
                    {
                        var dirAnim = anim.Duplicate(true) as Animation;

                        var numFrames = dirAnim.TrackGetKeyCount(trackNum);
                        for (var i = 0; i < numFrames; i++)
                        {
                            var currRect = (Rect2)dirAnim.TrackGetKeyValue(trackNum, i);
                            var dirRect = new Rect2(currRect.Position.X, this._currHeight,
                                currRect.Size.X, this._frameHeight);

                            dirAnim.TrackSetKeyValue(trackNum, i, Variant.From(dirRect));
                        }

                        var newAnimName = animName + GetFaceDirectionString(dir) + typeLabel;
                        GD.Print($"For {partPlayer.Name}'s animation '{newAnimName}', height is: {this._currHeight}");
                        partLibrary.AddAnimation(newAnimName, dirAnim);
                        this._currHeight += this._frameHeight;
                        //partLibrary.RemoveAnimation(animName);
                    }
                }
            }

            partPlayer.SetConfigOptions(configLabels);
            partNum++;
            partOffset += this._currHeight;
        }

        animPlayer.Free();
        this._sprite.Free();
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
        foreach (var child in node.GetChildren())
        {
            child.Owner = node;
        }

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
