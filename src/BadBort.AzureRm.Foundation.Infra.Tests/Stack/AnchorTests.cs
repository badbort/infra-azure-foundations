using System.Collections.Immutable;
using BadBort.AzureRm.Foundation.Infra.Serialization;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Testing;
using Shouldly;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

public class AnchorTests
{
    private readonly ITestOutputHelper _output;

    public AnchorTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public async Task Deploy_WithMultipleResourceGroups()
    {
        string yaml = @"
common:
  default-rg: &rg
    location: Australia East

resource_groups:
  sample-test1:
    <<: *rg
  sample-test2:
    <<: *rg";

        using var temp = new FileSystemConventionBuilder()
            .SetupRandomTenantAndSubscription()
            .SetupResources(subPath: "test", yamlContent: yaml)
            .Build()
            .SetPulumiConfig();
        
        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());
        resources.ShouldNotBeEmpty();
        await _output.WritePreviewSummaryAsync(resources);

        var resourceGroups = resources.OfType<ResourceGroup>().ToList();
        resourceGroups.Count.ShouldBe(2);

        (await Output.All(resourceGroups.Select(rg => rg.Name)).GetValueAsync()).ShouldBe(["sample-test1", "sample-test2"], ignoreOrder: true);
        
        var rg1 = resourceGroups.Single(rg => rg.GetResourceName().EndsWith("sample-test1")).ShouldNotBeNull();
        var rg2 = resourceGroups.Single(rg => rg.GetResourceName().EndsWith("sample-test2")).ShouldNotBeNull();
        
        (await rg1.Location.GetValueAsync()).ShouldBe("Australia East");
        (await rg2.Location.GetValueAsync()).ShouldBe("Australia East");
    }
}
