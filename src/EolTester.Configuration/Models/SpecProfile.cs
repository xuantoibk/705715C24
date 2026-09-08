using EolTester.Core.Models;

namespace EolTester.Configuration.Models;

public sealed class SpecProfile
{
    public required string Model { get; init; }
    public required List<TestStepDefinition> Steps { get; init; } = [];
}
