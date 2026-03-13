using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Testing;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ResourceGroupTests
{
    private readonly ITestOutputHelper _output;

    public ResourceGroupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Deploy_WithSingleResourceGroup()
    {
        using var temp = new FileSystemConventionBuilder();
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
        };
        
        var envJson = JsonSerializer.Serialize(cfg);
        Environment.SetEnvironmentVariable("PULUMI_CONFIG", envJson);

        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());
        
        resources.ShouldNotBeEmpty();
        await _output.WritePreviewSummaryAsync(resources);
        
        var rg = resources.OfType<ResourceGroup>().ShouldHaveSingleItem();

        (await rg.Name.GetValueAsync()).ShouldBe("rg-sample");
        (await rg.Location.GetValueAsync()).ShouldBe("Australia East");
    }

    [Fact]
    public async Task Deploy_WithMultipleResourceGroups()
    {
        string yaml = @"
resource_groups:
  sample-test1:
    location: Australia East
    
  sample-test2:
    location: Australia South East";

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
        (await rg2.Location.GetValueAsync()).ShouldBe("Australia South East");
    }
}
