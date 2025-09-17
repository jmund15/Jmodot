#region

using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public partial class LastAnimName : BTCondition
{
    #region TASK_VARIABLES

    [Export] private string _lastAnimName;
    private AnimationPlayer _animPlayer;
    private string _lastAnimFinished;

    public LastAnimName(string lastAnimName) // REAL????
    {
        _lastAnimName = lastAnimName;
    }

    public LastAnimName()
    {
        _lastAnimName = "";
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _animPlayer = BB.GetVar<AnimationPlayer>(BBDataSig.Anim);
        _animPlayer.AnimationStarted += OnAnimationStarted;
        //ConditionName = $"_LastAnimName:{_lastAnimName}";
        GD.Print("OOOOOH INITIALIZED last anim name condition! \nName: ", ConditionName);
    }

    private void OnAnimationStarted(StringName animName)
    {
        SetDeferred(PropertyName._lastAnimFinished, animName);
        //_lastAnimFinished = animName;
    }

    public override void Enter()
    {
        base.Enter();
        //var assignedAnim = _animPlayer.AssignedAnimation;
        GD.Print("last anim finished name: ", _lastAnimFinished, "\nlast anim key: ", _lastAnimName);
        if (_lastAnimFinished.Contains(_lastAnimName))
        {
            GD.Print("LAST ANIM KEY MATCHES, EXITING...");
            OnExitTask();
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}