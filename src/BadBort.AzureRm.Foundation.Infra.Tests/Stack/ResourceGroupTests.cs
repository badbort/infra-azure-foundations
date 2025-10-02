using System.Collections.Immutable;
using System.Text.Json;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using BadBort.Pulumi;
using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Testing;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

public class ResourceGroupTests
{
    [Fact]
    public async Task Deploy_WithSingleResourceGroup()
    {
        using var temp = new TempData();
        temp.WriteTenantAndSubscription(
            tenantAlias: "sample-tenant",
            tenantId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            subAlias: "sample-sub",
            subId: "11111111-2222-3333-4444-555555555555",
            yamlContent: @"
resource_groups:
  rg-sample:
    location: Australia East
");
        
        var cfg = new Dictionary<string, object?>
        {
            ["project:data_dir"] = temp.Root,
            ["project:tenant"] = "sample-tenant",
            ["project:subscription"] = "sample-sub"
        };
        
        var envJson = JsonSerializer.Serialize(cfg);
        Environment.SetEnvironmentVariable("PULUMI_CONFIG", envJson);

        // ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());
        ImmutableArray<Resource> resources = await TestUtility.TestAsync<SubscriptionStack>(new EmptyMocks());
        
        resources.ShouldNotBeEmpty();
        
        var rg = resources.OfType<ResourceGroup>().ShouldHaveSingleItem();

        (await rg.Name.GetValueAsync()).ShouldBe("rg-sample");
        (await rg.Location.GetValueAsync()).ShouldBe("Australia East");
    }
}