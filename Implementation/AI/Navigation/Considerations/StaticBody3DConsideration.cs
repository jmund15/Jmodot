namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using BB;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Shared;

[GlobalClass]
public partial class StaticBody3DConsideration : BaseAIConsideration3D
{
    // bidirectional dictionary (using '.Forward' & '.Reverse')
    private readonly Map<int, Vector3> _dirIds = new();

    // TODO: use Category instead of collision layer?
    [Export] private int _collLayer;
    [Export] private int _dirsToPropogate = 2;
    [Export] private Vector2 _distDiminishRange;
    [Export] private float _initPropWeight = 0.75f;

    [Export] private float _propDiminishWeight = 0.5f;

    [Export(PropertyHint.Range, "-2.5,2.5,0.1,or_greater,or_less")]
    protected float Consideration; // negative values are danger, positive are interest

    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions,
        DecisionContext context, IBlackboard blackboard)
    {
        var Agent = blackboard.GetVar<Node3D>(BBDataSig.Agent);
        var AINav = blackboard.GetVar<AINavigator3D>(BBDataSig.AINavComp);
        var percept = context.Memory;

        var considerVec = new Dictionary<Vector3, float>();
        foreach (var dir in directions.Directions)
        {
            considerVec[dir] = 0f;
        }

        var sensedPercepts = percept.GetSensedByCollLayer(this._collLayer);
        foreach (var perceptInfo in sensedPercepts)
        {
            var sensed = (CollisionObject3D)perceptInfo.Target!;
            if (sensed == Agent)
            {
                continue;
            }

            var collVec = (sensed.GlobalPosition - Agent.GlobalPosition).Normalized();
            var dist = collVec.Length();
            var dir = collVec.Normalized();
            var distWeight = this.GetDistanceConsideration(dist);
            var dangerAmt = this.Consideration * distWeight;

            considerVec[dir] = dangerAmt;
        }

        considerVec = this.PropogateConsiderations(considerVec);
        return considerVec;
    }

    public float GetDistanceConsideration(float detectDist)
    {
        if (detectDist > this._distDiminishRange.Y)
        {
            return 0f;
        }

        // the closer the collision is to the raycast, the higher the "danger" weight
        var minWeight = 0.1f;
        var k = 2.5f;
        float distWeight;

        if (detectDist <= this._distDiminishRange.X)
        {
            distWeight = 1.0f; // Ensure max weight
        }
        else
        {
            distWeight = 1f - (detectDist - this._distDiminishRange.X) /
                (this._distDiminishRange.Y - this._distDiminishRange.X);
        }

        //distWeight = minWeight + (1.0f - minWeight) *
        //    (float)Math.Exp(-k * (collDist - _distDiminishRange.X) / (_distDiminishRange.Y/*castLength*/ - _distDiminishRange.X));
        distWeight = Mathf.Clamp(distWeight, 0f, 1f);
        //GD.Print($"{raycast.TargetPosition.Normalized().GetDir16()}'s wall dist: {collDist}\ndistWeight: {distWeight}");
        return distWeight;
    }

    public Dictionary<Vector3, float> PropogateConsiderations(Dictionary<Vector3, float> considerations)
    {
        var preConsiderations = new Dictionary<Vector3, float>(considerations);


        foreach (var preConsid in preConsiderations)
        {
            var dir = preConsid.Key;
            var dangerAmt = preConsid.Value;
            if (dangerAmt == 0.0f)
            {
                continue;
            }

            //PROPOGATE DANGER OUT
            var propogateNum = this._dirsToPropogate;
            var propLDir = this._dirIds.Reverse[dir];
            var propRDir = this._dirIds.Reverse[dir];
            var dirId = this._dirIds.Reverse[dir];
            var propWeight = this._initPropWeight;
            while (propogateNum > 0)
            {
                if (propLDir == 0)
                {
                    propLDir = considerations.Count;
                }
                else
                {
                    propLDir--;
                }

                if (propRDir == considerations.Count)
                {
                    propRDir = 0;
                }
                else
                {
                    propRDir++;
                }

                //propLDir = propLDir.GetLeftDir();
                //propRDir = propRDir.GetRightDir();
                considerations[this._dirIds.Forward[propLDir]] += dangerAmt * propWeight;
                considerations[this._dirIds.Forward[propRDir]] += dangerAmt * propWeight;
                //GD.Print($"orig dir: {dir}; left dir: {propLDir}; right dir: {propRDir}; tbmb: {propWeight}" +
                //    $"\norig left: {preConsiderations[propLDir]}; new left: {considerations[propLDir]}");

                propWeight *= this._propDiminishWeight;
                propogateNum--;
            }
        }

        return considerations;
    }
}
