namespace Jmodot.Examples.AI.HSM.TransitionConditions;

public enum NumericalConditionType
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo,
}

public static class NumericalConditionLogic
{
    public static bool CalculateFloatCondition(this NumericalConditionType conditionType, float baseValue, float valueToCompare)
    {
        switch (conditionType)
        {
            case NumericalConditionType.GreaterThan:
                if (baseValue > valueToCompare)
                {
                    return true;
                }

                return false;
            case NumericalConditionType.LessThan:
                if (baseValue < valueToCompare)
                {
                    return true;
                }
                return false;
            case NumericalConditionType.EqualTo:
                if (Mathf.IsEqualApprox(baseValue, valueToCompare))
                {
                    return true;
                }
                return false;
            case NumericalConditionType.NotEqualTo:
                if (!Mathf.IsEqualApprox(baseValue, valueToCompare))
                {
                    return true;
                }
                return false;
            default:
                return false;
        }
    }
}
