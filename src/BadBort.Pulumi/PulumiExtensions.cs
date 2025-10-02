using Pulumi;

namespace BadBort.Pulumi;

public static class PulumiExtensions
{
    public static CustomResourceOptions WithOtelHooks(this CustomResourceOptions options, PulumiObservability observability) => observability.AddOtelHooks(options);
}