using System.Collections.Immutable;
using Pulumi;
using Pulumi.Testing;

namespace BadBort.Pulumi;

public static class TestUtility
{
    private static AsyncLocal<bool> TestState { get; } = new();

    public static bool IsInTest() => TestState.Value;
    
    public static async Task<ImmutableArray<Resource>> TestAsync<TStack>(IMocks mocks) where TStack : Stack, new()
    {
        TestState.Value = true;
        var result = await Deployment.TestAsync<TStack>(mocks);
        TestState.Value = false;
        return result;
    }
}