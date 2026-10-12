using Xunit;

namespace Portfolio.Tests.Helpers;

/// <summary>Snapshot assertions whose failures list the missing and unexpected items one per line.</summary>
public static class GoldenAssert
{
    public static void Equal(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var missing = expected.Except(actual).ToList();
        var unexpected = actual.Except(expected).ToList();

        Assert.True(
            missing.Count == 0 && unexpected.Count == 0,
            "Golden mismatch." + Environment.NewLine +
            "Missing (expected but not found):" + Environment.NewLine + string.Join(Environment.NewLine, missing) + Environment.NewLine +
            "Unexpected (found but not expected):" + Environment.NewLine + string.Join(Environment.NewLine, unexpected));

        // Order and duplicates still matter (the DI list has deliberate duplicates).
        Assert.Equal(expected, actual);
    }
}
