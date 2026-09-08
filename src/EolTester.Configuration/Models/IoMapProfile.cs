using EolTester.Core.Models;

namespace EolTester.Configuration.Models;

public sealed class IoMapProfile
{
    public required List<IoPointDefinition> Points { get; init; } = [];
}
