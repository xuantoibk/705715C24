namespace EolTester.Core.Models;

public sealed class TestStepResult
{
    public required TestStepDefinition Definition { get; init; }
    public double? MeasuredValue { get; set; }

    public bool? Passed
    {
        get
        {
            if (MeasuredValue is null || !Definition.IsConfigured)
            {
                return null;
            }

            if (Definition.LowerLimit.HasValue && MeasuredValue < (double?)Definition.LowerLimit)
            {
                return false;
            }

            if (Definition.UpperLimit.HasValue && MeasuredValue > (double?)Definition.UpperLimit)
            {
                return false;
            }

            return true;
        }
    }
}
