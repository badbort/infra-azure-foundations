using System.Diagnostics;
using BadBort.Pulumi;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Pulumi;

namespace BadBort.AzureRm.Foundation.Infra;

[UsedImplicitly]
public class Program
{
    internal static readonly ActivitySource ActivitySource = new("asdf");

    public static async Task Main()
    {
        using var _ = PulumiObservability.InitializeTracing(ActivitySource);
        using var rootSpan = ActivitySource.StartActivity("Deploy", ActivityKind.Server);
        await Deployment.RunAsync<SubscriptionStack>();
    }
}