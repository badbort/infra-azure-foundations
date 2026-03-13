using System.Collections.Immutable;
using Pulumi;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Utility;

public static class PulumiUtility
{
    /// <summary>
    /// Extract the value from an output.
    /// </summary>
    public static Task<T> GetValueAsync<T>(this Output<T> output)
    {
        var tcs = new TaskCompletionSource<T>();
        output.Apply(v =>
        {
            tcs.SetResult(v);
            return v;
        });
        return tcs.Task;
    }

    public static async Task WritePreviewSummaryAsync(this ITestOutputHelper output, ImmutableArray<Resource> resources)
    {
        output.WriteLine("Pulumi preview summary");
        output.WriteLine($"Total resources: {resources.Length}");

        foreach (var groupedResource in resources
                     .GroupBy(r => r.GetType().Name)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"- {groupedResource.Key}: {groupedResource.Count()}");
        }

        output.WriteLine("Resource tree:");

        foreach (var resource in resources
                     .OrderBy(GetDisplayDepth)
                     .ThenBy(r => r.GetType().Name, StringComparer.Ordinal)
                     .ThenBy(r => r.GetResourceName(), StringComparer.Ordinal))
        {
            var urn = await resource.Urn.GetValueAsync();
            var depth = GetDisplayDepth(resource);
            var indent = new string(' ', depth * 2);
            output.WriteLine($"{indent}- {resource.GetType().Name} | {resource.GetResourceName()} | {urn}");
        }
    }

    private static int GetDisplayDepth(Resource resource)
    {
        return resource.GetType().Name switch
        {
            nameof(SubscriptionStack) => 0,
            "Provider" => 1,
            "ResourceGroup" => 1,
            "UserAssignedIdentity" => 2,
            "FederatedIdentityCredential" => 3,
            _ => 1
        };
    }
}
