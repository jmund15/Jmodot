namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Implementation.AI.BB;

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
        this._lastAnimName = lastAnimName;
    }

    public LastAnimName()
    {
        this._lastAnimName = "";
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this._animPlayer = this.BB.GetVar<AnimationPlayer>(BBDataSig.Anim);
        this._animPlayer.AnimationStarted += this.OnAnimationStarted;
        //ConditionName = $"_LastAnimName:{_lastAnimName}";
        GD.Print("OOOOOH INITIALIZED last anim name condition! \nName: ", this.ConditionName);
    }

    private void OnAnimationStarted(StringName animName)
    {
        this.SetDeferred(PropertyName._lastAnimFinished, animName);
        //_lastAnimFinished = animName;
    }

    public override void Enter()
    {
        base.Enter();
        //var assignedAnim = _animPlayer.AssignedAnimation;
        GD.Print("last anim finished name: ", this._lastAnimFinished, "\nlast anim key: ", this._lastAnimName);
        if (this._lastAnimFinished.Contains(this._lastAnimName))
        {
            GD.Print("LAST ANIM KEY MATCHES, EXITING...");
            this.OnExitTask();
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
