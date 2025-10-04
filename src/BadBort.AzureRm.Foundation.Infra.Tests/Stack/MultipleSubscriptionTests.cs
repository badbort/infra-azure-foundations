using System.Collections.Immutable;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Testing;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

public class MultipleSubscriptionTests
{
    
    [Fact]
    public async Task Deploy_WithMultipleResourceGroups()
    {
        string yaml1 = @"
resource_groups:
  sample-test1:
    location: Australia East";
        
        string yaml2 = @"
resource_groups:
  sample-test2:
    location: Australia South East";

        using var temp = new FileSystemConventionBuilder()
            .SetupRandomTenantAndSubscription()
            .SetupResources(subPath: "test", yamlContent: yaml1)
            .SetupRandomSubscription()
            .SetupResources(subPath: "test2", yamlContent: yaml2)
            .Build()
            .SetPulumiConfig();
        
        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());
        resources.ShouldNotBeEmpty();

        var resourceGroups = resources.OfType<ResourceGroup>().ToList();
        resourceGroups.Count.ShouldBe(2);

        (await Output.All(resourceGroups.Select(rg => rg.Name)).GetValueAsync()).ShouldBe(["sample-test1", "sample-test2"], ignoreOrder: true);

        var rg1 = resourceGroups.Single(rg => rg.GetResourceName() == "sample-test1").ShouldNotBeNull();
        var rg2 = resourceGroups.Single(rg => rg.GetResourceName() == "sample-test2").ShouldNotBeNull();
        
        (await rg1.Location.GetValueAsync()).ShouldBe("Australia East");
        (await rg2.Location.GetValueAsync()).ShouldBe("Australia South East");
    }
}